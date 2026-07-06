import { billsoftBrandConfig, getBranchDisplayName, getRestaurantDisplayName } from '../../brand/brandConfig';
import { Badge } from '../ui/Badge';
import { BrandLogo } from './BrandLogo';

export interface BrandHeaderProps {
  restaurantName?: string;
  branchName?: string;
  operatorLabel?: string;
  compact?: boolean;
}

export const BrandHeader = ({
  restaurantName,
  branchName,
  operatorLabel = billsoftBrandConfig.previewLabel,
  compact = false,
}: BrandHeaderProps) => (
  <div className={['brand-header', compact && 'brand-header--compact'].filter(Boolean).join(' ')}>
    <BrandLogo size={compact ? 'sm' : 'md'} compact={compact} />
    <div className="brand-header__context">
      <div className="brand-header__title-row">
        <strong className="brand-header__restaurant">{getRestaurantDisplayName(restaurantName)}</strong>
        <Badge tone="neutral" label={operatorLabel} />
      </div>
      <div className="brand-header__subtle">
        <span>{billsoftBrandConfig.brandName}</span>
        <span>•</span>
        <span>{getBranchDisplayName(branchName)}</span>
      </div>
    </div>
  </div>
);

export default BrandHeader;
