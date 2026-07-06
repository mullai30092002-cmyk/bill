export interface BillReceiptLine {
  displayOrder: number;
  menuItemNameSnapshot: string;
  menuCategoryNameSnapshot: string;
  skuSnapshot: string | null;
  quantity: number;
  notes: string | null;
  unitPrice: number;
  lineSubtotal: number;
  lineTax: number;
  lineTotal: number;
}

export interface BillReceiptPayment {
  paymentNumber: string;
  paymentMode: string;
  status: string;
  amount: number;
  referenceNumber: string | null;
  notes: string | null;
  recordedByUserId: string | null;
  recordedByUserLabel: string;
  createdAt: string;
}

export interface BillReceiptResponse {
  billId: string;
  restaurantId: string;
  branchId: string;
  restaurantCode: string;
  countryCode: string;
  currencyCode: string;
  timeZoneId: string;
  restaurantName: string;
  branchName: string;
  branchAddress: string | null;
  posOrderId: string;
  businessDate: string;
  orderNumberSnapshot: string | null;
  orderTypeSnapshot: string | null;
  orderTableNameSnapshot: string | null;
  orderCustomerNameSnapshot: string | null;
  orderCustomerMobileSnapshot: string | null;
  billNumber: string;
  status: string;
  createdByUserId: string | null;
  createdByUserLabel: string;
  createdAt: string;
  updatedAt: string | null;
  printedAt: string;
  cancelledByUserId: string | null;
  cancelledAt: string | null;
  cancelReason: string | null;
  subtotal: number;
  taxTotal: number;
  grandTotal: number;
  amountPaid: number;
  balanceDue: number;
  printCount: number;
  isReprint: boolean;
  lines: BillReceiptLine[];
  payments: BillReceiptPayment[];
}

export interface RecordBillReceiptPrintEventRequest {
  reason?: string | null;
}
