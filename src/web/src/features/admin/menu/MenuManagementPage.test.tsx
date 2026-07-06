import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';

import App from '../../../App';
import { clearAuthSession, createJsonResponse, storeAuthSession } from '../../../test/authTestUtils';
import { renderWithRouter } from '../../../test/renderWithRouter';
import type { AuthSession } from '../../auth/authTypes';
import type { MenuImportResponse } from '../adminTypes';

const categoriesPath = '/api/v1/admin/menu/categories';
const categoryPath = (categoryId: string) => `/api/v1/admin/menu/categories/${categoryId}`;
const categoryActivatePath = (categoryId: string) => `/api/v1/admin/menu/categories/${categoryId}/activate`;
const categoryDeactivatePath = (categoryId: string) => `/api/v1/admin/menu/categories/${categoryId}/deactivate`;
const itemsPath = '/api/v1/admin/menu/items';
const itemPath = (itemId: string) => `/api/v1/admin/menu/items/${itemId}`;
const itemRecipePath = (itemId: string) => `/api/v1/admin/menu/items/${itemId}/recipe`;
const itemActivatePath = (itemId: string) => `/api/v1/admin/menu/items/${itemId}/activate`;
const itemDeactivatePath = (itemId: string) => `/api/v1/admin/menu/items/${itemId}/deactivate`;
const priceHistoryPath = (itemId: string) => `/api/v1/admin/menu/items/${itemId}/price-history`;
const inventoryItemsPath = '/api/v1/inventory/items';
const importPreviewPath = '/api/v1/admin/menu/import/preview';
const importConfirmPath = '/api/v1/admin/menu/import/confirm';

const timestamp = '2026-06-11T09:00:00Z';
const laterTimestamp = '2026-06-11T10:00:00Z';

const defaultMenuItemFields = {
  inventoryDeductionMode: 'RecipeOnServe',
  stockInventoryItemId: null,
  stockInventoryItemName: null,
};

const categoryListResponse = (items: Array<Record<string, unknown>>) =>
  createJsonResponse({
    items,
  });

const categoryDetailResponse = (item: Record<string, unknown>) =>
  createJsonResponse({
    ...item,
    createdAt: timestamp,
    updatedAt: laterTimestamp,
  });

const itemListResponse = (items: Array<Record<string, unknown>>) =>
  createJsonResponse({
    items: items.map(item => ({
      ...defaultMenuItemFields,
      ...item,
    })),
  });

const itemDetailResponse = (item: Record<string, unknown>) =>
  createJsonResponse({
    ...defaultMenuItemFields,
    ...item,
    createdAt: timestamp,
    updatedAt: laterTimestamp,
  });

const priceHistoryResponse = (items: Array<Record<string, unknown>>) =>
  createJsonResponse({
    items,
  });

const problemJsonResponse = (body: Record<string, unknown>, status = 400) =>
  new Response(JSON.stringify(body), {
    status,
    headers: {
      'Content-Type': 'application/problem+json',
    },
  });

const inventoryListResponse = (items: Array<Record<string, unknown>>) =>
  createJsonResponse({
    items,
  });

type MenuImportResponseFixture = {
  importName?: string;
  summary?: Partial<MenuImportResponse['summary']>;
  rows?: MenuImportResponse['rows'];
};

const importResponse = (payload: MenuImportResponseFixture) =>
  createJsonResponse({
    importName: payload.importName ?? 'Pasted CSV',
    summary: {
      totalRows: payload.summary?.totalRows ?? 1,
      readyRows: payload.summary?.readyRows ?? 1,
      duplicateRows: payload.summary?.duplicateRows ?? 0,
      invalidRows: payload.summary?.invalidRows ?? 0,
      importedRows: payload.summary?.importedRows ?? 0,
      updatedRows: payload.summary?.updatedRows ?? 0,
      skippedRows: payload.summary?.skippedRows ?? 0,
      failedRows: payload.summary?.failedRows ?? 0,
    },
    rows: payload.rows ?? [],
  });

const defaultCategoryList = () =>
  categoryListResponse([
    {
      menuCategoryId: 'category-1',
      restaurantId: 'restaurant-1',
      name: 'Breakfast',
      displayOrder: 1,
      status: 'Active',
      createdAt: timestamp,
      updatedAt: laterTimestamp,
    },
    {
      menuCategoryId: 'category-2',
      restaurantId: 'restaurant-1',
      name: 'Snacks',
      displayOrder: 2,
      status: 'Inactive',
      createdAt: timestamp,
      updatedAt: laterTimestamp,
    },
  ]);

const defaultItemList = () =>
  itemListResponse([
    {
      menuItemId: 'item-1',
      restaurantId: 'restaurant-1',
      menuCategoryId: 'category-1',
      categoryName: 'Breakfast',
      name: 'Masala Dosa',
      description: 'Crisp rice crepe',
      sku: 'DOSA-01',
      basePrice: 2.5,
      taxRate: 0,
      isVegetarian: false,
      isAvailableForEatIn: true,
      isAvailableForParcel: true,
      status: 'Active',
      createdAt: timestamp,
      updatedAt: laterTimestamp,
    },
    {
      menuItemId: 'item-2',
      restaurantId: 'restaurant-1',
      menuCategoryId: 'category-2',
      categoryName: 'Snacks',
      name: 'Vada',
      description: null,
      sku: 'VADA-01',
      basePrice: 1.75,
      taxRate: 0,
      isVegetarian: false,
      isAvailableForEatIn: true,
      isAvailableForParcel: false,
      status: 'Inactive',
      createdAt: timestamp,
      updatedAt: laterTimestamp,
    },
  ]);

const setupFetch = (responses: Record<string, Response[]>) => {
  const fetchMock = vi.fn(async (input, init) => {
    const method = (init?.method ?? 'GET').toUpperCase();
    const path = new URL(String(input)).pathname;
    const key = `${method} ${path}`;
    const queue = responses[key];

    if (!queue || queue.length === 0) {
      throw new Error(`Unhandled request: ${key}`);
    }

    return queue.shift()!;
  });

  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
};

const mockScrollIntoView = () => {
  const scrollIntoViewMock = vi.fn();
  Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', {
    configurable: true,
    writable: true,
    value: scrollIntoViewMock,
  });
  return scrollIntoViewMock;
};

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

const getJsonBody = (call: [RequestInfo | URL, RequestInit?]) =>
  call[1]?.body ? JSON.parse(String(call[1].body)) : undefined;

const waitForRequest = (fetchMock: ReturnType<typeof vi.fn>, method: string, path: string) =>
  waitFor(() => {
    expect(
      fetchMock.mock.calls.some(call => {
        const nextMethod = (call[1]?.method ?? 'GET').toUpperCase();
        const nextPath = new URL(String(call[0])).pathname;
        return nextMethod === method && nextPath === path;
      })
    ).toBe(true);
  }, { timeout: 30000 });

