import type { ReactNode } from 'react';

export interface PageHeaderProps {
  title: ReactNode;
  description?: ReactNode;
  breadcrumbs?: string[];
  actions?: ReactNode;
}

export const PageHeader = ({ title, description, breadcrumbs, actions }: PageHeaderProps) => (
  <header className="page-header">
    {breadcrumbs && breadcrumbs.length > 0 ? (
      <nav className="page-header__breadcrumbs" aria-label="Breadcrumb">
        {breadcrumbs.map((crumb, index) => (
          <span key={`${crumb}-${index}`} className="page-header__breadcrumb">
            {index > 0 ? <span className="page-header__separator">/</span> : null}
            <span>{crumb}</span>
          </span>
        ))}
      </nav>
    ) : null}
    <div className="page-header__row">
      <div className="page-header__text">
        <h1 className="page-header__title">{title}</h1>
        {description ? <p className="page-header__description">{description}</p> : null}
      </div>
      {actions ? <div className="page-header__actions">{actions}</div> : null}
    </div>
  </header>
);

export default PageHeader;
