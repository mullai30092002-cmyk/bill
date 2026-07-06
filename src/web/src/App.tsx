import { Navigate, Route, Routes } from 'react-router-dom';

import { billsoftBrandConfig } from './brand';
import { AuthProvider } from './features/auth/AuthProvider';
import LoginPage from './features/auth/LoginPage';
import DashboardPage from './features/dashboard/DashboardPage';
import OwnerDashboardPage from './features/dashboard/OwnerDashboardPage';
import InventoryWorkspacePreviewPage from './features/inventory/InventoryWorkspacePreviewPage';
import AdminUsersPreviewPage from './features/admin/AdminUsersPreviewPage';
import AdminUsersPage from './features/admin/AdminUsersPage';
import BranchManagementPage from './features/admin/branches/BranchManagementPage';
import MenuManagementPage from './features/admin/menu/MenuManagementPage';
import DailyCashSalesReportPage from './features/reports/DailyCashSalesReportPage';
import CashReconciliationReportPage from './features/reports/CashReconciliationReportPage';
import PreparedStockReportPage from './features/reports/PreparedStockReportPage';
import ExpiryStockReportPage from './features/reports/ExpiryStockReportPage';
import VendorPayablesReportPage from './features/reports/VendorPayablesReportPage';
import SetupChecklistPage from './features/setup/SetupChecklistPage';
import BillingPage from './features/billing/BillingPage';
import CashierShiftPage from './features/cashiering/CashierShiftPage';
import KitchenTicketsPage from './features/kitchen/KitchenTicketsPage';
import PosOrderCapturePage from './features/pos/PosOrderCapturePage';
import OrderWorkspacePreviewPage from './features/orders/OrderWorkspacePreviewPage';
import VendorWorkspacePage from './features/vendors/VendorWorkspacePage';
import VendorStatementPage from './features/vendors/VendorStatementPage';
import { getTranslatedShellNavItems, getVisibleShellNavItems } from './components/layout/navigation';
import ProtectedRoute from './routes/ProtectedRoute';
import PublicRoute from './routes/PublicRoute';
import { useAuth } from './features/auth/useAuth';
import { LanguageProvider, useLanguage } from './i18n/LanguageProvider';

const AppRoutes = () => {
  const { session } = useAuth();
  const { t } = useLanguage();
  const navItems = getTranslatedShellNavItems(getVisibleShellNavItems(session?.permissions), t);
  const workspaceProps = {
    navItems,
    restaurantName: session?.restaurantCode,
    branchName: session?.branchId ? 'Assigned branch' : undefined,
    operatorLabel: session?.fullName ?? billsoftBrandConfig.previewLabel,
  };

  return (
    <Routes>
      <Route
        path="/login"
        element={
          <PublicRoute>
            <LoginPage />
          </PublicRoute>
        }
      />
      <Route element={<ProtectedRoute />}>
        <Route path="/" element={<DashboardPage {...workspaceProps} />} />
        <Route path="/owner/dashboard" element={<OwnerDashboardPage {...workspaceProps} />} />
        <Route path="/orders-preview" element={<OrderWorkspacePreviewPage {...workspaceProps} />} />
        <Route path="/pos/orders" element={<PosOrderCapturePage {...workspaceProps} />} />
        <Route path="/billing" element={<BillingPage {...workspaceProps} />} />
        <Route path="/cashier/shifts" element={<CashierShiftPage {...workspaceProps} />} />
        <Route path="/reports/daily-cash-sales" element={<DailyCashSalesReportPage {...workspaceProps} />} />
        <Route path="/reports/cash-reconciliation" element={<CashReconciliationReportPage {...workspaceProps} />} />
        <Route path="/reports/prepared-stock" element={<PreparedStockReportPage {...workspaceProps} />} />
        <Route path="/reports/expiry-stock" element={<ExpiryStockReportPage {...workspaceProps} />} />
        <Route path="/reports/vendor-payables" element={<VendorPayablesReportPage {...workspaceProps} />} />
        <Route path="/setup" element={<SetupChecklistPage {...workspaceProps} />} />
        <Route path="/kitchen/tickets" element={<KitchenTicketsPage {...workspaceProps} />} />
        <Route path="/kitchen/display" element={<Navigate to="/kitchen/tickets" replace />} />
        <Route path="/inventory" element={<InventoryWorkspacePreviewPage {...workspaceProps} />} />
        <Route path="/inventory-preview" element={<InventoryWorkspacePreviewPage {...workspaceProps} />} />
        <Route path="/vendors" element={<VendorWorkspacePage {...workspaceProps} />} />
        <Route path="/vendors/statement" element={<VendorStatementPage {...workspaceProps} />} />
        <Route path="/admin-preview" element={<AdminUsersPreviewPage {...workspaceProps} />} />
        <Route path="/admin/users" element={<AdminUsersPage {...workspaceProps} />} />
        <Route path="/admin/branches" element={<BranchManagementPage {...workspaceProps} />} />
        <Route path="/admin/menu" element={<MenuManagementPage {...workspaceProps} />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
};

export const App = () => (
  <LanguageProvider>
    <AuthProvider>
      <AppRoutes />
    </AuthProvider>
  </LanguageProvider>
);

export default App;
