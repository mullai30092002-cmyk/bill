export interface DailyCashSalesReportSummary {
  totalBills: number;
  paidBills: number;
  partiallyPaidBills: number;
  unpaidBills: number;
  cancelledBills: number;
  grossSales: number;
  grossBillTotal: number;
  cancelledBillAmount: number;
  netSales: number;
  totalAmountPaid: number;
  totalBalanceDue: number;
  cashPayments: number;
  upiPayments: number;
  cardPayments: number;
  otherPayments: number;
  nonCashPayments: number;
  openShifts: number;
  closedShifts: number;
  openingCashTotal: number;
  declaredClosingCashTotal: number;
  expectedCashTotal: number;
  receiptPrints: number;
  receiptReprints: number;
  cashVarianceTotal: number;
}

export interface DailyCashSalesPaymentBreakdown {
  paymentMode: string;
  recordedAmount: number;
  cancelledAmount: number;
  netAmount: number;
  paymentCount: number;
  cancelledCount: number;
}

export interface DailyCashSalesCashShiftSummary {
  cashierShiftId: string;
  branchId: string;
  branchName: string;
  status: string;
  openedAt: string;
  closedAt: string | null;
  openingCashAmount: number;
  expectedCashAmount: number;
  countedCashAmount: number | null;
  cashVarianceAmount: number | null;
  cashMovementTotal: number;
  cashPaymentTotal: number;
}

export interface DailyCashSalesExceptionItem {
  id: string;
  referenceNumber: string;
  branchId: string;
  branchName: string;
  amount: number | null;
  status: string;
  occurredAt: string;
  reason: string | null;
  severity: 'Low' | 'Medium' | 'High';
  printCount?: number | null;
  reprintCount?: number | null;
  balanceDue?: number | null;
  expectedCashAmount?: number | null;
  countedCashAmount?: number | null;
  varianceAmount?: number | null;
}

export interface DailyCashSalesReportExceptions {
  unpaidBills: DailyCashSalesExceptionItem[];
  cancelledBills: DailyCashSalesExceptionItem[];
  cancelledPayments: DailyCashSalesExceptionItem[];
  receiptReprints: DailyCashSalesExceptionItem[];
  cashVariances: DailyCashSalesExceptionItem[];
  openShifts: DailyCashSalesExceptionItem[];
}

export interface DailyCashSalesReportResponse {
  restaurantId: string;
  restaurantCode: string;
  restaurantName: string;
  branchId: string | null;
  branchName: string | null;
  businessDate: string;
  currencyCode: string;
  generatedAt: string;
  summary: DailyCashSalesReportSummary;
  paymentBreakdown: DailyCashSalesPaymentBreakdown[];
  cashShiftSummaries: DailyCashSalesCashShiftSummary[];
  exceptions: DailyCashSalesReportExceptions;
}

export interface DailyCashSalesReportQuery {
  date?: string;
  branchId?: string | null;
}
