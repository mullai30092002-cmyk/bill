import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';

import App from '../../App';
import { clearAuthSession, createJsonResponse, storeAuthSession } from '../../test/authTestUtils';
import { LANGUAGE_STORAGE_KEY } from '../../i18n/translations';
import { renderWithRouter } from '../../test/renderWithRouter';
import type { BatchProductionListItem, InventoryItemListItem, InventorySummaryResponse } from './inventoryTypes';
import type { MenuItem } from '../admin/adminTypes';

const makeSummary = (): InventorySummaryResponse => ({
  restaurantId: 'restaurant-1',
  branchId: 'branch-1',
  totalItems: 0,
  activeItems: 0,
  inactiveItems: 0,
  lowStockCount: 0,
  outOfStockCount: 0,
  totalCurrentStock: 0,
  recentlyAdjustedCount: 0,
  lowStockItems: [],
  outOfStockItems: [],
});

const stubFetch = (items: InventoryItemListItem[] = []) =>
  vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    const pathname = new URL(url, 'http://localhost').pathname;
    const method = init?.method ?? 'GET';

    if (pathname === '/api/v1/admin/branches' && method === 'GET') {
      return createJsonResponse({
        items: [
          {
            branchId: 'branch-1',
            restaurantId: 'restaurant-1',
            name: 'Main Branch',
            address: '123 Market Street',
            phone: '60000000',
            timezone: 'Asia/Singapore',
            currency: 'SGD',
            status: 'Active',
            createdAt: '2026-06-11T09:00:00Z',
            updatedAt: '2026-06-11T09:30:00Z',
          },
        ],
      });
    }

    if (pathname === '/api/v1/inventory/items' && method === 'GET') {
      return createJsonResponse({ items });
    }

    if (pathname === '/api/v1/inventory/summary' && method === 'GET') {
      return createJsonResponse(makeSummary());
    }

    if (pathname === '/api/v1/admin/menu/items' && method === 'GET') {
      return createJsonResponse({
        items: [
          {
            menuItemId: 'menu-1',
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
            stockInventoryItemId: 'item-1',
            stockInventoryItemName: 'Prepared Idli Stock',
            status: 'Active',
            createdAt: '2026-06-11T09:00:00Z',
            updatedAt: null,
          } satisfies MenuItem,
        ],
      });
    }

    if (pathname === '/api/v1/inventory/batch-productions' && method === 'GET') {
      return createJsonResponse({
        items: [
          {
            batchProductionId: 'batch-1',
            restaurantId: 'restaurant-1',
            branchId: 'branch-1',
            menuItemId: 'menu-1',
            menuItemName: 'Idli',
            preparedInventoryItemId: 'item-1',
            preparedInventoryItemName: 'Prepared Idli Stock',
            quantityProduced: 50,
            businessDate: '2026-06-11',
            producedAtUtc: '2026-06-11T09:30:00Z',
            producedByUserId: 'user-1',
            producedByUserName: 'Maya Iyer',
            notes: 'Morning batch',
            totalRawQuantityConsumed: 12.5,
            createdAtUtc: '2026-06-11T09:30:00Z',
            shelfLifeHours: null,
            expiresAtUtc: null,
            storageNote: null,
            batchReference: null,
          } satisfies BatchProductionListItem,
        ],
      });
    }

    throw new Error(`Unhandled fetch in InventoryLanguage.test: ${method} ${url}`);
  });

afterEach(() => {
  clearAuthSession();
  localStorage.removeItem(LANGUAGE_STORAGE_KEY);
  vi.unstubAllGlobals();
});

describe('Inventory Tamil chrome', () => {
  it('renders Tamil UI chrome for the inventory workspace', async () => {
    localStorage.setItem(LANGUAGE_STORAGE_KEY, 'ta');
    storeAuthSession({
      userId: 'session-user',
      permissions: ['Inventory.View', 'Inventory.Adjust', 'MenuItem.View', 'Branch.Manage'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });

    vi.stubGlobal('fetch', stubFetch());
    renderWithRouter(<App />, '/inventory');

    expect(await screen.findByRole('heading', { name: /கையிருப்பு பணியிடம்/i })).toBeInTheDocument();
    expect(screen.getByText(/இன்னும் கையிருப்பு உருப்படிகள் இல்லை/i)).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /தொகுதி தயாரிப்பு/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /உற்பத்தியை பதிவு செய்/i })).toBeInTheDocument();
  });
});
