export interface VendorPayablesReportSummary {
  totalVendorBills: number;
  totalPurchaseAmount: number;
  totalPaidAmount: number;
  totalOutstandingAmount: number;
  unpaidBillCount: number;
  partiallyPaidBillCount: number;
  paidBillCount: number;
  cancelledBillCount: number;
  overdueBillCount: number;
}

export interface VendorPayablesVendorBalance {
  vendorId: string;
  vendorName: string;
  vendorType: string;
  totalBills: number;
  purchaseAmount: number;
  paidAmount: number;
  outstandingAmount: number;
  unpaidCount: number;
  partiallyPaidCount: number;
  overdueCount: number;
}

export interface VendorPayablesOverdueBillItem {
  billNumber: string | null;
  vendorName: string;
  vendorType: string;
  branchName: string | null;
  billDate: string;
  dueDate: string | null;
  totalAmount: number;
  paidAmount: number;
  outstandingAmount: number;
  status: string;
}

export interface VendorPayablesSettlementItem {
  vendorName: string;
  billNumber: string | null;
  branchName: string | null;
  paidAtUtc: string;
  amount: number;
  paymentMode: string;
  referenceNumberMasked: string | null;
}

export interface VendorPayablesInventoryPurchaseTotal {
  inventoryItemName: string;
  quantity: number;
  amount: number;
}

export interface VendorPayablesReportResponse {
  restaurantId: string;
  restaurantCode: string;
  restaurantName: string;
  branchId: string | null;
  branchName: string | null;
  fromDate: string;
  toDate: string;
  currencyCode: string;
  generatedAt: string;
  summary: VendorPayablesReportSummary;
  vendorBalances: VendorPayablesVendorBalance[];
  overdueBills: VendorPayablesOverdueBillItem[];
  recentSettlements: VendorPayablesSettlementItem[];
  inventoryPurchaseTotals: VendorPayablesInventoryPurchaseTotal[];
}

export interface VendorPayablesReportQuery {
  branchId?: string | null;
  fromDate?: string;
  toDate?: string;
}
