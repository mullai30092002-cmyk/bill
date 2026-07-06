import { requestJson } from '../../api/apiClient';
import type { VendorPayablesReportQuery, VendorPayablesReportResponse } from './vendorPayablesReportTypes';

const buildQueryString = (query?: Record<string, string | number | boolean | null | undefined>) => {
  if (!query) {
    return '';
  }

  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (value === undefined || value === null || value === '') {
      continue;
    }

    params.set(key, String(value));
  }

  const queryString = params.toString();
  return queryString ? `?${queryString}` : '';
};

export const getVendorPayablesReport = (query?: VendorPayablesReportQuery) =>
  requestJson<VendorPayablesReportResponse>(
    `/api/v1/reports/vendor-payables${buildQueryString({
      branchId: query?.branchId,
      fromDate: query?.fromDate,
      toDate: query?.toDate,
    })}`
  );
