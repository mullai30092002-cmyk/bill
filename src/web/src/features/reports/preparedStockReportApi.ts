import { requestJson } from '../../api/apiClient';
import type { PreparedStockReportQuery, PreparedStockReportResponse } from './preparedStockReportTypes';

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

export const getPreparedStockReport = (query?: PreparedStockReportQuery) =>
  requestJson<PreparedStockReportResponse>(
    `/api/v1/reports/prepared-stock${buildQueryString({
      branchId: query?.branchId,
      businessDate: query?.businessDate,
    })}`
  );
