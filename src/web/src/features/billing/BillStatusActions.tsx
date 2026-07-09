import type { FormEvent } from 'react';

import { Button, Card, EmptyState, Input, StatusBadge } from '../../components/ui';
import { useLanguage } from '../../i18n/LanguageProvider';
import { useRestaurantCurrency } from '../auth/useRestaurantCurrency';
import { formatBillingBillStatus, formatBillingCurrency, formatBillingTimestamp } from './billingDisplay';
import type { BillDetail } from './billingTypes';

export interface BillStatusActionsProps {
  bill: BillDetail | null;
  canManageBills: boolean;
  billCancelReason: string;
  billCancelSubmitting: boolean;
  onBillCancelReasonChange: (value: string) => void;
  onCancelBill: () => void;
}

const countRecordedPayments = (bill: BillDetail | null) =>
  (bill?.payments ?? []).filter(payment => payment.status === 'Recorded').length;

export const BillStatusActions = ({
  bill,
  canManageBills,
  billCancelReason,
  billCancelSubmitting,
  onBillCancelReasonChange,
  onCancelBill,
}: BillStatusActionsProps) => {
  const { t } = useLanguage();
  const { currencyCode, locale } = useRestaurantCurrency();

  if (!bill) {
    return null;
  }

  const recordedPayments = countRecordedPayments(bill);
  const canCancelBill = canManageBills && bill.status === 'Unpaid' && recordedPayments === 0;

  return (
    <Card
      title={t('billing.billActionsTitle')}
      description={t('billing.billActionsDescription')}
      tone="orders"
      actions={
        <>
          <StatusBadge
            status={bill.status}
            label={formatBillingBillStatus(bill.status, {
              partiallyPaid: t('billing.statusPartiallyPaid'),
              unpaid: t('billing.statusUnpaid'),
              paid: t('billing.statusPaid'),
              cancelled: t('billing.statusCancelled'),
            })}
          />
        </>
      }
    >
      <div className="billing-detail-summary">
        <div className="admin-selected-user__meta">
          <div className="admin-selected-user__row">
            <span className="admin-selected-user__label">{t('billing.billNumberLabel')}</span>
            <strong>{bill.billNumber}</strong>
          </div>
          <div className="admin-selected-user__row">
            <span className="admin-selected-user__label">{t('pos.grandTotal')}</span>
            <strong>{formatBillingCurrency(bill.grandTotal, currencyCode, locale)}</strong>
          </div>
          <div className="admin-selected-user__row">
            <span className="admin-selected-user__label">{t('billing.amountPaidLabel')}</span>
            <strong>{formatBillingCurrency(bill.amountPaid, currencyCode, locale)}</strong>
          </div>
          <div className="admin-selected-user__row">
            <span className="admin-selected-user__label">{t('billing.balanceDueLabel')}</span>
            <strong>{formatBillingCurrency(bill.balanceDue, currencyCode, locale)}</strong>
          </div>
          <div className="admin-selected-user__row">
            <span className="admin-selected-user__label">{t('pos.created')}</span>
            <strong>{formatBillingTimestamp(bill.createdAt, { notAvailable: t('billing.notAvailable') })}</strong>
          </div>
          <div className="admin-selected-user__row">
            <span className="admin-selected-user__label">{t('pos.updated')}</span>
            <strong>{formatBillingTimestamp(bill.updatedAt, { notAvailable: t('billing.notAvailable') })}</strong>
          </div>
        </div>
      </div>

      {canCancelBill ? (
        <form
          className="admin-form-section"
          onSubmit={(event: FormEvent<HTMLFormElement>) => {
            event.preventDefault();
            onCancelBill();
          }}
        >
          <Input
            label={t('billing.cancellationReasonLabel')}
            value={billCancelReason}
            onChange={event => onBillCancelReasonChange(event.target.value)}
            placeholder={t('billing.cancellationReasonPlaceholder')}
            helperText={t('billing.cancellationReasonHelp')}
          />
          <div className="admin-form-actions">
            <Button type="submit" variant="danger" disabled={!billCancelReason.trim() || billCancelSubmitting}>
              {billCancelSubmitting ? t('billing.cancelling') : t('billing.cancelBill')}
            </Button>
          </div>
        </form>
      ) : (
        <EmptyState
          className="billing-status-actions__empty-state"
          title={t('billing.cancellationUnavailableTitle')}
          description={
            canManageBills
              ? bill.status === 'Cancelled'
                ? t('billing.cancellationAlreadyCancelled')
                : recordedPayments > 0
                  ? t('billing.cancellationPayFirst')
                  : t('billing.cancellationOnlyUnpaid')
              : t('billing.cancellationRequiresManage')
          }
          tone="orders"
        />
      )}
    </Card>
  );
};

export default BillStatusActions;
