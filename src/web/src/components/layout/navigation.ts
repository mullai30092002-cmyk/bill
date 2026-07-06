import type { TranslationFunction } from '../../i18n/LanguageProvider';
import type { TranslationKey } from '../../i18n/translations';

export interface ShellNavItem {
  label: string;
  labelKey?: TranslationKey;
  to: string;
  hint: string;
  hintKey?: TranslationKey;
  icon: string;
  requiredPermission?: string;
  requiredPermissions?: string[];
}

export const shellNavItems: ShellNavItem[] = [
  { label: 'Dashboard', labelKey: 'nav.dashboard', to: '/', hint: 'Overview', hintKey: 'nav.dashboardHint', icon: 'home' },
  {
    label: 'Owner Dashboard',
    labelKey: 'nav.ownerDashboard',
    to: '/owner/dashboard',
    hint: 'Read only control',
    hintKey: 'nav.ownerDashboardHint',
    icon: 'crown',
    requiredPermission: 'Report.View',
  },
  {
    label: 'Setup',
    labelKey: 'nav.setup',
    to: '/setup',
    hint: 'Pilot readiness',
    hintKey: 'nav.setupHint',
    icon: 'clipboard-check',
    requiredPermissions: ['Report.View', 'Branch.Manage', 'User.Manage'],
  },
  {
    label: 'Orders preview',
    labelKey: 'nav.ordersPreview',
    to: '/orders-preview',
    hint: 'Touch-first',
    hintKey: 'nav.ordersPreviewHint',
    icon: 'layout',
  },
  {
    label: 'Orders',
    labelKey: 'nav.orders',
    to: '/pos/orders',
    hint: 'Eat-in and parcel',
    hintKey: 'nav.ordersHint',
    icon: 'receipt',
    requiredPermissions: ['Order.Create', 'Order.View'],
  },
  {
    label: 'Billing',
    labelKey: 'nav.billing',
    to: '/billing',
    hint: 'Cashier workspace',
    hintKey: 'nav.billingHint',
    icon: 'credit-card',
    requiredPermissions: ['Billing.View', 'Billing.Manage', 'Payment.Record'],
  },
  {
    label: 'Cashier Shifts',
    labelKey: 'nav.cashierShifts',
    to: '/cashier/shifts',
    hint: 'Shift control',
    hintKey: 'nav.cashierShiftsHint',
    icon: 'clock',
    requiredPermissions: ['CashShift.View', 'CashShift.Manage', 'CashMovement.Record'],
  },
  {
    label: 'Daily Report',
    labelKey: 'nav.dailyReport',
    to: '/reports/daily-cash-sales',
    hint: 'Leakage control',
    hintKey: 'nav.dailyReportHint',
    icon: 'calendar-days',
    requiredPermission: 'Report.View',
  },
  {
    label: 'Cash Reconciliation',
    labelKey: 'nav.cashReconciliation',
    to: '/reports/cash-reconciliation',
    hint: 'Cash exceptions',
    hintKey: 'nav.cashReconciliationHint',
    icon: 'bar-chart',
    requiredPermission: 'Report.View',
  },
  {
    label: 'Vendor Payables',
    labelKey: 'nav.vendorPayables',
    to: '/reports/vendor-payables',
    hint: 'Outstanding exposure',
    hintKey: 'nav.vendorPayablesHint',
    icon: 'wallet-cards',
    requiredPermission: 'Report.View',
  },
  {
    label: 'Prepared Stock',
    labelKey: 'nav.preparedStockReport',
    to: '/reports/prepared-stock',
    hint: 'Batch stock snapshot',
    hintKey: 'nav.preparedStockReportHint',
    icon: 'boxes',
    requiredPermission: 'Report.View',
  },
  {
    label: 'Expiry Stock',
    labelKey: 'nav.expiryStockReport',
    to: '/reports/expiry-stock',
    hint: 'Near-expiry and expired stock',
    hintKey: 'nav.expiryStockReportHint',
    icon: 'hourglass',
    requiredPermission: 'Report.View',
  },
  {
    label: 'Kitchen Display',
    labelKey: 'nav.kitchenDisplay',
    to: '/kitchen/tickets',
    hint: 'Kitchen queue',
    hintKey: 'nav.kitchenDisplayHint',
    icon: 'flame',
    requiredPermissions: ['KitchenTicket.View', 'KitchenTicket.Manage', 'KitchenTicket.UpdateStatus'],
  },
  {
    label: 'Inventory',
    labelKey: 'nav.inventory',
    to: '/inventory',
    hint: 'Stock and audit',
    hintKey: 'nav.inventoryHint',
    icon: 'package',
  },
  {
    label: 'Admin users',
    labelKey: 'nav.adminUsers',
    to: '/admin/users',
    hint: 'Users and roles',
    hintKey: 'nav.adminUsersHint',
    icon: 'users',
    requiredPermission: 'User.Manage',
  },
  {
    label: 'Branches',
    labelKey: 'nav.branches',
    to: '/admin/branches',
    hint: 'Branch management',
    hintKey: 'nav.branchesHint',
    icon: 'map-pin',
    requiredPermission: 'Branch.Manage',
  },
  {
    label: 'Menu',
    labelKey: 'nav.menuManagement',
    to: '/admin/menu',
    hint: 'Catalog and pricing',
    hintKey: 'nav.menuManagementHint',
    icon: 'book-open',
    requiredPermissions: ['MenuCategory.Manage', 'MenuItem.Manage', 'MenuItem.View'],
  },
  {
    label: 'Vendors',
    labelKey: 'nav.vendors',
    to: '/vendors',
    hint: 'Bills and settlements',
    hintKey: 'nav.vendorsHint',
    icon: 'truck',
    requiredPermissions: ['VendorBill.Upload', 'VendorBill.ReviewOcr', 'VendorBill.OverrideOcr', 'VendorBill.Confirm', 'VendorPayment.Create'],
  },
];

const hasAnyPermission = (permissions: string[] | undefined, requiredPermissions: string[]) =>
  requiredPermissions.some(permission => permissions?.includes(permission));

export const getVisibleShellNavItems = (permissions?: string[]) =>
  shellNavItems.filter(item => {
    if (item.requiredPermission) {
      return permissions?.includes(item.requiredPermission) ?? false;
    }

    if (item.requiredPermissions) {
      return hasAnyPermission(permissions, item.requiredPermissions);
    }

    return true;
  });

export const getTranslatedShellNavItems = (items: ShellNavItem[], t: TranslationFunction): ShellNavItem[] =>
  items.map(item => ({
    ...item,
    label: item.labelKey ? t(item.labelKey) : item.label,
    hint: item.hintKey ? t(item.hintKey) : item.hint,
  }));
