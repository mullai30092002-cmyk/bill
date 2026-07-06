import { requestJson } from '../../api/apiClient';
import type {
  CancelPosOrderRequest,
  CreatePosOrderRequest,
  PosOrderDetail,
  PosOrderListResponse,
  PosOrderStatus,
  PosOrderType,
  UpdatePosOrderRequest,
} from './posTypes';

export interface PosOrderListQuery {
  branchId?: string;
  status?: PosOrderStatus | 'All';
  orderType?: PosOrderType | 'All';
  from?: string;
  to?: string;
  search?: string;
}

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

export const listPosOrders = (query?: PosOrderListQuery) =>
  requestJson<PosOrderListResponse>(
    `/api/v1/pos/orders${buildQueryString({
      branchId: query?.branchId,
      status: query?.status,
      orderType: query?.orderType,
      from: query?.from,
      to: query?.to,
      search: query?.search,
    })}`
  );

export const getPosOrder = (orderId: string) => requestJson<PosOrderDetail>(`/api/v1/pos/orders/${orderId}`);

export const createPosOrder = (request: CreatePosOrderRequest) =>
  requestJson<PosOrderDetail>('/api/v1/pos/orders', {
    method: 'POST',
    body: request,
  });

export const updatePosOrder = (orderId: string, request: UpdatePosOrderRequest) =>
  requestJson<PosOrderDetail>(`/api/v1/pos/orders/${orderId}`, {
    method: 'PUT',
    body: request,
  });

export const confirmPosOrder = (orderId: string) =>
  requestJson<PosOrderDetail>(`/api/v1/pos/orders/${orderId}/confirm`, {
    method: 'POST',
  });

export const cancelPosOrder = (orderId: string, request: CancelPosOrderRequest) =>
  requestJson<PosOrderDetail>(`/api/v1/pos/orders/${orderId}/cancel`, {
    method: 'POST',
    body: request,
  });
