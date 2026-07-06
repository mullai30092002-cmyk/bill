export interface OwnerDashboardMetrics {
  grossSales: number;
  netSales: number;
  cashPayments: number;
  nonCashPayments: number;
  totalAmountPaid: number;
  totalBalanceDue: number;
  unpaidBills: number;
  cancelledBills: number;
  cancelledPayments: number;
  receiptReprints: number;
  cashVarianceTotal: number;
  openShifts: number;
}

export interface OwnerDashboardAlert {
  type: 'UnpaidBills' | 'CancelledActivity' | 'ReceiptReprints' | 'CashVariance' | 'OpenShift';
  title: string;
  message: string;
  severity: 'Low' | 'Medium' | 'High';
  count: number;
  amount: number | null;
  targetPath: string;
}

export interface OwnerDashboardInventoryAlertItem {
  inventoryItemId: string;
  name: string;
  category: string;
  unit: string;
  currentQuantity: number;
  minimumQuantity: number;
  status: string;
  lastUpdatedAt: string | null;
}

export interface OwnerDashboardInventoryAlerts {
  lowStockCount: number;
  outOfStockCount: number;
  totalAlertCount: number;
  criticalItems: OwnerDashboardInventoryAlertItem[];
}

export interface OwnerDashboardCriticalVendorDue {
  vendorId: string;
  vendorName: string;
  vendorType: string;
  branchId: string | null;
  branchName: string | null;
  outstandingAmount: number;
  oldestDueDate: string | null;
  openBillCount: number;
}

export interface OwnerDashboardVendorDues {
  totalVendorOutstanding: number;
  overdueVendorCount: number;
  vendorsWithOutstandingCount: number;
  criticalVendors: OwnerDashboardCriticalVendorDue[];
}

export interface OwnerDashboardQuickLink {
  label: string;
  path: string;
  description: string;
}

export interface OwnerDashboardResponse {
  restaurantId: string;
  restaurantCode: string;
  restaurantName: string;
  branchId: string | null;
  branchName: string | null;
  businessDate: string;
  currencyCode: string;
  generatedAt: string;
  metrics: OwnerDashboardMetrics;
  alerts: OwnerDashboardAlert[];
  inventoryAlerts: OwnerDashboardInventoryAlerts;
  vendorDues: OwnerDashboardVendorDues;
  quickLinks: OwnerDashboardQuickLink[];
}

export interface OwnerDashboardQuery {
  date?: string;
  branchId?: string | null;
}
