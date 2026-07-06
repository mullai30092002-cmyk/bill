export interface VendorStatementQuery {
  vendorId: string;
  branchId?: string | null;
  fromDate?: string;
  toDate?: string;
}

export interface VendorStatementSummary {
  totalBillAmount: number;
  totalSettlementAmount: number;
  payableBillCount: number;
  settlementCount: number;
  overdueBillCount: number;
}

export interface VendorStatementBillItem {
  vendorBillId: string;
  branchId: string;
  branchName: string | null;
  billNumber: string | null;
  billDate: string;
  dueDate: string | null;
  status: string;
  totalAmount: number;
  paidAmount: number;
  outstandingAmount: number;
  notes: string | null;
  createdAtUtc: string;
}

export interface VendorStatementSettlementItem {
  vendorSettlementId: string;
  vendorBillId: string;
  branchId: string;
  branchName: string | null;
  billNumber: string | null;
  paidAtUtc: string;
  paymentMode: string;
  amount: number;
  referenceNumberMasked: string | null;
  notes: string | null;
  previousOutstandingAmount: number;
  newOutstandingAmount: number;
  status: string;
}

export interface VendorStatementTimelineItem {
  entryType: string;
  timestampUtc: string;
  billNumber: string | null;
  reference: string | null;
  description: string | null;
  debitAmount: number;
  creditAmount: number;
  runningBalance: number;
  paymentMode: string | null;
  status: string | null;
}

export interface VendorStatementResponse {
  restaurantId: string;
  branchId: string | null;
  branchName: string | null;
  vendorId: string;
  vendorName: string;
  vendorType: string;
  currencyCode: string;
  fromDate: string;
  toDate: string;
  generatedAt: string;
  openingOutstandingAmount: number;
  currentOutstandingAmount: number;
  summary: VendorStatementSummary;
  payableBills: VendorStatementBillItem[];
  settlements: VendorStatementSettlementItem[];
  timeline: VendorStatementTimelineItem[];
}
