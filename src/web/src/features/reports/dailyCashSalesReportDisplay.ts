import { formatCurrency } from '../finance/currencyDisplay';
import type { DailyCashSalesReportResponse } from './dailyCashSalesReportTypes';

export const formatDailyCashSalesCurrency = (value: number, currencyCode?: string | null, locale?: string | null) =>
  formatCurrency(value, currencyCode, locale);

export interface DailyCashSalesReportMessages {
  notAvailable: string;
}

export const formatDailyCashSalesTimestamp = (value?: string | null, messages?: DailyCashSalesReportMessages) => {
  if (!value) {
    return messages?.notAvailable ?? 'Not available';
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

export const formatDailyCashSalesDateInput = (date = new Date()) => {
  const year = date.getFullYear();
  const month = `${date.getMonth() + 1}`.padStart(2, '0');
  const day = `${date.getDate()}`.padStart(2, '0');
  return `${year}-${month}-${day}`;
};

export const formatDailyCashSalesPaymentMode = (value: string) => (value === 'Upi' ? 'UPI' : value);

export const getDailyCashSalesSeverityTone = (severity: DailyCashSalesReportResponse['exceptions']['unpaidBills'][number]['severity']) => {
  switch (severity) {
    case 'High':
      return 'danger' as const;
    case 'Medium':
      return 'warning' as const;
    default:
      return 'info' as const;
  }
};

export const buildDailyCashSalesScopeSummary = (report: DailyCashSalesReportResponse) =>
  [
    report.restaurantName,
    report.branchName,
    report.businessDate,
  ]
    .filter(Boolean)
    .join(' · ');
