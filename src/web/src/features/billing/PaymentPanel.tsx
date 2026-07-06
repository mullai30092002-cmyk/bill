import type { FormEvent } from 'react';

import { Badge, Button, Card, EmptyState, Input, Select, StatusBadge } from '../../components/ui';
import { useLanguage } from '../../i18n/LanguageProvider';
import { useRestaurantCurrency } from '../auth/useRestaurantCurrency';
import { formatBillingCurrency, formatBillingPaymentMode, formatBillingPaymentStatus, formatBillingTimestamp } from './billingDisplay';
import type { BillingPaymentValidationErrors } from './billingValidation';
import type { BillDetail, BillingPaymentEntryMode } from './billingTypes';
import type { CashierShiftDetail } from '../cashiering/cashierShiftTypes';

export interface PaymentFormState {
  paymentMode: BillingPaymentEntryMode;
  amount: string;
  referenceNumber: string;
  notes: string;
}

export interface PaymentPanelProps {
  bill: BillDetail | null;
  canRecordPayments: boolean;
  canCancelPayments: boolean;
  currentCashierShift: CashierShiftDetail | null;
  currentCashierShiftLoading: boolean;
  currentCashierShiftError: string | null;
  paymentForm: PaymentFormState;
  paymentFormErrors: BillingPaymentValidationErrors;
  paymentSubmitting: boolean;
  selectedPaymentIdForCancel: string | null;
  cancelPaymentReason: string;
  cancelPaymentSubmitting: boolean;
  onPaymentFormSubmit: (event: FormEvent<HTMLFormElement>) => void;
  onPaymentModeChange: (value: BillingPaymentEntryMode) => void;
  onPaymentAmountChange: (value: string) => void;
  onPaymentReferenceChange: (value: string) => void;
  onPaymentNotesChange: (value: string) => void;
  onPaymentCancelTargetChange: (paymentId: string | null) => void;
  onCancelPaymentReasonChange: (value: string) => void;
  onCancelPayment: () => void;
}

