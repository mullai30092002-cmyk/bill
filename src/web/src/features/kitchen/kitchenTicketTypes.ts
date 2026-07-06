export type KitchenTicketStatus = 'Pending' | 'Preparing' | 'Ready' | 'Served' | 'Cancelled';

export type KitchenTicketInventoryDeductionStatus = 'NotDeducted' | 'Deducted' | 'DeductionWarning';

export type KitchenTicketQueueFilter = 'Active' | KitchenTicketStatus | 'All';

export interface KitchenTicketListQuery {
  branchId?: string;
  status?: KitchenTicketQueueFilter;
  from?: string;
  to?: string;
}

export interface KitchenTicketListItem {
  kitchenTicketId: string;
  branchId: string;
  posOrderId: string;
  ticketNumber: string;
  orderNumberSnapshot: string;
  orderTypeSnapshot: string;
  tableNameSnapshot: string | null;
  customerNameSnapshot: string | null;
  orderNotesSnapshot: string | null;
  status: KitchenTicketStatus;
  lineCount: number;
  createdAt: string;
  updatedAt: string | null;
  cancelledAt: string | null;
  cancelReason: string | null;
}

export interface KitchenTicketListResponse {
  items: KitchenTicketListItem[];
}

export interface KitchenTicketLineDetail {
  kitchenTicketLineId: string;
  posOrderLineId: string;
  menuItemId: string;
  menuCategoryId: string;
  menuItemNameSnapshot: string;
  menuCategoryNameSnapshot: string;
  skuSnapshot: string | null;
  quantity: number;
  notes: string | null;
  displayOrder: number;
  createdAt: string;
}

export interface KitchenTicketDetail {
  kitchenTicketId: string;
  restaurantId: string;
  branchId: string;
  posOrderId: string;
  ticketNumber: string;
  orderNumberSnapshot: string;
  orderTypeSnapshot: string;
  tableNameSnapshot: string | null;
  customerNameSnapshot: string | null;
  orderNotesSnapshot: string | null;
  status: KitchenTicketStatus;
  createdByUserId: string | null;
  lastStatusChangedByUserId: string | null;
  cancelledByUserId: string | null;
  cancelledAt: string | null;
  cancelReason: string | null;
  createdAt: string;
  updatedAt: string | null;
  preparingAt: string | null;
  readyAt: string | null;
  servedAt: string | null;
  inventoryDeductionStatus: KitchenTicketInventoryDeductionStatus;
  lines: KitchenTicketLineDetail[];
}

export type KitchenTicketDeductionPreviewStatus = 'Sufficient' | 'Insufficient' | 'NoRecipe';

export interface KitchenTicketDeductionPreviewLine {
  menuItemName: string;
  inventoryItemName: string;
  requiredQuantity: number;
  availableQuantity: number;
  resultingQuantity: number;
  status: KitchenTicketDeductionPreviewStatus;
}

export interface KitchenTicketDeductionPreviewResponse {
  ticketId: string;
  lines: KitchenTicketDeductionPreviewLine[];
  canComplete: boolean;
}

export interface CreateKitchenTicketRequest {
  posOrderId: string;
}

export interface UpdateKitchenTicketStatusRequest {
  status: KitchenTicketStatus;
}

export interface CancelKitchenTicketRequest {
  reason: string;
}
