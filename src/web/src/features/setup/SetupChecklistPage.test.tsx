import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import App from '../../App';
import { clearAuthSession, createJsonResponse, storeAuthSession } from '../../test/authTestUtils';
import { renderWithRouter } from '../../test/renderWithRouter';

const buildChecklistData = () => ({
    restaurantId: 'restaurant-1',
    restaurantName: 'Demo Restaurant',
    branchId: 'branch-1',
    branchName: 'Main Branch',
    businessType: 'Restaurant',
    completionPercent: 50,
    completedCount: 5,
    totalCount: 10,
    items: [
      {
        key: 'restaurantProfile',
        title: 'Restaurant profile ready',
        description: 'Confirm the restaurant name and code before pilot usage.',
        status: 'Complete',
        priority: 'Required',
        actionLabel: 'View setup',
        actionHref: '/owner/dashboard',
        count: 1,
        warningCount: null,
      },
      {
        key: 'branchCreated',
        title: 'Branch created',
        description: 'At least one active branch is available for pilot usage.',
        status: 'Complete',
        priority: 'Required',
        actionLabel: 'Add branch',
        actionHref: '/admin/branches',
        count: 2,
        warningCount: null,
      },
      {
        key: 'staffUsersAdded',
        title: 'Staff users added',
        description: 'Active users are ready for restaurant operations.',
        status: 'Warning',
        priority: 'Recommended',
        actionLabel: 'Add users',
        actionHref: '/admin/users',
        count: 1,
        warningCount: 1,
      },
      {
        key: 'menuCategoriesAdded',
        title: 'Menu categories added',
        description: 'Add at least one active menu category before loading items.',
        status: 'Complete',
        priority: 'Required',
        actionLabel: 'Add menu',
        actionHref: '/admin/menu',
        count: 3,
        warningCount: null,
      },
      {
        key: 'menuItemsAdded',
        title: 'Menu items added',
        description: 'Create active menu items so POS orders and billing can use the catalog.',
        status: 'Complete',
        priority: 'Required',
        actionLabel: 'Add menu',
        actionHref: '/admin/menu',
        count: 12,
        warningCount: null,
      },
      {
        key: 'inventoryItemsAdded',
        title: 'Inventory items added',
        description: 'Add at least one active inventory item for the selected branch.',
        status: 'Missing',
        priority: 'Recommended',
        actionLabel: 'Add inventory',
        actionHref: '/inventory',
        count: 0,
        warningCount: null,
      },
      {
        key: 'recipesOrStockMappingsConfigured',
        title: 'Recipes or stock mappings configured',
        description: 'Some menu items still need recipe or stock mappings before pilot usage.',
        status: 'Warning',
        priority: 'Recommended',
        actionLabel: 'Add menu',
        actionHref: '/admin/menu',
        count: 4,
        warningCount: 3,
      },
      {
        key: 'vendorsAdded',
        title: 'Vendors added',
        description: 'Add at least one active vendor before recording purchases or settlements.',
        status: 'Complete',
        priority: 'Recommended',
        actionLabel: 'Add vendors',
        actionHref: '/vendors',
        count: 2,
        warningCount: null,
      },
      {
        key: 'firstPosOrderCompleted',
        title: 'First test POS order completed',
        description: 'POS drafts exist, but no order has been confirmed yet.',
        status: 'Warning',
        priority: 'Required',
        actionLabel: 'Create test order',
        actionHref: '/pos/orders',
        count: 0,
        warningCount: 1,
      },
      {
        key: 'firstBillPaymentCompleted',
        title: 'First bill/payment completed',
        description: 'Bills exist, but no payment has been recorded yet.',
        status: 'Warning',
        priority: 'Required',
        actionLabel: 'Complete first bill',
        actionHref: '/billing',
        count: 0,
        warningCount: 1,
      },
    ],
  });

const buildChecklistResponse = () => createJsonResponse(buildChecklistData());

afterEach(() => {
  clearAuthSession();
  vi.unstubAllGlobals();
});

