import { useLanguage } from '../../i18n/LanguageProvider';
import { Button, EmptyState, ResponsiveDataList, StatusBadge } from '../../components/ui';
import { useRestaurantCurrency } from '../auth/useRestaurantCurrency';
import { formatBillingBillStatus, formatBillingCurrency, formatBillingDate, formatBillingTimestamp } from './billingDisplay';
import { getSafeBillingErrorMessage } from './billingErrorDisplay';
import type { BillListItem } from './billingTypes';

export interface BillListPanelProps {
  bills: BillListItem[];
  loading?: boolean;
  error?: string | null;
  selectedBillId: string | null;
  onRetry: () => void;
  onSelectBill: (billId: string) => void;
}

type BillRow = {
  id: string;
  billId: string;
  billNumber: string;
  businessDate: string;
  status: BillListItem['status'];
  grandTotal: string;
  amountPaid: string;
  balanceDue: string;
  createdAt: string;
  statusLabel: string;
  actionLabel: string;
};

export const BillListPanel = ({
  bills,
  loading,
  error,
  selectedBillId,
  onRetry,
  onSelectBill,
}: BillListPanelProps) => {
  const { t } = useLanguage();
  const { currencyCode, locale } = useRestaurantCurrency();

  if (loading && bills.length === 0) {
    return (
      <EmptyState
        title={t('billing.loadingBillsTitle')}
        description={t('billing.loadingBillsDescription')}
        tone="orders"
      />
    );
  }

  if (error) {
    return (
      <EmptyState
        title={t('billing.couldNotLoadBillsTitle')}
        description={getSafeBillingErrorMessage(error, t('billing.errorLoadBills'), {
          sessionExpired: t('billing.sessionExpired'),
          unauthorized: t('billing.unauthorizedBilling'),
        })}
        tone="orders"
        actionLabel={t('billing.tryAgain')}
        onAction={onRetry}
      />
    );
  }

  if (bills.length === 0) {
    return (
      <EmptyState
        title={t('billing.noBillsFoundTitle')}
        description={t('billing.noBillsFoundDescription')}
        tone="orders"
      />
    );
  }

  const rows: BillRow[] = bills.map(bill => ({
    id: bill.billId,
    billId: bill.billId,
    billNumber: bill.billNumber,
    businessDate: formatBillingDate(bill.businessDate),
    status: bill.status,
    statusLabel: formatBillingBillStatus(bill.status, {
      partiallyPaid: t('billing.statusPartiallyPaid'),
      unpaid: t('billing.statusUnpaid'),
      paid: t('billing.statusPaid'),
      cancelled: t('billing.statusCancelled'),
    }),
    grandTotal: formatBillingCurrency(bill.grandTotal, currencyCode, locale),
    amountPaid: formatBillingCurrency(bill.amountPaid, currencyCode, locale),
    balanceDue: formatBillingCurrency(bill.balanceDue, currencyCode, locale),
    createdAt: formatBillingTimestamp(bill.createdAt),
    actionLabel: selectedBillId === bill.billId ? t('billing.viewing') : t('billing.view'),
  }));

  return (
    <ResponsiveDataList
      className="billing-list"
      rows={rows}
      columns={[
        { key: 'billNumber', label: t('billing.billNumberLabel') },
        { key: 'businessDate', label: t('billing.businessDateLabel') },
        {
          key: 'status',
          label: t('pos.status'),
          render: row => <StatusBadge status={row.status} label={row.statusLabel} />,
        },
        { key: 'grandTotal', label: t('pos.grandTotal'), align: 'right' },
        { key: 'amountPaid', label: t('billing.amountPaidLabel'), align: 'right' },
        { key: 'balanceDue', label: t('billing.balanceDueLabel'), align: 'right' },
        { key: 'createdAt', label: t('pos.created') },
        {
          key: 'billId',
          label: t('billing.actionsLabel'),
          render: row => (
            <Button
              type="button"
              variant={selectedBillId === row.billId ? 'primary' : 'secondary'}
              fullWidth
              onClick={() => onSelectBill(row.billId)}
            >
              {row.actionLabel}
            </Button>
          ),
        },
      ]}
      mobileTitle={row => row.billNumber}
      mobileDescription={row => `${row.businessDate} · ${row.statusLabel} · ${row.balanceDue} ${t('billing.due')}`}
      emptyTitle={t('billing.noBillsFoundTitle')}
      emptyDescription={t('billing.noBillsFoundDescription')}
    />
  );
};

export default BillListPanel;
