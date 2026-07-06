import { formatCurrency } from '../finance/currencyDisplay';
import type { OwnerDashboardAlert, OwnerDashboardResponse } from './ownerDashboardTypes';

export const formatOwnerDashboardCurrency = (value: number, currencyCode?: string | null, locale?: string | null) =>
  formatCurrency(value, currencyCode, locale);

export interface OwnerDashboardMessages {
  notAvailable: string;
}

export const formatOwnerDashboardQuantity = (value: number) =>
  new Intl.NumberFormat(undefined, { maximumFractionDigits: 2, minimumFractionDigits: value % 1 === 0 ? 0 : 2 }).format(value);

export const formatOwnerDashboardDateInput = (date = new Date()) => {
  const year = date.getFullYear();
  const month = `${date.getMonth() + 1}`.padStart(2, '0');
  const day = `${date.getDate()}`.padStart(2, '0');
  return `${year}-${month}-${day}`;
};

export const formatOwnerDashboardTimestamp = (value?: string | null, messages?: OwnerDashboardMessages) => {
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

export const getOwnerDashboardSeverityTone = (severity: OwnerDashboardAlert['severity']) => {
  switch (severity) {
    case 'High':
      return 'danger' as const;
    case 'Medium':
      return 'warning' as const;
    default:
      return 'info' as const;
  }
};

export const getOwnerDashboardCardTone = (severity: OwnerDashboardAlert['severity']) => {
  switch (severity) {
    case 'High':
      return 'admin' as const;
    case 'Medium':
      return 'inventory' as const;
    default:
      return 'dashboard' as const;
  }
};

export const sortOwnerDashboardAlerts = (alerts: OwnerDashboardAlert[]) =>
  [...alerts].sort((left, right) => {
    const severityOrder: Record<OwnerDashboardAlert['severity'], number> = {
      High: 0,
      Medium: 1,
      Low: 2,
    };

    const severityDelta = severityOrder[left.severity] - severityOrder[right.severity];
    if (severityDelta !== 0) {
      return severityDelta;
    }

    return right.count - left.count;
  });

export const buildOwnerDashboardScopeSummary = (report: OwnerDashboardResponse) =>
  [report.restaurantName, report.branchName, report.businessDate].filter(Boolean).join(' · ');
