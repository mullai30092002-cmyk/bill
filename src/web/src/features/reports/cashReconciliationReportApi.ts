import { requestJson } from '../../api/apiClient';
import type { CashReconciliationReportQuery, CashReconciliationReportResponse } from './cashReconciliationReportTypes';

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

export const getCashReconciliationReport = (query: CashReconciliationReportQuery) =>
  requestJson<CashReconciliationReportResponse>(
    `/api/v1/reports/cash-reconciliation${buildQueryString({
      businessDate: query.businessDate,
      branchId: query.branchId,
    })}`
  );
