export type BillStatus = 'Unpaid' | 'PartiallyPaid' | 'Paid' | 'Cancelled';

export type PaymentStatus = 'Recorded' | 'Cancelled';

export type PaymentMode = 'Cash' | 'Card' | 'Upi' | 'Other';

export type BillingPaymentEntryMode = Exclude<PaymentMode, 'Other'>;

export interface BillListQuery {
  branchId?: string;
  businessDate?: string;
  status?: BillStatus;
  from?: string;
  to?: string;
  search?: string;
}

export interface BillListItem {
  billId: string;
  branchId: string;
  posOrderId: string;
  billNumber: string;
  businessDate: string;
  status: BillStatus;
  grandTotal: number;
  amountPaid: number;
  balanceDue: number;
  createdAt: string;
}

export interface BillListResponse {
  items: BillListItem[];
}

export interface BillLineDetail {
  billLineId: string;
  posOrderLineId: string;
  menuItemId: string;
  menuCategoryId: string;
  menuItemNameSnapshot: string;
  menuCategoryNameSnapshot: string;
  skuSnapshot: string | null;
  unitPrice: number;
  taxRate: number;
  quantity: number;
  lineSubtotal: number;
  lineTax: number;
  lineTotal: number;
  notes: string | null;
  displayOrder: number;
  createdAt: string;
}

export interface PaymentDetail {
  paymentId: string;
  billId: string;
  branchId: string;
  paymentNumber: string;
  paymentMode: PaymentMode;
  status: PaymentStatus;
  amount: number;
  referenceNumber: string | null;
  notes: string | null;
  recordedByUserId: string | null;
  cancelledByUserId: string | null;
  cancelledAt: string | null;
  cancelReason: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface BillDetail {
  billId: string;
  restaurantId: string;
  branchId: string;
  posOrderId: string;
  billNumber: string;
  businessDate: string;
  status: BillStatus;
  subtotal: number;
  taxTotal: number;
  grandTotal: number;
  amountPaid: number;
  balanceDue: number;
  createdByUserId: string | null;
  cancelledByUserId: string | null;
  cancelledAt: string | null;
  cancelReason: string | null;
  createdAt: string;
  updatedAt: string | null;
  lines: BillLineDetail[];
  payments: PaymentDetail[];
}

export interface CreateBillRequest {
  posOrderId: string;
}

export interface CancelBillRequest {
  reason: string;
}

export interface RecordPaymentRequest {
  paymentMode: PaymentMode;
  amount: number;
  referenceNumber?: string | null;
  notes?: string | null;
}

export interface CancelPaymentRequest {
  reason: string;
}
