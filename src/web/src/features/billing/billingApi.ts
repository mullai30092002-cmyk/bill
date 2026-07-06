import { requestJson } from '../../api/apiClient';
import type {
  BillDetail,
  BillListQuery,
  BillListResponse,
  CancelBillRequest,
  CancelPaymentRequest,
  CreateBillRequest,
  RecordPaymentRequest,
} from './billingTypes';

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

export const listBills = (query?: BillListQuery) =>
  requestJson<BillListResponse>(
    `/api/v1/billing/bills${buildQueryString({
      branchId: query?.branchId,
      businessDate: query?.businessDate,
      status: query?.status,
      from: query?.from,
      to: query?.to,
      search: query?.search,
    })}`
  );

export const getBill = (billId: string) => requestJson<BillDetail>(`/api/v1/billing/bills/${billId}`);

export const createBill = (request: CreateBillRequest) =>
  requestJson<BillDetail>('/api/v1/billing/bills', {
    method: 'POST',
    body: request,
  });

export const cancelBill = (billId: string, request: CancelBillRequest) =>
  requestJson<BillDetail>(`/api/v1/billing/bills/${billId}/cancel`, {
    method: 'POST',
    body: request,
  });

export const recordPayment = (billId: string, request: RecordPaymentRequest) =>
  requestJson<BillDetail>(`/api/v1/billing/bills/${billId}/payments`, {
    method: 'POST',
    body: request,
  });

export const cancelPayment = (paymentId: string, request: CancelPaymentRequest) =>
  requestJson<BillDetail>(`/api/v1/billing/payments/${paymentId}/cancel`, {
    method: 'POST',
    body: request,
  });
