export interface PreparedStockReportTotals {
  producedQuantity: number;
  servedQuantity: number;
  wastedQuantity: number;
  remainingQuantity: number;
  itemCount: number;
  warningCount: number;
}

export interface PreparedStockReportRow {
  menuItemId: string;
  menuItemName: string | null;
  preparedInventoryItemId: string | null;
  preparedInventoryItemName: string | null;
  unitOfMeasure: string | null;
  producedQuantity: number;
  servedQuantity: number;
  wastedQuantity: number;
  remainingQuantity: number;
  hasWarning: boolean;
  warningReason: string | null;
}

export interface PreparedStockReportResponse {
  branchId: string;
  branchName: string;
  businessDate: string;
  totals: PreparedStockReportTotals;
  rows: PreparedStockReportRow[];
}

export interface PreparedStockReportQuery {
  branchId?: string | null;
  businessDate?: string;
}
