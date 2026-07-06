import type {
  CreateMenuCategoryRequest,
  CreateMenuItemRequest,
  UpdateMenuCategoryRequest,
  UpdateMenuItemRequest,
  MenuItemInventoryDeductionMode,
} from '../adminTypes';

export interface MenuCategoryFormState {
  name: string;
  displayOrder: string;
}

export interface MenuCategoryFormErrors {
  name?: string;
  displayOrder?: string;
}

export interface MenuItemFormState {
  menuCategoryId: string;
  name: string;
  description: string;
  sku: string;
  basePrice: string;
  taxRate: string;
  isVegetarian: boolean;
  isAvailableForEatIn: boolean;
  isAvailableForParcel: boolean;
  inventoryDeductionMode: MenuItemInventoryDeductionMode;
  stockInventoryItemId: string;
}

export interface MenuItemFormErrors {
  menuCategoryId?: string;
  name?: string;
  basePrice?: string;
  taxRate?: string;
  availability?: string;
  inventoryDeductionMode?: string;
  stockInventoryItemId?: string;
}

export interface MenuItemFormMessages {
  inventoryDeductionModeRequired: string;
  stockInventoryItemRequired: string;
  stockInventoryItemBranchRequired: string;
}

const defaultMenuItemFormMessages: MenuItemFormMessages = {
  inventoryDeductionModeRequired: 'Inventory deduction mode is required.',
  stockInventoryItemRequired: 'Prepared stock item is required for the selected deduction mode.',
  stockInventoryItemBranchRequired: 'Branch is required to configure prepared stock or direct stock deduction.',
};

export const emptyMenuCategoryForm = (): MenuCategoryFormState => ({
  name: '',
  displayOrder: '0',
});

export const emptyMenuItemForm = (): MenuItemFormState => ({
  menuCategoryId: '',
  name: '',
  description: '',
  sku: '',
  basePrice: '',
  taxRate: '0',
  isVegetarian: false,
  isAvailableForEatIn: true,
  isAvailableForParcel: true,
  inventoryDeductionMode: 'RecipeOnServe',
  stockInventoryItemId: '',
});

export const trimOptionalText = (value: string) => {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
};

const parseInteger = (value: string) => {
  const parsed = Number.parseInt(value.trim(), 10);
  return Number.isNaN(parsed) ? null : parsed;
};

const parseDecimal = (value: string) => {
  const parsed = Number.parseFloat(value.trim());
  return Number.isNaN(parsed) ? null : parsed;
};

export const buildMenuCategoryFormErrors = (form: MenuCategoryFormState): MenuCategoryFormErrors => {
  const errors: MenuCategoryFormErrors = {};

  if (!form.name.trim()) {
    errors.name = 'Category name is required.';
  }

  if (form.displayOrder.trim()) {
    const parsed = parseInteger(form.displayOrder);
    if (parsed === null || parsed < 0) {
      errors.displayOrder = 'Display order must be a whole number at 0 or above.';
    }
  }

  return errors;
};

export const buildCreateMenuCategoryRequest = (form: MenuCategoryFormState): CreateMenuCategoryRequest => ({
  name: form.name.trim(),
  displayOrder: parseInteger(form.displayOrder) ?? 0,
});

export const buildUpdateMenuCategoryRequest = (form: MenuCategoryFormState): UpdateMenuCategoryRequest => ({
  name: form.name.trim(),
  displayOrder: parseInteger(form.displayOrder) ?? 0,
});

export const buildMenuItemFormErrors = (
  form: MenuItemFormState,
  branchId?: string | null,
  messages: MenuItemFormMessages = defaultMenuItemFormMessages
): MenuItemFormErrors => {
  const errors: MenuItemFormErrors = {};

  if (!form.menuCategoryId.trim()) {
    errors.menuCategoryId = 'Category is required.';
  }

  if (!form.name.trim()) {
    errors.name = 'Item name is required.';
  }

  const parsedBasePrice = parseDecimal(form.basePrice);
  if (form.basePrice.trim().length === 0 || parsedBasePrice === null || parsedBasePrice < 0) {
    errors.basePrice = 'Base price must be zero or greater.';
  }

  const parsedTaxRate = parseDecimal(form.taxRate);
  if (form.taxRate.trim().length === 0 || parsedTaxRate === null || parsedTaxRate < 0 || parsedTaxRate > 100) {
    errors.taxRate = 'Tax rate must be between 0 and 100.';
  }

  if (!form.isAvailableForEatIn && !form.isAvailableForParcel) {
    errors.availability = 'Select at least one availability option.';
  }

  if (!form.inventoryDeductionMode) {
    errors.inventoryDeductionMode = messages.inventoryDeductionModeRequired;
  }

  if (form.inventoryDeductionMode === 'BatchPrepared' || form.inventoryDeductionMode === 'DirectStockItem') {
    if (!branchId) {
      errors.stockInventoryItemId = messages.stockInventoryItemBranchRequired;
    } else if (!form.stockInventoryItemId.trim()) {
      errors.stockInventoryItemId = messages.stockInventoryItemRequired;
    }
  }

  return errors;
};

const buildMenuItemRequest = (form: MenuItemFormState): CreateMenuItemRequest => ({
  menuCategoryId: form.menuCategoryId.trim(),
  name: form.name.trim(),
  description: trimOptionalText(form.description),
  sku: trimOptionalText(form.sku),
  basePrice: parseDecimal(form.basePrice) ?? 0,
  taxRate: parseDecimal(form.taxRate) ?? 0,
  isVegetarian: form.isVegetarian,
  isAvailableForEatIn: form.isAvailableForEatIn,
  isAvailableForParcel: form.isAvailableForParcel,
  inventoryDeductionMode: form.inventoryDeductionMode,
  stockInventoryItemId: form.stockInventoryItemId.trim() || null,
});

export const buildCreateMenuItemRequest = (form: MenuItemFormState): CreateMenuItemRequest =>
  buildMenuItemRequest(form);

export const buildUpdateMenuItemRequest = (form: MenuItemFormState): UpdateMenuItemRequest =>
  buildMenuItemRequest(form);
