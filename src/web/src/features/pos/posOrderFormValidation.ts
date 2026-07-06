import type { MenuCategory, MenuItem } from '../admin/adminTypes';
import type { TranslationFunction } from '../../i18n/LanguageProvider';
import { parseQuantity } from './posDisplay';
import type {
  CancelPosOrderRequest,
  CreatePosOrderRequest,
  PosOrderDetail,
  PosOrderLineDetail,
  PosOrderLineRequest,
  PosOrderStatus,
  PosOrderType,
  UpdatePosOrderRequest,
} from './posTypes';

export interface PosOrderDraftLineForm {
  draftLineId: string;
  menuItemId: string;
  menuItemNameSnapshot: string;
  menuCategoryNameSnapshot: string;
  skuSnapshot: string | null;
  unitPrice: number;
  taxRate: number;
  quantity: string;
  notes: string;
  isAvailableForEatIn: boolean;
  isAvailableForParcel: boolean;
}

export interface PosOrderDraftForm {
  branchId: string;
  orderType: PosOrderType;
  tableName: string;
  customerName: string;
  customerMobile: string;
  notes: string;
  lines: PosOrderDraftLineForm[];
}

export interface PosOrderFormErrors {
  branchId?: string;
  lines?: string;
  state?: string;
}

export interface PosOrderLineErrors {
  [draftLineId: string]: {
    quantity?: string;
    notes?: string;
    availability?: string;
  };
}

export const emptyPosOrderDraftForm = (orderType: PosOrderType = 'EatIn'): PosOrderDraftForm => ({
  branchId: '',
  orderType,
  tableName: '',
  customerName: '',
  customerMobile: '',
  notes: '',
  lines: [],
});

export const createDraftLineId = () => `draft-line-${Date.now()}-${Math.random().toString(16).slice(2)}`;

export const createDraftLineFromMenuItem = (
  item: MenuItem,
  categoryName: string
): PosOrderDraftLineForm => ({
  draftLineId: createDraftLineId(),
  menuItemId: item.menuItemId,
  menuItemNameSnapshot: item.name,
  menuCategoryNameSnapshot: categoryName,
  skuSnapshot: item.sku ?? null,
  unitPrice: item.basePrice,
  taxRate: item.taxRate,
  quantity: '1',
  notes: '',
  isAvailableForEatIn: item.isAvailableForEatIn,
  isAvailableForParcel: item.isAvailableForParcel,
});

export const createDraftLineFromOrderLine = (
  line: PosOrderLineDetail,
  menuItem: MenuItem | undefined,
  categoryName: string | undefined
): PosOrderDraftLineForm => ({
  draftLineId: createDraftLineId(),
  menuItemId: line.menuItemId,
  menuItemNameSnapshot: menuItem?.name ?? line.menuItemNameSnapshot,
  menuCategoryNameSnapshot: categoryName ?? line.menuCategoryNameSnapshot,
  skuSnapshot: menuItem?.sku ?? line.skuSnapshot,
  unitPrice: menuItem?.basePrice ?? line.unitPrice,
  taxRate: menuItem?.taxRate ?? line.taxRate,
  quantity: line.quantity.toString(),
  notes: line.notes ?? '',
  isAvailableForEatIn: menuItem?.isAvailableForEatIn ?? true,
  isAvailableForParcel: menuItem?.isAvailableForParcel ?? true,
});

export const updateDraftLine = (
  line: PosOrderDraftLineForm,
  menuItem: MenuItem,
  categoryName: string
): PosOrderDraftLineForm => ({
  ...line,
  menuItemNameSnapshot: menuItem.name,
  menuCategoryNameSnapshot: categoryName,
  skuSnapshot: menuItem.sku ?? null,
  unitPrice: menuItem.basePrice,
  taxRate: menuItem.taxRate,
  isAvailableForEatIn: menuItem.isAvailableForEatIn,
  isAvailableForParcel: menuItem.isAvailableForParcel,
  quantity: line.quantity || '1',
  notes: line.notes,
});

export const buildPosOrderLineRequest = (line: PosOrderDraftLineForm): PosOrderLineRequest => ({
  menuItemId: line.menuItemId,
  quantity: parseQuantity(line.quantity),
  notes: line.notes.trim() ? line.notes.trim() : null,
});

