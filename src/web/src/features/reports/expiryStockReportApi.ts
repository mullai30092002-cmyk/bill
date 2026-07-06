import { requestJson } from '../../api/apiClient';
import type { ExpiryStockReportQuery, ExpiryStockReportResponse } from './expiryStockReportTypes';

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

export const getExpiryStockReport = (query?: ExpiryStockReportQuery) =>
  requestJson<ExpiryStockReportResponse>(
    `/api/v1/reports/expiry-stock${buildQueryString({
      branchId: query?.branchId,
      asOfDate: query?.asOfDate,
    })}`
  );
