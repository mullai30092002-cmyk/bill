import { billsoftBrandConfig } from '../../brand/brandConfig';

export interface BrandLogoProps {
  size?: 'sm' | 'md' | 'lg';
  compact?: boolean;
  className?: string;
}

const SIZE_CLASS: Record<NonNullable<BrandLogoProps['size']>, string> = {
  sm: 'brand-logo--sm',
  md: 'brand-logo--md',
  lg: 'brand-logo--lg',
};

export const BrandLogo = ({ size = 'md', compact = false, className }: BrandLogoProps) => (
  <div className={['brand-logo', SIZE_CLASS[size], className].filter(Boolean).join(' ')}>
    <div className="brand-logo__mark" aria-hidden="true">
      {billsoftBrandConfig.logoMark}
    </div>
    {compact ? null : (
      <div className="brand-logo__text">
        <span className="brand-logo__name">{billsoftBrandConfig.brandName}</span>
        <span className="brand-logo__tagline">Restaurant control foundation</span>
      </div>
    )}
  </div>
);

export default BrandLogo;
