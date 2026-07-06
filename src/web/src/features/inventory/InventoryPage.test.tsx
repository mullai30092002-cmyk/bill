import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import App from '../../App';
import { createJsonResponse, clearAuthSession, storeAuthSession } from '../../test/authTestUtils';
import { renderWithRouter } from '../../test/renderWithRouter';
import type {
  BatchProductionListItem,
  InventoryAlertItem,
  InventoryItemListItem,
  InventoryMovementItem,
  InventorySummaryResponse,
} from './inventoryTypes';
import type { MenuItem } from '../admin/adminTypes';
import type { MenuItemInventoryDeductionMode } from '../admin/adminTypes';

const makeBranch = (overrides: Record<string, unknown> = {}) => ({
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
  ...overrides,
});

const makeItem = (overrides: Partial<InventoryItemListItem> = {}): InventoryItemListItem => ({
  inventoryItemId: 'item-1',
  restaurantId: 'restaurant-1',
  branchId: 'branch-1',
  name: 'Rice',
  normalizedName: 'RICE',
  category: 'Grains',
  unitOfMeasure: 'kg',
  lowStockThreshold: 10,
  isActive: true,
  currentStock: 0,
  status: 'Out of stock',
  createdAtUtc: '2026-06-11T09:00:00Z',
  updatedAtUtc: null,
  ...overrides,
});

const makeMovement = (overrides: Partial<InventoryMovementItem> = {}): InventoryMovementItem => ({
  inventoryMovementId: 'movement-1',
  inventoryItemId: 'item-1',
  restaurantId: 'restaurant-1',
  branchId: 'branch-1',
  movementType: 'AdjustmentIncrease',
  quantity: 10,
  unitCost: 3.25,
  referenceNumber: 'PO-1',
  reason: 'Manual purchase entry',
  notes: 'Initial stock',
  movementDate: '2026-06-12T09:30:00Z',
  recordedByUserId: 'user-1',
  recordedByUserName: 'Maya Iyer',
  recordedByUserMobile: '90000001',
  createdAtUtc: '2026-06-12T09:30:00Z',
  previousStock: 0,
  delta: 10,
  resultingStock: 10,
  resultingStatus: 'In stock',
  expiresAtUtc: null,
  batchReference: null,
  ...overrides,
});

const makeMenuItem = (
  overrides: Partial<MenuItem> & { inventoryDeductionMode?: MenuItemInventoryDeductionMode } = {}
): MenuItem => ({
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
  ...overrides,
});

const makeBatchProduction = (overrides: Partial<BatchProductionListItem> = {}): BatchProductionListItem => ({
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
  ...overrides,
});

const makeAlert = (item: InventoryItemListItem): InventoryAlertItem => ({
  inventoryItemId: item.inventoryItemId,
  name: item.name,
  category: item.category,
  unitOfMeasure: item.unitOfMeasure,
  lowStockThreshold: item.lowStockThreshold,
  currentStock: item.currentStock,
  status: item.status,
});

const makeSummary = (items: InventoryItemListItem[]): InventorySummaryResponse => {
  const activeItems = items.filter(item => item.isActive);
  const lowStockItems = activeItems.filter(item => item.status === 'Low stock').map(makeAlert);
  const outOfStockItems = activeItems.filter(item => item.status === 'Out of stock').map(makeAlert);

  return {
    restaurantId: 'restaurant-1',
    branchId: 'branch-1',
    totalItems: items.length,
    activeItems: activeItems.length,
    inactiveItems: items.length - activeItems.length,
    lowStockCount: lowStockItems.length,
    outOfStockCount: outOfStockItems.length,
    totalCurrentStock: items.reduce((sum, item) => sum + item.currentStock, 0),
    recentlyAdjustedCount: 0,
    lowStockItems,
    outOfStockItems,
  };
};

const createProblemResponse = (body: Record<string, unknown>, status = 400) =>
  new Response(JSON.stringify(body), {
    status,
    headers: {
      'Content-Type': 'application/problem+json',
    },
  });

