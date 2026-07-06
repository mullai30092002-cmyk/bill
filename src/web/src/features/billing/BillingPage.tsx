import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react';
import { flushSync } from 'react-dom';

import { isApiError } from '../../api/apiErrors';
import { OrderManagementLayout } from '../../components/layout';
import type { ShellNavItem } from '../../components/layout/navigation';
import { Badge, Button, Card, EmptyState, Input, SummaryCard } from '../../components/ui';
import { useLanguage } from '../../i18n/LanguageProvider';
import { useAuth } from '../auth/useAuth';
import { useRestaurantCurrency } from '../auth/useRestaurantCurrency';
import { getBill, cancelBill, createBill, listBills, recordPayment, cancelPayment } from './billingApi';
import { getSafeBillingErrorMessage } from './billingErrorDisplay';
import { buildSelectedBillLabel, formatBillingCurrency, roundMoney } from './billingDisplay';
import {
  buildBillingPaymentValidationErrors,
  type BillingPaymentValidationErrors,
} from './billingValidation';
import type { BillDetail, BillListItem, BillingPaymentEntryMode } from './billingTypes';
import { getBillReceipt, recordBillReceiptPrintEvent } from './billReceiptApi';
import { getSafeBillReceiptErrorMessage } from './billReceiptErrorDisplay';
import { printReceiptView } from './billReceiptPrint';
import type { BillReceiptResponse } from './billReceiptTypes';
import { listPosOrders } from '../pos/posApi';
import type { PosOrderListItem } from '../pos/posTypes';
import BillDetailPanel from './BillDetailPanel';
import BillListPanel from './BillListPanel';
import CreateBillPanel from './CreateBillPanel';
import { formatCashierDateInput } from '../cashiering/cashierShiftDisplay';
import { getCurrentCashierShift } from '../cashiering/cashierShiftApi';
import type { CashierShiftDetail } from '../cashiering/cashierShiftTypes';

export interface BillingPageProps {
  navItems?: ShellNavItem[];
  restaurantName?: string;
  branchName?: string;
  operatorLabel?: string;
}

type NoticeTone = 'success' | 'info' | 'warning' | 'danger';

interface Notice {
  tone: NoticeTone;
  message: string;
}

interface PaymentFormState {
  paymentMode: BillingPaymentEntryMode;
  amount: string;
  referenceNumber: string;
  notes: string;
}

const emptyPaymentForm = (): PaymentFormState => ({
  paymentMode: 'Cash',
  amount: '',
  referenceNumber: '',
  notes: '',
});

const normalizeBillingDate = (value: string) => value.slice(0, 10);

const trimOptionalValue = (value: string) => {
  const trimmed = value.trim();
  return trimmed ? trimmed : null;
};

const sortBillsNewestFirst = (items: BillListItem[]) =>
  [...items].sort((left, right) => {
    const createdDelta = Date.parse(right.createdAt) - Date.parse(left.createdAt);
    if (createdDelta !== 0) {
      return createdDelta;
    }

    return right.billNumber.localeCompare(left.billNumber, undefined, { sensitivity: 'base' });
  });

const sortConfirmedOrdersNewestFirst = (items: PosOrderListItem[]) =>
  [...items].sort((left, right) => {
    const createdDelta = Date.parse(right.createdAt) - Date.parse(left.createdAt);
    if (createdDelta !== 0) {
      return createdDelta;
    }

    return right.orderNumber.localeCompare(left.orderNumber, undefined, { sensitivity: 'base' });
  });

const buildRecordedPaymentIds = (bill: BillDetail | null) =>
  new Set((bill?.payments ?? []).filter(payment => payment.status === 'Recorded').map(payment => payment.paymentId));

