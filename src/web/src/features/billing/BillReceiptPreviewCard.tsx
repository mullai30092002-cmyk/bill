import { Badge, Button, Card, EmptyState, StatusBadge } from '../../components/ui';
import { useLanguage } from '../../i18n/LanguageProvider';
import {
  buildReceiptActorLabel,
  formatReceiptCurrency,
  formatReceiptDate,
  formatReceiptPaymentMode,
  formatReceiptTimestamp,
} from './billReceiptDisplay';
import { getSafeBillReceiptErrorMessage } from './billReceiptErrorDisplay';
import { formatBillingBillStatus, formatBillingOrderType, formatBillingPaymentStatus } from './billingDisplay';
import type { BillDetail } from './billingTypes';
import type { BillReceiptResponse } from './billReceiptTypes';

export interface BillReceiptPreviewCardProps {
  bill: BillDetail | null;
  receipt: BillReceiptResponse | null;
  loading?: boolean;
  printing?: boolean;
  error?: string | null;
  canViewReceipt: boolean;
  canPrintReceipt: boolean;
  onViewReceipt: () => void;
  onPrintReceipt: () => void;
  onRetry?: () => void;
}

export const BillReceiptPreviewCard = ({
  bill,
  receipt,
  loading = false,
  printing = false,
  error,
  canViewReceipt,
  canPrintReceipt,
  onViewReceipt,
  onPrintReceipt,
  onRetry,
}: BillReceiptPreviewCardProps) => {
  const { t } = useLanguage();

  const buildReceiptSummary = (currentReceipt: BillReceiptResponse) =>
    [
      currentReceipt.restaurantName,
      currentReceipt.branchName,
      currentReceipt.orderNumberSnapshot ? t('billing.receiptOrderPrefix', { orderNumber: currentReceipt.orderNumberSnapshot }) : null,
      currentReceipt.orderTypeSnapshot
        ? formatBillingOrderType(currentReceipt.orderTypeSnapshot as 'EatIn' | 'Parcel', {
            eatIn: t('billing.orderTypeEatIn'),
            parcel: t('billing.orderTypeParcel'),
          })
        : null,
      currentReceipt.orderTableNameSnapshot ? t('billing.receiptTablePrefix', { tableName: currentReceipt.orderTableNameSnapshot }) : null,
      currentReceipt.orderCustomerNameSnapshot ? currentReceipt.orderCustomerNameSnapshot : null,
    ]
      .filter(Boolean)
      .join(' · ');

  if (!bill) {
    return (
      <Card title={t('billing.receiptPreviewTitle')} description={t('billing.receiptPreviewDescription')} tone="orders">
        <EmptyState
          title={t('billing.noBillSelectedReceiptTitle')}
          description={t('billing.noBillSelectedReceiptDescription')}
          tone="orders"
        />
      </Card>
    );
  }

  if (loading && !receipt) {
    return (
      <Card title={t('billing.receiptPreviewTitle')} description={t('billing.receiptLoadingDescription')} tone="orders">
        <EmptyState title={t('billing.loadingReceiptTitle')} description={t('billing.loadingReceiptMessage')} tone="orders" />
      </Card>
    );
  }

  if (error && !receipt) {
    return (
      <Card title={t('billing.receiptPreviewTitle')} description={t('billing.receiptLoadingDescription')} tone="orders">
        <EmptyState
          title={t('billing.couldNotLoadReceiptTitle')}
          description={getSafeBillReceiptErrorMessage(error, t('billing.errorLoadReceipt'), {
            sessionExpired: t('billing.sessionExpired'),
            unauthorized: t('billing.unauthorizedBillReceipts'),
          })}
          tone="orders"
          actionLabel={onRetry ? t('billing.tryAgain') : undefined}
          onAction={onRetry}
        />
      </Card>
    );
  }

  const lines = receipt?.lines ?? [];
  const payments = receipt?.payments ?? [];
  const isCancelledReceipt = receipt?.status === 'Cancelled';
  const receiptCopyLabel = isCancelledReceipt
    ? t('billing.cancelledCopy')
    : receipt?.isReprint
      ? t('billing.reprintCopy')
      : t('billing.originalCopy');
  const printButtonLabel = isCancelledReceipt ? t('billing.printCancelledCopy') : t('billing.printReceipt');
  const errorMessage = error
    ? getSafeBillReceiptErrorMessage(error, t('billing.errorRecordReceiptPrint'), {
        sessionExpired: t('billing.sessionExpired'),
        unauthorized: t('billing.unauthorizedBillReceipts'),
      })
    : null;

  return (
    <Card
      title={t('billing.receiptPreviewTitle')}
      description={t('billing.receiptPreviewDescription')}
      tone="orders"
      actions={
        <>
          {receipt ? <Badge tone={isCancelledReceipt ? 'warning' : receipt.isReprint ? 'warning' : 'neutral'} label={receiptCopyLabel} /> : null}
          <Button type="button" variant="secondary" disabled={!canViewReceipt || loading} onClick={onViewReceipt}>
            {loading ? t('billing.loading') : receipt ? t('billing.refreshReceipt') : t('billing.viewReceipt')}
          </Button>
          <Button type="button" disabled={!canPrintReceipt || loading || printing} onClick={onPrintReceipt}>
            {printing ? t('billing.printing') : printButtonLabel}
          </Button>
        </>
      }
    >
      {errorMessage ? (
        <div className="admin-notice admin-notice--danger" role="alert">
          {errorMessage}
        </div>
      ) : null}
      {!receipt ? (
        <EmptyState
          title={t('billing.receiptNotLoadedTitle')}
          description={t('billing.receiptNotLoadedDescription')}
          tone="orders"
        />
      ) : (
        <div className="billing-receipt">
          <div className="billing-receipt__header">
            <strong>{receipt.restaurantName}</strong>
            <span>{receipt.branchName}</span>
            {receipt.branchAddress ? <span>{receipt.branchAddress}</span> : null}
            <span>{receipt.restaurantCode}</span>
            <span>
              {receipt.countryCode} · {receipt.currencyCode} · {receipt.timeZoneId}
            </span>
          </div>

          <div className="billing-receipt__summary">
            <div className="admin-selected-user__meta">
              <div className="admin-selected-user__row">
                <span className="admin-selected-user__label">{t('billing.receiptBillNumberLabel')}</span>
                <strong>{receipt.billNumber}</strong>
              </div>
              <div className="admin-selected-user__row">
                <span className="admin-selected-user__label">{t('billing.receiptBillDateTimeLabel')}</span>
                <strong>{formatReceiptTimestamp(receipt.createdAt, { notAvailable: t('billing.notAvailable') })}</strong>
              </div>
              <div className="admin-selected-user__row">
                <span className="admin-selected-user__label">{t('billing.receiptBusinessDateLabel')}</span>
                <strong>{formatReceiptDate(receipt.businessDate, { notAvailable: t('billing.notAvailable') })}</strong>
              </div>
              <div className="admin-selected-user__row">
                <span className="admin-selected-user__label">{t('billing.receiptOrderLabel')}</span>
                <strong>{receipt.orderNumberSnapshot ?? t('billing.notAvailable')}</strong>
              </div>
              <div className="admin-selected-user__row">
                <span className="admin-selected-user__label">{t('billing.receiptOrderTypeLabel')}</span>
                <strong>
                  {receipt.orderTypeSnapshot
                    ? formatBillingOrderType(receipt.orderTypeSnapshot as 'EatIn' | 'Parcel', {
                        eatIn: t('billing.orderTypeEatIn'),
                        parcel: t('billing.orderTypeParcel'),
                      })
                    : t('billing.notAvailable')}
                </strong>
              </div>
              {receipt.orderTableNameSnapshot || receipt.orderCustomerNameSnapshot || receipt.orderCustomerMobileSnapshot ? (
                <>
                  <div className="admin-selected-user__row">
                    <span className="admin-selected-user__label">{t('billing.receiptTableLabel')}</span>
                    <strong>{receipt.orderTableNameSnapshot ?? t('billing.notAvailable')}</strong>
                  </div>
                  <div className="admin-selected-user__row">
                    <span className="admin-selected-user__label">{t('billing.receiptCustomerLabel')}</span>
                    <strong>{receipt.orderCustomerNameSnapshot ?? t('billing.notAvailable')}</strong>
                  </div>
                  <div className="admin-selected-user__row">
                    <span className="admin-selected-user__label">{t('billing.receiptMobileLabel')}</span>
                    <strong>{receipt.orderCustomerMobileSnapshot ?? t('billing.notAvailable')}</strong>
                  </div>
                </>
              ) : null}
              <div className="admin-selected-user__row">
                <span className="admin-selected-user__label">{t('billing.receiptStatusLabel')}</span>
                <StatusBadge
                  status={receipt.status}
                  label={formatBillingBillStatus(receipt.status as 'Unpaid' | 'PartiallyPaid' | 'Paid' | 'Cancelled', {
                    partiallyPaid: t('billing.statusPartiallyPaid'),
                    unpaid: t('billing.statusUnpaid'),
                    paid: t('billing.statusPaid'),
                    cancelled: t('billing.statusCancelled'),
                  })}
                />
              </div>
              <div className="admin-selected-user__row">
                <span className="admin-selected-user__label">{t('billing.receiptCreatedByLabel')}</span>
                <strong>
                  {buildReceiptActorLabel(receipt.createdByUserLabel, receipt.createdByUserId, t('billing.recordedBySystem'))}
                </strong>
              </div>
              <div className="admin-selected-user__row">
                <span className="admin-selected-user__label">
                  {receipt.printCount > 0 ? t('billing.receiptPrintedLabel') : t('billing.receiptPreviewedLabel')}
                </span>
                <strong>{formatReceiptTimestamp(receipt.printedAt, { notAvailable: t('billing.notAvailable') })}</strong>
              </div>
              <div className="admin-selected-user__row">
                <span className="admin-selected-user__label">{t('billing.receiptPrintCountLabel')}</span>
                <strong>{receipt.printCount.toString()}</strong>
              </div>
              {receipt.cancelReason ? (
                <div className="admin-selected-user__row">
                  <span className="admin-selected-user__label">{t('billing.receiptCancelReasonLabel')}</span>
                  <strong>{receipt.cancelReason}</strong>
                </div>
              ) : null}
            </div>
            <div className="summary-grid">
              <Badge tone="neutral" label={buildReceiptSummary(receipt)} />
              <Badge tone={isCancelledReceipt ? 'warning' : receipt.isReprint ? 'warning' : 'success'} label={receiptCopyLabel} />
            </div>
          </div>

          <div className="billing-receipt__amounts">
            <div className="admin-selected-user__row">
              <span className="admin-selected-user__label">{t('billing.receiptSubtotalLabel')}</span>
              <strong>{formatReceiptCurrency(receipt.subtotal, receipt.currencyCode)}</strong>
            </div>
            <div className="admin-selected-user__row">
              <span className="admin-selected-user__label">{t('billing.receiptTaxLabel')}</span>
              <strong>{formatReceiptCurrency(receipt.taxTotal, receipt.currencyCode)}</strong>
            </div>
            <div className="admin-selected-user__row">
              <span className="admin-selected-user__label">{t('billing.receiptGrandTotalLabel')}</span>
              <strong>{formatReceiptCurrency(receipt.grandTotal, receipt.currencyCode)}</strong>
            </div>
            <div className="admin-selected-user__row">
              <span className="admin-selected-user__label">{t('billing.receiptAmountPaidLabel')}</span>
              <strong>{formatReceiptCurrency(receipt.amountPaid, receipt.currencyCode)}</strong>
            </div>
            <div className="admin-selected-user__row">
              <span className="admin-selected-user__label">{t('billing.receiptBalanceDueLabel')}</span>
              <strong>{formatReceiptCurrency(receipt.balanceDue, receipt.currencyCode)}</strong>
            </div>
          </div>

          <div className="billing-receipt__section">
            <h4>{t('billing.receiptLinesTitle')}</h4>
            {lines.length === 0 ? (
              <EmptyState
                title={t('billing.receiptNoLinesTitle')}
                description={t('billing.receiptNoLinesDescription')}
                tone="orders"
              />
            ) : (
              <div className="billing-receipt__line-list">
                {lines.map(line => (
                  <article key={`${line.displayOrder}-${line.menuItemNameSnapshot}`} className="billing-receipt__line">
                    <div className="billing-receipt__line-main">
                      <strong>{line.menuItemNameSnapshot}</strong>
                      <span>{line.menuCategoryNameSnapshot}</span>
                      {line.skuSnapshot ? <span>{t('pos.skuLabel')}: {line.skuSnapshot}</span> : null}
                      <span>
                        {t('billing.qtyLabel')}: {line.quantity}
                      </span>
                      {line.notes ? <span>{t('pos.notesLabel')}: {line.notes}</span> : null}
                      <span>
                        {t('billing.displayOrderLabel')}: {line.displayOrder}
                      </span>
                    </div>
                    <div className="billing-receipt__line-totals">
                      <strong>{formatReceiptCurrency(line.lineTotal, receipt.currencyCode)}</strong>
                      <span>
                        {formatReceiptCurrency(line.unitPrice, receipt.currencyCode)} {t('billing.eachLabel')}
                      </span>
                    </div>
                  </article>
                ))}
              </div>
            )}
          </div>

          <div className="billing-receipt__section">
            <h4>{t('billing.receiptPaymentsTitle')}</h4>
            {payments.length === 0 ? (
              <EmptyState
                title={t('billing.receiptNoPaymentsTitle')}
                description={t('billing.receiptNoPaymentsDescription')}
                tone="orders"
              />
            ) : (
              <div className="billing-receipt__payment-list">
                {payments.map(payment => (
                  <article key={`${payment.paymentNumber}-${payment.createdAt}`} className="billing-receipt__payment">
                    <div className="billing-receipt__payment-main">
                      <strong>{payment.paymentNumber}</strong>
                      <span>
                        {formatReceiptPaymentMode(payment.paymentMode, {
                          cash: t('billing.paymentModeCash'),
                          card: t('billing.paymentModeCard'),
                          upi: t('billing.paymentModeUpi'),
                        })}
                      </span>
                      <span>{formatBillingPaymentStatus(payment.status, { recorded: t('billing.statusRecorded'), cancelled: t('billing.statusCancelled') })}</span>
                      <span>{formatReceiptCurrency(payment.amount, receipt.currencyCode)}</span>
                      {payment.referenceNumber ? <span>{t('billing.referenceLabel')}: {payment.referenceNumber}</span> : null}
                      {payment.notes ? <span>{t('pos.notesLabel')}: {payment.notes}</span> : null}
                      <span>
                        {t('billing.recordedByLabel')}: {buildReceiptActorLabel(payment.recordedByUserLabel, payment.recordedByUserId, t('billing.recordedBySystem'))}
                      </span>
                      <span>
                        {t('billing.createdLabel')}: {formatReceiptTimestamp(payment.createdAt, { notAvailable: t('billing.notAvailable') })}
                      </span>
                    </div>
                  </article>
                ))}
              </div>
            )}
          </div>

          <div className="admin-form-note">{t('billing.receiptThankYou')}</div>
          <div className="billing-receipt__footer">{t('software.receiptFooter')}</div>
        </div>
      )}
    </Card>
  );
};

export default BillReceiptPreviewCard;