export const buildCreatePosOrderRequest = (draft: PosOrderDraftForm): CreatePosOrderRequest => ({
  branchId: draft.branchId,
  orderType: draft.orderType,
  tableName: draft.tableName.trim() ? draft.tableName.trim() : null,
  customerName: draft.customerName.trim() ? draft.customerName.trim() : null,
  customerMobile: draft.customerMobile.trim() ? draft.customerMobile.trim() : null,
  notes: draft.notes.trim() ? draft.notes.trim() : null,
  lines: draft.lines.map(buildPosOrderLineRequest),
});

export const buildUpdatePosOrderRequest = (draft: PosOrderDraftForm): UpdatePosOrderRequest => ({
  orderType: draft.orderType,
  tableName: draft.tableName.trim() ? draft.tableName.trim() : null,
  customerName: draft.customerName.trim() ? draft.customerName.trim() : null,
  customerMobile: draft.customerMobile.trim() ? draft.customerMobile.trim() : null,
  notes: draft.notes.trim() ? draft.notes.trim() : null,
  lines: draft.lines.map(buildPosOrderLineRequest),
});

export const buildCancelPosOrderRequest = (reason: string): CancelPosOrderRequest => ({
  reason: reason.trim(),
});

export const buildPosOrderDraftFromDetail = (
  order: PosOrderDetail,
  menuItems: MenuItem[],
  categories: MenuCategory[]
): PosOrderDraftForm => {
  const menuItemLookup = new Map(menuItems.map(item => [item.menuItemId, item] as const));
  const categoryLookup = new Map(categories.map(category => [category.menuCategoryId, category] as const));

  return {
    branchId: order.branchId,
    orderType: order.orderType,
    tableName: order.tableName ?? '',
    customerName: order.customerName ?? '',
    customerMobile: order.customerMobile ?? '',
    notes: order.notes ?? '',
    lines: order.lines.map(line =>
      createDraftLineFromOrderLine(line, menuItemLookup.get(line.menuItemId), categoryLookup.get(line.menuCategoryId)?.name)
    ),
  };
};

export const getDraftLineQuantityError = (line: PosOrderDraftLineForm, t: TranslationFunction) => {
  const quantity = parseQuantity(line.quantity);
  if (!Number.isFinite(quantity) || quantity <= 0) {
    return t('pos.quantityGreaterThanZero');
  }

  if (quantity > 9999) {
    return t('pos.quantityLooksTooLarge');
  }

  return null;
};

export const canUseDraftLineForOrderType = (line: PosOrderDraftLineForm, orderType: PosOrderType) =>
  (orderType === 'EatIn' && line.isAvailableForEatIn) || (orderType === 'Parcel' && line.isAvailableForParcel);

export const buildPosOrderDraftValidation = (
  draft: PosOrderDraftForm,
  currentStatus: PosOrderStatus | null,
  canCreate: boolean,
  t: TranslationFunction
) => {
  const formErrors: PosOrderFormErrors = {};
  const lineErrors: PosOrderLineErrors = {};

  if (!canCreate) {
    formErrors.state = t('pos.orderEditingUnavailable');
    return { formErrors, lineErrors };
  }

  if (currentStatus && currentStatus !== 'Draft') {
    formErrors.state = t('pos.onlyDraftOrdersCanBeSaved');
  }

  if (!draft.branchId.trim()) {
    formErrors.branchId = t('pos.branchRequired');
  }

  if (draft.lines.length === 0) {
    formErrors.lines = t('pos.addAtLeastOneMenuItem');
  }

  draft.lines.forEach(line => {
    const quantityError = getDraftLineQuantityError(line, t);
    if (quantityError) {
      lineErrors[line.draftLineId] = {
        ...(lineErrors[line.draftLineId] ?? {}),
        quantity: quantityError,
      };
    }

    if (!canUseDraftLineForOrderType(line, draft.orderType)) {
      lineErrors[line.draftLineId] = {
        ...(lineErrors[line.draftLineId] ?? {}),
        availability:
          draft.orderType === 'EatIn'
            ? t('pos.notAvailableForEatInOrders')
            : t('pos.notAvailableForParcelOrders'),
      };
    }
  });

  return { formErrors, lineErrors };
};
