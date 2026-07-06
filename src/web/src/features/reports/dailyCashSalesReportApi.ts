import { requestJson } from '../../api/apiClient';
import type { DailyCashSalesReportQuery, DailyCashSalesReportResponse } from './dailyCashSalesReportTypes';

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

export const getDailyCashSalesReport = (query?: DailyCashSalesReportQuery) =>
  requestJson<DailyCashSalesReportResponse>(
    `/api/v1/reports/daily-cash-sales${buildQueryString({
      date: query?.date,
      branchId: query?.branchId,
    })}`
  );
