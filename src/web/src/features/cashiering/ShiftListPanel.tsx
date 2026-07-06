import { Badge, Card, EmptyState, StatusBadge } from '../../components/ui';
import { useLanguage } from '../../i18n/LanguageProvider';
import { formatCashierCurrency, formatCashierSignedCurrency, formatCashierTimestamp, sortCashierShiftsNewestFirst } from './cashierShiftDisplay';
import { formatCashierShiftStatus } from './cashierShiftDisplay';
import type { CashierShiftListItem } from './cashierShiftTypes';

export interface ShiftListPanelProps {
  shifts: CashierShiftListItem[];
  loading?: boolean;
  error?: string | null;
  currencyCode?: string;
  onRetry?: () => void;
}

export const ShiftListPanel = ({ shifts, loading, error, currencyCode, onRetry }: ShiftListPanelProps) => {
  const currency = currencyCode ?? 'INR';
  const sortedShifts = sortCashierShiftsNewestFirst(shifts);
  const { t } = useLanguage();
  const statusMessages = {
    statusOpen: t('cashier.statusOpen'),
    statusClosed: t('cashier.statusClosed'),
    statusVoided: t('cashier.statusVoided'),
    notAvailable: t('cashier.notAvailable'),
  };

  return (
    <Card
      title={t('cashier.shiftHistoryTitle')}
      description={t('cashier.shiftHistoryDescription')}
      tone="orders"
      actions={<Badge tone="neutral" label={loading ? t('cashier.refreshing') : t('cashier.shiftHistoryCount', { count: sortedShifts.length })} />}
    >
      {error ? (
        <EmptyState
          title={t('cashier.couldNotLoadShiftHistoryTitle')}
          description={error}
          tone="orders"
          actionLabel={onRetry ? t('cashier.tryAgain') : undefined}
          onAction={onRetry}
        />
      ) : sortedShifts.length === 0 ? (
        <EmptyState
          title={t('cashier.noShiftsFoundTitle')}
          description={t('cashier.noShiftsFoundDescription')}
          tone="orders"
        />
      ) : (
        <div className="admin-table-wrapper">
          <table className="admin-table">
            <thead>
              <tr>
                <th>{t('cashier.businessDateColumn')}</th>
                <th>{t('cashier.cashierColumn')}</th>
                <th>{t('cashier.branchColumn')}</th>
                <th>{t('cashier.openedColumn')}</th>
                <th>{t('cashier.closedColumn')}</th>
                <th>{t('cashier.openingCashColumn')}</th>
                <th>{t('cashier.declaredCashColumn')}</th>
                <th>{t('cashier.expectedCashColumn')}</th>
                <th>{t('cashier.varianceColumn')}</th>
                <th>{t('cashier.statusColumn')}</th>
              </tr>
            </thead>
            <tbody>
              {sortedShifts.map(shift => (
                <tr key={shift.cashierShiftId}>
                  <td>{shift.businessDate.slice(0, 10)}</td>
                  <td>{shift.cashierName}</td>
                  <td>{shift.branchName}</td>
                  <td>{formatCashierTimestamp(shift.openedAtUtc, t('cashier.notAvailable'))}</td>
                  <td>{formatCashierTimestamp(shift.closedAtUtc, t('cashier.notAvailable'))}</td>
                  <td>{formatCashierCurrency(shift.openingCashAmount, currency)}</td>
                  <td>{shift.declaredClosingCashAmount === null ? t('cashier.statusOpen') : formatCashierCurrency(shift.declaredClosingCashAmount, currency)}</td>
                  <td>{formatCashierCurrency(shift.expectedClosingCashAmount, currency)}</td>
                  <td>
                    {shift.cashVarianceAmount === null
                      ? t('cashier.statusOpen')
                      : formatCashierSignedCurrency(shift.cashVarianceAmount, currency)}
                  </td>
                  <td>
                    <StatusBadge status={shift.status} label={formatCashierShiftStatus(shift.status, statusMessages)} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Card>
  );
};

export default ShiftListPanel;
