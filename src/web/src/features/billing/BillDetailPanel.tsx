import { useLanguage } from '../../i18n/LanguageProvider';
import type { FormEvent } from 'react';

import { Badge, Card, EmptyState, StatusBadge } from '../../components/ui';
import { useRestaurantCurrency } from '../auth/useRestaurantCurrency';
import BillReceiptPreviewCard from './BillReceiptPreviewCard';
import { formatBillingBillStatus, formatBillingCurrency, formatBillingDate, formatBillingTimestamp } from './billingDisplay';
import { getSafeBillingErrorMessage } from './billingErrorDisplay';
import type { BillingPaymentValidationErrors } from './billingValidation';
import type { BillDetail, BillingPaymentEntryMode } from './billingTypes';
import type { BillReceiptResponse } from './billReceiptTypes';
import type { CashierShiftDetail } from '../cashiering/cashierShiftTypes';
import BillStatusActions from './BillStatusActions';
import PaymentPanel, { type PaymentFormState } from './PaymentPanel';

export interface BillDetailPanelProps {
  bill: BillDetail | null;
  loading?: boolean;
  error?: string | null;
  selectedBillLabel?: string;
  canManageBills: boolean;
  canRecordPayments: boolean;
  canCancelPayments: boolean;
  currentCashierShift: CashierShiftDetail | null;
  currentCashierShiftLoading: boolean;
  currentCashierShiftError: string | null;
  receipt: BillReceiptResponse | null;
  receiptLoading: boolean;
  receiptPrinting: boolean;
  receiptError: string | null;
  canViewReceipt: boolean;
  canPrintReceipt: boolean;
  paymentForm: PaymentFormState;
  paymentFormErrors: BillingPaymentValidationErrors;
  paymentSubmitting: boolean;
  selectedPaymentIdForCancel: string | null;
  cancelPaymentReason: string;
  cancelPaymentSubmitting: boolean;
  billCancelReason: string;
  billCancelSubmitting: boolean;
  onRetry?: () => void;
  onPaymentFormSubmit: (event: FormEvent<HTMLFormElement>) => void;
  onPaymentModeChange: (value: BillingPaymentEntryMode) => void;
  onPaymentAmountChange: (value: string) => void;
  onPaymentReferenceChange: (value: string) => void;
  onPaymentNotesChange: (value: string) => void;
  onPaymentCancelTargetChange: (paymentId: string | null) => void;
  onCancelPaymentReasonChange: (value: string) => void;
  onCancelPayment: () => void;
  onBillCancelReasonChange: (value: string) => void;
  onCancelBill: () => void;
  onViewReceipt: () => void;
  onPrintReceipt: () => void;
  onRetryReceipt?: () => void;
}

