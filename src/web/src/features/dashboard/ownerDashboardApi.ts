import { requestJson } from '../../api/apiClient';
import type { OwnerDashboardQuery, OwnerDashboardResponse } from './ownerDashboardTypes';

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

export const getOwnerDashboard = (query?: OwnerDashboardQuery) =>
  requestJson<OwnerDashboardResponse>(
    `/api/v1/dashboard/owner${buildQueryString({
      date: query?.date,
      branchId: query?.branchId,
    })}`
  );
