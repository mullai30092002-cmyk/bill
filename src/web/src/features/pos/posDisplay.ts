import { formatCurrency } from '../finance/currencyDisplay';
import type { MenuCategory, MenuItem } from '../admin/adminTypes';
import type { PosOrderDetail, PosOrderLineDetail, PosOrderStatus, PosOrderType, PosOrderListItem } from './posTypes';

const categoryStatusPriority: Record<MenuCategory['status'], number> = {
  Active: 0,
  Inactive: 1,
};

const itemStatusPriority: Record<MenuItem['status'], number> = {
  Active: 0,
  Inactive: 1,
};

const orderStatusPriority: Record<PosOrderStatus, number> = {
  Draft: 0,
  Confirmed: 1,
  Cancelled: 2,
};

export const sortPosCategories = (categories: MenuCategory[]) =>
  [...categories].sort((left, right) => {
    const statusDelta = categoryStatusPriority[left.status] - categoryStatusPriority[right.status];
    if (statusDelta !== 0) {
      return statusDelta;
    }

    if (left.displayOrder !== right.displayOrder) {
      return left.displayOrder - right.displayOrder;
    }

    return left.name.localeCompare(right.name, undefined, { sensitivity: 'base' });
  });

export const sortPosItems = (items: MenuItem[]) =>
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

export const sortPosOrders = (orders: PosOrderListItem[]) =>
  [...orders].sort((left, right) => {
    const createdDelta = Date.parse(right.createdAt) - Date.parse(left.createdAt);
    if (createdDelta !== 0) {
      return createdDelta;
    }

    return right.orderNumber.localeCompare(left.orderNumber, undefined, { sensitivity: 'base' });
  });

export const sortPosLines = (lines: PosOrderLineDetail[]) =>
  [...lines].sort((left, right) => left.displayOrder - right.displayOrder);

export const formatPosCurrency = (value: number, currencyCode?: string | null, locale?: string | null) =>
  formatCurrency(value, currencyCode, locale);

export const formatPosTimestamp = (value?: string | null) => {
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

export const formatPosOrderType = (value: PosOrderType) => (value === 'EatIn' ? 'Eat-in' : 'Parcel');

export const formatPosOrderStatus = (value: PosOrderStatus) => value;

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

export const canAddMenuItemToOrderType = (item: MenuItem, orderType: PosOrderType) =>
  item.status === 'Active' &&
  ((orderType === 'EatIn' && item.isAvailableForEatIn) || (orderType === 'Parcel' && item.isAvailableForParcel));

export const buildSelectedOrderLabel = (order?: PosOrderDetail | null) => {
  if (!order) {
    return 'No order selected';
  }

  return `${order.orderNumber} · ${formatPosOrderStatus(order.status)}`;
};

export const roundMoney = (value: number) => Math.round(value * 100) / 100;

export const parseQuantity = (value: string) => {
  const parsed = Number.parseFloat(value);
  return Number.isFinite(parsed) ? parsed : Number.NaN;
};

export const buildEstimatedTotals = (lines: Array<{ quantity: string; unitPrice: number; taxRate: number }>) => {
  const subtotal = roundMoney(
    lines.reduce((sum, line) => {
      const quantity = parseQuantity(line.quantity);
      return Number.isFinite(quantity) && quantity > 0 ? sum + quantity * line.unitPrice : sum;
    }, 0)
  );

  const taxTotal = roundMoney(
    lines.reduce((sum, line) => {
      const quantity = parseQuantity(line.quantity);
      if (!Number.isFinite(quantity) || quantity <= 0) {
        return sum;
      }

      const lineSubtotal = quantity * line.unitPrice;
      return sum + (lineSubtotal * line.taxRate) / 100;
    }, 0)
  );

  const grandTotal = roundMoney(subtotal + taxTotal);

  return { subtotal, taxTotal, grandTotal };
};
