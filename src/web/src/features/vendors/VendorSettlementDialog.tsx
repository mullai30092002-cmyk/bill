import { useId, type FormEvent } from 'react';

import { Button, Card, Input, Select, SummaryCard } from '../../components/ui';
import { useLanguage } from '../../i18n/LanguageProvider';
import { formatCurrency } from '../finance/currencyDisplay';
import type { VendorBillDetail } from './vendorTypes';

export interface VendorSettlementFormState {
  paymentMode: string;
  amount: string;
  referenceNumber: string;
  notes: string;
  paidAtUtc: string;
}

export interface VendorSettlementFormErrors {
  paymentMode?: string;
  amount?: string;
  referenceNumber?: string;
  notes?: string;
}

export interface VendorSettlementDialogProps {
  bill: VendorBillDetail;
  form: VendorSettlementFormState;
  errors: VendorSettlementFormErrors;
  previewOutstandingAmount: number | null;
  submitting: boolean;
  confirmDisabled: boolean;
  currencyCode: string;
  locale: string | null;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
  onClose: () => void;
  onPaymentModeChange: (value: string) => void;
  onAmountChange: (value: string) => void;
  onReferenceNumberChange: (value: string) => void;
  onNotesChange: (value: string) => void;
  onPaidAtChange: (value: string) => void;
}

const paymentModes = ['Cash', 'UPI', 'Card', 'BankTransfer', 'Other'];

const formatDateTimeInput = (value: string) => value.slice(0, 16);
const formatBillNumber = (value: string | null | undefined) => value?.trim() || '-';

const TextAreaField = ({
  label,
  value,
  onChange,
  helperText,
  error,
  placeholder,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  helperText?: string;
  error?: string;
  placeholder?: string;
}) => {
  const generatedId = useId();
  const id = `${label.toLowerCase().replace(/[^a-z0-9]+/g, '-')}-${generatedId}`;
  const helperId = helperText || error ? `${id}-hint` : undefined;

  return (
    <label className="ui-field" htmlFor={id}>
      <span className="ui-field__label">{label}</span>
      <textarea
        id={id}
        className={['ui-input', error && 'ui-input--error', 'vendor-textarea'].filter(Boolean).join(' ')}
        value={value}
        onChange={event => onChange(event.target.value)}
        placeholder={placeholder}
        aria-invalid={Boolean(error)}
        aria-describedby={helperId}
        rows={3}
      />
      {error ? (
        <span id={helperId} className="ui-field__helper ui-field__helper--error">
          {error}
        </span>
      ) : helperText ? (
        <span id={helperId} className="ui-field__helper">
          {helperText}
        </span>
      ) : null}
    </label>
  );
};

export const VendorSettlementDialog = ({
  bill,
  form,
  errors,
  previewOutstandingAmount,
  submitting,
  confirmDisabled,
  currencyCode,
  locale,
  onSubmit,
  onClose,
  onPaymentModeChange,
  onAmountChange,
  onReferenceNumberChange,
  onNotesChange,
  onPaidAtChange,
}: VendorSettlementDialogProps) => {
  const { t } = useLanguage();
  const formatMoney = (value: number) => formatCurrency(value, currencyCode, locale);
  const remainingLabel = previewOutstandingAmount === null ? t('vendorSettlement.enterAmount') : formatMoney(previewOutstandingAmount);

  return (
    <div className="vendor-settlement-dialog" role="dialog" aria-modal="true" aria-label="Record vendor settlement">
      <Card
        title={t('vendorSettlement.recordTitle')}
        description={t('vendorSettlement.recordDescription')}
        tone="admin"
      >
        <form className="admin-form" onSubmit={onSubmit} noValidate>
          <div className="summary-grid">
            <SummaryCard label={t('vendorSettlement.vendorLabel')} value={bill.vendorName} tone="admin" detail={bill.vendorType} />
            <SummaryCard
              label={t('vendorSettlement.billReferenceLabel')}
              value={formatBillNumber(bill.billNumber)}
              tone="orders"
              detail={bill.dueDate ? t('vendorSettlement.dueDateDetail', { date: bill.dueDate.slice(0, 10) }) : t('vendorSettlement.noDueDate')}
            />
            <SummaryCard
              label={t('vendorSettlement.currentOutstandingLabel')}
              value={formatMoney(bill.balanceAmount)}
              tone="inventory"
              detail={bill.status}
            />
            <SummaryCard
              label={t('vendorSettlement.previewNewOutstandingLabel')}
              value={remainingLabel}
              tone={previewOutstandingAmount === null ? 'inventory' : previewOutstandingAmount <= 0 ? 'dashboard' : 'accent'}
              detail={t('vendorSettlement.previewDetail')}
            />
          </div>

          <div className="admin-form-grid">
            <Select
              label={t('vendorSettlement.paymentModeLabel')}
              value={form.paymentMode}
              onChange={event => onPaymentModeChange(event.target.value)}
              error={errors.paymentMode}
            >
              {paymentModes.map(mode => (
                <option key={mode} value={mode}>
                  {mode}
                </option>
              ))}
            </Select>
            <Input
              label={t('vendorSettlement.amountLabel')}
              type="number"
              min="0.01"
              step="0.01"
              value={form.amount}
              onChange={event => onAmountChange(event.target.value)}
              error={errors.amount}
            />
            <Input
              label={t('vendorSettlement.referenceLabel')}
              value={form.referenceNumber}
              onChange={event => onReferenceNumberChange(event.target.value)}
              error={errors.referenceNumber}
              helperText={t('vendorSettlement.referenceHelper')}
            />
            <Input
              label={t('vendorSettlement.paidAtLabel')}
              type="datetime-local"
              value={formatDateTimeInput(form.paidAtUtc)}
              onChange={event => onPaidAtChange(event.target.value)}
            />
          </div>

          <TextAreaField
            label={t('vendorSettlement.noteLabel')}
            value={form.notes}
            onChange={onNotesChange}
            error={errors.notes}
            helperText={t('vendorSettlement.noteHelper')}
            placeholder="Reason for this settlement"
          />

          <div className="admin-form-note">
            {t('vendorSettlement.amountNote')}
          </div>

          <div className="admin-form-actions">
            <Button type="button" variant="secondary" onClick={onClose} disabled={submitting}>
              {t('vendorSettlement.cancelButton')}
            </Button>
            <Button type="submit" size="lg" disabled={submitting || confirmDisabled}>
              {submitting ? t('vendorSettlement.savingButton') : t('vendorSettlement.confirmButton')}
            </Button>
          </div>
        </form>
      </Card>
    </div>
  );
};

export default VendorSettlementDialog;
