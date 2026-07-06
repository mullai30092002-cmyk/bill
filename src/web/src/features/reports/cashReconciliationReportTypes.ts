export type CashReconciliationVarianceStatus = 'OpenShift' | 'Balanced' | 'MinorVariance' | 'MajorVariance';

export interface CashReconciliationReportQuery {
  businessDate: string;
  branchId?: string | null;
}

export interface CashReconciliationReportResponse {
  restaurantId: string;
  restaurantName: string;
  branchId: string | null;
  branchName: string | null;
  businessDate: string;
  generatedAtUtc: string;
  currencyCode: string;
  totals: CashReconciliationReportTotals;
  shifts: CashReconciliationShiftRow[];
}

export interface CashReconciliationReportTotals {
  shiftCount: number;
  openShiftCount: number;
  closedShiftCount: number;
  openingCashTotal: number;
  cashPaymentTotal: number;
  cashInTotal: number;
  cashOutTotal: number;
  adjustmentTotal: number;
  expectedCashTotal: number;
  declaredCashTotal: number;
  varianceTotal: number;
  majorVarianceCount: number;
  minorVarianceCount: number;
  balancedShiftCount: number;
}

export interface CashReconciliationShiftRow {
  cashierShiftId: string;
  branchId: string;
  branchName: string;
  cashierUserId: string;
  cashierName: string;
  status: string;
  openedAt: string;
  closedAt: string | null;
  openingCashAmount: number;
  cashPaymentTotal: number;
  cashInTotal: number;
  cashOutTotal: number;
  adjustmentTotal: number;
  expectedCashAmount: number;
  declaredClosingCashAmount: number | null;
  varianceAmount: number | null;
  varianceStatus: CashReconciliationVarianceStatus;
  paymentCount: number;
  movementCount: number;
  closingNote: string | null;
}
