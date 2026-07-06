import { useLanguage } from '../../i18n/LanguageProvider';
import { Button, EmptyState, ResponsiveDataList, StatusBadge } from '../../components/ui';
import { useRestaurantCurrency } from '../auth/useRestaurantCurrency';
import { formatBillingCurrency, formatBillingOrderStatus, formatBillingOrderType, formatBillingTimestamp } from './billingDisplay';
import { getSafeBillingErrorMessage } from './billingErrorDisplay';
import type { PosOrderListItem } from '../pos/posTypes';

export interface CreateBillPanelProps {
  candidates: PosOrderListItem[];
  loading?: boolean;
  error?: string | null;
  createSubmittingPosOrderId: string | null;
  onRetry: () => void;
  onCreateBill: (posOrderId: string) => void;
}

type CandidateRow = {
  id: string;
  posOrderId: string;
  orderNumber: string;
  status: PosOrderListItem['status'];
  orderType: PosOrderListItem['orderType'];
  lineCount: number;
  grandTotal: string;
  createdAt: string;
  actionLabel: string;
};

export const CreateBillPanel = ({
  candidates,
  loading,
  error,
  createSubmittingPosOrderId,
  onRetry,
  onCreateBill,
}: CreateBillPanelProps) => {
  const { t } = useLanguage();
  const { currencyCode, locale } = useRestaurantCurrency();

  if (loading && candidates.length === 0) {
    return (
      <EmptyState
        title={t('billing.loadingConfirmedOrdersTitle')}
        description={t('billing.loadingConfirmedOrdersDescription')}
        tone="orders"
      />
    );
  }

  if (error) {
    return (
      <EmptyState
        title={t('billing.couldNotLoadConfirmedOrdersTitle')}
        description={getSafeBillingErrorMessage(error, t('billing.errorLoadConfirmedOrders'), {
          sessionExpired: t('billing.sessionExpired'),
          unauthorized: t('billing.unauthorizedBilling'),
        })}
        tone="orders"
        actionLabel={t('billing.tryAgain')}
        onAction={onRetry}
      />
    );
  }

  if (candidates.length === 0) {
    return (
      <EmptyState
        title={t('billing.noConfirmedOrdersTitle')}
        description={t('billing.noConfirmedOrdersDescription')}
        tone="orders"
      />
    );
  }

  const rows: CandidateRow[] = candidates.map(candidate => ({
    id: candidate.posOrderId,
    posOrderId: candidate.posOrderId,
    orderNumber: candidate.orderNumber,
    status: candidate.status,
    orderType: candidate.orderType,
    lineCount: candidate.lineCount,
    grandTotal: formatBillingCurrency(candidate.grandTotal, currencyCode, locale),
    createdAt: formatBillingTimestamp(candidate.createdAt, { notAvailable: t('billing.notAvailable') }),
    actionLabel: createSubmittingPosOrderId === candidate.posOrderId ? t('billing.creatingBill') : t('billing.createBill'),
  }));

  return (
    <div className="billing-candidate-list">
      <ResponsiveDataList
        rows={rows}
        columns={[
          { key: 'orderNumber', label: t('pos.order') },
          {
            key: 'status',
            label: t('pos.status'),
            render: row => (
              <StatusBadge
                status={row.status}
                label={formatBillingOrderStatus(row.status, {
                  draft: t('pos.statusDraft'),
                  confirmed: t('pos.statusConfirmed'),
                  cancelled: t('pos.statusCancelled'),
                })}
              />
            ),
          },
          { key: 'grandTotal', label: t('pos.grandTotal'), align: 'right' },
          { key: 'createdAt', label: t('billing.confirmedLabel') },
          {
            key: 'posOrderId',
            label: t('billing.actionsLabel'),
            render: row => (
              <Button
                type="button"
                variant="secondary"
                fullWidth
                disabled={Boolean(createSubmittingPosOrderId)}
                onClick={() => onCreateBill(row.posOrderId)}
              >
                {row.actionLabel}
              </Button>
            ),
          },
        ]}
        mobileTitle={row => row.orderNumber}
        mobileDescription={row =>
          `${formatBillingOrderType(row.orderType, {
            eatIn: t('billing.orderTypeEatIn'),
            parcel: t('billing.orderTypeParcel'),
          })} · ${t('pos.lineCount', { count: row.lineCount, suffix: row.lineCount === 1 ? '' : 's' })}`
        }
        emptyTitle={t('billing.noConfirmedOrdersTitle')}
        emptyDescription={t('billing.noConfirmedOrdersDescription')}
      />
      <div className="admin-form-note">{t('billing.alreadyBilledHiddenNote')}</div>
    </div>
  );
};

export default CreateBillPanel;
