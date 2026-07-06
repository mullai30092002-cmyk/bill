export const billsoftBrandConfig = {
  brandName: 'BillSoft',
  logoMark: 'BS',
  defaultRestaurantName: 'Sample Restaurant',
  defaultBranchName: 'Main Branch',
  previewLabel: 'Preview mode',
  whiteLabelReady: true,
  personality: ['warm', 'direct', 'fast', 'audit-friendly'] as const,
  moduleLabels: {
    dashboard: 'Dashboard',
    orders: 'Order management',
    inventory: 'Inventory & stock',
    admin: 'Admin & setup',
  },
} as const;

export const getRestaurantDisplayName = (restaurantName?: string) =>
  restaurantName?.trim() || billsoftBrandConfig.defaultRestaurantName;

export const getBranchDisplayName = (branchName?: string) =>
  branchName?.trim() || billsoftBrandConfig.defaultBranchName;
