import { requestJson } from '../../api/apiClient';
import type { VendorStatementQuery, VendorStatementResponse } from './vendorStatementTypes';

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

export const getVendorStatement = (query: VendorStatementQuery) =>
  requestJson<VendorStatementResponse>(
    `/api/v1/vendors/${query.vendorId}/statement${buildQueryString({
      branchId: query.branchId,
      fromDate: query.fromDate,
      toDate: query.toDate,
    })}`
  );
