import { Button, EmptyState, Input, ResponsiveDataList, Select, Badge, StatusBadge } from '../../../components/ui';
import { useLanguage } from '../../../i18n/LanguageProvider';
import { useRestaurantCurrency } from '../../auth/useRestaurantCurrency';
import type { MenuCategory, MenuItem, MenuItemStatus } from '../adminTypes';
import {
  buildMenuItemAvailabilityLabel,
  formatMenuPrice,
  sortMenuCategories,
  sortMenuItems,
} from './menuDisplay';

interface MenuItemRow {
  id: string;
  menuItemId: string;
  name: string;
  categoryName: string;
  basePriceLabel: string;
  sku: string;
  isVegetarian: boolean;
  vegetarianLabel: string;
  availabilityLabel: string;
  status: MenuItemStatus;
  updatedAt: string | null;
  actionLabel: string;
}

interface MenuItemListProps {
  categories: MenuCategory[];
  items: MenuItem[];
  loading?: boolean;
  error?: string | null;
  search: string;
  statusFilter: 'All' | MenuItemStatus;
  categoryFilter: string;
  availabilityFilter: 'All' | 'EatIn' | 'Parcel';
  canManageItems: boolean;
  onSearchChange: (value: string) => void;
  onStatusFilterChange: (value: 'All' | MenuItemStatus) => void;
  onCategoryFilterChange: (value: string) => void;
  onAvailabilityFilterChange: (value: 'All' | 'EatIn' | 'Parcel') => void;
  onRetry: () => void;
  onSelectItem: (itemId: string) => void;
}

export const MenuItemList = ({
  categories,
  items,
  loading,
  error,
  search,
  statusFilter,
  categoryFilter,
  availabilityFilter,
  canManageItems,
  onSearchChange,
  onStatusFilterChange,
  onCategoryFilterChange,
  onAvailabilityFilterChange,
  onRetry,
  onSelectItem,
}: MenuItemListProps) => {
  const { t } = useLanguage();
  const { currencyCode, locale } = useRestaurantCurrency();

  if (loading && items.length === 0) {
    return (
      <EmptyState
        title={t('menu.loadingItemsTitle')}
        description={t('menu.loadingItemsDescription')}
        tone="admin"
      />
    );
  }

  if (error) {
    return (
      <EmptyState
        title={t('menu.couldNotLoadItemsTitle')}
        description={error}
        tone="admin"
        actionLabel={t('menu.tryAgain')}
        onAction={onRetry}
      />
    );
  }

  const rows: MenuItemRow[] = sortMenuItems(items).map(item => ({
    id: item.menuItemId,
    menuItemId: item.menuItemId,
    name: item.name,
    categoryName: item.categoryName,
    basePriceLabel: formatMenuPrice(item.basePrice, currencyCode, locale),
    sku: item.sku ?? t('menu.notProvided'),
    isVegetarian: item.isVegetarian,
    vegetarianLabel: item.isVegetarian ? t('menu.vegetarianLabel') : t('menu.nonVegetarianLabel'),
    availabilityLabel: buildMenuItemAvailabilityLabel(item),
    status: item.status,
    updatedAt: item.updatedAt,
    actionLabel: canManageItems ? t('menu.editButton') : t('menu.viewDetailsButton'),
  }));

  const categoryOptions = [
    <option key="all-categories" value="">
      {t('menu.allCategoriesOption')}
    </option>,
    ...sortMenuCategories(categories).map(category => (
      <option key={category.menuCategoryId} value={category.menuCategoryId}>
        {category.name}
      </option>
    )),
  ];

  return (
    <div className="admin-controls">
      <div className="admin-form-grid menu-management__filters">
        <Input
          label={t('menu.searchItemsLabel')}
          value={search}
          onChange={event => onSearchChange(event.target.value)}
          placeholder="Name, SKU, description, or category"
          helperText={t('menu.searchItemsHelper')}
        />
        <Select
          label={t('menu.categoryFilterLabel')}
          value={categoryFilter}
          onChange={event => onCategoryFilterChange(event.target.value)}
          helperText={t('menu.categoryFilterHelper')}
        >
          {categoryOptions}
        </Select>
        <Select
          label={t('menu.statusFilterLabel')}
          value={statusFilter}
          onChange={event => onStatusFilterChange(event.target.value as 'All' | MenuItemStatus)}
          helperText={t('menu.statusFilterHelper')}
        >
          <option value="All">{t('menu.filterAll')}</option>
          <option value="Active">{t('menu.filterActive')}</option>
          <option value="Inactive">{t('menu.filterInactive')}</option>
        </Select>
        <Select
          label={t('menu.availabilityFilterLabel')}
          value={availabilityFilter}
          onChange={event => onAvailabilityFilterChange(event.target.value as 'All' | 'EatIn' | 'Parcel')}
          helperText={t('menu.availabilityFilterHelper')}
        >
          <option value="All">{t('menu.filterAll')}</option>
          <option value="EatIn">{t('menu.filterEatIn')}</option>
          <option value="Parcel">{t('menu.filterParcel')}</option>
        </Select>
      </div>

      <ResponsiveDataList
        rows={rows}
        columns={[
          { key: 'name', label: t('menu.columnItem') },
          { key: 'categoryName', label: t('menu.columnCategory') },
          {
            key: 'basePriceLabel',
            label: t('menu.columnBasePrice'),
            align: 'right',
            render: row => row.basePriceLabel,
          },
          { key: 'sku', label: t('menu.columnSKU') },
          {
            key: 'vegetarianLabel',
            label: t('menu.columnVegetarian'),
            render: row => <Badge tone={row.isVegetarian ? 'success' : 'neutral'} label={row.vegetarianLabel} />,
          },
          {
            key: 'availabilityLabel',
            label: t('menu.columnAvailability'),
            render: row => <Badge tone="accent" label={row.availabilityLabel} />,
          },
          {
            key: 'status',
            label: t('menu.columnStatus'),
            render: row => <StatusBadge status={row.status} />,
          },
          {
            key: 'menuItemId',
            label: t('menu.columnActions'),
            render: row => (
              <Button
                type="button"
                variant="secondary"
                size="md"
                fullWidth
                onClick={() => onSelectItem(row.menuItemId)}
              >
                {row.actionLabel}
              </Button>
            ),
          },
        ]}
        mobileTitle={row => row.name}
        mobileDescription={row => `${row.categoryName} · ${row.basePriceLabel}`}
        emptyTitle={t('menu.noItemsFoundTitle')}
        emptyDescription={t('menu.noItemsFoundDescription')}
      />

      {categories.length === 0 ? <div className="admin-form-note">{t('menu.noCategoriesAvailable')}</div> : null}
    </div>
  );
};

export default MenuItemList;
