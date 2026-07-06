import type { ReactNode } from 'react';
import { AppShell } from './AppShell';
import { PageHeader } from './PageHeader';
import type { ShellNavItem } from './navigation';

export interface ModuleLayoutProps {
  tone: 'dashboard' | 'orders' | 'inventory' | 'admin';
  title: ReactNode;
  description?: ReactNode;
  breadcrumbs?: string[];
  actions?: ReactNode;
  children: ReactNode;
  navItems?: ShellNavItem[];
  restaurantName?: string;
  branchName?: string;
  operatorLabel?: string;
  maxWidth?: 'md' | 'lg' | 'xl' | 'full';
}

const maxWidthClass: Record<NonNullable<ModuleLayoutProps['maxWidth']>, string> = {
  md: 'module-layout__inner--md',
  lg: 'module-layout__inner--lg',
  xl: 'module-layout__inner--xl',
  full: 'module-layout__inner--full',
};

export const ModuleLayout = ({
  tone,
  title,
  description,
  breadcrumbs,
  actions,
  children,
  navItems,
  restaurantName,
  branchName,
  operatorLabel,
  maxWidth = 'xl',
}: ModuleLayoutProps) => (
  <AppShell
    tone={tone}
    navItems={navItems}
    restaurantName={restaurantName}
    branchName={branchName}
    operatorLabel={operatorLabel}
  >
    <div className={['module-layout', `module-layout--${tone}`].join(' ')}>
      <div className={['module-layout__inner', maxWidthClass[maxWidth]].join(' ')}>
        <PageHeader title={title} description={description} breadcrumbs={breadcrumbs} actions={actions} />
        <div className="module-layout__content">{children}</div>
      </div>
    </div>
  </AppShell>
);

export default ModuleLayout;
