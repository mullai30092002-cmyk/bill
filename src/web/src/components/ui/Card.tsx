import type { ReactNode } from 'react';

export interface CardProps {
  title?: ReactNode;
  description?: ReactNode;
  actions?: ReactNode;
  children?: ReactNode;
  tone?: 'default' | 'accent' | 'dashboard' | 'orders' | 'inventory' | 'admin';
  className?: string;
}

export const Card = ({ title, description, actions, children, tone = 'default', className }: CardProps) => (
  <section className={['ui-card', tone !== 'default' && `ui-card--${tone}`, className].filter(Boolean).join(' ')}>
    {title || description || actions ? (
      <header className="ui-card__header">
        <div className="ui-card__title-block">
          {title ? <h3 className="ui-card__title">{title}</h3> : null}
          {description ? <p className="ui-card__description">{description}</p> : null}
        </div>
        {actions ? <div className="ui-card__actions">{actions}</div> : null}
      </header>
    ) : null}
    {children ? <div className="ui-card__body">{children}</div> : null}
  </section>
);

export default Card;
