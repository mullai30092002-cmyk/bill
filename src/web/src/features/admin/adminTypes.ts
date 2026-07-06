export interface AdminRoleListItem {
  roleId: string;
  restaurantId: string | null;
  name: string;
  description: string | null;
  isSystemRole: boolean;
  isAssignable: boolean;
  assignmentBlockedReason: string | null;
  permissionCodes: string[];
}

export interface AdminRoleListResponse {
  items: AdminRoleListItem[];
}

export type AdminBranchStatus = 'Active' | 'Inactive';

export interface AdminBranchListItem {
  branchId: string;
  restaurantId: string;
  name: string;
  address: string | null;
  phone: string | null;
  timezone: string;
  currency: string;
  status: AdminBranchStatus;
  createdAt?: string;
  updatedAt?: string | null;
}

export interface AdminBranchDetail extends AdminBranchListItem {
  createdAt: string;
  updatedAt: string | null;
}

export interface AdminBranchListResponse {
  items: AdminBranchListItem[];
}

export interface CreateAdminBranchRequest {
  name: string;
  address?: string | null;
  phone?: string | null;
  timezone: string;
  currency: string;
}

export interface UpdateAdminBranchRequest {
  name: string;
  address?: string | null;
  phone?: string | null;
  timezone: string;
  currency: string;
}

export type AdminUserStatus = 'Active' | 'Inactive' | 'Locked';

export interface AdminUserListItem {
  userId: string;
  restaurantId: string;
  branchId: string | null;
  fullName: string;
  mobileNumber: string;
  email: string | null;
  status: string;
  roleNames: string[];
}

export interface AdminUserDetail extends AdminUserListItem {
  createdAt: string;
  updatedAt: string | null;
}

export interface AdminUserListResponse {
  items: AdminUserListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface CreateAdminUserRequest {
  branchId: string | null;
  fullName: string;
  mobileNumber: string;
  email: string | null;
  initialPassword: string;
  roleNames: string[];
}

export interface UpdateAdminUserRequest {
  branchId: string | null;
  fullName: string;
  mobileNumber: string;
  email: string | null;
  status: AdminUserStatus;
}

export interface UpdateAdminUserRolesRequest {
  roleNames: string[];
}

export interface ResetAdminUserPasswordRequest {
  newPassword: string;
  confirmPassword?: string | null;
}

export interface ResetAdminUserPasswordResponse {
  userId: string;
  message: string;
}

export type MenuCategoryStatus = 'Active' | 'Inactive';

export interface MenuCategory {
  menuCategoryId: string;
  restaurantId: string;
  name: string;
  displayOrder: number;
  status: MenuCategoryStatus;
  createdAt: string;
  updatedAt: string | null;
}

export interface MenuCategoryListResponse {
  items: MenuCategory[];
}

export interface CreateMenuCategoryRequest {
  name: string;
  displayOrder: number;
}

export interface UpdateMenuCategoryRequest {
  name: string;
  displayOrder: number;
}

export type MenuItemStatus = 'Active' | 'Inactive';

export type MenuItemInventoryDeductionMode = 'RecipeOnServe' | 'BatchPrepared' | 'DirectStockItem' | 'NoDeduction';

export interface MenuItem {
  menuItemId: string;
  restaurantId: string;
  menuCategoryId: string;
  categoryName: string;
  name: string;
  description: string | null;
  sku: string | null;
  basePrice: number;
  taxRate: number;
  isVegetarian: boolean;
  isAvailableForEatIn: boolean;
  isAvailableForParcel: boolean;
  inventoryDeductionMode: MenuItemInventoryDeductionMode;
  stockInventoryItemId: string | null;
  stockInventoryItemName: string | null;
  status: MenuItemStatus;
  createdAt: string;
  updatedAt: string | null;
}

export interface MenuItemListResponse {
  items: MenuItem[];
}

export interface MenuItemListQuery {
  menuCategoryId?: string;
  status?: MenuItemStatus | string;
  search?: string;
  availability?: 'All' | 'EatIn' | 'Parcel' | string;
}

export interface CreateMenuItemRequest {
  menuCategoryId: string;
  name: string;
  description?: string | null;
  sku?: string | null;
  basePrice: number;
  taxRate: number;
  isVegetarian: boolean;
  isAvailableForEatIn: boolean;
  isAvailableForParcel: boolean;
  inventoryDeductionMode?: MenuItemInventoryDeductionMode | null;
  stockInventoryItemId?: string | null;
}

export interface UpdateMenuItemRequest {
  menuCategoryId: string;
  name: string;
  description?: string | null;
  sku?: string | null;
  basePrice: number;
  taxRate: number;
  isVegetarian: boolean;
  isAvailableForEatIn: boolean;
  isAvailableForParcel: boolean;
  inventoryDeductionMode?: MenuItemInventoryDeductionMode | null;
  stockInventoryItemId?: string | null;
}

export interface MenuItemRecipeIngredientRequest {
  inventoryItemId: string;
  quantityRequired: number;
}

export interface UpdateMenuItemRecipeRequest {
  ingredients: MenuItemRecipeIngredientRequest[];
}

export interface MenuItemRecipeIngredientDetail {
  menuItemRecipeIngredientId: string;
  menuItemId: string;
  inventoryItemId: string;
  inventoryItemName: string;
  quantityRequired: number;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface MenuItemRecipeResponse {
  menuItemId: string;
  menuItemName: string;
  branchId: string;
  branchName: string | null;
  ingredients: MenuItemRecipeIngredientDetail[];
}

export interface MenuItemPriceHistory {
  menuItemPriceHistoryId: string;
  menuItemId: string;
  oldPrice: number;
  newPrice: number;
  changedByUserId: string | null;
  changedAt: string;
  reason: string | null;
}

export interface MenuItemPriceHistoryResponse {
  items: MenuItemPriceHistory[];
}

export interface MenuImportPreviewRequest {
  csvText: string;
  importName?: string | null;
}

export interface MenuImportRowDecision {
  rowNumber: number;
  action: 'Import' | 'Update' | 'Skip' | string;
}

export interface MenuImportConfirmRequest {
  csvText: string;
  importName?: string | null;
  decisions?: MenuImportRowDecision[];
}

export interface MenuImportPreviewRow {
  rowNumber: number;
  category: string;
  itemName: string;
  description: string | null;
  eatInPrice: number | null;
  available: boolean | null;
  branchName: string | null;
  status: string;
  message: string;
  errors: string[];
  warnings: string[];
  isDuplicate: boolean;
  existingCategoryName: string | null;
  existingMenuItemId: string | null;
  suggestedAction: string;
}

export interface MenuImportSummary {
  totalRows: number;
  readyRows: number;
  duplicateRows: number;
  invalidRows: number;
  importedRows: number;
  updatedRows: number;
  skippedRows: number;
  failedRows: number;
}

export interface MenuImportResponse {
  importName: string | null;
  summary: MenuImportSummary;
  rows: MenuImportPreviewRow[];
}

export const privilegedAdminRoleNames = [
  'SuperAdmin',
  'RestaurantOwner',
  'Admin',
  'AccountsUser',
  'InventoryUser',
] as const;
