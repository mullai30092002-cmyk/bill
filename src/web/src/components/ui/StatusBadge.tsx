import { Badge, type BadgeTone } from './Badge';

const STATUS_TONE: Record<string, BadgeTone> = {
  active: 'success',
  ready: 'success',
  served: 'success',
  packed: 'success',
  paid: 'success',
  confirmed: 'success',
  open: 'success',
  closed: 'neutral',
  inprogress: 'primary',
  preparing: 'primary',
  pending: 'warning',
  warning: 'warning',
  lowstock: 'warning',
  outofstock: 'danger',
  cancelled: 'danger',
  voided: 'danger',
  rejected: 'danger',
  draft: 'neutral',
  review: 'info',
  info: 'info',
};

export interface StatusBadgeProps {
  status?: string | null;
  label?: string;
  className?: string;
}

const normalise = (input: string) => input.toLowerCase().replace(/[\s_-]+/g, '');

export const StatusBadge = ({ status, label, className }: StatusBadgeProps) => {
  const safeStatus = typeof status === 'string' && status.trim() ? status : 'Unknown';
  const normalized = normalise(safeStatus);
  const tone = STATUS_TONE[normalized] ?? 'neutral';
  return <Badge label={label ?? safeStatus} tone={tone} className={className} />;
};

export default StatusBadge;
