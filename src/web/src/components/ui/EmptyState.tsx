import type { ReactNode } from 'react';
import { Card } from './Card';
import { Button } from './Button';

export interface EmptyStateProps {
  title: string;
  description: string;
  actionLabel?: string;
  onAction?: () => void;
  className?: string;
  tone?: 'default' | 'accent' | 'orders' | 'inventory' | 'admin';
  icon?: ReactNode;
}

export const EmptyState = ({
  title,
  description,
  actionLabel,
  onAction,
  className,
  tone = 'default',
  icon,
}: EmptyStateProps) => (
  <Card className={className} tone={tone}>
    <div className="ui-empty-state">
      {icon ? <div className="ui-empty-state__icon">{icon}</div> : null}
      <div className="ui-empty-state__copy">
        <h3 className="ui-empty-state__title">{title}</h3>
        <p className="ui-empty-state__description">{description}</p>
      </div>
      {actionLabel ? (
        <div className="ui-empty-state__actions">
          <Button onClick={onAction}>{actionLabel}</Button>
        </div>
      ) : null}
    </div>
  </Card>
);

export default EmptyState;
