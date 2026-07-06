import { formatCurrency } from '../finance/currencyDisplay';
import type { VendorPayablesReportResponse } from './vendorPayablesReportTypes';

export const formatVendorPayablesCurrency = (value: number, currencyCode?: string | null, locale?: string | null) =>
  formatCurrency(value, currencyCode, locale);

export interface VendorPayablesReportMessages {
  notAvailable: string;
}

export const formatVendorPayablesTimestamp = (value?: string | null, messages?: VendorPayablesReportMessages) => {
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

export const formatVendorPayablesDateInput = (date = new Date()) => {
  const year = date.getFullYear();
  const month = `${date.getMonth() + 1}`.padStart(2, '0');
  const day = `${date.getDate()}`.padStart(2, '0');
  return `${year}-${month}-${day}`;
};

export const formatVendorPayablesScopeSummary = (report: VendorPayablesReportResponse) =>
  [report.restaurantName, report.branchName, `${report.fromDate} to ${report.toDate}`]
    .filter(Boolean)
    .join(' · ');

export const formatVendorPayablesReference = (value: string | null | undefined, messages?: VendorPayablesReportMessages) => {
  if (!value) {
    return messages?.notAvailable ?? 'Not available';
  }

  return value;
};
