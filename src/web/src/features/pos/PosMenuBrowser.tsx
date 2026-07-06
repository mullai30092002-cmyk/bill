import { useState } from 'react';

import { Badge, Card, EmptyState, Input } from '../../components/ui';
import { useLanguage } from '../../i18n/LanguageProvider';
import { useRestaurantCurrency } from '../auth/useRestaurantCurrency';
import type { MenuCategory, MenuItem } from '../admin/adminTypes';
import { canAddMenuItemToOrderType, formatPosCurrency, sortPosCategories, sortPosItems } from './posDisplay';
import type { PosOrderType } from './posTypes';

export interface PosMenuBrowserProps {
  categories: MenuCategory[];
  items: MenuItem[];
  selectedCategoryId: string;
  selectedOrderType: PosOrderType;
  canCreate: boolean;
  onCategorySelect: (categoryId: string) => void;
  onAddItem: (itemId: string) => void;
}

export const PosMenuBrowser = ({
  categories,
  items,
  selectedCategoryId,
  selectedOrderType,
  canCreate,
  onCategorySelect,
  onAddItem,
}: PosMenuBrowserProps) => {
  const { currencyCode, locale } = useRestaurantCurrency();
  const { t } = useLanguage();
  const [searchQuery, setSearchQuery] = useState('');

  const activeCategories = sortPosCategories(categories).filter(category => category.status === 'Active');
  const activeItems = sortPosItems(items).filter(item => item.status === 'Active');

  const normalizedQuery = searchQuery.trim().toLowerCase();
  const visibleItems = activeItems.filter(item => {
    const matchesCategory = !selectedCategoryId || item.menuCategoryId === selectedCategoryId;
    const matchesSearch = !normalizedQuery || item.name.toLowerCase().includes(normalizedQuery);
    return matchesCategory && matchesSearch;
  });

  return (
    <Card
      title={t('pos.menuBrowserTitle')}
      description={t('pos.menuBrowserDescription')}
      tone="orders"
      actions={<Badge tone="neutral" label={t('pos.activeItemsCount', { count: activeItems.length })} />}
    >
      {/* Search input */}
      <div className="pos-menu-search">
        <Input
          label={t('pos.searchItemsLabel')}
          placeholder={t('pos.searchItemsPlaceholder')}
          value={searchQuery}
          onChange={event => setSearchQuery(event.target.value)}
        />
      </div>

      {/* Category strip */}
      <div className="pos-category-strip">
        <button
          type="button"
          className={`pos-category-chip${!selectedCategoryId ? ' pos-category-chip--active' : ''}`}
          onClick={() => onCategorySelect('')}
        >
          {t('pos.allCategories')}
        </button>
        {activeCategories.map(category => (
          <button
            key={category.menuCategoryId}
            type="button"
            className={`pos-category-chip${selectedCategoryId === category.menuCategoryId ? ' pos-category-chip--active' : ''}`}
            onClick={() => onCategorySelect(category.menuCategoryId)}
          >
            {category.name}
          </button>
        ))}
      </div>

      {visibleItems.length === 0 ? (
        <EmptyState
          title={t('pos.noMenuItemsTitle')}
          description={
            normalizedQuery
              ? t('pos.noMenuItemsSearchDescription', { searchQuery })
              : t('pos.noMenuItemsDescription')
          }
          tone="orders"
        />
      ) : (
        <div className="pos-item-grid">
          {visibleItems.map(item => {
            const canAdd = canCreate && canAddMenuItemToOrderType(item, selectedOrderType);
            return (
              <button
                key={item.menuItemId}
                type="button"
                className={`pos-item-card${canAdd ? '' : ' pos-item-card--disabled'}`}
                disabled={!canAdd}
                onClick={() => onAddItem(item.menuItemId)}
                aria-label={item.name}
              >
                <span className="pos-item-card__name">{item.name}</span>
                <span className="pos-item-card__price">{formatPosCurrency(item.basePrice, currencyCode, locale)}</span>
                {item.isVegetarian ? (
                  <span className="pos-item-card__veg" title={t('pos.vegetarian')} aria-label={t('pos.vegetarian')}>🟢</span>
                ) : null}
                <span className="pos-item-card__add">{canAdd ? t('pos.add') : t('pos.unavailable')}</span>
              </button>
            );
          })}
        </div>
      )}

      {!canCreate ? <div className="admin-form-note">{t('pos.menuReadOnlyNote')}</div> : null}
    </Card>
  );
};

export default PosMenuBrowser;
