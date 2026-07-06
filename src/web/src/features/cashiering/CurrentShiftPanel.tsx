import { Button, Card, SummaryCard, StatusBadge } from '../../components/ui';
import { useLanguage } from '../../i18n/LanguageProvider';
import { formatCashierCurrency, formatCashierTimestamp } from './cashierShiftDisplay';
import { formatCashierShiftStatus } from './cashierShiftDisplay';
import type { CashierShiftDetail } from './cashierShiftTypes';

export interface CurrentShiftPanelProps {
  shift: CashierShiftDetail;
  currencyCode?: string;
  canCloseShift?: boolean;
  onClose: () => void;
}

export const CurrentShiftPanel = ({
  shift,
  currencyCode,
  canCloseShift = true,
  onClose,
}: CurrentShiftPanelProps) => {
  const currency = currencyCode ?? 'INR';
  const { t } = useLanguage();
  const statusLabel = formatCashierShiftStatus(shift.status, {
    statusOpen: t('cashier.statusOpen'),
    statusClosed: t('cashier.statusClosed'),
    statusVoided: t('cashier.statusVoided'),
    notAvailable: t('cashier.notAvailable'),
  });

  return (
    <Card
      title={t('cashier.activeShiftTitle')}
      description={t('cashier.activeShiftDescription')}
      tone="dashboard"
      actions={canCloseShift ? <Button variant="danger" onClick={onClose}>{t('cashier.closeShiftAction')}</Button> : null}
    >
      <div className="summary-grid">
        <SummaryCard label={t('cashier.cashierLabel')} value={shift.cashierName} tone="orders" />
        <SummaryCard label={t('cashier.branchLabel')} value={shift.branchName} tone="inventory" />
        <SummaryCard label={t('cashier.businessDateLabel')} value={shift.businessDate.slice(0, 10)} tone="admin" />
        <SummaryCard label={t('cashier.openedLabel')} value={formatCashierTimestamp(shift.openedAtUtc, t('cashier.notAvailable'))} tone="dashboard" />
        <SummaryCard
          label={t('cashier.openingCashLabel')}
          value={formatCashierCurrency(shift.openingCashAmount, currency)}
          tone="accent"
        />
        <SummaryCard
          label={t('cashier.expectedCashLabel')}
          value={formatCashierCurrency(shift.expectedClosingCashAmount, currency)}
          tone="inventory"
        />
      </div>

      <div className="admin-form-note">
        <strong>{t('cashier.statusLabel')}:</strong> <StatusBadge status={shift.status} label={statusLabel} />
        <br />
        <strong>{t('cashier.shiftIdLabel')}:</strong> {shift.cashierShiftId}
        <br />
        {t('cashier.expectedCashMatchesOpeningNote')}
      </div>
    </Card>
  );
};

export default CurrentShiftPanel;
