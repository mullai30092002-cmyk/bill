import { requestJson } from '../../api/apiClient';

import type {
  VendorBillOcrDraftDetail,
  VendorBillOcrDraftListQuery,
  VendorBillOcrDraftListResponse,
  VendorBillOcrDraftUpdateRequest,
} from './vendorBillOcrTypes';
import type { VendorBillDetail } from './vendorTypes';

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

export const listVendorBillOcrDrafts = (query?: VendorBillOcrDraftListQuery) =>
  requestJson<VendorBillOcrDraftListResponse>(
    `/api/v1/vendor-bill-ocr/drafts${buildQueryString({
      branchId: query?.branchId,
    })}`
  );

export const getVendorBillOcrDraft = (draftId: string) =>
  requestJson<VendorBillOcrDraftDetail>(`/api/v1/vendor-bill-ocr/drafts/${draftId}`);

export const uploadVendorBillOcrDraft = (branchId: string | undefined, file: File) => {
  const formData = new FormData();
  if (branchId) {
    formData.set('branchId', branchId);
  }
  formData.set('file', file);

  return requestJson<VendorBillOcrDraftDetail>('/api/v1/vendor-bill-ocr/drafts', {
    method: 'POST',
    body: formData,
  });
};

export const updateVendorBillOcrDraft = (draftId: string, request: VendorBillOcrDraftUpdateRequest) =>
  requestJson<VendorBillOcrDraftDetail>(`/api/v1/vendor-bill-ocr/drafts/${draftId}`, {
    method: 'PUT',
    body: request,
  });

export const confirmVendorBillOcrDraft = (draftId: string) =>
  requestJson<VendorBillDetail>(`/api/v1/vendor-bill-ocr/drafts/${draftId}/confirm`, {
    method: 'POST',
  });
