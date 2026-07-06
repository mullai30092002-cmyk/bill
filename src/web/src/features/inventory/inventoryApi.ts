import { requestJson } from '../../api/apiClient';
import type {
  BatchProductionListQuery,
  BatchProductionListResponse,
  CreateBatchProductionRequest,
  CreateInventoryItemRequest,
  CreateInventoryMovementRequest,
  InventoryItemListQuery,
  InventoryItemListItem,
  InventoryItemListResponse,
  InventoryMovementItem,
  InventoryMovementListResponse,
  InventorySummaryQuery,
  InventorySummaryResponse,
  RecordPreparedStockWastageRequest,
  UpdateInventoryItemRequest,
} from './inventoryTypes';

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

export const listInventoryItems = (query?: InventoryItemListQuery) =>
  requestJson<InventoryItemListResponse>(
    `/api/v1/inventory/items${buildQueryString({
      branchId: query?.branchId,
    })}`
  );

export const getInventorySummary = (query?: InventorySummaryQuery) =>
  requestJson<InventorySummaryResponse>(
    `/api/v1/inventory/summary${buildQueryString({
      branchId: query?.branchId,
    })}`
  );

export const createInventoryItem = (request: CreateInventoryItemRequest) =>
  requestJson<InventoryItemListItem>('/api/v1/inventory/items', {
    method: 'POST',
    body: request,
  });

export const updateInventoryItem = (itemId: string, request: UpdateInventoryItemRequest) =>
  requestJson<InventoryItemListItem>(`/api/v1/inventory/items/${itemId}`, {
    method: 'PUT',
    body: request,
  });

export const listInventoryMovements = (itemId: string) =>
  requestJson<InventoryMovementListResponse>(`/api/v1/inventory/items/${itemId}/movements`);

export const recordInventoryMovement = (itemId: string, request: CreateInventoryMovementRequest) =>
  requestJson<InventoryMovementItem>(`/api/v1/inventory/items/${itemId}/movements`, {
    method: 'POST',
    body: request,
  });

export const listBatchProductions = (query?: BatchProductionListQuery) =>
  requestJson<BatchProductionListResponse>(
    `/api/v1/inventory/batch-productions${buildQueryString({
      branchId: query?.branchId,
      fromBusinessDate: query?.fromBusinessDate,
      toBusinessDate: query?.toBusinessDate,
    })}`
  );

export const createBatchProduction = (request: CreateBatchProductionRequest) =>
  requestJson('/api/v1/inventory/batch-productions', {
    method: 'POST',
    body: request,
  });

export const recordPreparedStockWastage = (request: RecordPreparedStockWastageRequest) =>
  requestJson('/api/v1/inventory/prepared-stock/wastage', {
    method: 'POST',
    body: request,
  });
