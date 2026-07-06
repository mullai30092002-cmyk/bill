export type PosOrderType = 'EatIn' | 'Parcel';

export type PosOrderStatus = 'Draft' | 'Confirmed' | 'Cancelled';

export interface PosOrderListItem {
  posOrderId: string;
  branchId: string;
  orderNumber: string;
  orderType: PosOrderType;
  status: PosOrderStatus;
  tableName: string | null;
  customerName: string | null;
  grandTotal: number;
  lineCount: number;
  createdAt: string;
}

export interface PosOrderLineDetail {
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
  updatedAt: string | null;
}

export interface PosOrderDetail {
  posOrderId: string;
  restaurantId: string;
  branchId: string;
  orderNumber: string;
  orderType: PosOrderType;
  status: PosOrderStatus;
  tableName: string | null;
  customerName: string | null;
  customerMobile: string | null;
  notes: string | null;
  subtotal: number;
  taxTotal: number;
  grandTotal: number;
  confirmedAt: string | null;
  cancelledAt: string | null;
  cancelReason: string | null;
  createdByUserId: string | null;
  confirmedByUserId: string | null;
  cancelledByUserId: string | null;
  createdAt: string;
  updatedAt: string | null;
  lines: PosOrderLineDetail[];
  kitchenTicketId?: string | null;
  kitchenTicketNumber?: string | null;
  kitchenTicketStatus?: string | null;
}

export interface PosOrderLineRequest {
  menuItemId: string;
  quantity: number;
  notes?: string | null;
}

export interface CreatePosOrderRequest {
  branchId: string;
  orderType: PosOrderType;
  tableName?: string | null;
  customerName?: string | null;
  customerMobile?: string | null;
  notes?: string | null;
  lines: PosOrderLineRequest[];
}

export interface UpdatePosOrderRequest {
  orderType: PosOrderType;
  tableName?: string | null;
  customerName?: string | null;
  customerMobile?: string | null;
  notes?: string | null;
  lines: PosOrderLineRequest[];
}

export interface CancelPosOrderRequest {
  reason: string;
}

export interface PosOrderListResponse {
  items: PosOrderListItem[];
}
