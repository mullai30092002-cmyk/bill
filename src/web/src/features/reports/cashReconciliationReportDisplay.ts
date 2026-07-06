import { formatCurrency } from '../finance/currencyDisplay';
import type { CashReconciliationReportResponse, CashReconciliationVarianceStatus } from './cashReconciliationReportTypes';

export const formatCashReconciliationCurrency = (value: number, currencyCode?: string | null, locale?: string | null) =>
  formatCurrency(value, currencyCode, locale);

export const formatCashReconciliationDateInput = (date = new Date()) => {
  const year = date.getFullYear();
  const month = `${date.getMonth() + 1}`.padStart(2, '0');
  const day = `${date.getDate()}`.padStart(2, '0');
  return `${year}-${month}-${day}`;
};

export const formatCashReconciliationTimestamp = (value?: string | null, notAvailable = 'Not available') => {
  if (!value) {
    return notAvailable;
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return parsed.toLocaleString(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  });
};

export const buildCashReconciliationScopeSummary = (report: CashReconciliationReportResponse) =>
  [report.restaurantName, report.branchName, report.businessDate].filter(Boolean).join(' · ');

export const getCashReconciliationVarianceTone = (status: CashReconciliationVarianceStatus) => {
  switch (status) {
    case 'Balanced':
      return 'success' as const;
    case 'MinorVariance':
      return 'warning' as const;
    case 'MajorVariance':
      return 'danger' as const;
    case 'OpenShift':
      return 'info' as const;
    default:
      return 'neutral' as const;
  }
};
