import { FormEvent } from 'react';

import { Button, Card, Input } from '../../components/ui';
import { useLanguage } from '../../i18n/LanguageProvider';
import type { CashierShiftOpenFormState, CashierShiftOpenValidationErrors } from './cashierShiftValidation';

export interface OpenShiftPanelProps {
  form: CashierShiftOpenFormState;
  errors: CashierShiftOpenValidationErrors;
  submitting?: boolean;
  canOpenShift?: boolean;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
  onBusinessDateChange: (value: string) => void;
  onOpeningCashAmountChange: (value: string) => void;
}

export const OpenShiftPanel = ({
  form,
  errors,
  submitting,
  canOpenShift = true,
  onSubmit,
  onBusinessDateChange,
  onOpeningCashAmountChange,
}: OpenShiftPanelProps) => (
  <OpenShiftPanelContent
    form={form}
    errors={errors}
    submitting={submitting}
    canOpenShift={canOpenShift}
    onSubmit={onSubmit}
    onBusinessDateChange={onBusinessDateChange}
    onOpeningCashAmountChange={onOpeningCashAmountChange}
  />
);

const OpenShiftPanelContent = ({
  form,
  errors,
  submitting,
  canOpenShift,
  onSubmit,
  onBusinessDateChange,
  onOpeningCashAmountChange,
}: OpenShiftPanelProps) => {
  const { t } = useLanguage();

  return (
    <Card title={t('cashier.openShiftTitle')} description={t('cashier.openShiftDescription')} tone="orders">
    <form className="admin-form" onSubmit={onSubmit}>
      <div className="admin-form-grid">
        <Input
          label={t('cashier.businessDateLabel')}
          type="date"
          value={form.businessDate}
          onChange={event => onBusinessDateChange(event.target.value)}
          error={errors.businessDate}
          helperText={t('cashier.businessDateHelper')}
        />
        <Input
          label={t('cashier.openingCashAmountLabel')}
          value={form.openingCashAmount}
          onChange={event => onOpeningCashAmountChange(event.target.value)}
          error={errors.openingCashAmount}
          inputMode="decimal"
          placeholder="100.00"
          helperText={t('cashier.openingCashAmountHelper')}
        />
      </div>

      <div className="admin-form-note">
        {t('cashier.openingCashNote')}
      </div>

      {canOpenShift ? (
        <div className="admin-form-actions">
          <Button type="submit" size="lg" disabled={submitting}>
            {submitting ? t('cashier.openingShiftAction') : t('cashier.openShiftAction')}
          </Button>
        </div>
      ) : (
        <div className="admin-form-note">{t('cashier.openShiftReadOnlyNote')}</div>
      )}
    </form>
    </Card>
  );
};

export default OpenShiftPanel;
