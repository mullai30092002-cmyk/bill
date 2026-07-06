import type { ReactNode } from 'react';
import { ModuleLayout } from './ModuleLayout';

export interface InventoryManagementLayoutProps {
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

export const InventoryManagementLayout = (props: InventoryManagementLayoutProps) => (
  <ModuleLayout tone="inventory" maxWidth="xl" {...props} />
);

export default InventoryManagementLayout;
