import { requestJson } from '../../api/apiClient';
import type {
  CreateVendorBillRequest,
  CreateVendorRequest,
  RecordVendorSettlementRequest,
  UpdateVendorRequest,
  VendorDetail,
  VendorBillDetail,
  VendorBillListQuery,
  VendorBillListResponse,
  VendorListQuery,
  VendorListResponse,
} from './vendorTypes';

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

export const listVendors = (query?: VendorListQuery) =>
  requestJson<VendorListResponse>(
    `/api/v1/vendors${buildQueryString({
      branchId: query?.branchId,
    })}`
  );

export const createVendor = (request: CreateVendorRequest) =>
  requestJson<VendorDetail>('/api/v1/vendors', {
    method: 'POST',
    body: request,
  });

export const updateVendor = (vendorId: string, request: UpdateVendorRequest) =>
  requestJson<VendorDetail>(`/api/v1/vendors/${vendorId}`, {
    method: 'PUT',
    body: request,
  });

export const listVendorBills = (query?: VendorBillListQuery) =>
  requestJson<VendorBillListResponse>(
    `/api/v1/vendor-bills${buildQueryString({
      branchId: query?.branchId,
      fromDate: query?.fromDate,
      toDate: query?.toDate,
      status: query?.status,
    })}`
  );

export const getVendorBill = (vendorBillId: string) =>
  requestJson<VendorBillDetail>(`/api/v1/vendor-bills/${vendorBillId}`);

export const createVendorBill = (request: CreateVendorBillRequest) =>
  requestJson<VendorBillDetail>('/api/v1/vendor-bills', {
    method: 'POST',
    body: request,
  });

export const recordVendorSettlement = (vendorBillId: string, request: RecordVendorSettlementRequest) =>
  requestJson<VendorBillDetail>(`/api/v1/vendor-bills/${vendorBillId}/settlements`, {
    method: 'POST',
    body: request,
  });
