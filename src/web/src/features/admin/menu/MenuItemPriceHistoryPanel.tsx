import { Badge, EmptyState, ResponsiveDataList, StatusBadge } from '../../../components/ui';
import { useLanguage } from '../../../i18n/LanguageProvider';
import { useRestaurantCurrency } from '../../auth/useRestaurantCurrency';
import type { MenuItem, MenuItemPriceHistory } from '../adminTypes';
import { formatMenuPrice, formatMenuTimestamp, formatPriceHistoryReason } from './menuDisplay';

interface MenuItemHistoryRow {
  id: string;
  menuItemPriceHistoryId: string;
  oldPriceLabel: string;
  newPriceLabel: string;
  changedAt: string;
  reason: string;
}

interface MenuItemPriceHistoryPanelProps {
  item: MenuItem | null;
  history: MenuItemPriceHistory[];
  loading?: boolean;
  error?: string | null;
  onRetry: () => void;
}

export const MenuItemPriceHistoryPanel = ({
  item,
  history,
  loading,
  error,
  onRetry,
}: MenuItemPriceHistoryPanelProps) => {
  const { t } = useLanguage();
  const { currencyCode, locale } = useRestaurantCurrency();

  if (loading) {
    return (
      <EmptyState
        title={t('menu.priceHistoryLoadingTitle')}
        description={t('menu.priceHistoryLoadingDescription')}
        tone="admin"
      />
    );
  }

  if (error) {
    return (
      <EmptyState
        title={t('menu.priceHistoryErrorTitle')}
        description={error}
        tone="admin"
        actionLabel={t('menu.tryAgain')}
        onAction={onRetry}
      />
    );
  }

  if (!item) {
    return (
      <EmptyState
        title={t('menu.priceHistoryChooseItemTitle')}
        description={t('menu.priceHistoryChooseItemDescription')}
        tone="admin"
      />
    );
  }

  if (history.length === 0) {
    return (
      <EmptyState
        title={t('menu.priceHistoryEmptyTitle')}
        description={t('menu.priceHistoryEmptyDescription')}
        tone="admin"
      />
    );
  }

  const rows: MenuItemHistoryRow[] = history.map(entry => ({
    id: entry.menuItemPriceHistoryId,
    menuItemPriceHistoryId: entry.menuItemPriceHistoryId,
    oldPriceLabel: formatMenuPrice(entry.oldPrice, currencyCode, locale),
    newPriceLabel: formatMenuPrice(entry.newPrice, currencyCode, locale),
    changedAt: formatMenuTimestamp(entry.changedAt),
    reason: formatPriceHistoryReason(entry),
  }));

  return (
    <div className="menu-price-history-panel">
      <div className="menu-item-detail-summary">
        <div className="menu-item-detail-summary__row">
          <span className="menu-item-detail-summary__label">{t('menu.metaCategory')}</span>
          <strong>{item.categoryName}</strong>
        </div>
        <div className="menu-item-detail-summary__row">
          <span className="menu-item-detail-summary__label">{t('menu.metaCurrentPrice')}</span>
          <strong>{formatMenuPrice(item.basePrice, currencyCode, locale)}</strong>
        </div>
        <div className="menu-item-detail-summary__row">
          <span className="menu-item-detail-summary__label">{t('menu.metaTax')}</span>
          <strong>{item.taxRate.toFixed(2)}%</strong>
        </div>
        <div className="menu-item-detail-summary__row">
          <span className="menu-item-detail-summary__label">{t('menu.columnStatus')}</span>
          <div className="menu-item-detail-summary__badges">
            <StatusBadge status={item.status} />
            {item.isVegetarian ? <Badge tone="success" label={t('menu.badgeVegetarian')} /> : <Badge tone="neutral" label={t('menu.badgeNonVegetarian')} />}
            <Badge tone="accent" label={item.isAvailableForEatIn ? t('menu.badgeEatIn') : t('menu.badgeNoEatIn')} />
            <Badge tone="accent" label={item.isAvailableForParcel ? t('menu.badgeParcel') : t('menu.badgeNoParcel')} />
          </div>
        </div>
      </div>

      <ResponsiveDataList
        rows={rows}
        columns={[
          { key: 'oldPriceLabel', label: t('menu.priceHistoryColumnOldPrice') },
          { key: 'newPriceLabel', label: t('menu.priceHistoryColumnNewPrice') },
          { key: 'changedAt', label: t('menu.priceHistoryColumnChangedAt') },
          { key: 'reason', label: t('menu.priceHistoryColumnReason') },
        ]}
        mobileTitle={row => `${row.oldPriceLabel} → ${row.newPriceLabel}`}
        mobileDescription={row => row.reason}
        emptyTitle={t('menu.priceHistoryEmptyTitle')}
        emptyDescription={t('menu.priceHistoryEmptyDescription')}
      />
    </div>
  );
};

export default MenuItemPriceHistoryPanel;
