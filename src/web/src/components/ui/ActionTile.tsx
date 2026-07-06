import type { ButtonHTMLAttributes, ReactNode } from 'react';

export interface ActionTileProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  title: string;
  description?: ReactNode;
  badge?: ReactNode;
  selected?: boolean;
  tone?: 'default' | 'dashboard' | 'orders' | 'inventory' | 'admin' | 'accent';
}

export const ActionTile = ({
  title,
  description,
  badge,
  selected = false,
  tone = 'default',
  className,
  children,
  type = 'button',
  ...props
}: ActionTileProps) => (
  <button
    type={type}
    className={[
      'action-tile',
      tone !== 'default' && `action-tile--${tone}`,
      selected && 'action-tile--selected',
      className,
    ]
      .filter(Boolean)
      .join(' ')}
    aria-pressed={selected || undefined}
    {...props}
  >
    <div className="action-tile__header">
      <span className="action-tile__title">{title}</span>
      {badge ? <span className="action-tile__badge">{badge}</span> : null}
    </div>
    {description ? <div className="action-tile__description">{description}</div> : null}
    {children ? <div className="action-tile__body">{children}</div> : null}
  </button>
);

export default ActionTile;
