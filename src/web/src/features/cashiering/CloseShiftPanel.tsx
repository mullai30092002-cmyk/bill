import { FormEvent } from 'react';

import { Button, Card, Input, SummaryCard } from '../../components/ui';
import { useLanguage } from '../../i18n/LanguageProvider';
import { formatCashierCurrency, formatCashierSignedCurrency } from './cashierShiftDisplay';
import { getCashierShiftVariancePreview, type CashierShiftCloseFormState, type CashierShiftCloseValidationErrors } from './cashierShiftValidation';
import type { CashierShiftDetail } from './cashierShiftTypes';

export interface CloseShiftPanelProps {
  shift: CashierShiftDetail;
  form: CashierShiftCloseFormState;
  errors: CashierShiftCloseValidationErrors;
  currencyCode?: string;
  submitting?: boolean;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
  onClose: () => void;
  onDeclaredClosingCashAmountChange: (value: string) => void;
  onCloseNotesChange: (value: string) => void;
}

export const CloseShiftPanel = ({
  shift,
  form,
  errors,
  currencyCode,
  submitting,
  onSubmit,
  onClose,
  onDeclaredClosingCashAmountChange,
  onCloseNotesChange,
}: CloseShiftPanelProps) => {
  const currency = currencyCode ?? 'INR';
  const variancePreview = getCashierShiftVariancePreview(shift.expectedClosingCashAmount, form.declaredClosingCashAmount);
  const { t } = useLanguage();

  return (
    <div className="cashier-shift-dialog" role="dialog" aria-modal="true" aria-label={t('cashier.closeShiftDialogAriaLabel')}>
      <Card
        title={t('cashier.closeShiftTitle')}
        description={t('cashier.closeShiftDescription')}
        tone="admin"
      >
        <form className="admin-form" onSubmit={onSubmit}>
          <div className="summary-grid">
            <SummaryCard
              label={t('cashier.expectedCashLabel')}
              value={formatCashierCurrency(shift.expectedClosingCashAmount, currency)}
              tone="dashboard"
            />
            <SummaryCard
              label={t('cashier.variancePreviewLabel')}
              value={variancePreview === null ? t('cashier.variancePreviewEmpty') : formatCashierSignedCurrency(variancePreview, currency)}
              tone={variancePreview === null || variancePreview === 0 ? 'inventory' : 'admin'}
              detail={t('cashier.variancePreviewDetail')}
            />
          </div>

          <div className="admin-form-grid">
            <Input
              label={t('cashier.declaredClosingCashLabel')}
              value={form.declaredClosingCashAmount}
              onChange={event => onDeclaredClosingCashAmountChange(event.target.value)}
              error={errors.declaredClosingCashAmount}
              inputMode="decimal"
              placeholder="130.00"
              helperText={t('cashier.declaredClosingCashHelper')}
            />
            <Input
              label={t('cashier.closeNotesLabel')}
              value={form.closeNotes}
              onChange={event => onCloseNotesChange(event.target.value)}
              placeholder={t('cashier.closeNotesPlaceholder')}
              helperText={t('cashier.closeNotesHelper')}
            />
          </div>

          <div className="admin-form-note">
            {t('cashier.closeShiftNote')}
          </div>

          <div className="admin-form-actions">
            <Button type="button" variant="secondary" onClick={onClose}>
              {t('cashier.cancel')}
            </Button>
            <Button type="submit" variant="danger" size="lg" disabled={submitting}>
              {submitting ? t('cashier.closingShiftAction') : t('cashier.closeShiftAction')}
            </Button>
          </div>
        </form>
      </Card>
    </div>
  );
};

export default CloseShiftPanel;