const stubScrollIntoView = () => {
  const original = HTMLElement.prototype.scrollIntoView;
  const scrollIntoViewSpy = vi.fn();

  Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', {
    configurable: true,
    writable: true,
    value: scrollIntoViewSpy,
  });

  return {
    scrollIntoViewSpy,
    restore: () => {
      Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', {
        configurable: true,
        writable: true,
        value: original,
      });
    },
  };
};

const stubInventoryBootstrap = (
  branches: Array<ReturnType<typeof makeBranch>>,
  items: InventoryItemListItem[],
  movements: InventoryMovementItem[] = [],
  summary: InventorySummaryResponse = makeSummary(items),
  menuItems: MenuItem[] = [],
  batchProductions: BatchProductionListItem[] = []
) =>
  vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    const pathname = new URL(url, 'http://localhost').pathname;
    const method = init?.method ?? 'GET';

    if (pathname === '/api/v1/admin/branches' && method === 'GET') {
      return createJsonResponse({ items: branches });
    }

    if (pathname === '/api/v1/inventory/items' && method === 'GET') {
      return createJsonResponse({ items });
    }

    if (pathname === '/api/v1/inventory/summary' && method === 'GET') {
      return createJsonResponse(summary);
    }

    if (pathname === '/api/v1/admin/menu/items' && method === 'GET') {
      return createJsonResponse({ items: menuItems });
    }

    if (pathname === '/api/v1/inventory/batch-productions' && method === 'GET') {
      return createJsonResponse({ items: batchProductions });
    }

    if (pathname.match(/\/api\/v1\/inventory\/items\/[^/]+\/movements$/) && method === 'GET') {
      return createJsonResponse({ items: movements });
    }

    throw new Error(`Unexpected fetch in inventory bootstrap: ${method} ${url}`);
  });

const renderInventoryRoute = (permissions: string[], fetchMock: ReturnType<typeof vi.fn>) => {
  storeAuthSession({ permissions, roles: ['Admin'], activeRole: 'Admin' });
  vi.stubGlobal('fetch', fetchMock);
  return renderWithRouter(<App />, '/inventory');
};