export const PaymentPanel = ({
  bill,
  canRecordPayments,
  canCancelPayments,
  currentCashierShift,
  currentCashierShiftLoading,
  currentCashierShiftError,
  paymentForm,
  paymentFormErrors,
  paymentSubmitting,
  selectedPaymentIdForCancel,
  cancelPaymentReason,
  cancelPaymentSubmitting,
  onPaymentFormSubmit,
  onPaymentModeChange,
  onPaymentAmountChange,
  onPaymentReferenceChange,
  onPaymentNotesChange,
  onPaymentCancelTargetChange,
  onCancelPaymentReasonChange,
  onCancelPayment,
}: PaymentPanelProps) => {
  const { t } = useLanguage();
  const { currencyCode, locale } = useRestaurantCurrency();

  if (!bill) {
    return null;
  }

  const canRecord = canRecordPayments && bill.status !== 'Paid' && bill.status !== 'Cancelled';
  const payments = bill.payments ?? [];
  const cashPaymentNeedsShift = paymentForm.paymentMode === 'Cash' && !currentCashierShift && !currentCashierShiftLoading;

  return (
    <Card
      title={t('billing.paymentsTitle')}
      description={t('billing.paymentsDescription')}
      tone="orders"
      actions={<Badge tone="neutral" label={t('billing.paymentCount', { count: payments.length, suffix: payments.length === 1 ? '' : 's' })} />}
    >
      <div className="billing-payment-stack">
        {payments.length === 0 ? (
          <EmptyState
            title={t('billing.noPaymentsRecordedTitle')}
            description={t('billing.noPaymentsRecordedDescription')}
            tone="orders"
          />
        ) : (
          <div className="billing-payment-list">
            {payments.map(payment => {
              const isRecorded = payment.status === 'Recorded';
              const showCancelForm = canCancelPayments && isRecorded && selectedPaymentIdForCancel === payment.paymentId;

              return (
                <article key={payment.paymentId} className="billing-payment-row">
                  <div className="billing-payment-row__main">
                    <div className="billing-payment-row__header">
                      <strong>{payment.paymentNumber}</strong>
                      <div className="billing-payment-row__badges">
                        <StatusBadge
                          status={payment.status}
                          label={formatBillingPaymentStatus(payment.status, {
                            recorded: t('billing.statusRecorded'),
                            cancelled: t('billing.statusCancelled'),
                          })}
                        />
                        <Badge
                          tone="primary"
                          label={formatBillingPaymentMode(payment.paymentMode, {
                            cash: t('billing.paymentModeCash'),
                            card: t('billing.paymentModeCard'),
                            upi: t('billing.paymentModeUpi'),
                          })}
                        />
                      </div>
                    </div>
                    <div className="billing-payment-row__meta">
                      <span>
                        {t('billing.amountLabel')}: {formatBillingCurrency(payment.amount, currencyCode, locale)}
                      </span>
                      <span>
                        {t('billing.createdLabel')}: {formatBillingTimestamp(payment.createdAt, { notAvailable: t('billing.notAvailable') })}
                      </span>
                      {payment.referenceNumber ? (
                        <span>
                          {t('billing.referenceLabel')}: {payment.referenceNumber}
                        </span>
                      ) : null}
                      {payment.notes ? (
                        <span>
                          {t('pos.notesLabel')}: {payment.notes}
                        </span>
                      ) : null}
                      {payment.cancelledAt ? (
                        <span>
                          {t('billing.cancelledLabel')}: {formatBillingTimestamp(payment.cancelledAt, { notAvailable: t('billing.notAvailable') })}
                        </span>
                      ) : null}
                      {payment.cancelReason ? (
                        <span>
                          {t('billing.cancelReasonLabel')}: {payment.cancelReason}
                        </span>
                      ) : null}
                    </div>
                  </div>

                  {canCancelPayments && isRecorded ? (
                    <div className="billing-payment-row__actions">
                      {showCancelForm ? (
                        <form
                          className="admin-form-section"
                          onSubmit={(event: FormEvent<HTMLFormElement>) => {
                            event.preventDefault();
                            onCancelPayment();
                          }}
                        >
                          <Input
                            label={t('billing.cancelPaymentReasonLabel')}
                            value={cancelPaymentReason}
                            onChange={event => onCancelPaymentReasonChange(event.target.value)}
                            helperText={t('billing.cancelPaymentReasonHelp')}
                            placeholder={t('billing.cancelPaymentReasonPlaceholder')}
                          />
                          <div className="admin-form-actions">
                            <Button
                              type="submit"
                              variant="danger"
                              disabled={!cancelPaymentReason.trim() || cancelPaymentSubmitting}
                            >
                              {cancelPaymentSubmitting ? t('billing.cancelling') : t('billing.confirmCancel')}
                            </Button>
                            <Button
                              type="button"
                              variant="secondary"
                              disabled={cancelPaymentSubmitting}
                              onClick={() => onPaymentCancelTargetChange(null)}
                            >
                              {t('billing.close')}
                            </Button>
                          </div>
                        </form>
                      ) : payment.status === 'Recorded' ? (
                        <Button
                          type="button"
                          variant="secondary"
                          disabled={cancelPaymentSubmitting}
                          onClick={() => onPaymentCancelTargetChange(payment.paymentId)}
                        >
                          {t('billing.cancelPayment')}
                        </Button>
                      ) : null}
                    </div>
                  ) : null}
                </article>
              );
            })}
          </div>
        )}

        {canRecord ? (
          <form className="admin-form-section" onSubmit={onPaymentFormSubmit}>
            <div className="admin-form-grid">
              <Select
                label={t('billing.paymentModeLabel')}
                value={paymentForm.paymentMode}
                onChange={event => onPaymentModeChange(event.target.value as BillingPaymentEntryMode)}
              >
                <option value="Cash">{t('billing.paymentModeCash')}</option>
                <option value="Card">{t('billing.paymentModeCard')}</option>
                <option value="Upi">{t('billing.paymentModeUpi')}</option>
              </Select>
              <Input
                label={t('billing.amountLabel')}
                value={paymentForm.amount}
                onChange={event => onPaymentAmountChange(event.target.value)}
                error={paymentFormErrors.amount}
                inputMode="decimal"
                placeholder={t('billing.amountPlaceholder')}
              />
            </div>
            <div className="admin-form-grid">
              <Input
                label={t('billing.referenceNumberLabel')}
                value={paymentForm.referenceNumber}
                onChange={event => onPaymentReferenceChange(event.target.value)}
                error={paymentFormErrors.referenceNumber}
                helperText={
                  paymentForm.paymentMode === 'Cash'
                    ? t('billing.optionalForCash')
                    : t('billing.referenceRequiredForUpiAndCard')
                }
                placeholder={t('billing.paymentReferencePlaceholder')}
              />
              <Input
                label={t('billing.paymentNotesLabel')}
                value={paymentForm.notes}
                onChange={event => onPaymentNotesChange(event.target.value)}
                placeholder={t('billing.paymentNotesPlaceholder')}
              />
            </div>
            {currentCashierShiftError ? (
              <div className="admin-notice admin-notice--danger" role="alert">
                {currentCashierShiftError}
              </div>
            ) : cashPaymentNeedsShift ? (
              <div className="admin-notice admin-notice--warning" role="alert">
                {t('billing.cashPaymentRequiresShift')}
              </div>
            ) : currentCashierShift ? (
              <div className="admin-form-note">
                {t('billing.cashPaymentAttachedToShift')}
              </div>
            ) : null}
            <div className="admin-form-actions">
              <Button type="submit" disabled={paymentSubmitting}>
                {paymentSubmitting ? t('billing.recording') : t('billing.recordPayment')}
              </Button>
            </div>
            <div className="admin-form-note">
              {t('billing.backendControlNote')}
            </div>
          </form>
        ) : (
          <div className="admin-form-note">
            {t('billing.paymentReadOnlyNote')}
          </div>
        )}
      </div>
    </Card>
  );
};

export default PaymentPanel;