const renderMenuPage = (
  responses: Record<string, Response[]> = {},
  authOverrides: Partial<AuthSession> = {}
) => {
  clearAuthSession();
  storeAuthSession({
    userId: 'session-user',
    permissions: ['MenuItem.View'],
    roles: ['Admin'],
    activeRole: 'Admin',
    ...authOverrides,
  });

  const fetchMock = setupFetch({
    [`GET ${categoriesPath}`]: [defaultCategoryList(), defaultCategoryList(), defaultCategoryList(), defaultCategoryList()],
    [`GET ${itemsPath}`]: [defaultItemList(), defaultItemList(), defaultItemList(), defaultItemList()],
    [`GET ${inventoryItemsPath}`]: [
      inventoryListResponse([]),
      inventoryListResponse([]),
      inventoryListResponse([]),
      inventoryListResponse([]),
    ],
    ...responses,
  });
  const user = userEvent.setup();

  renderWithRouter(<App />, '/admin/menu');

  return { fetchMock, user };
};

const clickFirstButton = async (user: ReturnType<typeof userEvent.setup>, name: RegExp) => {
  await user.click(screen.getAllByRole('button', { name })[0]);
};

const getItemCategorySelect = () => {
  const createItemForm = screen.getByRole('button', { name: /create item/i }).closest('form');
  if (!createItemForm) {
    throw new Error('Create item form not found');
  }

  return within(createItemForm).getByRole('combobox', { name: /category/i });
};