describe('InventoryPage', () => {
  beforeEach(() => {
    clearAuthSession();
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it('renders the inventory workspace and empty state for stock lists', async () => {
    const fetchMock = stubInventoryBootstrap([makeBranch()], []);
    renderInventoryRoute(['Inventory.View', 'Inventory.Adjust', 'Branch.Manage'], fetchMock);

    expect(await screen.findByRole('heading', { name: /inventory workspace/i })).toBeInTheDocument();
    expect(screen.getByText(/no inventory items yet/i)).toBeInTheDocument();
  });

  it('validates required fields when adding an inventory item', async () => {
    const fetchMock = stubInventoryBootstrap([makeBranch()], []);
    renderInventoryRoute(['Inventory.Adjust', 'Branch.Manage'], fetchMock);
    const user = userEvent.setup();

    expect(await screen.findByRole('heading', { name: /inventory workspace/i })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /create item/i }));

    const itemCard = screen.getByRole('heading', { name: /add inventory item/i }).closest('section');
    expect(itemCard).not.toBeNull();
    const itemForm = within(itemCard as HTMLElement);

    expect(itemForm.getByLabelText(/item name/i)).toBeInTheDocument();
    expect(itemForm.getByRole('checkbox', { name: /active item/i })).toBeChecked();
    await user.click(itemForm.getByRole('button', { name: /create item/i }));

    expect(itemForm.getByText(/item name is required/i)).toBeInTheDocument();
    expect(itemForm.getByText(/category is required/i)).toBeInTheDocument();
    expect(itemForm.getByText(/unit is required/i)).toBeInTheDocument();
  });

  it('displays duplicate-name API errors safely when creating an item', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      const pathname = new URL(url, 'http://localhost').pathname;
      const method = init?.method ?? 'GET';

      if (pathname === '/api/v1/admin/branches') {
        return createJsonResponse({ items: [makeBranch()] });
      }

      if (pathname === '/api/v1/inventory/items' && method === 'GET') {
        return createJsonResponse({ items: [] });
      }

      if (pathname === '/api/v1/inventory/summary') {
        return createJsonResponse(makeSummary([]));
      }

      if (pathname === '/api/v1/admin/menu/items' && method === 'GET') {
        return createJsonResponse({ items: [] });
      }

      if (pathname === '/api/v1/inventory/batch-productions' && method === 'GET') {
        return createJsonResponse({ items: [] });
      }

      if (pathname === '/api/v1/inventory/items' && method === 'POST') {
        return createProblemResponse({
          type: 'https://datatracker.ietf.org/doc/html/rfc7807',
          title: 'Bad Request',
          status: 400,
          detail: 'Inventory item name already exists in this branch.',
        });
      }

      throw new Error(`Unexpected fetch in duplicate-name test: ${method} ${url}`);
    });
    renderInventoryRoute(['Inventory.Adjust', 'Branch.Manage'], fetchMock);
    const user = userEvent.setup();

    expect(await screen.findByRole('heading', { name: /inventory workspace/i })).toBeInTheDocument();
    expect(await screen.findByText(/no inventory items yet/i)).toBeInTheDocument();
    const itemCard = screen.getByRole('heading', { name: /add inventory item/i }).closest('section');
    expect(itemCard).not.toBeNull();
    const itemForm = within(itemCard as HTMLElement);

    await user.type(itemForm.getByLabelText(/item name/i), 'Rice');
    await user.type(itemForm.getByLabelText(/category/i), 'Grains');
    await user.type(itemForm.getByLabelText(/unit/i), 'kg');
    await user.type(itemForm.getByLabelText(/low-stock threshold/i), '10');
    await user.click(itemForm.getByRole('button', { name: /create item/i }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(/already exists/i);
    expect(alert).not.toHaveTextContent(/datatracker\.ietf\.org/i);
    expect(alert).not.toHaveTextContent(/"type":/i);
    expect(alert).not.toHaveTextContent(/"detail":/i);
    expect(alert.textContent?.trim().startsWith('{')).toBe(false);
  });

  it('renders batch production recipe errors safely', async () => {
    const items = [makeItem({ inventoryItemId: 'item-1', name: 'Prepared Idli Stock', category: 'Prepared', currentStock: 20, status: 'In stock' })];
    const menuItems = [makeMenuItem({ menuItemId: 'menu-1', name: 'Idli', stockInventoryItemName: 'Prepared Idli Stock' })];
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      const pathname = new URL(url, 'http://localhost').pathname;
      const method = init?.method ?? 'GET';

      if (pathname === '/api/v1/admin/branches' && method === 'GET') {
        return createJsonResponse({ items: [makeBranch()] });
      }

      if (pathname === '/api/v1/inventory/items' && method === 'GET') {
        return createJsonResponse({ items });
      }

      if (pathname === '/api/v1/inventory/summary' && method === 'GET') {
        return createJsonResponse(makeSummary(items));
      }

      if (pathname === '/api/v1/admin/menu/items' && method === 'GET') {
        return createJsonResponse({ items: menuItems });
      }

      if (pathname === '/api/v1/inventory/batch-productions' && method === 'GET') {
        return createJsonResponse({ items: [] });
      }

      if (pathname.match(/\/api\/v1\/inventory\/items\/[^/]+\/movements$/) && method === 'GET') {
        return createJsonResponse({ items: [] });
      }

      if (pathname === '/api/v1/inventory/batch-productions' && method === 'POST') {
        return createProblemResponse({
          type: 'https://datatracker.ietf.org/doc/html/rfc7807',
          title: 'Bad Request',
          status: 400,
          detail: 'Batch production requires a recipe for the current branch.',
        });
      }

      throw new Error(`Unexpected fetch in batch production recipe test: ${method} ${url}`);
    });
    renderInventoryRoute(['Inventory.View', 'Inventory.Adjust', 'MenuItem.View', 'Branch.Manage'], fetchMock);
    const user = userEvent.setup();

    expect(await screen.findByRole('heading', { name: /inventory workspace/i })).toBeInTheDocument();
    const batchCard = screen.getByRole('heading', { name: /^batch production$/i }).closest('section');
    expect(batchCard).not.toBeNull();
    const batchScope = within(batchCard as HTMLElement);
    await user.clear(batchScope.getByLabelText(/quantity produced/i));
    await user.type(batchScope.getByLabelText(/quantity produced/i), '4');
    await user.click(batchScope.getByRole('button', { name: /record production/i }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(/recipe for the current branch/i);
    expect(alert).not.toHaveTextContent(/datatracker\.ietf\.org/i);
    expect(alert).not.toHaveTextContent(/"type":/i);
    expect(alert).not.toHaveTextContent(/"detail":/i);
  });

  it('renders batch production stock errors safely', async () => {
    const items = [makeItem({ inventoryItemId: 'item-1', name: 'Prepared Idli Stock', category: 'Prepared', currentStock: 20, status: 'In stock' })];
    const menuItems = [makeMenuItem({ menuItemId: 'menu-1', name: 'Idli', stockInventoryItemName: 'Prepared Idli Stock' })];
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      const pathname = new URL(url, 'http://localhost').pathname;
      const method = init?.method ?? 'GET';

      if (pathname === '/api/v1/admin/branches' && method === 'GET') {
        return createJsonResponse({ items: [makeBranch()] });
      }

      if (pathname === '/api/v1/inventory/items' && method === 'GET') {
        return createJsonResponse({ items });
      }

      if (pathname === '/api/v1/inventory/summary' && method === 'GET') {
        return createJsonResponse(makeSummary(items));
      }

      if (pathname === '/api/v1/admin/menu/items' && method === 'GET') {
        return createJsonResponse({ items: menuItems });
      }

      if (pathname === '/api/v1/inventory/batch-productions' && method === 'GET') {
        return createJsonResponse({ items: [] });
      }

      if (pathname.match(/\/api\/v1\/inventory\/items\/[^/]+\/movements$/) && method === 'GET') {
        return createJsonResponse({ items: [] });
      }

      if (pathname === '/api/v1/inventory/batch-productions' && method === 'POST') {
        return createProblemResponse({
          type: 'https://datatracker.ietf.org/doc/html/rfc7807',
          title: 'Bad Request',
          status: 400,
          detail: 'Insufficient stock for batch production: Carrot requires 1400.',
        });
      }

      throw new Error(`Unexpected fetch in batch production stock test: ${method} ${url}`);
    });
    renderInventoryRoute(['Inventory.View', 'Inventory.Adjust', 'MenuItem.View', 'Branch.Manage'], fetchMock);
    const user = userEvent.setup();

    expect(await screen.findByRole('heading', { name: /inventory workspace/i })).toBeInTheDocument();
    const batchCard = screen.getByRole('heading', { name: /^batch production$/i }).closest('section');
    expect(batchCard).not.toBeNull();
    const batchScope = within(batchCard as HTMLElement);
    await user.clear(batchScope.getByLabelText(/quantity produced/i));
    await user.type(batchScope.getByLabelText(/quantity produced/i), '4');
    await user.click(batchScope.getByRole('button', { name: /record production/i }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(/carrot requires 1400/i);
    expect(alert).not.toHaveTextContent(/datatracker\.ietf\.org/i);
    expect(alert).not.toHaveTextContent(/"type":/i);
    expect(alert).not.toHaveTextContent(/"detail":/i);
  });

  it('scrolls and focuses the inventory item form when adding an item', async () => {
    const fetchMock = stubInventoryBootstrap([makeBranch()], []);
    const { scrollIntoViewSpy, restore } = stubScrollIntoView();
    renderInventoryRoute(['Inventory.Adjust', 'Branch.Manage'], fetchMock);
    const user = userEvent.setup();

    expect(await screen.findByRole('heading', { name: /inventory workspace/i })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /add item/i }));

    const itemCard = await screen.findByRole('heading', { name: /add inventory item/i });
    const itemForm = within(itemCard.closest('section') as HTMLElement);
    await waitFor(() => expect(scrollIntoViewSpy).toHaveBeenCalled());
    await waitFor(() => expect(itemForm.getByLabelText(/item name/i)).toHaveFocus());
    restore();
  });

  it('scrolls and focuses the stock adjustment dialog when opening adjust stock', async () => {
    const item = makeItem({ currentStock: 8, status: 'In stock', lowStockThreshold: 5 });
    const fetchMock = stubInventoryBootstrap([makeBranch()], [item], [], makeSummary([item]));
    const { scrollIntoViewSpy, restore } = stubScrollIntoView();
    renderInventoryRoute(['Inventory.View', 'Inventory.Adjust', 'Branch.Manage'], fetchMock);
    const user = userEvent.setup();

    expect(await screen.findByRole('heading', { name: /inventory workspace/i })).toBeInTheDocument();
    const itemsSection = screen.getByRole('heading', { name: /inventory items/i }).closest('section');
    expect(itemsSection).not.toBeNull();
    const adjustButtons = within(itemsSection as HTMLElement).getAllByRole('button', { name: /adjust stock/i });
    await user.click(adjustButtons[0]);

    const dialog = await screen.findByRole('dialog', { name: /adjust stock dialog/i });
    await waitFor(() => expect(scrollIntoViewSpy).toHaveBeenCalled());
    await waitFor(() => expect(within(dialog).getByLabelText(/quantity/i)).toHaveFocus());
    restore();
  });

  it('shows stock filters and the recently adjusted KPI', async () => {
    const items = [
      makeItem({
        inventoryItemId: 'item-1',
        name: 'Rice',
        category: 'Grains',
        currentStock: 3,
        lowStockThreshold: 5,
        status: 'Low stock',
      }),
      makeItem({
        inventoryItemId: 'item-2',
        name: 'Oil',
        category: 'Cooking',
        unitOfMeasure: 'l',
        currentStock: 0,
        lowStockThreshold: 5,
        status: 'Out of stock',
      }),
    ];
    const summary = { ...makeSummary(items), recentlyAdjustedCount: 2 };
    const fetchMock = stubInventoryBootstrap([makeBranch()], items, [], summary);
    renderInventoryRoute(['Inventory.View', 'Inventory.Adjust', 'Branch.Manage'], fetchMock);

    expect(await screen.findByRole('heading', { name: /inventory workspace/i })).toBeInTheDocument();
    const filtersCard = screen.getByRole('heading', { name: /filters/i }).closest('section');
    expect(filtersCard).not.toBeNull();
    expect(within(filtersCard as HTMLElement).getByLabelText(/search item/i)).toBeInTheDocument();
    expect(within(filtersCard as HTMLElement).getByLabelText(/category/i)).toBeInTheDocument();
    expect(within(filtersCard as HTMLElement).getByLabelText(/stock status/i)).toBeInTheDocument();
    const recentlyAdjustedCard = screen.getByText(/recently adjusted/i).closest('section');
    expect(recentlyAdjustedCard).not.toBeNull();
    expect(within(recentlyAdjustedCard as HTMLElement).getByText('2')).toBeInTheDocument();
  });

  it('renders the batch production and wastage controls', async () => {
    const items = [makeItem({ inventoryItemId: 'item-1', name: 'Prepared Idli Stock', category: 'Prepared', currentStock: 20, status: 'In stock' })];
    const menuItems = [makeMenuItem({ menuItemId: 'menu-1', name: 'Idli', stockInventoryItemName: 'Prepared Idli Stock' })];
    const batchProductions = [makeBatchProduction({ menuItemName: 'Idli', preparedInventoryItemName: 'Prepared Idli Stock' })];
    const fetchMock = stubInventoryBootstrap([makeBranch()], items, [], makeSummary(items), menuItems, batchProductions);
    renderInventoryRoute(['Inventory.View', 'Inventory.Adjust', 'MenuItem.View', 'Branch.Manage'], fetchMock);

    expect(await screen.findByRole('heading', { name: /inventory workspace/i })).toBeInTheDocument();
    const batchCard = screen.getByRole('heading', { name: /batch production/i }).closest('section');
    expect(batchCard).not.toBeNull();
    const batchScope = within(batchCard as HTMLElement);
    expect(batchScope.getByRole('button', { name: /record production/i })).toBeInTheDocument();
    expect(batchScope.getByRole('button', { name: /record wastage/i })).toBeInTheDocument();
    expect(batchScope.getByRole('cell', { name: /prepared idli stock/i })).toBeInTheDocument();
    expect(batchScope.getByRole('cell', { name: /morning batch/i })).toBeInTheDocument();
  });

  it('filters the inventory list by item name category and stock status', async () => {
    const items = [
      makeItem({
        inventoryItemId: 'item-1',
        name: 'Rice',
        category: 'Grains',
        currentStock: 6,
        status: 'In stock',
      }),
      makeItem({
        inventoryItemId: 'item-2',
        name: 'Oil',
        category: 'Cooking',
        unitOfMeasure: 'l',
        currentStock: 0,
        status: 'Out of stock',
      }),
    ];
    const fetchMock = stubInventoryBootstrap([makeBranch()], items, [], makeSummary(items));
    renderInventoryRoute(['Inventory.View', 'Inventory.Adjust', 'Branch.Manage'], fetchMock);
    const user = userEvent.setup();

    expect(await screen.findByRole('heading', { name: /inventory workspace/i })).toBeInTheDocument();
    const itemsSection = screen.getByRole('heading', { name: /inventory items/i }).closest('section');
    expect(itemsSection).not.toBeNull();
    const itemsTableScope = within(itemsSection as HTMLElement);
    expect(itemsTableScope.getByRole('cell', { name: 'Rice' })).toBeInTheDocument();
    expect(itemsTableScope.getByRole('cell', { name: 'Oil' })).toBeInTheDocument();

    const filtersCard = screen.getByRole('heading', { name: /filters/i }).closest('section');
    expect(filtersCard).not.toBeNull();
    const filterScope = within(filtersCard as HTMLElement);

    await user.type(filterScope.getByLabelText(/search item/i), 'Oil');
    expect(itemsTableScope.queryByRole('cell', { name: 'Rice' })).not.toBeInTheDocument();
    expect(itemsTableScope.getByRole('cell', { name: 'Oil' })).toBeInTheDocument();

    await user.selectOptions(filterScope.getByLabelText(/category/i), 'Cooking');
    await user.selectOptions(filterScope.getByLabelText(/stock status/i), 'Out of stock');
    expect(itemsTableScope.queryByRole('cell', { name: 'Rice' })).not.toBeInTheDocument();
    expect(itemsTableScope.getByRole('cell', { name: 'Oil' })).toBeInTheDocument();
  });

  it('opens the adjustment dialog and previews the new quantity', async () => {
    const item = makeItem({ currentStock: 8, status: 'In stock', lowStockThreshold: 5 });
    const fetchMock = stubInventoryBootstrap([makeBranch()], [item], [], makeSummary([item]));
    renderInventoryRoute(['Inventory.View', 'Inventory.Adjust', 'Branch.Manage'], fetchMock);
    const user = userEvent.setup();

    expect(await screen.findByRole('heading', { name: /inventory workspace/i })).toBeInTheDocument();
    const itemsSection = screen.getByRole('heading', { name: /inventory items/i }).closest('section');
    expect(itemsSection).not.toBeNull();
    const adjustButtons = within(itemsSection as HTMLElement).getAllByRole('button', { name: /adjust stock/i });
    await user.click(adjustButtons[0]);

    const dialog = await screen.findByRole('dialog', { name: /adjust stock dialog/i });
    expect(within(dialog).getByText(/rice/i)).toBeInTheDocument();

    const currentQuantityCard = within(dialog).getByText(/current quantity/i).closest('section');
    const previewCard = within(dialog).getByText(/preview new quantity/i).closest('section');
    expect(currentQuantityCard).not.toBeNull();
    expect(previewCard).not.toBeNull();
    expect(within(currentQuantityCard as HTMLElement).getByText('8')).toBeInTheDocument();

    await user.selectOptions(within(dialog).getByLabelText(/adjustment type/i), 'Decrease');
    await user.clear(within(dialog).getByLabelText(/quantity/i));
    await user.type(within(dialog).getByLabelText(/quantity/i), '3');

    expect(within(previewCard as HTMLElement).getByText('5')).toBeInTheDocument();
  });

  it('rejects invalid adjustment input before submit', async () => {
    const item = makeItem({ currentStock: 8, status: 'In stock' });
    const fetchMock = stubInventoryBootstrap([makeBranch()], [item], [], makeSummary([item]));
    renderInventoryRoute(['Inventory.View', 'Inventory.Adjust', 'Branch.Manage'], fetchMock);
    const user = userEvent.setup();

    expect(await screen.findByRole('heading', { name: /inventory workspace/i })).toBeInTheDocument();
    const itemsSection = screen.getByRole('heading', { name: /inventory items/i }).closest('section');
    expect(itemsSection).not.toBeNull();
    const adjustButtons = within(itemsSection as HTMLElement).getAllByRole('button', { name: /adjust stock/i });
    await user.click(adjustButtons[0]);

    const dialog = await screen.findByRole('dialog', { name: /adjust stock dialog/i });
    await user.clear(within(dialog).getByLabelText(/quantity/i));
    await user.type(within(dialog).getByLabelText(/quantity/i), '0');

    expect(within(dialog).getByRole('button', { name: /^confirm adjustment$/i })).toBeDisabled();
    expect(within(dialog).getAllByText(/enter a quantity/i).length).toBeGreaterThan(0);
  });

  it('refreshes the stock row and history after a successful adjustment', async () => {
    const initialItem = makeItem({ currentStock: 5, status: 'In stock', lowStockThreshold: 5 });
    const updatedItem = makeItem({ currentStock: 8, status: 'In stock', lowStockThreshold: 5 });
    const movement = makeMovement({
      movementType: 'AdjustmentIncrease',
      quantity: 3,
      reason: 'Manual purchase entry',
      previousStock: 5,
      delta: 3,
      resultingStock: 8,
      resultingStatus: 'In stock',
      notes: 'Bought from market',
    });
    let adjusted = false;
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      const pathname = new URL(url, 'http://localhost').pathname;
      const method = init?.method ?? 'GET';

      if (pathname === '/api/v1/admin/branches') {
        return createJsonResponse({ items: [makeBranch()] });
      }

      if (pathname === '/api/v1/inventory/items' && method === 'GET') {
        return createJsonResponse({ items: adjusted ? [updatedItem] : [initialItem] });
      }

      if (pathname === '/api/v1/inventory/summary') {
        return createJsonResponse(adjusted ? makeSummary([updatedItem]) : makeSummary([initialItem]));
      }

      if (pathname.match(/\/api\/v1\/inventory\/items\/[^/]+\/movements$/) && method === 'GET') {
        return createJsonResponse({ items: adjusted ? [movement] : [] });
      }

      if (pathname.match(/\/api\/v1\/inventory\/items\/[^/]+\/movements$/) && method === 'POST') {
        adjusted = true;
        return createJsonResponse(movement);
      }

      throw new Error(`Unexpected fetch in adjustment test: ${method} ${url}`);
    });
    renderInventoryRoute(['Inventory.View', 'Inventory.Adjust', 'Branch.Manage'], fetchMock);
    const user = userEvent.setup();

    expect(await screen.findByRole('heading', { name: /inventory workspace/i })).toBeInTheDocument();
    const itemsSection = screen.getByRole('heading', { name: /inventory items/i }).closest('section');
    expect(itemsSection).not.toBeNull();
    const adjustButtons = within(itemsSection as HTMLElement).getAllByRole('button', { name: /adjust stock/i });
    await user.click(adjustButtons[0]);
    const dialog = await screen.findByRole('dialog', { name: /adjust stock dialog/i });
    await user.selectOptions(within(dialog).getByLabelText(/adjustment type/i), 'Increase');
    await user.clear(within(dialog).getByLabelText(/quantity/i));
    await user.type(within(dialog).getByLabelText(/quantity/i), '3');
    await user.selectOptions(within(dialog).getByLabelText(/reason/i), 'Manual purchase entry');
    await user.click(within(dialog).getByRole('button', { name: /^confirm adjustment$/i }));

    await waitFor(() => expect(screen.queryByRole('dialog', { name: /adjust stock dialog/i })).not.toBeInTheDocument());
    await waitFor(() => expect(screen.getAllByRole('cell', { name: '8' }).length).toBeGreaterThan(0));
    const historySection = screen.getByRole('heading', { name: /movement history/i }).closest('section');
    expect(historySection).not.toBeNull();
    const historyTable = within(historySection as HTMLElement).getByRole('table');
    expect(within(historyTable).getByRole('cell', { name: /manual purchase entry/i })).toBeInTheDocument();
  });

  it('hides adjustment actions for inventory-view-only users', async () => {
    const item = makeItem({ currentStock: 6, status: 'In stock' });
    const fetchMock = stubInventoryBootstrap([makeBranch()], [item], [], makeSummary([item]));
    renderInventoryRoute(['Inventory.View'], fetchMock);

    expect(await screen.findByRole('heading', { name: /inventory workspace/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /adjust stock/i })).not.toBeInTheDocument();
  });

  it('renders low-stock and out-of-stock statuses', async () => {
    const lowStockItem = makeItem({
      inventoryItemId: 'item-1',
      name: 'Rice',
      currentStock: 3,
      lowStockThreshold: 5,
      status: 'Low stock',
    });
    const outOfStockItem = makeItem({
      inventoryItemId: 'item-2',
      name: 'Oil',
      category: 'Cooking',
      unitOfMeasure: 'l',
      currentStock: 0,
      lowStockThreshold: 5,
      status: 'Out of stock',
    });
    const fetchMock = stubInventoryBootstrap([makeBranch()], [lowStockItem, outOfStockItem], []);
    renderInventoryRoute(['Inventory.View', 'Inventory.Adjust', 'Branch.Manage'], fetchMock);

    expect(await screen.findByRole('heading', { name: /inventory workspace/i })).toBeInTheDocument();
    expect(screen.getAllByText(/low stock/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/out of stock/i).length).toBeGreaterThan(0);
  });

  it('renders movement history for the selected item', async () => {
    const item = makeItem({ currentStock: 10, status: 'In stock' });
    const movements = [
      makeMovement({ inventoryMovementId: 'movement-1', resultingStock: 5, quantity: 5 }),
      makeMovement({
        inventoryMovementId: 'movement-2',
        movementType: 'AdjustmentIncrease',
        quantity: 5,
        resultingStock: 10,
        resultingStatus: 'In stock',
      }),
    ];
    const fetchMock = stubInventoryBootstrap([makeBranch()], [item], movements, makeSummary([item]));
    renderInventoryRoute(['Inventory.View', 'Inventory.Adjust', 'Branch.Manage'], fetchMock);

    expect(await screen.findByRole('heading', { name: /inventory workspace/i })).toBeInTheDocument();
    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(5));
    const historySection = screen.getByRole('heading', { name: /rice movement history/i }).closest('section');
    expect(historySection).not.toBeNull();
    const historyTable = within(historySection as HTMLElement).getByRole('table');
    expect(within(historyTable).getByRole('columnheader', { name: /new qty/i })).toBeInTheDocument();
  });

  it('does not expose OCR or vendor bill controls', async () => {
    const fetchMock = stubInventoryBootstrap([makeBranch()], []);
    renderInventoryRoute(['Inventory.View', 'Inventory.Adjust', 'Branch.Manage'], fetchMock);

    expect(await screen.findByRole('heading', { name: /inventory workspace/i })).toBeInTheDocument();
    expect(screen.queryByText(/ocr/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/vendor bill/i)).not.toBeInTheDocument();
  });
});
