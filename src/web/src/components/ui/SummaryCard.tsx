import type { ReactNode } from 'react';
import { Card } from './Card';

export interface SummaryCardProps {
  label: string;
  value: ReactNode;
  detail?: ReactNode;
  tone?: 'default' | 'accent' | 'dashboard' | 'orders' | 'inventory' | 'admin';
  delta?: ReactNode;
}

export const SummaryCard = ({ label, value, detail, tone = 'default', delta }: SummaryCardProps) => (
  <Card tone={tone} className="summary-card">
    <div className="summary-card__label">{label}</div>
    <div className="summary-card__value-row">
      <div className="summary-card__value">{value}</div>
      {delta ? <div className="summary-card__delta">{delta}</div> : null}
    </div>
    {detail ? <div className="summary-card__detail">{detail}</div> : null}
  </Card>
);

export default SummaryCard;
