export interface InventoryItemListQuery {
  branchId?: string;
}

export interface InventorySummaryQuery {
  branchId?: string;
}

export interface CreateInventoryItemRequest {
  branchId?: string;
  name: string;
  category: string;
  unitOfMeasure: string;
  lowStockThreshold: number;
  isActive: boolean;
}

export interface UpdateInventoryItemRequest {
  name: string;
  category: string;
  unitOfMeasure: string;
  lowStockThreshold: number;
  isActive: boolean;
}

export interface CreateInventoryMovementRequest {
  movementType: string;
  quantity: number;
  unitCost?: number | null;
  referenceNumber?: string | null;
  reason?: string | null;
  notes?: string | null;
  movementDate?: string | null;
  expiresAt?: string | null;
  batchReference?: string | null;
}

export interface InventoryItemListItem {
  inventoryItemId: string;
  restaurantId: string;
  branchId: string;
  name: string;
  normalizedName: string;
  category: string;
  unitOfMeasure: string;
  lowStockThreshold: number;
  isActive: boolean;
  currentStock: number;
  status: string;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface InventoryItemListResponse {
  items: InventoryItemListItem[];
}

export interface InventoryAlertItem {
  inventoryItemId: string;
  name: string;
  category: string;
  unitOfMeasure: string;
  lowStockThreshold: number;
  currentStock: number;
  status: string;
}

export interface InventorySummaryResponse {
  restaurantId: string;
  branchId: string;
  totalItems: number;
  activeItems: number;
  inactiveItems: number;
  lowStockCount: number;
  outOfStockCount: number;
  totalCurrentStock: number;
  recentlyAdjustedCount: number;
  lowStockItems: InventoryAlertItem[];
  outOfStockItems: InventoryAlertItem[];
}

export interface InventoryMovementItem {
  inventoryMovementId: string;
  inventoryItemId: string;
  restaurantId: string;
  branchId: string;
  movementType: string;
  quantity: number;
  unitCost: number | null;
  referenceNumber: string | null;
  reason: string | null;
  notes: string | null;
  movementDate: string;
  recordedByUserId: string;
  recordedByUserName: string;
  recordedByUserMobile: string | null;
  createdAtUtc: string;
  previousStock: number;
  delta: number;
  resultingStock: number;
  resultingStatus: string;
  expiresAtUtc: string | null;
  batchReference: string | null;
}

export interface InventoryMovementListResponse {
  items: InventoryMovementItem[];
}

export interface BatchProductionListQuery {
  branchId?: string;
  fromBusinessDate?: string;
  toBusinessDate?: string;
}

export interface BatchProductionListItem {
  batchProductionId: string;
  restaurantId: string;
  branchId: string;
  menuItemId: string;
  menuItemName: string;
  preparedInventoryItemId: string;
  preparedInventoryItemName: string;
  quantityProduced: number;
  businessDate: string;
  producedAtUtc: string;
  producedByUserId: string;
  producedByUserName: string;
  notes: string | null;
  totalRawQuantityConsumed: number;
  createdAtUtc: string;
  shelfLifeHours: number | null;
  expiresAtUtc: string | null;
  storageNote: string | null;
  batchReference: string | null;
}

export interface BatchProductionListResponse {
  items: BatchProductionListItem[];
}

export interface CreateBatchProductionRequest {
  branchId?: string | null;
  menuItemId: string;
  quantityProduced: number;
  businessDate?: string | null;
  producedAtUtc?: string | null;
  notes?: string | null;
  shelfLifeHours?: number | null;
  expiresAt?: string | null;
  storageNote?: string | null;
  batchReference?: string | null;
}

export interface RecordPreparedStockWastageRequest {
  branchId?: string | null;
  menuItemId: string;
  quantity: number;
  wastedAtUtc?: string | null;
  reason?: string | null;
  notes?: string | null;
}
