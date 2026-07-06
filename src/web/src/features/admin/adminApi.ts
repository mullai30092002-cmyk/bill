import { requestJson } from '../../api/apiClient';
import type {
  AdminBranchDetail,
  AdminBranchListResponse,
  AdminRoleListResponse,
  CreateAdminBranchRequest,
  AdminUserDetail,
  AdminUserListResponse,
  CreateAdminUserRequest,
  CreateMenuCategoryRequest,
  CreateMenuItemRequest,
  MenuCategory,
  MenuCategoryListResponse,
  MenuItem,
  MenuItemListQuery,
  MenuItemListResponse,
  MenuItemPriceHistoryResponse,
  MenuImportConfirmRequest,
  MenuImportPreviewRequest,
  MenuImportResponse,
  MenuItemRecipeResponse,
  UpdateMenuItemRecipeRequest,
  UpdateAdminBranchRequest,
  UpdateAdminUserRequest,
  UpdateAdminUserRolesRequest,
  UpdateMenuCategoryRequest,
  UpdateMenuItemRequest,
  ResetAdminUserPasswordRequest,
  ResetAdminUserPasswordResponse,
} from './adminTypes';

const buildQueryString = (query?: Record<string, string | number | boolean | null | undefined>) => {
  if (!query) {
    return '';
  }

  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (value === undefined || value === null || value === '') {
      continue;
    }

    params.set(key, String(value));
  }

  const queryString = params.toString();
  return queryString ? `?${queryString}` : '';
};

export const listAdminUsers = () =>
  requestJson<AdminUserListResponse>('/api/v1/admin/users');

export const listAdminRoles = () =>
  requestJson<AdminRoleListResponse>('/api/v1/admin/roles');

export const listAdminBranches = () =>
  requestJson<AdminBranchListResponse>('/api/v1/admin/branches');

export const listMenuCategories = () =>
  requestJson<MenuCategoryListResponse>('/api/v1/admin/menu/categories');

export const getMenuCategory = (categoryId: string) =>
  requestJson<MenuCategory>(`/api/v1/admin/menu/categories/${categoryId}`);

export const createMenuCategory = (request: CreateMenuCategoryRequest) =>
  requestJson<MenuCategory>('/api/v1/admin/menu/categories', {
    method: 'POST',
    body: request,
  });

export const updateMenuCategory = (categoryId: string, request: UpdateMenuCategoryRequest) =>
  requestJson<MenuCategory>(`/api/v1/admin/menu/categories/${categoryId}`, {
    method: 'PUT',
    body: request,
  });

export const activateMenuCategory = (categoryId: string) =>
  requestJson<MenuCategory>(`/api/v1/admin/menu/categories/${categoryId}/activate`, {
    method: 'POST',
  });

export const deactivateMenuCategory = (categoryId: string) =>
  requestJson<MenuCategory>(`/api/v1/admin/menu/categories/${categoryId}/deactivate`, {
    method: 'POST',
  });

export const listMenuItems = (query?: MenuItemListQuery) =>
  requestJson<MenuItemListResponse>(
    `/api/v1/admin/menu/items${buildQueryString({
      menuCategoryId: query?.menuCategoryId,
      status: query?.status,
      search: query?.search,
      availability: query?.availability,
    })}`
  );

export const getMenuItem = (itemId: string) =>
  requestJson<MenuItem>(`/api/v1/admin/menu/items/${itemId}`);

export const getMenuItemRecipe = (itemId: string) =>
  requestJson<MenuItemRecipeResponse>(`/api/v1/admin/menu/items/${itemId}/recipe`);

export const createMenuItem = (request: CreateMenuItemRequest) =>
  requestJson<MenuItem>('/api/v1/admin/menu/items', {
    method: 'POST',
    body: request,
  });

export const updateMenuItem = (itemId: string, request: UpdateMenuItemRequest) =>
  requestJson<MenuItem>(`/api/v1/admin/menu/items/${itemId}`, {
    method: 'PUT',
    body: request,
  });

export const activateMenuItem = (itemId: string) =>
  requestJson<MenuItem>(`/api/v1/admin/menu/items/${itemId}/activate`, {
    method: 'POST',
  });

export const deactivateMenuItem = (itemId: string) =>
  requestJson<MenuItem>(`/api/v1/admin/menu/items/${itemId}/deactivate`, {
    method: 'POST',
  });

export const updateMenuItemRecipe = (itemId: string, request: UpdateMenuItemRecipeRequest) =>
  requestJson<MenuItemRecipeResponse>(`/api/v1/admin/menu/items/${itemId}/recipe`, {
    method: 'PUT',
    body: request,
  });

export const getMenuItemPriceHistory = (itemId: string) =>
  requestJson<MenuItemPriceHistoryResponse>(`/api/v1/admin/menu/items/${itemId}/price-history`);

export const previewMenuImport = (request: MenuImportPreviewRequest) =>
  requestJson<MenuImportResponse>('/api/v1/admin/menu/import/preview', {
    method: 'POST',
    body: request,
  });

export const confirmMenuImport = (request: MenuImportConfirmRequest) =>
  requestJson<MenuImportResponse>('/api/v1/admin/menu/import/confirm', {
    method: 'POST',
    body: request,
  });

export const getAdminBranch = (branchId: string) =>
  requestJson<AdminBranchDetail>(`/api/v1/admin/branches/${branchId}`);

export const getAdminUser = (userId: string) =>
  requestJson<AdminUserDetail>(`/api/v1/admin/users/${userId}`);

export const createAdminUser = (request: CreateAdminUserRequest) =>
  requestJson<AdminUserDetail>('/api/v1/admin/users', {
    method: 'POST',
    body: request,
  });

export const createAdminBranch = (request: CreateAdminBranchRequest) =>
  requestJson<AdminBranchDetail>('/api/v1/admin/branches', {
    method: 'POST',
    body: request,
  });

export const updateAdminUser = (userId: string, request: UpdateAdminUserRequest) =>
  requestJson<AdminUserDetail>(`/api/v1/admin/users/${userId}`, {
    method: 'PUT',
    body: request,
  });

export const updateAdminBranch = (branchId: string, request: UpdateAdminBranchRequest) =>
  requestJson<AdminBranchDetail>(`/api/v1/admin/branches/${branchId}`, {
    method: 'PUT',
    body: request,
  });

export const updateAdminUserRoles = (userId: string, request: UpdateAdminUserRolesRequest) =>
  requestJson<AdminUserDetail>(`/api/v1/admin/users/${userId}/roles`, {
    method: 'PUT',
    body: request,
  });

export const activateAdminUser = (userId: string) =>
  requestJson<AdminUserDetail>(`/api/v1/admin/users/${userId}/activate`, {
    method: 'POST',
  });

export const activateAdminBranch = (branchId: string) =>
  requestJson<AdminBranchDetail>(`/api/v1/admin/branches/${branchId}/activate`, {
    method: 'POST',
  });

export const deactivateAdminUser = (userId: string) =>
  requestJson<AdminUserDetail>(`/api/v1/admin/users/${userId}/deactivate`, {
    method: 'POST',
  });

export const resetAdminUserPassword = (userId: string, request: ResetAdminUserPasswordRequest) =>
  requestJson<ResetAdminUserPasswordResponse>(`/api/v1/admin/users/${userId}/reset-password`, {
    method: 'POST',
    body: request,
  });

export const deactivateAdminBranch = (branchId: string) =>
  requestJson<AdminBranchDetail>(`/api/v1/admin/branches/${branchId}/deactivate`, {
    method: 'POST',
  });