export const BillingPage = ({ navItems, restaurantName, branchName, operatorLabel }: BillingPageProps) => {
  const auth = useAuth();
  const { currencyCode, locale } = useRestaurantCurrency();
  const { t } = useLanguage();
  const canViewBills = auth.hasPermission('Billing.View');
  const canManageBills = auth.hasPermission('Billing.Manage');
  const canRecordPayments = auth.hasPermission('Payment.Record');
  const canCancelPayments = auth.hasPermission('Payment.Cancel');
  const canAccess = canViewBills || canManageBills || canRecordPayments;
  const activeBranchId = auth.session?.branchId ?? undefined;

  const [bills, setBills] = useState<BillListItem[]>([]);
  const [billsLoading, setBillsLoading] = useState(canAccess);
  const [billsError, setBillsError] = useState<string | null>(null);
  const [confirmedOrders, setConfirmedOrders] = useState<PosOrderListItem[]>([]);
  const [confirmedOrdersLoading, setConfirmedOrdersLoading] = useState(canAccess);
  const [confirmedOrdersError, setConfirmedOrdersError] = useState<string | null>(null);
  const [businessDate, setBusinessDate] = useState(() => formatCashierDateInput());
  const [selectedBillId, setSelectedBillId] = useState<string | null>(null);
  const [selectedBill, setSelectedBill] = useState<BillDetail | null>(null);
  const [selectedBillLoading, setSelectedBillLoading] = useState(false);
  const [selectedBillError, setSelectedBillError] = useState<string | null>(null);
  const [currentCashierShift, setCurrentCashierShift] = useState<CashierShiftDetail | null>(null);
  const [currentCashierShiftLoading, setCurrentCashierShiftLoading] = useState(false);
  const [currentCashierShiftError, setCurrentCashierShiftError] = useState<string | null>(null);
  const [selectedBillReceipt, setSelectedBillReceipt] = useState<BillReceiptResponse | null>(null);
  const [selectedBillReceiptBillId, setSelectedBillReceiptBillId] = useState<string | null>(null);
  const [selectedBillReceiptLoading, setSelectedBillReceiptLoading] = useState(false);
  const [selectedBillReceiptError, setSelectedBillReceiptError] = useState<string | null>(null);
  const [receiptPrintSubmitting, setReceiptPrintSubmitting] = useState(false);
  const [notice, setNotice] = useState<Notice | null>(null);
  const [billCancelReason, setBillCancelReason] = useState('');
  const [billCancelSubmitting, setBillCancelSubmitting] = useState(false);
  const [selectedBillLoadToken, setSelectedBillLoadToken] = useState(0);
  const [paymentForm, setPaymentForm] = useState<PaymentFormState>(() => emptyPaymentForm());
  const [paymentFormErrors, setPaymentFormErrors] = useState<BillingPaymentValidationErrors>({});
  const [paymentSubmitting, setPaymentSubmitting] = useState(false);
  const [selectedPaymentIdForCancel, setSelectedPaymentIdForCancel] = useState<string | null>(null);
  const [cancelPaymentReason, setCancelPaymentReason] = useState('');
  const [cancelPaymentSubmitting, setCancelPaymentSubmitting] = useState(false);
  const [createSubmittingPosOrderId, setCreateSubmittingPosOrderId] = useState<string | null>(null);

  const refreshBills = useCallback(async () => {
    if (!canAccess) {
      return;
    }

    setBillsLoading(true);
    setBillsError(null);

    try {
      const response = await listBills({
        branchId: activeBranchId,
        businessDate,
      });
      setBills(sortBillsNewestFirst(response.items));
    } catch (caughtError) {
      setBillsError(
        getSafeBillingErrorMessage(caughtError, t('billing.errorLoadBills'), {
          sessionExpired: t('billing.sessionExpired'),
          unauthorized: t('billing.unauthorizedBilling'),
        })
      );
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
      throw caughtError;
    } finally {
      setBillsLoading(false);
    }
  }, [activeBranchId, auth, businessDate, canAccess, t]);

  const refreshConfirmedOrders = useCallback(async () => {
    if (!canAccess) {
      return;
    }

    setConfirmedOrdersLoading(true);
    setConfirmedOrdersError(null);

    try {
      const response = await listPosOrders({ branchId: activeBranchId, status: 'Confirmed' });
      setConfirmedOrders(sortConfirmedOrdersNewestFirst(response.items));
    } catch (caughtError) {
      setConfirmedOrdersError(
        getSafeBillingErrorMessage(caughtError, t('billing.errorLoadConfirmedOrders'), {
          sessionExpired: t('billing.sessionExpired'),
          unauthorized: t('billing.unauthorizedBilling'),
        })
      );
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
      throw caughtError;
    } finally {
      setConfirmedOrdersLoading(false);
    }
  }, [activeBranchId, auth, canAccess, t]);

  const loadSelectedBill = useCallback(
    async (billId: string) => {
      setSelectedBillId(billId);
      setSelectedBillLoading(true);
      setSelectedBillError(null);
      setNotice(null);
      setCurrentCashierShift(null);
      setCurrentCashierShiftError(null);
      setCurrentCashierShiftLoading(false);

      try {
        const detail = await getBill(billId);
        setSelectedBill(detail);
        setSelectedBillLoadToken(current => current + 1);
        setSelectedBillReceipt(null);
        setSelectedBillReceiptBillId(null);
        setSelectedBillReceiptError(null);
        setSelectedBillReceiptLoading(false);
        setReceiptPrintSubmitting(false);
        setBillCancelReason('');
        setSelectedPaymentIdForCancel(null);
        setCancelPaymentReason('');
        return detail;
      } catch (caughtError) {
        setSelectedBillError(
          getSafeBillingErrorMessage(caughtError, t('billing.errorLoadSelectedBill'), {
            sessionExpired: t('billing.sessionExpired'),
            unauthorized: t('billing.unauthorizedBilling'),
          })
        );
        setSelectedBill(null);
        if (isApiError(caughtError) && caughtError.status === 401) {
          void auth.logout();
        }
        throw caughtError;
      } finally {
        setSelectedBillLoading(false);
      }
    },
    [auth, t]
  );

  const loadSelectedBillReceipt = useCallback(
    async (billId: string) => {
      setSelectedBillReceiptBillId(billId);
      setSelectedBillReceiptLoading(true);
      setSelectedBillReceiptError(null);

      try {
        const receipt = await getBillReceipt(billId);
        setSelectedBillReceiptBillId(receipt.billId);
        setSelectedBillReceipt(receipt);
        return receipt;
      } catch (caughtError) {
        setSelectedBillReceiptBillId(billId);
        setSelectedBillReceiptError(
          getSafeBillReceiptErrorMessage(caughtError, t('billing.errorLoadReceipt'), {
            sessionExpired: t('billing.sessionExpired'),
            unauthorized: t('billing.unauthorizedBillReceipts'),
          })
        );
        setSelectedBillReceipt(null);
        if (isApiError(caughtError) && caughtError.status === 401) {
          void auth.logout();
        }
        throw caughtError;
      } finally {
        setSelectedBillReceiptLoading(false);
      }
    },
    [auth, t]
  );

  const handleViewReceipt = useCallback(() => {
    if (!selectedBillId) {
      return;
    }

    void loadSelectedBillReceipt(selectedBillId).catch(() => undefined);
  }, [loadSelectedBillReceipt, selectedBillId]);

  const handlePrintReceipt = useCallback(async () => {
    if (!selectedBillId) {
      return;
    }

    setReceiptPrintSubmitting(true);
    setSelectedBillReceiptError(null);

    try {
      const receipt = await recordBillReceiptPrintEvent(selectedBillId);
      flushSync(() => {
        setSelectedBillReceiptBillId(receipt.billId);
        setSelectedBillReceipt(receipt);
      });
      printReceiptView();
    } catch (caughtError) {
      setSelectedBillReceiptBillId(selectedBillId);
      setSelectedBillReceiptError(
        getSafeBillReceiptErrorMessage(caughtError, t('billing.errorRecordReceiptPrint'), {
          sessionExpired: t('billing.sessionExpired'),
          unauthorized: t('billing.unauthorizedBillReceipts'),
        })
      );
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setReceiptPrintSubmitting(false);
    }
  }, [auth, selectedBillId, t]);

  const loadCurrentCashierShift = useCallback(
    async (branchId: string | undefined) => {
      if (!branchId || !canRecordPayments) {
        setCurrentCashierShift(null);
        setCurrentCashierShiftError(null);
        setCurrentCashierShiftLoading(false);
        return;
      }

      setCurrentCashierShiftLoading(true);
      setCurrentCashierShiftError(null);

      try {
        const shift = await getCurrentCashierShift(branchId);
        setCurrentCashierShift(shift ?? null);
      } catch (caughtError) {
        if (isApiError(caughtError) && caughtError.status === 404) {
          setCurrentCashierShift(null);
        } else {
          setCurrentCashierShift(null);
          setCurrentCashierShiftError(t('billing.errorLoadCurrentCashierShift'));
          if (isApiError(caughtError) && caughtError.status === 401) {
            void auth.logout();
          }
        }
      } finally {
        setCurrentCashierShiftLoading(false);
      }
    },
    [auth, canRecordPayments, t]
  );

  useEffect(() => {
    if (!canAccess) {
      return;
    }

    void Promise.all([refreshBills(), refreshConfirmedOrders()]).catch(() => undefined);
  }, [canAccess, refreshBills, refreshConfirmedOrders]);

  useEffect(() => {
    if (!selectedBill || !canRecordPayments || selectedBill.status === 'Paid' || selectedBill.status === 'Cancelled') {
      setCurrentCashierShift(null);
      setCurrentCashierShiftError(null);
      setCurrentCashierShiftLoading(false);
      return;
    }

    void loadCurrentCashierShift(activeBranchId);
  }, [activeBranchId, canRecordPayments, loadCurrentCashierShift, selectedBillLoadToken]);

  const billedPosOrderIds = useMemo(
    () => new Set(bills.filter(bill => bill.status !== 'Cancelled').map(bill => bill.posOrderId)),
    [bills]
  );

  const billCandidates = useMemo(
    () =>
      confirmedOrders.filter(candidate => candidate.status === 'Confirmed' && !billedPosOrderIds.has(candidate.posOrderId)),
    [billedPosOrderIds, confirmedOrders]
  );

  const selectedBillRecordedPaymentIds = useMemo(() => buildRecordedPaymentIds(selectedBill), [selectedBill]);
  const outstandingBalance = useMemo(
    () => bills.filter(bill => bill.status !== 'Cancelled').reduce((sum, bill) => sum + bill.balanceDue, 0),
    [bills]
  );

  const handleCreateBill = useCallback(
    async (posOrderId: string) => {
      if (!canManageBills) {
        return;
      }

      setCreateSubmittingPosOrderId(posOrderId);
      setNotice(null);
      setCurrentCashierShift(null);
      setCurrentCashierShiftError(null);
      setCurrentCashierShiftLoading(false);

      try {
        const created = await createBill({ posOrderId });
        setSelectedBillId(created.billId);
        setSelectedBill(created);
        setSelectedBillLoadToken(current => current + 1);
        setSelectedBillReceipt(null);
        setSelectedBillReceiptBillId(null);
        setSelectedBillReceiptError(null);
        setBillCancelReason('');
        setSelectedPaymentIdForCancel(null);
        setCancelPaymentReason('');
        await Promise.all([refreshBills(), refreshConfirmedOrders()]).catch(() => undefined);
        setNotice({
          tone: 'success',
          message: t('billing.noticeCreatedBill', { billNumber: created.billNumber }),
        });
      } catch (caughtError) {
        setNotice({
          tone: 'danger',
          message: getSafeBillingErrorMessage(caughtError, t('billing.errorCreateBill'), {
            sessionExpired: t('billing.sessionExpired'),
            unauthorized: t('billing.unauthorizedBilling'),
          }),
        });
        if (isApiError(caughtError) && caughtError.status === 401) {
          void auth.logout();
        }
      } finally {
        setCreateSubmittingPosOrderId(null);
      }
    },
    [auth, canManageBills, refreshBills, refreshConfirmedOrders, t]
  );

  const handleRecordPaymentSubmit = useCallback(
    async (event: FormEvent<HTMLFormElement>) => {
      event.preventDefault();

      if (!selectedBill || !canRecordPayments) {
        return;
      }

      const validationErrors = buildBillingPaymentValidationErrors(
        { amount: paymentForm.amount, paymentMode: paymentForm.paymentMode, referenceNumber: paymentForm.referenceNumber },
        selectedBill.balanceDue,
        {
          amountRequired: t('billing.paymentAmountRequired'),
          amountInvalid: t('billing.paymentAmountInvalid'),
          amountTooLow: t('billing.paymentAmountTooLow'),
          amountTooHigh: t('billing.paymentAmountTooHigh'),
          referenceNumberRequired: t('billing.paymentReferenceRequired'),
        }
      );
      setPaymentFormErrors(validationErrors);

      if (Object.keys(validationErrors).length > 0) {
        setNotice({
          tone: 'warning',
          message:
            validationErrors.amount ??
            validationErrors.referenceNumber ??
            t('billing.reviewPaymentBeforeRecording'),
        });
        return;
      }

      if (paymentForm.paymentMode === 'Cash' && !currentCashierShift) {
        setNotice({
          tone: 'warning',
          message: t('billing.cashPaymentRequiresShift'),
        });
        return;
      }

      setPaymentSubmitting(true);
      setNotice(null);

      try {
        const amount = roundMoney(Number.parseFloat(paymentForm.amount.trim()));
        const updated = await recordPayment(selectedBill.billId, {
          paymentMode: paymentForm.paymentMode,
          amount,
          referenceNumber: trimOptionalValue(paymentForm.referenceNumber),
          notes: trimOptionalValue(paymentForm.notes),
        });
        setSelectedBillId(updated.billId);
        setSelectedBill(updated);
        setSelectedBillReceipt(null);
        setSelectedBillReceiptBillId(null);
        setSelectedBillReceiptError(null);
        setPaymentForm(emptyPaymentForm());
        setPaymentFormErrors({});
        setBillCancelReason('');
        setSelectedPaymentIdForCancel(null);
        setCancelPaymentReason('');
        await refreshBills();
        setNotice({
          tone: 'success',
          message: t('billing.noticeRecordedPayment', { billNumber: updated.billNumber }),
        });
      } catch (caughtError) {
        setNotice({
          tone: 'danger',
          message: getSafeBillingErrorMessage(caughtError, t('billing.errorRecordPayment'), {
            sessionExpired: t('billing.sessionExpired'),
            unauthorized: t('billing.unauthorizedBilling'),
          }),
        });
        if (isApiError(caughtError) && caughtError.status === 401) {
          void auth.logout();
        }
      } finally {
        setPaymentSubmitting(false);
      }
    },
    [auth, canRecordPayments, currentCashierShift, paymentForm, refreshBills, selectedBill, t]
  );

  const handleCancelBill = useCallback(async () => {
    if (!selectedBill || !canManageBills || selectedBill.status !== 'Unpaid' || selectedBillRecordedPaymentIds.size > 0) {
      return;
    }

    const reason = billCancelReason.trim();
    if (!reason) {
      setNotice({
        tone: 'warning',
        message: t('billing.enterBillCancelReason'),
      });
      return;
    }

    setBillCancelSubmitting(true);
    setNotice(null);

    try {
      const updated = await cancelBill(selectedBill.billId, { reason });
      setSelectedBillId(updated.billId);
      setSelectedBill(updated);
      setSelectedBillReceipt(null);
      setSelectedBillReceiptBillId(null);
      setSelectedBillReceiptError(null);
      setBillCancelReason('');
      setSelectedPaymentIdForCancel(null);
      setCancelPaymentReason('');
        await Promise.all([refreshBills(), refreshConfirmedOrders()]).catch(() => undefined);
        setNotice({
        tone: 'success',
        message: t('billing.noticeCancelledBill', { billNumber: updated.billNumber }),
      });
    } catch (caughtError) {
      setNotice({
        tone: 'danger',
        message: getSafeBillingErrorMessage(caughtError, t('billing.errorCancelBill'), {
          sessionExpired: t('billing.sessionExpired'),
          unauthorized: t('billing.unauthorizedBilling'),
        }),
      });
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setBillCancelSubmitting(false);
    }
  }, [auth, billCancelReason, canManageBills, refreshBills, refreshConfirmedOrders, selectedBill, selectedBillRecordedPaymentIds.size, t]);

  const handleCancelPayment = useCallback(async () => {
    if (!selectedBill || !canCancelPayments || !selectedPaymentIdForCancel) {
      return;
    }

    const reason = cancelPaymentReason.trim();
    if (!reason) {
      setNotice({
        tone: 'warning',
        message: t('billing.enterPaymentCancelReason'),
      });
      return;
    }

    setCancelPaymentSubmitting(true);
    setNotice(null);

    try {
      const updated = await cancelPayment(selectedPaymentIdForCancel, { reason });
      setSelectedBillId(updated.billId);
      setSelectedBill(updated);
      setSelectedBillReceipt(null);
      setSelectedBillReceiptBillId(null);
      setSelectedBillReceiptError(null);
      setSelectedPaymentIdForCancel(null);
      setCancelPaymentReason('');
      setBillCancelReason('');
      await refreshBills();
      setNotice({
        tone: 'success',
        message: t('billing.noticeCancelledPayment', { billNumber: updated.billNumber }),
      });
    } catch (caughtError) {
      setNotice({
        tone: 'danger',
        message: getSafeBillingErrorMessage(caughtError, t('billing.errorCancelPayment'), {
          sessionExpired: t('billing.sessionExpired'),
          unauthorized: t('billing.unauthorizedBilling'),
        }),
      });
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setCancelPaymentSubmitting(false);
    }
  }, [auth, canCancelPayments, cancelPaymentReason, refreshBills, selectedBill, selectedPaymentIdForCancel, t]);

  const handlePaymentModeChange = useCallback((value: BillingPaymentEntryMode) => {
    setPaymentForm(current => ({ ...current, paymentMode: value }));
    setPaymentFormErrors(current => {
      const nextErrors: BillingPaymentValidationErrors = {};
      if (current.amount) {
        nextErrors.amount = current.amount;
      }
      return nextErrors;
    });
  }, []);

  const handlePaymentAmountChange = useCallback((value: string) => {
    setPaymentForm(current => ({ ...current, amount: value }));
    setPaymentFormErrors({});
  }, []);

  const handlePaymentReferenceChange = useCallback((value: string) => {
    setPaymentForm(current => ({ ...current, referenceNumber: value }));
    setPaymentFormErrors(current => {
      const nextErrors: BillingPaymentValidationErrors = {};
      if (current.amount) {
        nextErrors.amount = current.amount;
      }
      return nextErrors;
    });
  }, []);

  const handlePaymentNotesChange = useCallback((value: string) => {
    setPaymentForm(current => ({ ...current, notes: value }));
  }, []);

  const visibleBillReceipt = selectedBillReceiptBillId === selectedBillId ? selectedBillReceipt : null;
  const visibleBillReceiptError = selectedBillReceiptBillId === selectedBillId ? selectedBillReceiptError : null;
  const visibleBillReceiptLoading = selectedBillReceiptBillId === selectedBillId ? selectedBillReceiptLoading : false;

  if (!canAccess) {
    return (
      <OrderManagementLayout
        title={t('billing.workspaceTitle')}
        description={t('billing.workspaceDescription')}
        breadcrumbs={[t('nav.dashboard'), t('nav.billing')]}
        operatorLabel={operatorLabel}
        restaurantName={restaurantName}
        branchName={branchName}
        navItems={navItems}
      >
        <EmptyState
          title={t('billing.notAuthorizedTitle')}
          description={t('billing.notAuthorizedDescription')}
          tone="orders"
        />
      </OrderManagementLayout>
    );
  }

  return (
    <OrderManagementLayout
      title={t('billing.workspaceTitle')}
      description={t('billing.workspaceDescription')}
      breadcrumbs={[t('nav.dashboard'), t('nav.billing')]}
      operatorLabel={operatorLabel}
      restaurantName={restaurantName}
      branchName={branchName}
      navItems={navItems}
      actions={
        <Button variant="secondary" onClick={() => void refreshBills().catch(() => undefined)}>
          {t('billing.refreshBills')}
        </Button>
      }
    >
      <div className="preview-sequence billing-workspace">
        <div className="summary-grid">
          <SummaryCard
            label={t('billing.billsLoadedLabel')}
            value={billsLoading && bills.length === 0 ? t('billing.loading') : bills.length.toString()}
            tone="orders"
            detail={t('billing.billsLoadedDetail')}
          />
          <SummaryCard
            label={t('billing.confirmedOrdersLabel')}
            value={confirmedOrdersLoading && confirmedOrders.length === 0 ? t('billing.loading') : billCandidates.length.toString()}
            tone="accent"
            detail={t('billing.confirmedOrdersDetail')}
          />
          <SummaryCard
            label={t('billing.openBalanceLabel')}
            value={formatBillingCurrency(outstandingBalance, currencyCode, locale)}
            tone="dashboard"
            detail={t('billing.openBalanceDetail')}
          />
          <SummaryCard
            label={t('billing.selectedBillLabel')}
            value={selectedBill ? selectedBill.billNumber : t('billing.none')}
            tone="inventory"
            detail={selectedBill ? buildSelectedBillLabel(selectedBill, {
              noBillSelected: t('billing.noBillSelectedYet'),
              partiallyPaid: t('billing.statusPartiallyPaid'),
              unpaid: t('billing.statusUnpaid'),
              paid: t('billing.statusPaid'),
              cancelled: t('billing.statusCancelled'),
            }) : t('billing.noBillSelectedYet')}
          />
        </div>

        {notice ? (
          <div className={['admin-notice', `admin-notice--${notice.tone}`].join(' ')} role={notice.tone === 'danger' ? 'alert' : 'status'}>
            {notice.message}
          </div>
        ) : null}

        <Card
          title={t('billing.billFiltersTitle')}
          description={t('billing.billFiltersDescription')}
          tone="orders"
          actions={<Badge tone="neutral" label={activeBranchId ? t('billing.branchScoped') : t('billing.branchMissing')} />}
        >
          <div className="admin-controls">
            <Input
              label={t('billing.businessDateLabel')}
              type="date"
              value={businessDate}
              onChange={event => {
                setBusinessDate(normalizeBillingDate(event.target.value));
                setNotice(null);
              }}
              helperText={t('billing.businessDateHelper')}
            />
          </div>
        </Card>

        <div className="preview-split billing-workspace__split">
          <div className="preview-main billing-workspace__main">
            <Card
              title={t('billing.billsTitle')}
              description={t('billing.billsDescription')}
              tone="orders"
              actions={<Badge tone="neutral" label={billsLoading ? t('billing.refreshing') : t('billing.loadedCount', { count: bills.length })} />}
            >
              <BillListPanel
                bills={bills}
                loading={billsLoading}
                error={billsError}
                selectedBillId={selectedBillId}
                onRetry={() => void refreshBills().catch(() => undefined)}
                onSelectBill={billId => void loadSelectedBill(billId)}
              />
            </Card>

            <BillDetailPanel
              bill={selectedBill}
              loading={selectedBillLoading}
              error={selectedBillError}
              selectedBillLabel={selectedBill ? buildSelectedBillLabel(selectedBill) : undefined}
              canManageBills={canManageBills}
              canRecordPayments={canRecordPayments}
              canCancelPayments={canCancelPayments}
              currentCashierShift={currentCashierShift}
              currentCashierShiftLoading={currentCashierShiftLoading}
              currentCashierShiftError={currentCashierShiftError}
              receipt={visibleBillReceipt}
              receiptLoading={visibleBillReceiptLoading}
              receiptPrinting={receiptPrintSubmitting}
              receiptError={visibleBillReceiptError}
              canViewReceipt={canAccess}
              canPrintReceipt={canAccess}
              paymentForm={paymentForm}
              paymentFormErrors={paymentFormErrors}
              paymentSubmitting={paymentSubmitting}
              selectedPaymentIdForCancel={selectedPaymentIdForCancel}
              cancelPaymentReason={cancelPaymentReason}
              cancelPaymentSubmitting={cancelPaymentSubmitting}
              billCancelReason={billCancelReason}
              billCancelSubmitting={billCancelSubmitting}
              onRetry={selectedBillId ? () => void loadSelectedBill(selectedBillId).catch(() => undefined) : undefined}
              onPaymentFormSubmit={handleRecordPaymentSubmit}
              onPaymentModeChange={handlePaymentModeChange}
              onPaymentAmountChange={handlePaymentAmountChange}
              onPaymentReferenceChange={handlePaymentReferenceChange}
              onPaymentNotesChange={handlePaymentNotesChange}
              onPaymentCancelTargetChange={paymentId => {
                setSelectedPaymentIdForCancel(paymentId);
                setCancelPaymentReason('');
              }}
              onCancelPaymentReasonChange={setCancelPaymentReason}
              onCancelPayment={() => void handleCancelPayment()}
              onBillCancelReasonChange={setBillCancelReason}
              onCancelBill={() => void handleCancelBill()}
              onViewReceipt={handleViewReceipt}
              onPrintReceipt={() => void handlePrintReceipt()}
              onRetryReceipt={() => void handleViewReceipt()}
            />
          </div>

          {canManageBills ? (
            <div className="preview-side-column billing-workspace__aside">
              <CreateBillPanel
                candidates={billCandidates}
                loading={confirmedOrdersLoading}
                error={confirmedOrdersError}
                createSubmittingPosOrderId={createSubmittingPosOrderId}
                onRetry={() => void refreshConfirmedOrders().catch(() => undefined)}
                onCreateBill={posOrderId => void handleCreateBill(posOrderId)}
              />
            </div>
          ) : null}
        </div>
      </div>
    </OrderManagementLayout>
  );
};

export default BillingPage;
