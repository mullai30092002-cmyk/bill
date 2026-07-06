export interface VendorBillOcrDraftListQuery {
  branchId?: string;
}

export interface VendorBillOcrDraftListItem {
  vendorBillOcrDraftId: string;
  restaurantId: string;
  branchId: string;
  originalFileName: string;
  status: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface VendorBillOcrDraftListResponse {
  items: VendorBillOcrDraftListItem[];
}

export interface VendorBillOcrDraftLineDetail {
  vendorBillOcrDraftLineId: string;
  lineNumber: number;
  extractedDescription: string;
  extractedQuantity: number | null;
  extractedUnitCost: number | null;
  extractedLineTotal: number | null;
  confidenceScore: number | null;
  selectedInventoryItemId: string | null;
  isIgnored: boolean;
  reviewedDescription: string | null;
  reviewedQuantity: number | null;
  reviewedUnitCost: number | null;
  reviewedLineTotal: number | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface VendorBillOcrDraftDetail {
  vendorBillOcrDraftId: string;
  restaurantId: string;
  branchId: string;
  uploadedByUserId: string;
  originalFileName: string;
  contentType: string;
  fileSizeBytes: number;
  status: string;
  extractedVendorName: string | null;
  extractedBillNumber: string | null;
  extractedBillDate: string | null;
  extractedTotalAmount: number | null;
  extractedConfidenceScore: number | null;
  providerWarnings: string[];
  hasDuplicateReceipt: boolean;
  duplicateReceiptWarning: string | null;
  canOverrideDuplicateReceipt: boolean;
  reviewedVendorId: string | null;
  reviewedBillNumber: string | null;
  reviewedBillDate: string | null;
  reviewedTotalAmount: number | null;
  safeErrorMessage: string | null;
  confirmedVendorBillId: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  confirmedAtUtc: string | null;
  lines: VendorBillOcrDraftLineDetail[];
}

export interface VendorBillOcrDraftLineUpdateRequest {
  vendorBillOcrDraftLineId: string;
  reviewedDescription?: string | null;
  reviewedQuantity?: number | null;
  reviewedUnitCost?: number | null;
  reviewedLineTotal?: number | null;
  selectedInventoryItemId?: string | null;
  isIgnored: boolean;
}

export interface VendorBillOcrDraftLineCreateRequest {
  reviewedDescription: string;
  reviewedQuantity: number;
  reviewedUnitCost: number;
  reviewedLineTotal: number;
  selectedInventoryItemId?: string | null;
  isIgnored: boolean;
}

export interface VendorBillOcrDraftUpdateRequest {
  reviewedVendorId?: string | null;
  reviewedBillNumber?: string | null;
  reviewedBillDate?: string | null;
  reviewedTotalAmount?: number | null;
  lines?: VendorBillOcrDraftLineUpdateRequest[];
  addedLines?: VendorBillOcrDraftLineCreateRequest[];
  removedLineIds?: string[];
}

export interface VendorBillOcrDraftUploadResponse {
  vendorBillOcrDraftId: string;
}
