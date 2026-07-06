export type CashierShiftStatus = 'Open' | 'Closed' | 'Voided';

export interface CashierShiftListQuery {
  businessDate?: string;
  branchId?: string;
}

export interface CashierShiftListItem {
  cashierShiftId: string;
  restaurantId: string;
  branchId: string;
  cashierUserId: string;
  cashierName: string;
  branchName: string;
  businessDate: string;
  status: CashierShiftStatus;
  openedAtUtc: string;
  openingCashAmount: number;
  closedAtUtc: string | null;
  declaredClosingCashAmount: number | null;
  expectedClosingCashAmount: number;
  cashVarianceAmount: number | null;
  closeNotes: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface CashierShiftDetail extends CashierShiftListItem {}

export interface CashierShiftListResponse {
  items: CashierShiftListItem[];
}

export interface OpenCashierShiftRequest {
  branchId: string;
  businessDate: string;
  openingCashAmount: number;
}

export interface CloseCashierShiftRequest {
  declaredClosingCashAmount: number;
  closeNotes?: string | null;
}