export const BillDetailPanel = ({
  bill,
  loading = false,
  error,
  selectedBillLabel,
  canManageBills,
  canRecordPayments,
  canCancelPayments,
  currentCashierShift,
  currentCashierShiftLoading,
  currentCashierShiftError,
  receipt,
  receiptLoading,
  receiptPrinting,
  receiptError,
  canViewReceipt,
  canPrintReceipt,
  paymentForm,
  paymentFormErrors,
  paymentSubmitting,
  selectedPaymentIdForCancel,
  cancelPaymentReason,
  cancelPaymentSubmitting,
  billCancelReason,
  billCancelSubmitting,
  onRetry,
  onPaymentFormSubmit,
  onPaymentModeChange,
  onPaymentAmountChange,
  onPaymentReferenceChange,
  onPaymentNotesChange,
  onPaymentCancelTargetChange,
  onCancelPaymentReasonChange,
  onCancelPayment,
  onBillCancelReasonChange,
  onCancelBill,
  onViewReceipt,
  onPrintReceipt,
  onRetryReceipt,
}: BillDetailPanelProps) => {
  const { t } = useLanguage();
  const { currencyCode, locale } = useRestaurantCurrency();

  if (loading && !bill) {
    return (
      <Card title={t('billing.selectedBillCardTitle')} description={t('billing.selectedBillCardDescription')} tone="orders">
        <EmptyState title={t('billing.loadingBillTitle')} description={t('billing.loadingBillDescription')} tone="orders" />
      </Card>
    );
  }

  if (error && !bill) {
    return (
      <Card title={t('billing.selectedBillCardTitle')} description={t('billing.selectedBillCardDescription')} tone="orders">
        <EmptyState
          title={t('billing.couldNotLoadBillTitle')}
          description={getSafeBillingErrorMessage(error, t('billing.errorLoadSelectedBill'), {
            sessionExpired: t('billing.sessionExpired'),
            unauthorized: t('billing.unauthorizedBilling'),
          })}
          tone="orders"
          actionLabel={onRetry ? t('billing.tryAgain') : undefined}
          onAction={onRetry}
        />
      </Card>
    );
  }

  if (!bill) {
    return (
      <Card title={t('billing.selectedBillCardTitle')} description={t('billing.selectedBillCardDescription')} tone="orders">
        <EmptyState
          title={t('billing.noBillSelectedTitle')}
          description={t('billing.noBillSelectedDescription')}
          tone="orders"
        />
      </Card>
    );
  }

  const lines = bill.lines ?? [];
  const lineCount = lines.length;

  return (
    <div className="billing-detail-stack">
      <Card
        title={t('billing.selectedBillCardTitle')}
        description={t('billing.selectedBillCardDescription')}
        tone="orders"
        actions={
          <>
            <Badge tone="neutral" label={selectedBillLabel ?? bill.billNumber} />
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
              <span className="admin-selected-user__label">{t('pos.lines')}</span>
              <strong>{t('pos.lineCount', { count: lineCount, suffix: lineCount === 1 ? '' : 's' })}</strong>
            </div>
            <div className="admin-selected-user__row">
              <span className="admin-selected-user__label">{t('billing.businessDateLabel')}</span>
              <strong>{formatBillingDate(bill.businessDate, { notAvailable: t('billing.notAvailable') })}</strong>
            </div>
            <div className="admin-selected-user__row">
              <span className="admin-selected-user__label">{t('pos.created')}</span>
              <strong>{formatBillingTimestamp(bill.createdAt, { notAvailable: t('billing.notAvailable') })}</strong>
            </div>
            <div className="admin-selected-user__row">
              <span className="admin-selected-user__label">{t('pos.updated')}</span>
              <strong>{formatBillingTimestamp(bill.updatedAt, { notAvailable: t('billing.notAvailable') })}</strong>
            </div>
            {bill.cancelledAt ? (
              <div className="admin-selected-user__row">
                <span className="admin-selected-user__label">{t('billing.cancelledLabel')}</span>
                <strong>{formatBillingTimestamp(bill.cancelledAt, { notAvailable: t('billing.notAvailable') })}</strong>
              </div>
            ) : null}
            {bill.cancelReason ? (
              <div className="admin-selected-user__row">
                <span className="admin-selected-user__label">{t('billing.cancelReasonLabel')}</span>
                <strong>{bill.cancelReason}</strong>
              </div>
            ) : null}
          </div>
        </div>
      </Card>

      <Card
        title={t('billing.billLinesTitle', { billNumber: bill.billNumber })}
        description={t('billing.billLinesDescription')}
        tone="orders"
      >
        {lines.length === 0 ? (
          <EmptyState title={t('billing.noBillLinesTitle')} description={t('billing.noBillLinesDescription')} tone="orders" />
        ) : (
          <div className="order-line-list">
            {lines.map(line => (
              <div key={line.billLineId} className="order-line">
                <div className="order-line__main">
                  <strong>{line.menuItemNameSnapshot}</strong>
                  <span>
                    {line.quantity} x {line.menuCategoryNameSnapshot}
                  </span>
                  {line.skuSnapshot ? (
                    <span>
                      {t('pos.skuLabel')}: {line.skuSnapshot}
                    </span>
                  ) : null}
                  {line.notes ? (
                    <span>
                      {t('pos.notesLabel')}: {line.notes}
                    </span>
                  ) : null}
                </div>
                <strong>{formatBillingCurrency(line.lineTotal, currencyCode, locale)}</strong>
              </div>
            ))}
          </div>
        )}
      </Card>

      {canManageBills ? (
        <BillStatusActions
          bill={bill}
          canManageBills={canManageBills}
          billCancelReason={billCancelReason}
          billCancelSubmitting={billCancelSubmitting}
          onBillCancelReasonChange={onBillCancelReasonChange}
          onCancelBill={onCancelBill}
        />
      ) : null}

      <PaymentPanel
        bill={bill}
        canRecordPayments={canRecordPayments}
        canCancelPayments={canCancelPayments}
        currentCashierShift={currentCashierShift}
        currentCashierShiftLoading={currentCashierShiftLoading}
        currentCashierShiftError={currentCashierShiftError}
        paymentForm={paymentForm}
        paymentFormErrors={paymentFormErrors}
        paymentSubmitting={paymentSubmitting}
        selectedPaymentIdForCancel={selectedPaymentIdForCancel}
        cancelPaymentReason={cancelPaymentReason}
        cancelPaymentSubmitting={cancelPaymentSubmitting}
        onPaymentFormSubmit={onPaymentFormSubmit}
        onPaymentModeChange={onPaymentModeChange}
        onPaymentAmountChange={onPaymentAmountChange}
        onPaymentReferenceChange={onPaymentReferenceChange}
        onPaymentNotesChange={onPaymentNotesChange}
        onPaymentCancelTargetChange={onPaymentCancelTargetChange}
        onCancelPaymentReasonChange={onCancelPaymentReasonChange}
        onCancelPayment={onCancelPayment}
      />

      <div className="billing-receipt-shell">
        <BillReceiptPreviewCard
          bill={bill}
          receipt={receipt}
          loading={receiptLoading}
          printing={receiptPrinting}
          error={receiptError}
          canViewReceipt={canViewReceipt}
          canPrintReceipt={canPrintReceipt}
          onViewReceipt={onViewReceipt}
          onPrintReceipt={onPrintReceipt}
          onRetry={onRetryReceipt}
        />
      </div>
    </div>
  );
};

export default BillDetailPanel;
