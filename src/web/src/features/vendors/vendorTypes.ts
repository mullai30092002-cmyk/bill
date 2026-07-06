export interface VendorListQuery {
  branchId?: string;
}

export interface VendorDetail {
  vendorId: string;
  restaurantId: string;
  branchId: string | null;
  name: string;
  normalizedName: string;
  vendorType: string;
  contactName: string | null;
  mobileNumber: string | null;
  address: string | null;
  notes: string | null;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface VendorListResponse {
  items: VendorDetail[];
}

export interface CreateVendorRequest {
  branchId?: string | null;
  name: string;
  vendorType: string;
  contactName?: string | null;
  mobileNumber?: string | null;
  address?: string | null;
  notes?: string | null;
  isActive: boolean;
}

export interface UpdateVendorRequest {
  branchId?: string | null;
  name: string;
  vendorType: string;
  contactName?: string | null;
  mobileNumber?: string | null;
  address?: string | null;
  notes?: string | null;
  isActive: boolean;
}

export interface VendorBillListQuery {
  branchId?: string;
  fromDate?: string;
  toDate?: string;
  status?: string;
}

export interface VendorBillListItem {
  vendorBillId: string;
  vendorId: string;
  branchId: string;
  vendorName: string;
  vendorType: string;
  billNumber: string | null;
  billDate: string;
  dueDate: string | null;
  status: string;
  totalAmount: number;
  paidAmount: number;
  balanceAmount: number;
  createdAtUtc: string;
}

export interface VendorBillListResponse {
  items: VendorBillListItem[];
}

export interface VendorBillLineDetail {
  vendorBillLineId: string;
  inventoryItemId: string | null;
  inventoryItemName: string | null;
  inventoryMovementId: string | null;
  description: string;
  quantity: number;
  unitCost: number;
  lineTotal: number;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface VendorSettlementDetail {
  vendorSettlementId: string;
  paymentMode: string;
  status: string;
  amount: number;
  referenceNumber: string | null;
  paidAtUtc: string;
  recordedByUserId: string;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  cancelledAtUtc: string | null;
  cancelledByUserId: string | null;
  cancellationReason: string | null;
  notes: string | null;
  previousOutstandingAmount: number;
  newOutstandingAmount: number;
}

export interface VendorBillDetail {
  vendorBillId: string;
  restaurantId: string;
  branchId: string;
  vendorId: string;
  vendorName: string;
  vendorType: string;
  billNumber: string | null;
  billDate: string;
  dueDate: string | null;
  status: string;
  totalAmount: number;
  paidAmount: number;
  balanceAmount: number;
  notes: string | null;
  cancelledAtUtc: string | null;
  cancelledByUserId: string | null;
  cancellationReason: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  lines: VendorBillLineDetail[];
  settlements: VendorSettlementDetail[];
}

export interface CreateVendorBillLineRequest {
  inventoryItemId?: string | null;
  description: string;
  quantity: number;
  unitCost: number;
}

export interface CreateVendorBillRequest {
  vendorId: string;
  branchId: string;
  billNumber?: string | null;
  billDate: string;
  dueDate?: string | null;
  notes?: string | null;
  lines: CreateVendorBillLineRequest[];
}

export interface RecordVendorSettlementRequest {
  paymentMode: string;
  amount: number;
  referenceNumber?: string | null;
  paidAtUtc?: string | null;
  notes?: string | null;
}
