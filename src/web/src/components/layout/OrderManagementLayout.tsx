import type { ReactNode } from 'react';
import { ModuleLayout } from './ModuleLayout';

export interface OrderManagementLayoutProps {
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

export const OrderManagementLayout = (props: OrderManagementLayoutProps) => (
  <ModuleLayout tone="orders" maxWidth="full" {...props} />
);

export default OrderManagementLayout;
