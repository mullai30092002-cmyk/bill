import type { FC } from 'react';

import { useLanguage } from '../../i18n/LanguageProvider';
import type { PosOrderType } from './posTypes';

const EatInIcon: FC = () => (
  <svg
    className="segment-button__icon"
    viewBox="0 0 20 20"
    fill="none"
    stroke="currentColor"
    strokeWidth="1.7"
    strokeLinecap="round"
    strokeLinejoin="round"
    aria-hidden="true"
    focusable="false"
  >
    <circle cx="10" cy="11" r="5" />
    <path d="M10 6V4" />
    <path d="M7 4v3c0 1.1.9 2 2 2" />
    <path d="M13 4v2a1 1 0 0 1-1 1h-1" />
  </svg>
);

const ParcelIcon: FC = () => (
  <svg
    className="segment-button__icon"
    viewBox="0 0 20 20"
    fill="none"
    stroke="currentColor"
    strokeWidth="1.7"
    strokeLinecap="round"
    strokeLinejoin="round"
    aria-hidden="true"
    focusable="false"
  >
    <path d="M6 7V6a4 4 0 0 1 8 0v1" />
    <rect x="3" y="7" width="14" height="10" rx="2" />
    <path d="M8 12h4" />
  </svg>
);

const icons: Record<PosOrderType, FC> = {
  EatIn: EatInIcon,
  Parcel: ParcelIcon,
};

export interface PosOrderTypeToggleProps {
  value: PosOrderType;
  onChange: (value: PosOrderType) => void;
  disabled?: boolean;
}

export const PosOrderTypeToggle = ({ value, onChange, disabled = false }: PosOrderTypeToggleProps) => {
  const { t } = useLanguage();

  return (
    <div className="toggle-row" role="group" aria-label={t('pos.orderType')}>
      {(Object.keys(icons) as PosOrderType[]).map(type => {
        const Icon = icons[type];

        return (
          <button
            key={type}
            type="button"
            className={['segment-button', 'segment-button--icon-text', value === type && 'segment-button--active'].filter(Boolean).join(' ')}
            aria-pressed={value === type}
            disabled={disabled}
            onClick={() => onChange(type)}
          >
            <Icon />
            <span>{type === 'EatIn' ? t('pos.orderTypeEatIn') : t('pos.orderTypeParcel')}</span>
          </button>
        );
      })}
    </div>
  );
};

export default PosOrderTypeToggle;
