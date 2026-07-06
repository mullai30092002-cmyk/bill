import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';

import App from '../../../App';
import { clearAuthSession, createJsonResponse, storeAuthSession } from '../../../test/authTestUtils';
import { LANGUAGE_STORAGE_KEY } from '../../../i18n/translations';
import { renderWithRouter } from '../../../test/renderWithRouter';

const categoriesPath = '/api/v1/admin/menu/categories';
const itemsPath = '/api/v1/admin/menu/items';
const inventoryItemsPath = '/api/v1/inventory/items';

const timestamp = '2026-06-11T09:00:00Z';

const makeCategoryList = () =>
  createJsonResponse({
    items: [
      {
        menuCategoryId: 'category-1',
        restaurantId: 'restaurant-1',
        name: 'Breakfast',
        displayOrder: 1,
        status: 'Active',
        createdAt: timestamp,
        updatedAt: timestamp,
      },
    ],
  });

const makeItemList = () =>
  createJsonResponse({
    items: [
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
        inventoryDeductionMode: 'RecipeOnServe',
        stockInventoryItemId: null,
        stockInventoryItemName: null,
        status: 'Active',
        createdAt: timestamp,
        updatedAt: timestamp,
      },
    ],
  });

const setupFetch = (responses: Record<string, Response[]>) => {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
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

afterEach(() => {
  clearAuthSession();
  localStorage.removeItem(LANGUAGE_STORAGE_KEY);
  vi.unstubAllGlobals();
});

describe('MenuManagement Tamil chrome', () => {
  it('renders Tamil UI chrome for the menu management page', async () => {
    localStorage.setItem(LANGUAGE_STORAGE_KEY, 'ta');
    storeAuthSession({
      permissions: ['MenuItem.Manage'],
      roles: ['Admin'],
      activeRole: 'Admin',
      branchId: 'branch-1',
    });

    setupFetch({
      [`GET ${categoriesPath}`]: [makeCategoryList(), makeCategoryList(), makeCategoryList(), makeCategoryList()],
      [`GET ${itemsPath}`]: [makeItemList(), makeItemList(), makeItemList(), makeItemList()],
      [`GET ${inventoryItemsPath}`]: [
        createJsonResponse({
          items: [
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
              updatedAtUtc: timestamp,
            },
          ],
        }),
      ],
    });

    renderWithRouter(<App />, '/admin/menu');

    expect(await screen.findByRole('heading', { name: /மெனு நிர்வாகம்/i })).toBeInTheDocument();
    expect((await screen.findAllByText('Masala Dosa')).length).toBeGreaterThan(0);
    expect((await screen.findAllByText('Breakfast')).length).toBeGreaterThan(0);
    expect(await screen.findByLabelText(/கையிருப்பு கழிப்பு முறை/i)).toBeInTheDocument();
  });
});