describe('SetupChecklistPage', () => {
  it('renders the business type as read only for Report.View users', async () => {
    storeAuthSession({
      permissions: ['Report.View'],
      roles: ['RestaurantOwner'],
      activeRole: 'RestaurantOwner',
      branchId: 'branch-1',
    });
    let updateCalled = false;
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      const pathname = new URL(url, 'http://localhost').pathname;
      const method = init?.method ?? 'GET';

      if (pathname === '/api/v1/setup/business-type' && method === 'PUT') {
        updateCalled = true;
        return createJsonResponse(undefined, { status: 204 });
      }

      return buildChecklistResponse();
    });
    vi.stubGlobal('fetch', fetchMock);

    renderWithRouter(<App />, '/setup');

    expect(await screen.findByRole('heading', { name: /setup checklist/i })).toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: /business type/i })).not.toBeInTheDocument();
    expect(screen.getByText(/profile: restaurant/i)).toBeInTheDocument();
    expect(screen.getByText(/^Read only$/i)).toBeInTheDocument();
    await userEvent.click(screen.getByText(/^Read only$/i));
    expect(screen.getByText('50%')).toBeInTheDocument();
    expect(screen.getByText('5 / 10')).toBeInTheDocument();
    expect(screen.getAllByText('Complete').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Missing').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Warning').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Required').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Recommended').length).toBeGreaterThan(0);
    expect(screen.getByRole('link', { name: /add branch/i })).toHaveAttribute('href', '/admin/branches');
    expect(screen.getByRole('link', { name: /add users/i })).toHaveAttribute('href', '/admin/users');
    expect(screen.getAllByRole('link', { name: /add menu/i })).toHaveLength(3);
    screen.getAllByRole('link', { name: /add menu/i }).forEach(link => {
      expect(link).toHaveAttribute('href', '/admin/menu');
    });
    expect(screen.getByRole('link', { name: /add inventory/i })).toHaveAttribute('href', '/inventory');
    expect(screen.getByRole('link', { name: /add vendors/i })).toHaveAttribute('href', '/vendors');
    expect(screen.getByRole('link', { name: /create test order/i })).toHaveAttribute('href', '/pos/orders');
    expect(screen.getByRole('link', { name: /complete first bill/i })).toHaveAttribute('href', '/billing');
    expect(updateCalled).toBe(false);
  });

  it('saves the selected business type and reloads the checklist for Branch.Manage users', async () => {
    const user = userEvent.setup();
    const checklistResponse = buildChecklistData();
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      const pathname = new URL(url, 'http://localhost').pathname;
      const method = init?.method ?? 'GET';

      if (pathname === '/api/v1/setup/checklist' && method === 'GET') {
        return createJsonResponse(JSON.parse(JSON.stringify(checklistResponse)));
      }

      if (pathname === '/api/v1/setup/business-type' && method === 'PUT') {
        checklistResponse.businessType = 'JuiceShop';
        return createJsonResponse(undefined, { status: 204 });
      }

      throw new Error(`Unexpected fetch in setup checklist test: ${method} ${url}`);
    });

    storeAuthSession({
      permissions: ['Branch.Manage'],
      roles: ['RestaurantOwner'],
      activeRole: 'RestaurantOwner',
      branchId: 'branch-1',
    });
    vi.stubGlobal('fetch', fetchMock);

    renderWithRouter(<App />, '/setup');

    const selector = await screen.findByRole('combobox', { name: /business type/i });
    await user.selectOptions(selector, 'JuiceShop');

    expect(await screen.findByText(/setup profile saved/i)).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([request]) => String(request).includes('/api/v1/setup/business-type'))).toBe(true);
    expect(fetchMock.mock.calls.filter(([request]) => String(request).includes('/api/v1/setup/checklist')).length).toBeGreaterThanOrEqual(2);
  });

  it('saves the selected business type and reloads the checklist for User.Manage users', async () => {
    const user = userEvent.setup();
    const checklistResponse = buildChecklistData();
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      const pathname = new URL(url, 'http://localhost').pathname;
      const method = init?.method ?? 'GET';

      if (pathname === '/api/v1/setup/checklist' && method === 'GET') {
        return createJsonResponse(JSON.parse(JSON.stringify(checklistResponse)));
      }

      if (pathname === '/api/v1/setup/business-type' && method === 'PUT') {
        checklistResponse.businessType = 'Bakery';
        return createJsonResponse(undefined, { status: 204 });
      }

      throw new Error(`Unexpected fetch in setup checklist test: ${method} ${url}`);
    });

    storeAuthSession({
      permissions: ['User.Manage'],
      roles: ['RestaurantOwner'],
      activeRole: 'RestaurantOwner',
      branchId: 'branch-1',
    });
    vi.stubGlobal('fetch', fetchMock);

    renderWithRouter(<App />, '/setup');

    const selector = await screen.findByRole('combobox', { name: /business type/i });
    await user.selectOptions(selector, 'Bakery');

    expect(await screen.findByText(/setup profile saved/i)).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([request]) => String(request).includes('/api/v1/setup/business-type'))).toBe(true);
    expect(fetchMock.mock.calls.filter(([request]) => String(request).includes('/api/v1/setup/checklist')).length).toBeGreaterThanOrEqual(2);
  });

  it('renders loading and session-expired states safely', async () => {
    storeAuthSession({
      permissions: ['Report.View'],
      roles: ['RestaurantOwner'],
      activeRole: 'RestaurantOwner',
      branchId: 'branch-1',
    });

    let resolveFetch!: (value: Response) => void;
    vi.stubGlobal(
      'fetch',
      vi.fn(
        () =>
          new Promise<Response>(resolve => {
            resolveFetch = resolve;
          })
      )
    );

    renderWithRouter(<App />, '/setup');

    expect(screen.getByRole('heading', { name: /loading setup checklist/i })).toBeInTheDocument();

    resolveFetch(
      createJsonResponse(
        {
          title: 'Unauthorized',
          detail: 'Token expired',
        },
        { status: 401 }
      )
    );

    expect(await screen.findByRole('alert')).toHaveTextContent(/your session expired/i);
  });

  it('shows an unauthorized state when the session lacks setup access', async () => {
    storeAuthSession({
      permissions: ['Billing.View'],
      roles: ['Cashier'],
      activeRole: 'Cashier',
      branchId: 'branch-1',
    });

    renderWithRouter(<App />, '/setup');

    expect(await screen.findByRole('heading', { name: /not authorized/i })).toBeInTheDocument();
    expect(screen.getByText(/setup checklist access requires/i)).toBeInTheDocument();
  });
});
