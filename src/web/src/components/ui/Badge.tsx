import type { ReactNode } from 'react';

export type BadgeTone = 'neutral' | 'primary' | 'accent' | 'success' | 'warning' | 'danger' | 'info';

export interface BadgeProps {
  label: ReactNode;
  tone?: BadgeTone;
  icon?: ReactNode;
  className?: string;
}

const toneClass: Record<BadgeTone, string> = {
  neutral: 'ui-badge--neutral',
  primary: 'ui-badge--primary',
  accent: 'ui-badge--accent',
  success: 'ui-badge--success',
  warning: 'ui-badge--warning',
  danger: 'ui-badge--danger',
  info: 'ui-badge--info',
};

export const Badge = ({ label, tone = 'neutral', icon, className }: BadgeProps) => (
  <span className={['ui-badge', toneClass[tone], className].filter(Boolean).join(' ')}>
    {icon ? <span className="ui-badge__icon">{icon}</span> : null}
    <span>{label}</span>
  </span>
);

export default Badge;