describe('MenuManagementPage', () => {
  it('renders the route for a menu viewer and shows the Menu nav item', async () => {
    renderMenuPage();

    expect(await screen.findByRole('heading', { name: /menu management/i })).toBeInTheDocument();
    expect(screen.getAllByText('Menu', { selector: '.responsive-nav__link-label' }).length).toBeGreaterThan(0);
  });

  it('shows a not-authorized state without menu permissions and does not call menu APIs', async () => {
    clearAuthSession();
    storeAuthSession({
      permissions: ['Report.View'],
      roles: ['Cashier'],
      activeRole: 'Cashier',
    });

    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);

    renderWithRouter(<App />, '/admin/menu');

    expect(await screen.findByRole('heading', { name: /not authorized/i })).toBeInTheDocument();
    expect(
      screen.getByText(/menu management requires menucategory\.manage, menuitem\.manage, or menuitem\.view/i)
    ).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
    expect(screen.queryByText('Menu', { selector: '.responsive-nav__link-label' })).not.toBeInTheDocument();
  });

  it('loads categories and items from the backend', async () => {
    renderMenuPage();

    expect(await screen.findByRole('heading', { name: /menu management/i })).toBeInTheDocument();
    expect(screen.getAllByText(/breakfast/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/snacks/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/masala dosa/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/vada/i).length).toBeGreaterThan(0);
    expect(screen.queryByText('category-1')).not.toBeInTheDocument();
    expect(screen.queryByText('item-1')).not.toBeInTheDocument();
  });

  it('shows recipe ingredients and saves branch-scoped inventory usage', async () => {
    const { user, fetchMock } = renderMenuPage(
      {
        [`GET ${itemPath('item-1')}`]: [
          itemDetailResponse({
            menuItemId: 'item-1',
            restaurantId: 'restaurant-1',
            menuCategoryId: 'category-1',
            categoryName: 'Breakfast',
            name: 'Masala Dosa',
            description: 'Crisp rice crepe',
            sku: 'DOSA-01',
            basePrice: 2.5,
            taxRate: 0,
            isVegetarian: true,
            isAvailableForEatIn: true,
            isAvailableForParcel: true,
            status: 'Active',
          }),
        ],
        [`GET ${itemRecipePath('item-1')}`]: [
          createJsonResponse({
            menuItemId: 'item-1',
            menuItemName: 'Masala Dosa',
            branchId: 'branch-1',
            branchName: 'Main Branch',
            ingredients: [
              {
                menuItemRecipeIngredientId: 'recipe-1',
                menuItemId: 'item-1',
                inventoryItemId: 'inventory-1',
                inventoryItemName: 'Rice Flour',
                quantityRequired: 1.5,
                createdAtUtc: timestamp,
                updatedAtUtc: laterTimestamp,
              },
            ],
          }),
        ],
        [`PUT ${itemRecipePath('item-1')}`]: [
          createJsonResponse({
            menuItemId: 'item-1',
            menuItemName: 'Masala Dosa',
            branchId: 'branch-1',
            branchName: 'Main Branch',
            ingredients: [
              {
                menuItemRecipeIngredientId: 'recipe-1',
                menuItemId: 'item-1',
                inventoryItemId: 'inventory-1',
                inventoryItemName: 'Rice Flour',
                quantityRequired: 2,
                createdAtUtc: timestamp,
                updatedAtUtc: laterTimestamp,
              },
            ],
          }),
        ],
        [`GET ${inventoryItemsPath}`]: [
          inventoryListResponse([
            {
              inventoryItemId: 'inventory-1',
              restaurantId: 'restaurant-1',
              branchId: 'branch-1',
              name: 'Rice Flour',
              normalizedName: 'RICE FLOUR',
              category: 'Grains',
              unitOfMeasure: 'kg',
              lowStockThreshold: 5,
              isActive: true,
              currentStock: 12,
              status: 'Healthy',
              createdAtUtc: timestamp,
              updatedAtUtc: laterTimestamp,
            },
            {
              inventoryItemId: 'inventory-2',
              restaurantId: 'restaurant-1',
              branchId: 'branch-1',
              name: 'Oil',
              normalizedName: 'OIL',
              category: 'Cooking',
              unitOfMeasure: 'l',
              lowStockThreshold: 3,
              isActive: true,
              currentStock: 8,
              status: 'Healthy',
              createdAtUtc: timestamp,
              updatedAtUtc: laterTimestamp,
            },
          ]),
        ],
      },
      {
        permissions: ['MenuItem.Manage'],
        roles: ['Admin'],
        activeRole: 'Admin',
        branchId: 'branch-1',
      }
    );

    expect(await screen.findByRole('heading', { name: /menu management/i })).toBeInTheDocument();
    await clickFirstButton(user, /^edit$/i);
    await screen.findByDisplayValue('Masala Dosa');

    expect(await screen.findByText(/^recipe ingredients$/i)).toBeInTheDocument();
    expect(screen.getByRole('cell', { name: /^rice flour$/i })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: /rice flour \(grains\)/i })).toBeInTheDocument();
    expect(screen.queryByText(/vendor bill/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/\bocr\b/i)).not.toBeInTheDocument();

    const recipeQuantityInput = screen.getByLabelText(/quantity required/i);
    await user.clear(recipeQuantityInput);
    await user.type(recipeQuantityInput, ' 2 ');
    await user.click(screen.getByRole('button', { name: /save recipe ingredients/i }));

    await waitFor(() => {
      expect(screen.getByText(/saved recipe ingredients for masala dosa/i)).toBeInTheDocument();
    });

    const branchInventoryCall = fetchMock.mock.calls.find(call => {
      const method = (call[1]?.method ?? 'GET').toUpperCase();
      const url = new URL(String(call[0]));
      return method === 'GET' && url.pathname === inventoryItemsPath;
    });

    expect(new URL(String(branchInventoryCall?.[0])).searchParams.get('branchId')).toBe('branch-1');

    const saveCall = fetchMock.mock.calls.find(call => {
      const method = (call[1]?.method ?? 'GET').toUpperCase();
      const path = new URL(String(call[0])).pathname;
      return method === 'PUT' && path === itemRecipePath('item-1');
    });

    expect(getJsonBody(saveCall as [RequestInfo | URL, RequestInit?])).toEqual({
      ingredients: [
        {
          inventoryItemId: 'inventory-1',
          quantityRequired: 2,
        },
      ],
    });
  }, 20000);

  it('validates recipe ingredients before saving', async () => {
    const { user, fetchMock } = renderMenuPage(
      {
        [`GET ${itemPath('item-1')}`]: [
          itemDetailResponse({
            menuItemId: 'item-1',
            restaurantId: 'restaurant-1',
            menuCategoryId: 'category-1',
            categoryName: 'Breakfast',
            name: 'Masala Dosa',
            description: 'Crisp rice crepe',
            sku: 'DOSA-01',
            basePrice: 2.5,
            taxRate: 0,
            isVegetarian: true,
            isAvailableForEatIn: true,
            isAvailableForParcel: true,
            status: 'Active',
          }),
        ],
        [`GET ${itemRecipePath('item-1')}`]: [
          createJsonResponse({
            menuItemId: 'item-1',
            menuItemName: 'Masala Dosa',
            branchId: 'branch-1',
            branchName: 'Main Branch',
            ingredients: [],
          }),
        ],
        [`GET ${inventoryItemsPath}`]: [
          inventoryListResponse([
            {
              inventoryItemId: 'inventory-1',
              restaurantId: 'restaurant-1',
              branchId: 'branch-1',
              name: 'Rice Flour',
              normalizedName: 'RICE FLOUR',
              category: 'Grains',
              unitOfMeasure: 'kg',
              lowStockThreshold: 5,
              isActive: true,
              currentStock: 12,
              status: 'Healthy',
              createdAtUtc: timestamp,
              updatedAtUtc: laterTimestamp,
            },
          ]),
        ],
      },
      {
        permissions: ['MenuItem.Manage'],
        roles: ['Admin'],
        activeRole: 'Admin',
        branchId: 'branch-1',
      }
    );

    expect(await screen.findByRole('heading', { name: /menu management/i })).toBeInTheDocument();
    await clickFirstButton(user, /^edit$/i);
    await screen.findByDisplayValue('Masala Dosa');
    await user.click(screen.getByRole('button', { name: /save recipe ingredients/i }));

    await waitFor(() => {
      expect(screen.getByText(/please fix the recipe ingredients before saving/i)).toBeInTheDocument();
    });

    expect(screen.getByText(/inventory item is required/i)).toBeInTheDocument();
    expect(screen.getByText(/quantity required must be greater than zero/i)).toBeInTheDocument();
    expect(
      fetchMock.mock.calls.some(call => {
        const method = (call[1]?.method ?? 'GET').toUpperCase();
        const path = new URL(String(call[0])).pathname;
        return method === 'PUT' && path === itemRecipePath('item-1');
      })
    ).toBe(false);
  });

  it('shows read-only data for MenuItem.View without create, edit, or status buttons', async () => {
    renderMenuPage(undefined, {
      permissions: ['MenuItem.View'],
      roles: ['Cashier'],
      activeRole: 'Cashier',
    });

    expect(await screen.findByRole('heading', { name: /menu management/i })).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: /view details/i }).length).toBeGreaterThan(0);
    expect(screen.queryByRole('button', { name: /create category/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /create item/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^edit$/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /deactivate category/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /activate category/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /deactivate item/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /activate item/i })).not.toBeInTheDocument();
  });

  it('shows an import menu action for menu managers', async () => {
    renderMenuPage(undefined, {
      permissions: ['MenuItem.Manage'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });

    expect(await screen.findByRole('heading', { name: /menu management/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /import menu/i })).toBeInTheDocument();
  });

  it('scrolls and focuses the import panel when clicking Import Menu', async () => {
    const scrollIntoViewMock = mockScrollIntoView();
    const { user } = renderMenuPage(undefined, {
      permissions: ['MenuItem.Manage'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });

    expect(await screen.findByRole('heading', { name: /menu management/i })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /import menu/i }));

    await waitFor(() => {
      expect(scrollIntoViewMock).toHaveBeenCalled();
    });

    expect(screen.getByLabelText(/menu csv/i)).toHaveFocus();
  });

  it('previews imported rows and formats money using the restaurant currency', async () => {
    const { user } = renderMenuPage({
      [`POST ${importPreviewPath}`]: [
        importResponse({
          summary: {
            totalRows: 1,
            readyRows: 1,
            duplicateRows: 0,
            invalidRows: 0,
          },
          rows: [
            {
              rowNumber: 2,
              category: 'Breakfast',
              itemName: 'Idli',
              description: 'Steamed rice cakes',
              eatInPrice: 2.5,
              available: true,
              branchName: 'Main Branch',
              status: 'Ready',
              message: 'Ready for import.',
              errors: [],
              warnings: [],
              isDuplicate: false,
              existingCategoryName: null,
              existingMenuItemId: null,
              suggestedAction: 'Import',
            },
          ],
        }),
      ],
    }, {
      permissions: ['MenuItem.Manage'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });

    expect(await screen.findByRole('heading', { name: /menu management/i })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /import menu/i }));
    expect(screen.getByDisplayValue(/Category,ItemName,Description,EatInPrice,Available,BranchName/i)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /preview csv/i }));

    await waitFor(() => {
      expect(screen.getByText(/idli/i, { selector: 'td' })).toBeInTheDocument();
    });

    expect(screen.getAllByText('₹2.50', { selector: 'td' }).length).toBeGreaterThan(0);
    expect(screen.getByText(/main branch/i, { selector: 'td' })).toBeInTheDocument();
    expect(screen.queryByText(/parcel price/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/kitchen station/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/sort order/i)).not.toBeInTheDocument();
    expect(screen.getByText(/ready for import/i, { selector: 'td' })).toBeInTheDocument();
  });

  it('shows duplicate warnings and disables confirm when blocking errors exist', async () => {
    const { user } = renderMenuPage({
      [`POST ${importPreviewPath}`]: [
        importResponse({
          summary: {
            totalRows: 2,
            readyRows: 1,
            duplicateRows: 1,
            invalidRows: 1,
          },
          rows: [
            {
              rowNumber: 2,
              category: 'Breakfast',
              itemName: 'Idli',
              description: null,
              eatInPrice: 2.5,
              available: true,
              branchName: null,
              status: 'Duplicate',
              message: 'Existing menu item found. Choose Skip or Update.',
              errors: [],
              warnings: ['Duplicate row'],
              isDuplicate: true,
              existingCategoryName: 'Breakfast',
              existingMenuItemId: 'item-1',
              suggestedAction: 'Skip',
            },
            {
              rowNumber: 3,
              category: 'Breakfast',
              itemName: '',
              description: null,
              eatInPrice: null,
              available: null,
              branchName: null,
              status: 'Invalid',
              message: 'Item name is required.',
              errors: ['Item name is required.'],
              warnings: [],
              isDuplicate: false,
              existingCategoryName: null,
              existingMenuItemId: null,
              suggestedAction: 'Import',
            },
          ],
        }),
      ],
    }, {
      permissions: ['MenuItem.Manage'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });

    expect(await screen.findByRole('heading', { name: /menu management/i })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /import menu/i }));
    await user.click(screen.getByRole('button', { name: /preview csv/i }));

    await waitFor(() => {
    expect(screen.getByText(/existing menu item found/i, { selector: 'td' })).toBeInTheDocument();
    });

    expect(screen.getByText(/item name is required/i, { selector: 'td' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /confirm import/i })).toBeDisabled();
    expect(screen.getAllByRole('combobox', { name: /duplicate action/i }).length).toBeGreaterThan(0);
  });

  it('shows a success summary after confirming a menu import', async () => {
    const { user } = renderMenuPage({
      [`POST ${importPreviewPath}`]: [
        importResponse({
          summary: {
            totalRows: 1,
            readyRows: 1,
            duplicateRows: 0,
            invalidRows: 0,
          },
          rows: [
            {
              rowNumber: 2,
              category: 'Breakfast',
              itemName: 'Idli',
              description: null,
              eatInPrice: 2.5,
              available: true,
              branchName: null,
              status: 'Ready',
              message: 'Ready for import.',
              errors: [],
              warnings: [],
              isDuplicate: false,
              existingCategoryName: null,
              existingMenuItemId: null,
              suggestedAction: 'Import',
            },
          ],
        }),
      ],
      [`POST ${importConfirmPath}`]: [
        importResponse({
          summary: {
            totalRows: 1,
            readyRows: 1,
            duplicateRows: 0,
            invalidRows: 0,
            importedRows: 1,
            updatedRows: 0,
            skippedRows: 0,
            failedRows: 0,
          },
          rows: [
            {
              rowNumber: 2,
              category: 'Breakfast',
              itemName: 'Idli',
              description: null,
              eatInPrice: 2.5,
              available: true,
              branchName: null,
              status: 'Imported',
              message: 'Imported new menu item.',
              errors: [],
              warnings: [],
              isDuplicate: false,
              existingCategoryName: null,
              existingMenuItemId: null,
              suggestedAction: 'Import',
            },
          ],
        }),
      ],
    }, {
      permissions: ['MenuItem.Manage'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });

    expect(await screen.findByRole('heading', { name: /menu management/i })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /import menu/i }));
    await user.click(screen.getByRole('button', { name: /preview csv/i }));
    await user.click(screen.getByRole('button', { name: /confirm import/i }));

    await waitFor(() => {
      expect(screen.getByText(/rows imported in the latest confirmed run/i)).toBeInTheDocument();
    });

    expect(screen.getByText(/imported new menu item/i, { selector: 'td' })).toBeInTheDocument();
  });

  it('creates a category without sending restaurantId', async () => {
    const { user, fetchMock } = renderMenuPage({
      [`POST ${categoriesPath}`]: [
        categoryDetailResponse({
          menuCategoryId: 'category-3',
          restaurantId: 'restaurant-1',
          name: 'Lunch',
          displayOrder: 3,
          status: 'Active',
        }),
      ],
    }, {
      permissions: ['MenuCategory.Manage'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });

    expect(await screen.findByRole('heading', { name: /menu management/i })).toBeInTheDocument();
    await user.type(screen.getByLabelText(/category name/i), '  Lunch  ');
    await user.clear(screen.getByLabelText(/display order/i));
    await user.type(screen.getByLabelText(/display order/i), ' 3 ');
    await user.click(screen.getByRole('button', { name: /create category/i }));

    await waitFor(() => {
      expect(screen.getByText(/created lunch/i)).toBeInTheDocument();
    });

    const createCall = fetchMock.mock.calls.find(call => {
      const method = (call[1]?.method ?? 'GET').toUpperCase();
      const path = new URL(String(call[0])).pathname;
      return method === 'POST' && path === categoriesPath;
    });

    expect(getJsonBody(createCall as [RequestInfo | URL, RequestInit?])).toEqual({
      name: 'Lunch',
      displayOrder: 3,
    });
    expect(
      Object.keys(getJsonBody(createCall as [RequestInfo | URL, RequestInit?]))
    ).not.toContain('restaurantId');
  });

  it('shows a clean duplicate-category error when create fails with RFC7807 JSON', async () => {
    const { user } = renderMenuPage({
      [`POST ${categoriesPath}`]: [
        problemJsonResponse(
          {
            type: 'https://datatracker.ietf.org/doc/html/rfc7807',
            title: 'Bad Request',
            status: 400,
            detail: 'Category name already exists in this restaurant.',
          },
          400
        ),
      ],
    }, {
      permissions: ['MenuCategory.Manage'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });

    expect(await screen.findByRole('heading', { name: /menu management/i })).toBeInTheDocument();
    await user.type(screen.getByLabelText(/category name/i), '  Breakfast  ');
    await user.clear(screen.getByLabelText(/display order/i));
    await user.type(screen.getByLabelText(/display order/i), ' 1 ');
    await user.click(screen.getByRole('button', { name: /create category/i }));

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent(
        'Category name already exists in this restaurant.'
      );
    });

    expect(screen.queryByText(/datatracker\.ietf\.org/i)).not.toBeInTheDocument();
  });

  it('scrolls and focuses the category form when clicking New Category', async () => {
    const scrollIntoViewMock = mockScrollIntoView();
    const { user } = renderMenuPage(undefined, {
      permissions: ['MenuCategory.Manage'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });

    expect(await screen.findByRole('heading', { name: /menu management/i })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /new category/i }));

    await waitFor(() => {
      expect(scrollIntoViewMock).toHaveBeenCalled();
    });

    expect(screen.getByLabelText(/category name/i)).toHaveFocus();
  });

  it('blocks blank category names before create', async () => {
    const { user, fetchMock } = renderMenuPage(undefined, {
      permissions: ['MenuCategory.Manage'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });

    expect(await screen.findByRole('heading', { name: /menu management/i })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /create category/i }));

    await waitFor(() => {
      expect(screen.getByText(/please fix the category fields before saving/i)).toBeInTheDocument();
    });

    expect(screen.getByText(/category name is required/i)).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it('updates a category without sending restaurantId', async () => {
    const { user, fetchMock } = renderMenuPage({
      [`GET ${categoryPath('category-1')}`]: [
        categoryDetailResponse({
          menuCategoryId: 'category-1',
          restaurantId: 'restaurant-1',
          name: 'Breakfast',
          displayOrder: 1,
          status: 'Active',
        }),
      ],
      [`PUT ${categoryPath('category-1')}`]: [
        categoryDetailResponse({
          menuCategoryId: 'category-1',
          restaurantId: 'restaurant-1',
          name: 'All Day Breakfast',
          displayOrder: 5,
          status: 'Active',
        }),
      ],
    }, {
      permissions: ['MenuCategory.Manage'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });

    expect(await screen.findByRole('heading', { name: /menu management/i })).toBeInTheDocument();
    await clickFirstButton(user, /^edit$/i);
    await screen.findByDisplayValue('Breakfast');

    await user.clear(screen.getByLabelText(/category name/i));
    await user.type(screen.getByLabelText(/category name/i), ' All Day Breakfast ');
    await user.clear(screen.getByLabelText(/display order/i));
    await user.type(screen.getByLabelText(/display order/i), ' 5 ');
    await user.click(screen.getByRole('button', { name: /save changes/i }));

    await waitFor(() => {
      expect(screen.getByText(/saved changes for all day breakfast/i)).toBeInTheDocument();
    });

    const updateCall = fetchMock.mock.calls.find(call => {
      const method = (call[1]?.method ?? 'GET').toUpperCase();
      const path = new URL(String(call[0])).pathname;
      return method === 'PUT' && path === categoryPath('category-1');
    });

    expect(getJsonBody(updateCall as [RequestInfo | URL, RequestInit?])).toEqual({
      name: 'All Day Breakfast',
      displayOrder: 5,
    });
    expect(
      Object.keys(getJsonBody(updateCall as [RequestInfo | URL, RequestInit?]))
    ).not.toContain('restaurantId');
  });

  it('requires confirmation before deactivating a category', async () => {
    const { user, fetchMock } = renderMenuPage({
      [`GET ${categoryPath('category-1')}`]: [
        categoryDetailResponse({
          menuCategoryId: 'category-1',
          restaurantId: 'restaurant-1',
          name: 'Breakfast',
          displayOrder: 1,
          status: 'Active',
        }),
      ],
      [`POST ${categoryDeactivatePath('category-1')}`]: [
        categoryDetailResponse({
          menuCategoryId: 'category-1',
          restaurantId: 'restaurant-1',
          name: 'Breakfast',
          displayOrder: 1,
          status: 'Inactive',
        }),
      ],
    }, {
      permissions: ['MenuCategory.Manage'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });

    expect(await screen.findByRole('heading', { name: /menu management/i })).toBeInTheDocument();
    await clickFirstButton(user, /^edit$/i);
    await screen.findByDisplayValue('Breakfast');

    expect(screen.queryByRole('button', { name: /^confirm deactivate$/i })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /deactivate category/i }));
    expect(screen.getByRole('button', { name: /^confirm deactivate$/i })).toBeInTheDocument();
    expect(
      fetchMock.mock.calls.some(call => {
        const method = (call[1]?.method ?? 'GET').toUpperCase();
        const path = new URL(String(call[0])).pathname;
        return method === 'POST' && path === categoryDeactivatePath('category-1');
      })
    ).toBe(false);

    await user.click(screen.getByRole('button', { name: /^confirm deactivate$/i }));

    await waitFor(() => {
      expect(screen.getByText(/breakfast is now inactive/i)).toBeInTheDocument();
    });
  });

  it('shows the category deactivation guard error safely', async () => {
    const { user } = renderMenuPage({
      [`GET ${categoryPath('category-1')}`]: [
        categoryDetailResponse({
          menuCategoryId: 'category-1',
          restaurantId: 'restaurant-1',
          name: 'Breakfast',
          displayOrder: 1,
          status: 'Active',
        }),
      ],
      [`POST ${categoryDeactivatePath('category-1')}`]: [
        problemJsonResponse(
          {
            type: 'https://datatracker.ietf.org/doc/html/rfc7807',
            title: 'Bad Request',
            status: 400,
            detail: 'Category cannot be deactivated while active menu items exist.',
          },
          400
        ),
      ],
    }, {
      permissions: ['MenuCategory.Manage'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });

    expect(await screen.findByRole('heading', { name: /menu management/i })).toBeInTheDocument();
    await clickFirstButton(user, /^edit$/i);
    await screen.findByDisplayValue('Breakfast');
    await user.click(screen.getByRole('button', { name: /deactivate category/i }));
    await user.click(screen.getByRole('button', { name: /^confirm deactivate$/i }));

    await waitFor(() => {
      expect(
        screen.getByText(/category cannot be deactivated while active menu items exist/i)
      ).toBeInTheDocument();
    });
  });

  it('creates an item without sending restaurantId', async () => {
    const { user, fetchMock } = renderMenuPage({
      [`POST ${itemsPath}`]: [
        itemDetailResponse({
          menuItemId: 'item-3',
          restaurantId: 'restaurant-1',
          menuCategoryId: 'category-1',
          categoryName: 'Breakfast',
          name: 'Pongal',
          description: 'Savory rice and lentil bowl',
          sku: 'PONGAL-01',
          basePrice: 3.25,
          taxRate: 0,
          isVegetarian: true,
          isAvailableForEatIn: true,
          isAvailableForParcel: true,
          status: 'Active',
        }),
      ],
    }, {
      permissions: ['MenuItem.Manage'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });

    expect(await screen.findByRole('heading', { name: /menu management/i })).toBeInTheDocument();
    await user.selectOptions(getItemCategorySelect(), 'category-1');
    await user.type(screen.getByLabelText(/item name/i), '  Pongal  ');
    const createDescriptionInput = screen.getByPlaceholderText(/crisp rice crepe with potato filling/i);
    await user.clear(createDescriptionInput);
    await user.type(createDescriptionInput, '  Savory rice and lentil bowl  ');
    const createSkuInput = screen.getByPlaceholderText(/dosa-01/i);
    await user.clear(createSkuInput);
    await user.type(createSkuInput, '  PONGAL-01  ');
    await user.clear(screen.getByLabelText(/base price/i));
    await user.type(screen.getByLabelText(/base price/i), '3.25');
    await user.clear(screen.getByLabelText(/tax rate/i));
    await user.type(screen.getByLabelText(/tax rate/i), '0');
    const createItemForm = screen.getByRole('button', { name: /create item/i }).closest('form');
    expect(createItemForm).not.toBeNull();
    fireEvent.submit(createItemForm!);

    await waitForRequest(fetchMock, 'POST', itemsPath);

    const createCall = fetchMock.mock.calls.find(call => {
      const method = (call[1]?.method ?? 'GET').toUpperCase();
      const path = new URL(String(call[0])).pathname;
      return method === 'POST' && path === itemsPath;
    });

    expect(getJsonBody(createCall as [RequestInfo | URL, RequestInit?])).toEqual({
      menuCategoryId: 'category-1',
      name: 'Pongal',
      description: 'Savory rice and lentil bowl',
      sku: 'PONGAL-01',
      basePrice: 3.25,
      taxRate: 0,
      isVegetarian: false,
      isAvailableForEatIn: true,
      isAvailableForParcel: true,
      inventoryDeductionMode: 'RecipeOnServe',
      stockInventoryItemId: null,
    });
    expect(
      Object.keys(getJsonBody(createCall as [RequestInfo | URL, RequestInit?]))
    ).not.toContain('restaurantId');
  }, 20000);

  it('shows a clean duplicate-SKU error when item create fails with RFC7807 JSON', async () => {
    const { user, fetchMock } = renderMenuPage({
      [`POST ${itemsPath}`]: [
        problemJsonResponse(
          {
            type: 'https://datatracker.ietf.org/doc/html/rfc7807',
            title: 'Bad Request',
            status: 400,
            detail: 'SKU already exists. Please enter a unique SKU.',
          },
          400
        ),
      ],
    }, {
      permissions: ['MenuItem.Manage'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });

    expect(await screen.findByRole('heading', { name: /menu management/i })).toBeInTheDocument();
    await user.selectOptions(getItemCategorySelect(), 'category-1');
    await user.type(screen.getByLabelText(/item name/i), '  Pongal  ');
    const createSkuInput = screen.getByPlaceholderText(/dosa-01/i);
    await user.clear(createSkuInput);
    await user.type(createSkuInput, '  PONGAL-01  ');
    await user.clear(screen.getByLabelText(/base price/i));
    await user.type(screen.getByLabelText(/base price/i), '3.25');
    await user.clear(screen.getByLabelText(/tax rate/i));
    await user.type(screen.getByLabelText(/tax rate/i), '0');
    await user.click(screen.getByRole('button', { name: /create item/i }));

    await waitForRequest(fetchMock, 'POST', itemsPath);
    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent('SKU already exists. Please enter a unique SKU.');
    });

    expect(screen.queryByText(/datatracker\.ietf\.org/i)).not.toBeInTheDocument();
  });

  it('scrolls and focuses the item form when clicking New Item', async () => {
    const scrollIntoViewMock = mockScrollIntoView();
    const { user } = renderMenuPage(undefined, {
      permissions: ['MenuItem.Manage'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });

    expect(await screen.findByRole('heading', { name: /menu management/i })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /new item/i }));

    await waitFor(() => {
      expect(scrollIntoViewMock).toHaveBeenCalled();
    });

    await waitFor(() => {
      expect(screen.getAllByRole('combobox', { name: /category/i })[1]).toHaveFocus();
    });
  });

  it('sends the selected inventory deduction mode and stock item when configured', async () => {
    const { user, fetchMock } = renderMenuPage(
      {
        [`POST ${itemsPath}`]: [
          itemDetailResponse({
            menuItemId: 'item-3',
            restaurantId: 'restaurant-1',
            menuCategoryId: 'category-1',
            categoryName: 'Breakfast',
            name: 'Idli',
            description: 'Steamed rice cakes',
            sku: 'IDLI-01',
            basePrice: 2.75,
            taxRate: 0,
            isVegetarian: true,
            isAvailableForEatIn: true,
            isAvailableForParcel: true,
            inventoryDeductionMode: 'BatchPrepared',
            stockInventoryItemId: 'inventory-1',
            stockInventoryItemName: 'Prepared Idli Stock',
            status: 'Active',
          }),
        ],
        [`GET ${inventoryItemsPath}`]: [
          inventoryListResponse([
            {
              inventoryItemId: 'inventory-1',
              restaurantId: 'restaurant-1',
              branchId: 'branch-1',
              name: 'Prepared Idli Stock',
              normalizedName: 'PREPARED IDLI STOCK',
              category: 'Prepared',
              unitOfMeasure: 'pcs',
              lowStockThreshold: 5,
              isActive: true,
              currentStock: 20,
              status: 'Healthy',
              createdAtUtc: timestamp,
              updatedAtUtc: laterTimestamp,
            },
          ]),
        ],
      },
      {
        permissions: ['MenuItem.Manage'],
        roles: ['Admin'],
        activeRole: 'Admin',
        branchId: 'branch-1',
      }
    );

    expect(await screen.findByRole('heading', { name: /menu management/i })).toBeInTheDocument();
    await user.selectOptions(getItemCategorySelect(), 'category-1');
    await user.type(screen.getByLabelText(/item name/i), '  Idli  ');
    await user.selectOptions(screen.getByLabelText(/inventory deduction mode/i), 'BatchPrepared');
    await user.selectOptions(screen.getByLabelText(/prepared stock item/i), 'inventory-1');
    await user.clear(screen.getByLabelText(/base price/i));
    await user.type(screen.getByLabelText(/base price/i), '2.75');
    await user.clear(screen.getByLabelText(/tax rate/i));
    await user.type(screen.getByLabelText(/tax rate/i), '0');
    const createItemForm = screen.getByRole('button', { name: /create item/i }).closest('form');
    expect(createItemForm).not.toBeNull();
    fireEvent.submit(createItemForm!);

    await waitForRequest(fetchMock, 'POST', itemsPath);

    const createCall = fetchMock.mock.calls.find(call => {
      const method = (call[1]?.method ?? 'GET').toUpperCase();
      const path = new URL(String(call[0])).pathname;
      return method === 'POST' && path === itemsPath;
    });

    expect(getJsonBody(createCall as [RequestInfo | URL, RequestInit?])).toEqual({
      menuCategoryId: 'category-1',
      name: 'Idli',
      description: null,
      sku: null,
      basePrice: 2.75,
      taxRate: 0,
      isVegetarian: false,
      isAvailableForEatIn: true,
      isAvailableForParcel: true,
      inventoryDeductionMode: 'BatchPrepared',
      stockInventoryItemId: 'inventory-1',
    });
  }, 20000);

  it('blocks blank item names before create', async () => {
    const { user, fetchMock } = renderMenuPage(undefined, {
      permissions: ['MenuItem.Manage'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });

    expect(await screen.findByRole('heading', { name: /menu management/i })).toBeInTheDocument();
    const createItemForm = screen.getByRole('button', { name: /create item/i }).closest('form');
    expect(createItemForm).not.toBeNull();
    fireEvent.submit(createItemForm!);

    await waitFor(() => {
      expect(screen.getByText(/please fix the item fields before saving/i)).toBeInTheDocument();
    });

    expect(screen.getByText(/item name is required/i)).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalled();
  });

  it('blocks negative item prices before create', async () => {
    const { user } = renderMenuPage(undefined, {
      permissions: ['MenuItem.Manage'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });

    expect(await screen.findByRole('heading', { name: /menu management/i })).toBeInTheDocument();
    await user.selectOptions(getItemCategorySelect(), 'category-1');
    await user.type(screen.getByLabelText(/item name/i), 'Poori');
    const basePriceInput = screen.getByLabelText(/base price/i);
    Object.defineProperty(basePriceInput, 'value', {
      configurable: true,
      value: '-1',
    });
    fireEvent.input(basePriceInput, { target: { value: '-1' } });
    const createItemForm = screen.getByRole('button', { name: /create item/i }).closest('form');
    expect(createItemForm).not.toBeNull();
    fireEvent.submit(createItemForm!);

    await waitFor(() => {
      expect(screen.getByText(/base price must be zero or greater/i)).toBeInTheDocument();
    });
  });

  it('blocks item create when both availability flags are false', async () => {
    const { user } = renderMenuPage(undefined, {
      permissions: ['MenuItem.Manage'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });

    expect(await screen.findByRole('heading', { name: /menu management/i })).toBeInTheDocument();
    await user.selectOptions(getItemCategorySelect(), 'category-1');
    await user.type(screen.getByLabelText(/item name/i), 'Poori');
    await user.clear(screen.getByLabelText(/base price/i));
    await user.type(screen.getByLabelText(/base price/i), '1.25');
    await user.click(screen.getByLabelText(/available for eat-in/i));
    await user.click(screen.getByLabelText(/available for parcel/i));
    await user.click(screen.getByRole('button', { name: /create item/i }));

    await waitFor(() => {
      expect(screen.getByText(/select at least one availability option/i)).toBeInTheDocument();
    });
  });

  it('updates an item without sending restaurantId or status', async () => {
    const { user, fetchMock } = renderMenuPage({
      [`GET ${itemPath('item-1')}`]: [
        itemDetailResponse({
          menuItemId: 'item-1',
          restaurantId: 'restaurant-1',
          menuCategoryId: 'category-1',
          categoryName: 'Breakfast',
          name: 'Masala Dosa',
          description: 'Crisp rice crepe',
          sku: 'DOSA-01',
          basePrice: 2.5,
          taxRate: 0,
          isVegetarian: true,
          isAvailableForEatIn: true,
          isAvailableForParcel: true,
          status: 'Active',
        }),
      ],
      [`PUT ${itemPath('item-1')}`]: [
        itemDetailResponse({
          menuItemId: 'item-1',
          restaurantId: 'restaurant-1',
          menuCategoryId: 'category-1',
          categoryName: 'Breakfast',
          name: 'Masala Dosa Special',
          description: 'Crisp rice crepe with chutney',
          sku: 'DOSA-99',
          basePrice: 3.25,
          taxRate: 5,
          isVegetarian: true,
          isAvailableForEatIn: true,
          isAvailableForParcel: false,
          status: 'Active',
        }),
      ],
      [`GET ${priceHistoryPath('item-1')}`]: [
        priceHistoryResponse([
          {
            menuItemPriceHistoryId: 'history-1',
            menuItemId: 'item-1',
            oldPrice: 2.5,
            newPrice: 3.25,
            changedByUserId: 'user-1',
            changedAt: timestamp,
            reason: 'Price updated from menu admin',
          },
        ]),
        priceHistoryResponse([
          {
            menuItemPriceHistoryId: 'history-1',
            menuItemId: 'item-1',
            oldPrice: 2.5,
            newPrice: 3.25,
            changedByUserId: 'user-1',
            changedAt: timestamp,
            reason: 'Price updated from menu admin',
          },
        ]),
      ],
    }, {
      permissions: ['MenuItem.Manage'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });

    expect(await screen.findByRole('heading', { name: /menu management/i })).toBeInTheDocument();
    await clickFirstButton(user, /edit/i);
    await screen.findByDisplayValue('Masala Dosa');

    await user.clear(screen.getByLabelText(/^item name$/i));
    await user.type(screen.getByLabelText(/^item name$/i), ' Masala Dosa Special ');
    const updateDescriptionInput = screen.getByDisplayValue('Crisp rice crepe');
    await user.clear(updateDescriptionInput);
    await user.type(updateDescriptionInput, ' Crisp rice crepe with chutney ');
    const updateSkuInput = screen.getByDisplayValue('DOSA-01');
    await user.clear(updateSkuInput);
    await user.type(updateSkuInput, ' DOSA-99 ');
    await user.clear(screen.getByLabelText(/base price/i));
    await user.type(screen.getByLabelText(/base price/i), '3.25');
    await user.clear(screen.getByLabelText(/tax rate/i));
    await user.type(screen.getByLabelText(/tax rate/i), '5');
    await user.click(screen.getByRole('button', { name: /save changes/i }));

    await waitForRequest(fetchMock, 'PUT', itemPath('item-1'));

    const updateCall = fetchMock.mock.calls.find(call => {
      const method = (call[1]?.method ?? 'GET').toUpperCase();
      const path = new URL(String(call[0])).pathname;
      return method === 'PUT' && path === itemPath('item-1');
    });

    expect(getJsonBody(updateCall as [RequestInfo | URL, RequestInit?])).toEqual({
      menuCategoryId: 'category-1',
      name: 'Masala Dosa Special',
      description: 'Crisp rice crepe with chutney',
      sku: 'DOSA-99',
      basePrice: 3.25,
      taxRate: 5,
      isVegetarian: true,
      isAvailableForEatIn: true,
      isAvailableForParcel: true,
      inventoryDeductionMode: 'RecipeOnServe',
      stockInventoryItemId: null,
    });
    expect(
      Object.keys(getJsonBody(updateCall as [RequestInfo | URL, RequestInit?]))
    ).not.toContain('restaurantId');
    expect(
      Object.keys(getJsonBody(updateCall as [RequestInfo | URL, RequestInit?]))
    ).not.toContain('status');
  }, 20000);

  it('activates an inactive item through the activate endpoint', async () => {
    const { user, fetchMock } = renderMenuPage({
      [`GET ${itemPath('item-2')}`]: [
        itemDetailResponse({
          menuItemId: 'item-2',
          restaurantId: 'restaurant-1',
          menuCategoryId: 'category-2',
          categoryName: 'Snacks',
          name: 'Vada',
          description: null,
          sku: 'VADA-01',
          basePrice: 1.75,
          taxRate: 0,
          isVegetarian: true,
          isAvailableForEatIn: true,
          isAvailableForParcel: false,
          status: 'Inactive',
        }),
      ],
      [`POST ${itemActivatePath('item-2')}`]: [
        itemDetailResponse({
          menuItemId: 'item-2',
          restaurantId: 'restaurant-1',
          menuCategoryId: 'category-2',
          categoryName: 'Snacks',
          name: 'Vada',
          description: null,
          sku: 'VADA-01',
          basePrice: 1.75,
          taxRate: 0,
          isVegetarian: true,
          isAvailableForEatIn: true,
          isAvailableForParcel: false,
          status: 'Active',
        }),
      ],
      [`GET ${priceHistoryPath('item-2')}`]: [priceHistoryResponse([]), priceHistoryResponse([])],
    }, {
      permissions: ['MenuItem.Manage'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });

    expect(await screen.findByRole('heading', { name: /menu management/i })).toBeInTheDocument();
    await user.click(screen.getAllByRole('button', { name: /edit/i })[1]);
    await screen.findByDisplayValue('Vada');
    await user.click(screen.getByRole('button', { name: /activate item/i }));

    await waitFor(() => {
      expect(screen.getByText(/vada is now active/i)).toBeInTheDocument();
    });

    const activateCall = fetchMock.mock.calls.find(call => {
      const method = (call[1]?.method ?? 'GET').toUpperCase();
      const path = new URL(String(call[0])).pathname;
      return method === 'POST' && path === itemActivatePath('item-2');
    });

    expect(activateCall).toBeDefined();
  });

  it('requires confirmation before deactivating an active item', async () => {
    const { user, fetchMock } = renderMenuPage({
      [`GET ${itemPath('item-1')}`]: [
        itemDetailResponse({
          menuItemId: 'item-1',
          restaurantId: 'restaurant-1',
          menuCategoryId: 'category-1',
          categoryName: 'Breakfast',
          name: 'Masala Dosa',
          description: 'Crisp rice crepe',
          sku: 'DOSA-01',
          basePrice: 2.5,
          taxRate: 0,
          isVegetarian: true,
          isAvailableForEatIn: true,
          isAvailableForParcel: true,
          status: 'Active',
        }),
      ],
      [`POST ${itemDeactivatePath('item-1')}`]: [
        itemDetailResponse({
          menuItemId: 'item-1',
          restaurantId: 'restaurant-1',
          menuCategoryId: 'category-1',
          categoryName: 'Breakfast',
          name: 'Masala Dosa',
          description: 'Crisp rice crepe',
          sku: 'DOSA-01',
          basePrice: 2.5,
          taxRate: 0,
          isVegetarian: true,
          isAvailableForEatIn: true,
          isAvailableForParcel: true,
          status: 'Inactive',
        }),
      ],
      [`GET ${priceHistoryPath('item-1')}`]: [priceHistoryResponse([]), priceHistoryResponse([])],
    }, {
      permissions: ['MenuItem.Manage'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });

    expect(await screen.findByRole('heading', { name: /menu management/i })).toBeInTheDocument();
    await user.click(screen.getAllByRole('button', { name: /edit/i })[0]);
    await screen.findByDisplayValue('Masala Dosa');

    expect(screen.queryByRole('button', { name: /^confirm deactivate$/i })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /deactivate item/i }));
    expect(screen.getByRole('button', { name: /^confirm deactivate$/i })).toBeInTheDocument();
    expect(
      fetchMock.mock.calls.some(call => {
        const method = (call[1]?.method ?? 'GET').toUpperCase();
        const path = new URL(String(call[0])).pathname;
        return method === 'POST' && path === itemDeactivatePath('item-1');
      })
    ).toBe(false);

    await user.click(screen.getByRole('button', { name: /^confirm deactivate$/i }));

    await waitFor(() => {
      expect(screen.getByText(/masala dosa is now inactive/i)).toBeInTheDocument();
    });
  });

  it('loads price history for a selected item', async () => {
    const { user } = renderMenuPage({
      [`GET ${itemPath('item-1')}`]: [
        itemDetailResponse({
          menuItemId: 'item-1',
          restaurantId: 'restaurant-1',
          menuCategoryId: 'category-1',
          categoryName: 'Breakfast',
          name: 'Masala Dosa',
          description: 'Crisp rice crepe',
          sku: 'DOSA-01',
          basePrice: 2.5,
          taxRate: 0,
          isVegetarian: true,
          isAvailableForEatIn: true,
          isAvailableForParcel: true,
          status: 'Active',
        }),
      ],
      [`GET ${priceHistoryPath('item-1')}`]: [
        priceHistoryResponse([
          {
            menuItemPriceHistoryId: 'history-1',
            menuItemId: 'item-1',
            oldPrice: 2.5,
            newPrice: 3.25,
            changedByUserId: 'user-1',
            changedAt: timestamp,
            reason: 'Price updated from menu admin',
          },
        ]),
      ],
    }, {
      permissions: ['MenuItem.View'],
      roles: ['Cashier'],
      activeRole: 'Cashier',
    });

    expect(await screen.findByRole('heading', { name: /menu management/i })).toBeInTheDocument();
    await clickFirstButton(user, /view details/i);

    await waitFor(() => {
      expect(screen.getByText(/price updated from menu admin/i, { selector: 'td' })).toBeInTheDocument();
    });
    expect(screen.getAllByText(/2\.50/, { selector: 'td' })[0]).toBeInTheDocument();
    expect(screen.getAllByText(/3\.25/, { selector: 'td' })[0]).toBeInTheDocument();
  });

  it('shows an empty price history state for items without changes', async () => {
    const { user } = renderMenuPage({
      [`GET ${itemPath('item-2')}`]: [
        itemDetailResponse({
          menuItemId: 'item-2',
          restaurantId: 'restaurant-1',
          menuCategoryId: 'category-2',
          categoryName: 'Snacks',
          name: 'Vada',
          description: null,
          sku: 'VADA-01',
          basePrice: 1.75,
          taxRate: 0,
          isVegetarian: true,
          isAvailableForEatIn: true,
          isAvailableForParcel: false,
          status: 'Inactive',
        }),
      ],
      [`GET ${priceHistoryPath('item-2')}`]: [priceHistoryResponse([])],
    }, {
      permissions: ['MenuItem.View'],
      roles: ['Cashier'],
      activeRole: 'Cashier',
    });

    expect(await screen.findByRole('heading', { name: /menu management/i })).toBeInTheDocument();
    await user.click(screen.getAllByRole('button', { name: /view details/i })[1]);

    await waitFor(() => {
      expect(screen.getByText(/no price changes yet/i)).toBeInTheDocument();
    });
  });

  it('does not render a delete action', async () => {
    renderMenuPage(undefined, {
      permissions: ['MenuItem.View'],
      roles: ['Cashier'],
      activeRole: 'Cashier',
    });

    expect(await screen.findByRole('heading', { name: /menu management/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /delete/i })).not.toBeInTheDocument();
  });
});
