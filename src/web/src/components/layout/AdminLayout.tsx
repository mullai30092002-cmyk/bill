import type { ReactNode } from 'react';
import { ModuleLayout } from './ModuleLayout';

export interface AdminLayoutProps {
  title: ReactNode;
  description?: ReactNode;
  breadcrumbs?: string[];
  actions?: ReactNode;
  children: ReactNode;
  navItems?: import('./navigation').ShellNavItem[];
  restaurantName?: string;
  branchName?: string;
  operatorLabel?: string;
}

export const AdminLayout = (props: AdminLayoutProps) => (
  <ModuleLayout tone="admin" maxWidth="lg" {...props} />
);

export default AdminLayout;
