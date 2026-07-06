import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react';

import { isApiError } from '../../api/apiErrors';
import { OrderManagementLayout } from '../../components/layout';
import type { ShellNavItem } from '../../components/layout/navigation';
import { Badge, Card, EmptyState } from '../../components/ui';
import { useLanguage } from '../../i18n/LanguageProvider';
import { useAuth } from '../auth/useAuth';
import { listAdminBranches } from '../admin/adminApi';
import type { AdminBranchListItem } from '../admin/adminTypes';
import { sortBranches } from '../admin/branches/branchDisplay';
import {
  closeCashierShift,
  getCurrentCashierShift,
  listCashierShifts,
  openCashierShift,
} from './cashierShiftApi';
import { formatCashierDateInput } from './cashierShiftDisplay';
import { getSafeCashierShiftErrorMessage } from './cashierShiftErrorDisplay';
import {
  buildCashierShiftCloseRequest,
  buildCashierShiftCloseValidationErrors,
  buildCashierShiftOpenRequest,
  buildCashierShiftOpenValidationErrors,
  type CashierShiftCloseFormState,
  type CashierShiftCloseValidationErrors,
  type CashierShiftOpenFormState,
  type CashierShiftOpenValidationErrors,
} from './cashierShiftValidation';
import type { CashierShiftDetail, CashierShiftListItem } from './cashierShiftTypes';
import BranchShiftSelector from './BranchShiftSelector';
import CloseShiftPanel from './CloseShiftPanel';
import CurrentShiftPanel from './CurrentShiftPanel';
import OpenShiftPanel from './OpenShiftPanel';
import ShiftListPanel from './ShiftListPanel';

