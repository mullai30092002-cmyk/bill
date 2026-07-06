import { requestJson } from '../../api/apiClient';
import type {
  CancelKitchenTicketRequest,
  CreateKitchenTicketRequest,
  KitchenTicketDeductionPreviewResponse,
  KitchenTicketDetail,
  KitchenTicketListQuery,
  KitchenTicketListResponse,
  UpdateKitchenTicketStatusRequest,
} from './kitchenTicketTypes';

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

const resolveStatusFilter = (status?: KitchenTicketListQuery['status']) => {
  if (!status || status === 'All' || status === 'Active') {
    return undefined;
  }

  return status;
};

export const listKitchenTickets = (query?: KitchenTicketListQuery) =>
  requestJson<KitchenTicketListResponse>(
    `/api/v1/kitchen/tickets${buildQueryString({
      branchId: query?.branchId,
      status: resolveStatusFilter(query?.status),
      from: query?.from,
      to: query?.to,
    })}`
  );

export const getKitchenTicket = (ticketId: string) =>
  requestJson<KitchenTicketDetail>(`/api/v1/kitchen/tickets/${ticketId}`);

export const getKitchenTicketDeductionPreview = (ticketId: string) =>
  requestJson<KitchenTicketDeductionPreviewResponse>(`/api/v1/kitchen/tickets/${ticketId}/deduction-preview`);

export const createKitchenTicket = (request: CreateKitchenTicketRequest) =>
  requestJson<KitchenTicketDetail>('/api/v1/kitchen/tickets', {
    method: 'POST',
    body: request,
  });

export const updateKitchenTicketStatus = (ticketId: string, request: UpdateKitchenTicketStatusRequest) =>
  requestJson<KitchenTicketDetail>(`/api/v1/kitchen/tickets/${ticketId}/status`, {
    method: 'POST',
    body: request,
  });

export const cancelKitchenTicket = (ticketId: string, request: CancelKitchenTicketRequest) =>
  requestJson<KitchenTicketDetail>(`/api/v1/kitchen/tickets/${ticketId}/cancel`, {
    method: 'POST',
    body: request,
  });
