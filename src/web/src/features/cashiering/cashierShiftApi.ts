import { requestJson } from '../../api/apiClient';
import type {
  CashierShiftDetail,
  CashierShiftListQuery,
  CashierShiftListResponse,
  CloseCashierShiftRequest,
  OpenCashierShiftRequest,
} from './cashierShiftTypes';

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

export const listCashierShifts = (query?: CashierShiftListQuery) =>
  requestJson<CashierShiftListResponse>(
    `/api/v1/cashier/shifts${buildQueryString({
      businessDate: query?.businessDate,
      branchId: query?.branchId,
    })}`
  );

export const getCurrentCashierShift = (branchId: string) =>
  requestJson<CashierShiftDetail | null>(`/api/v1/cashier/shifts/current?branchId=${encodeURIComponent(branchId)}`);

export const getCashierShift = (shiftId: string) => requestJson<CashierShiftDetail>(`/api/v1/cashier/shifts/${shiftId}`);

export const openCashierShift = (request: OpenCashierShiftRequest) =>
  requestJson<CashierShiftDetail>('/api/v1/cashier/shifts/open', {
    method: 'POST',
    body: request,
  });

export const closeCashierShift = (shiftId: string, request: CloseCashierShiftRequest) =>
  requestJson<CashierShiftDetail>(`/api/v1/cashier/shifts/${shiftId}/close`, {
    method: 'POST',
    body: request,
  });