export interface CashierShiftPageProps {
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

const emptyOpenForm = (): CashierShiftOpenFormState => ({
  businessDate: formatCashierDateInput(),
  openingCashAmount: '',
});

const emptyCloseForm = (): CashierShiftCloseFormState => ({
  declaredClosingCashAmount: '',
  closeNotes: '',
});

const isActiveShift = (shift: CashierShiftDetail | null) => Boolean(shift && shift.status === 'Open');

export const CashierShiftPage = ({ navItems, restaurantName, branchName, operatorLabel }: CashierShiftPageProps) => {
  const auth = useAuth();
  const { t } = useLanguage();
  const canViewShifts = auth.hasPermission('CashShift.View') || auth.hasPermission('CashShift.Manage');
  const canManageShifts = auth.hasPermission('CashShift.Manage');
  const cashierErrorMessages = useMemo(
    () => ({
      sessionExpired: t('cashier.sessionExpired'),
      unauthorized: t('cashier.unauthorized'),
    }),
    [t]
  );
  const cashierOpenValidationMessages = useMemo(
    () => ({
      businessDateRequired: t('cashier.businessDateRequired'),
      openingCashAmountRequired: t('cashier.openingCashAmountRequired'),
      openingCashAmountInvalid: t('cashier.openingCashAmountInvalid'),
      openingCashAmountTooLow: t('cashier.openingCashAmountTooLow'),
      declaredClosingCashRequired: t('cashier.declaredClosingCashRequired'),
      declaredClosingCashInvalid: t('cashier.declaredClosingCashInvalid'),
      declaredClosingCashTooLow: t('cashier.declaredClosingCashTooLow'),
    }),
    [t]
  );
  const cashierCloseValidationMessages = useMemo(
    () => ({
      businessDateRequired: t('cashier.businessDateRequired'),
      openingCashAmountRequired: t('cashier.openingCashAmountRequired'),
      openingCashAmountInvalid: t('cashier.openingCashAmountInvalid'),
      openingCashAmountTooLow: t('cashier.openingCashAmountTooLow'),
      declaredClosingCashRequired: t('cashier.declaredClosingCashRequired'),
      declaredClosingCashInvalid: t('cashier.declaredClosingCashInvalid'),
      declaredClosingCashTooLow: t('cashier.declaredClosingCashTooLow'),
    }),
    [t]
  );

  const [branches, setBranches] = useState<AdminBranchListItem[]>([]);
  const [branchesLoading, setBranchesLoading] = useState(canViewShifts);
  const [branchesError, setBranchesError] = useState<string | null>(null);
  const [selectedBranchId, setSelectedBranchId] = useState(auth.session?.branchId ?? '');
  const [businessDate, setBusinessDate] = useState(formatCashierDateInput());
  const [currentShift, setCurrentShift] = useState<CashierShiftDetail | null>(null);
  const [currentShiftLoading, setCurrentShiftLoading] = useState(false);
  const [currentShiftError, setCurrentShiftError] = useState<string | null>(null);
  const [history, setHistory] = useState<CashierShiftListItem[]>([]);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [historyError, setHistoryError] = useState<string | null>(null);
  const [notice, setNotice] = useState<Notice | null>(null);
  const [openForm, setOpenForm] = useState<CashierShiftOpenFormState>(() => emptyOpenForm());
  const [openErrors, setOpenErrors] = useState<CashierShiftOpenValidationErrors>({});
  const [openSubmitting, setOpenSubmitting] = useState(false);
  const [closeForm, setCloseForm] = useState<CashierShiftCloseFormState>(() => emptyCloseForm());
  const [closeErrors, setCloseErrors] = useState<CashierShiftCloseValidationErrors>({});
  const [closeSubmitting, setCloseSubmitting] = useState(false);
  const [closeDialogOpen, setCloseDialogOpen] = useState(false);

  const activeBranches = useMemo(() => sortBranches(branches.filter(branch => branch.status === 'Active')), [branches]);
  const selectedBranch = useMemo(
    () => activeBranches.find(branch => branch.branchId === selectedBranchId) ?? null,
    [activeBranches, selectedBranchId]
  );
  const selectedBranchCurrency = selectedBranch?.currency ?? auth.session?.currencyCode ?? 'INR';

  const clearMessages = useCallback(() => {
    setNotice(null);
    setCurrentShiftError(null);
    setHistoryError(null);
  }, []);

  const loadBranches = useCallback(async () => {
    if (!canViewShifts) {
      return;
    }

    setBranchesLoading(true);
    setBranchesError(null);

    try {
      const response = await listAdminBranches();
      const nextBranches = sortBranches(response.items.filter(branch => branch.status === 'Active'));
      setBranches(nextBranches);

      const activeBranchIds = new Set(nextBranches.map(branch => branch.branchId));
      const fallbackBranchId = auth.session?.branchId && activeBranchIds.has(auth.session.branchId)
        ? auth.session.branchId
        : nextBranches.length === 1
          ? nextBranches[0].branchId
          : '';
      const nextSelectedBranchId = selectedBranchId && activeBranchIds.has(selectedBranchId) ? selectedBranchId : fallbackBranchId;

      if (nextSelectedBranchId !== selectedBranchId) {
        setSelectedBranchId(nextSelectedBranchId);
      }
    } catch (caughtError) {
      setBranchesError(
        getSafeCashierShiftErrorMessage(caughtError, t('cashier.errorLoadBranches'), cashierErrorMessages)
      );
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setBranchesLoading(false);
    }
  }, [auth, canViewShifts, cashierErrorMessages, t, selectedBranchId]);

  const loadWorkspace = useCallback(
    async (branchId: string, date: string) => {
      if (!canViewShifts || !branchId) {
        setCurrentShift(null);
        setHistory([]);
        return;
      }

      setCurrentShiftLoading(true);
      setHistoryLoading(true);
      setCurrentShiftError(null);
      setHistoryError(null);

      const currentShiftPromise = getCurrentCashierShift(branchId);
      const historyPromise = listCashierShifts({ branchId, businessDate: date });

      try {
        const [currentResult, historyResult] = await Promise.allSettled([currentShiftPromise, historyPromise]);

        if (currentResult.status === 'fulfilled') {
          setCurrentShift(currentResult.value ?? null);
        } else if (isApiError(currentResult.reason) && currentResult.reason.status === 404) {
          setCurrentShift(null);
        } else {
          setCurrentShift(null);
          setCurrentShiftError(
            getSafeCashierShiftErrorMessage(currentResult.reason, t('cashier.errorLoadCurrentShift'), cashierErrorMessages)
          );
          if (isApiError(currentResult.reason) && currentResult.reason.status === 401) {
            void auth.logout();
          }
        }

        if (historyResult.status === 'fulfilled') {
          setHistory(historyResult.value.items);
        } else {
          setHistory([]);
          setHistoryError(
            getSafeCashierShiftErrorMessage(historyResult.reason, t('cashier.errorLoadShiftHistory'), cashierErrorMessages)
          );
          if (isApiError(historyResult.reason) && historyResult.reason.status === 401) {
            void auth.logout();
          }
        }
      } finally {
        setCurrentShiftLoading(false);
        setHistoryLoading(false);
      }
    },
    [auth, canViewShifts, cashierErrorMessages, t]
  );

  useEffect(() => {
    if (!canViewShifts) {
      return;
    }

    void loadBranches();
  }, [canViewShifts, loadBranches]);

  useEffect(() => {
    if (!canViewShifts || !selectedBranchId) {
      setCurrentShift(null);
      setHistory([]);
      return;
    }

    clearMessages();
    void loadWorkspace(selectedBranchId, businessDate);
  }, [businessDate, canViewShifts, clearMessages, loadWorkspace, selectedBranchId]);

  const handleOpenSubmit = useCallback(
    async (event: FormEvent<HTMLFormElement>) => {
      event.preventDefault();

      if (!canManageShifts || !selectedBranchId) {
        return;
      }

      const translatedErrors = buildCashierShiftOpenValidationErrors(openForm, cashierOpenValidationMessages);
      setOpenErrors(translatedErrors);

      if (Object.keys(translatedErrors).length > 0) {
        setNotice({
          tone: 'warning',
          message:
            translatedErrors.openingCashAmount ?? translatedErrors.businessDate ?? t('cashier.reviewOpenShiftForm'),
        });
        return;
      }

      setOpenSubmitting(true);
      setCurrentShiftError(null);
      setHistoryError(null);

      try {
        const opened = await openCashierShift(buildCashierShiftOpenRequest(selectedBranchId, openForm));
        setCurrentShift(opened);
        setOpenForm(current => ({
          ...current,
          openingCashAmount: '',
        }));
        await loadWorkspace(selectedBranchId, openForm.businessDate);
        setNotice({
          tone: 'success',
          message: t('cashier.shiftOpenedSuccessfully'),
        });
      } catch (caughtError) {
        setNotice({
          tone: 'danger',
          message: getSafeCashierShiftErrorMessage(caughtError, t('cashier.errorOpenShift'), cashierErrorMessages),
        });
        if (isApiError(caughtError) && caughtError.status === 401) {
          void auth.logout();
        }
      } finally {
        setOpenSubmitting(false);
      }
    },
    [auth, canManageShifts, cashierErrorMessages, cashierOpenValidationMessages, loadWorkspace, openForm, selectedBranchId, t]
  );

  const handleCloseSubmit = useCallback(
    async (event: FormEvent<HTMLFormElement>) => {
      event.preventDefault();

      if (!canManageShifts || !currentShift || !selectedBranchId) {
        return;
      }

      const nextErrors = buildCashierShiftCloseValidationErrors(closeForm, cashierCloseValidationMessages);
      setCloseErrors(nextErrors);

      if (Object.keys(nextErrors).length > 0) {
        setNotice({
          tone: 'warning',
          message: nextErrors.declaredClosingCashAmount ?? t('cashier.reviewCloseShiftForm'),
        });
        return;
      }

      setCloseSubmitting(true);
      setCurrentShiftError(null);
      setHistoryError(null);

      try {
        await closeCashierShift(currentShift.cashierShiftId, buildCashierShiftCloseRequest(closeForm));
        setCloseDialogOpen(false);
        setCloseForm(emptyCloseForm());
        await loadWorkspace(selectedBranchId, businessDate);
        setNotice({
          tone: 'success',
          message: t('cashier.shiftClosedSuccessfully'),
        });
      } catch (caughtError) {
        setNotice({
          tone: 'danger',
          message: getSafeCashierShiftErrorMessage(caughtError, t('cashier.errorCloseShift'), cashierErrorMessages),
        });
        if (isApiError(caughtError) && caughtError.status === 401) {
          void auth.logout();
        }
      } finally {
        setCloseSubmitting(false);
      }
    },
    [
      auth,
      businessDate,
      canManageShifts,
      cashierCloseValidationMessages,
      cashierErrorMessages,
      currentShift,
      closeForm,
      loadWorkspace,
      selectedBranchId,
      t,
    ]
  );

  if (!canViewShifts) {
    return (
      <OrderManagementLayout
        title={t('cashier.workspaceTitle')}
        description={t('cashier.workspaceDescription')}
        breadcrumbs={[t('nav.dashboard'), t('nav.cashierShifts')]}
        operatorLabel={operatorLabel}
        restaurantName={restaurantName}
        branchName={branchName}
        navItems={navItems}
      >
        <EmptyState
          title={t('cashier.notAuthorizedTitle')}
          description={t('cashier.notAuthorizedDescription')}
          tone="accent"
        />
      </OrderManagementLayout>
    );
  }

  return (
    <OrderManagementLayout
      title={t('cashier.workspaceTitle')}
      description={t('cashier.workspaceDescription')}
      breadcrumbs={[t('nav.dashboard'), t('nav.cashierShifts')]}
      operatorLabel={operatorLabel}
      restaurantName={restaurantName}
      branchName={selectedBranch?.name ?? branchName}
      navItems={navItems}
    >
      <div className="preview-sequence cashier-shift-page">
        {notice ? (
          <div className={`admin-notice admin-notice--${notice.tone}`} role="alert">
            {notice.message}
          </div>
        ) : null}

        <Card
          title={t('cashier.branchSelectionTitle')}
          description={t('cashier.branchSelectionDescription')}
          tone="orders"
          actions={<Badge tone={branchesLoading ? 'warning' : 'neutral'} label={branchesLoading ? t('cashier.refreshing') : t('cashier.ready')} />}
        >
          <BranchShiftSelector
            branches={branches}
            selectedBranchId={selectedBranchId}
            loading={branchesLoading}
            error={branchesError}
            helperText={t('cashier.branchSelectionHelper')}
            onChange={branchId => {
              setSelectedBranchId(branchId);
              setNotice(null);
            }}
            onRetry={() => void loadBranches()}
          />
        </Card>

        {!selectedBranchId ? (
          <EmptyState
            title={t('cashier.selectBranchTitle')}
            description={t('cashier.selectBranchDescription')}
            tone="orders"
          />
        ) : (
          <>
            {currentShiftError ? (
              <div className="admin-notice admin-notice--danger" role="alert">
                {currentShiftError}
              </div>
            ) : null}

            {historyError ? (
              <div className="admin-notice admin-notice--danger" role="alert">
                {historyError}
              </div>
            ) : null}

            {isActiveShift(currentShift) ? (
              <CurrentShiftPanel
                shift={currentShift!}
                currencyCode={selectedBranchCurrency}
                canCloseShift={canManageShifts}
                onClose={() => {
                  setCloseForm(emptyCloseForm());
                  setCloseErrors({});
                  setCloseDialogOpen(true);
                }}
              />
            ) : (
              <>
                <EmptyState
                  title={t('cashier.noActiveShiftTitle')}
                  description={
                    canManageShifts
                      ? t('cashier.noActiveShiftManagerDescription')
                      : t('cashier.noActiveShiftViewerDescription')
                  }
                  tone="orders"
                />
                {canManageShifts ? (
                  <OpenShiftPanel
                    form={openForm}
                    errors={openErrors}
                    submitting={openSubmitting}
                    canOpenShift={canManageShifts}
                    onSubmit={handleOpenSubmit}
                    onBusinessDateChange={value => {
                      setBusinessDate(value);
                      setOpenForm(current => ({ ...current, businessDate: value }));
                      setOpenErrors({});
                      setNotice(null);
                    }}
                    onOpeningCashAmountChange={value => {
                      setOpenForm(current => ({ ...current, openingCashAmount: value }));
                      setOpenErrors({});
                    }}
                  />
                ) : null}
              </>
            )}

            {closeDialogOpen && currentShift ? (
              <CloseShiftPanel
                shift={currentShift}
                form={closeForm}
                errors={closeErrors}
                currencyCode={selectedBranchCurrency}
                submitting={closeSubmitting}
                onSubmit={handleCloseSubmit}
                onClose={() => {
                  setCloseDialogOpen(false);
                  setCloseForm(emptyCloseForm());
                  setCloseErrors({});
                }}
                onDeclaredClosingCashAmountChange={value => {
                  setCloseForm(current => ({ ...current, declaredClosingCashAmount: value }));
                  setCloseErrors({});
                }}
                onCloseNotesChange={value => setCloseForm(current => ({ ...current, closeNotes: value }))}
              />
            ) : null}

            <ShiftListPanel
              shifts={history}
              loading={historyLoading || currentShiftLoading}
              error={historyError}
              currencyCode={selectedBranchCurrency}
              onRetry={() => void loadWorkspace(selectedBranchId, businessDate)}
            />
          </>
        )}
      </div>
    </OrderManagementLayout>
  );
};

export default CashierShiftPage;
