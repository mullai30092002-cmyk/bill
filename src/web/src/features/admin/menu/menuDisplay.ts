import { formatCurrency } from '../../finance/currencyDisplay';
import type { MenuCategory, MenuItem, MenuItemPriceHistory, MenuItemStatus } from '../adminTypes';

const statusPriority: Record<MenuCategory['status'], number> = {
  Active: 0,
  Inactive: 1,
};

export const sortMenuCategories = (categories: MenuCategory[]) =>
  [...categories].sort((left, right) => {
    const statusDelta = statusPriority[left.status] - statusPriority[right.status];
    if (statusDelta !== 0) {
      return statusDelta;
    }

    const orderDelta = left.displayOrder - right.displayOrder;
    if (orderDelta !== 0) {
      return orderDelta;
    }

    return left.name.localeCompare(right.name, undefined, { sensitivity: 'base' });
  });

const itemStatusPriority: Record<MenuItemStatus, number> = {
  Active: 0,
  Inactive: 1,
};

export const sortMenuItems = (items: MenuItem[]) =>
  [...items].sort((left, right) => {
    const statusDelta = itemStatusPriority[left.status] - itemStatusPriority[right.status];
    if (statusDelta !== 0) {
      return statusDelta;
    }

    const categoryDelta = left.categoryName.localeCompare(right.categoryName, undefined, { sensitivity: 'base' });
    if (categoryDelta !== 0) {
      return categoryDelta;
    }

    return left.name.localeCompare(right.name, undefined, { sensitivity: 'base' });
  });

export const buildCategoryItemCounts = (items: MenuItem[]) =>
  items.reduce<Record<string, number>>((counts, item) => {
    counts[item.menuCategoryId] = (counts[item.menuCategoryId] ?? 0) + 1;
    return counts;
  }, {});

export const buildMenuItemAvailabilityLabel = (item: MenuItem) => {
  if (item.isAvailableForEatIn && item.isAvailableForParcel) {
    return 'Eat-in and parcel';
  }

  if (item.isAvailableForEatIn) {
    return 'Eat-in only';
  }

  if (item.isAvailableForParcel) {
    return 'Parcel only';
  }

  return 'Unavailable';
};

export const formatMenuPrice = (value: number, currencyCode?: string | null, locale?: string | null) =>
  formatCurrency(value, currencyCode, locale);

export const formatMenuTimestamp = (value?: string | null) => {
  if (!value) {
    return 'Not available';
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return parsed.toLocaleString(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  });
};

export const formatPriceHistoryReason = (history: MenuItemPriceHistory) => history.reason?.trim() || 'No reason provided';

