export type ExpiryStatus = 'Fresh' | 'NearExpiry' | 'Expired' | 'NoExpiry';

export interface ExpiryStockReportTotals {
  freshCount: number;
  nearExpiryCount: number;
  expiredCount: number;
  noExpiryCount: number;
  totalTrackedItems: number;
}

export interface ExpiryStockReportRow {
  inventoryItemId: string;
  inventoryItemName: string;
  unitOfMeasure: string;
  sourceType: string;
  batchReference: string | null;
  quantity: number;
  producedOrReceivedAt: string | null;
  expiresAtUtc: string | null;
  expiryStatus: ExpiryStatus;
  warningReason: string | null;
  sourceReference: string | null;
}

export interface ExpiryStockReportResponse {
  branchId: string;
  branchName: string;
  asOfDate: string;
  totals: ExpiryStockReportTotals;
  rows: ExpiryStockReportRow[];
}

export interface ExpiryStockReportQuery {
  branchId?: string | null;
  asOfDate?: string;
}
