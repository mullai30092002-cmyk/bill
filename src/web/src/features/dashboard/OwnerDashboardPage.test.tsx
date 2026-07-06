import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import App from '../../App';
import { clearAuthSession, createJsonResponse, storeAuthSession } from '../../test/authTestUtils';
import { renderWithRouter } from '../../test/renderWithRouter';
import type { OwnerDashboardResponse } from './ownerDashboardTypes';
import type { SetupChecklistResponse } from '../setup/setupChecklistTypes';

const normalizeText = (value: string | null | undefined) => value?.replace(/\s+/g, ' ').replace(/\u00a0/g, ' ').trim() ?? '';

const makeSetupChecklist = (overrides: Partial<SetupChecklistResponse> = {}): SetupChecklistResponse => ({
  restaurantId: 'restaurant-1',
  restaurantName: 'Demo Restaurant',
  businessType: 'Restaurant',
  branchId: 'branch-1',
  branchName: 'Main Branch',
  completionPercent: 80,
  completedCount: 8,
  totalCount: 10,
  items: [],
  ...overrides,
});

const makeDashboard = (overrides: Partial<OwnerDashboardResponse> = {}): OwnerDashboardResponse => ({
  restaurantId: 'restaurant-1',
  restaurantCode: 'DEMO',
  restaurantName: 'Demo Restaurant',
  branchId: null,
  branchName: null,
  businessDate: '2026-06-13',
  currencyCode: 'SGD',
  generatedAt: '2026-06-13T10:30:00Z',
  metrics: {
    grossSales: 44,
    netSales: 33,
    cashPayments: 10,
    nonCashPayments: 6,
    totalAmountPaid: 16,
    totalBalanceDue: 28,
    unpaidBills: 1,
    cancelledBills: 1,
    cancelledPayments: 1,
    receiptReprints: 1,
    cashVarianceTotal: 5,
    openShifts: 1,
  },
  alerts: [
    {
      type: 'UnpaidBills',
      title: 'Unpaid bills need follow-up',
      message: '28.00 remains unpaid across the selected business date.',
      severity: 'High',
      count: 1,
      amount: 28,
      targetPath: '/reports/daily-cash-sales',
    },
    {
      type: 'CancelledActivity',
      title: 'Cancelled activity recorded',
      message: '2 cancelled bill or payment event(s) were recorded.',
      severity: 'Medium',
      count: 2,
      amount: 17,
      targetPath: '/reports/daily-cash-sales',
    },
    {
      type: 'ReceiptReprints',
      title: 'Receipt reprints detected',
      message: '1 receipt reprint(s) were recorded for the selected date.',
      severity: 'Low',
      count: 1,
      amount: null,
      targetPath: '/reports/daily-cash-sales',
    },
    {
      type: 'CashVariance',
      title: 'Closed shift variance needs review',
      message:
        'Closed shift variance total is 5.00 for the selected date. Counted closing cash minus expected cash (opening cash + recorded cash payments).',
      severity: 'High',
      count: 1,
      amount: 5,
      targetPath: '/cashier/shifts',
    },
    {
      type: 'OpenShift',
      title: 'Open shifts remain active',
      message: '1 shift(s) are still open for the selected business date.',
      severity: 'Medium',
      count: 1,
      amount: null,
      targetPath: '/cashier/shifts',
    },
  ],
  quickLinks: [
    { label: 'Daily Report', path: '/reports/daily-cash-sales', description: 'Open the detailed daily cash sales report.' },
    { label: 'Billing', path: '/billing', description: 'Review bills, payments, and receipts.' },
    { label: 'Cashier Shifts', path: '/cashier/shifts', description: 'Inspect shift status and cash control.' },
    { label: 'Kitchen Tickets', path: '/kitchen/tickets', description: 'Jump to kitchen ticket workflow.' },
    { label: 'POS Orders', path: '/pos/orders', description: 'Open the order capture workspace.' },
  ],
  vendorDues: {
    totalVendorOutstanding: 0,
    overdueVendorCount: 0,
    vendorsWithOutstandingCount: 0,
    criticalVendors: [],
  },
  inventoryAlerts: {
    lowStockCount: 2,
    outOfStockCount: 1,
    totalAlertCount: 3,
    criticalItems: [
      {
        inventoryItemId: 'inventory-1',
        name: 'Butter',
        category: 'Dairy',
        unit: 'kg',
        currentQuantity: 0,
        minimumQuantity: 5,
        status: 'Out of stock',
        lastUpdatedAt: '2026-06-12T08:00:00Z',
      },
      {
        inventoryItemId: 'inventory-2',
        name: 'Rice',
        category: 'Grains',
        unit: 'kg',
        currentQuantity: 3,
        minimumQuantity: 10,
        status: 'Low stock',
        lastUpdatedAt: '2026-06-12T08:00:00Z',
      },
      {
        inventoryItemId: 'inventory-3',
        name: 'Oil',
        category: 'Cooking',
        unit: 'l',
        currentQuantity: 4,
        minimumQuantity: 10,
        status: 'Low stock',
        lastUpdatedAt: null,
      },
    ],
  },
  ...overrides,
});

const createOwnerDashboardFetchMock = ({
  dashboardResponse = createJsonResponse(makeDashboard()),
  setupResponse = createJsonResponse(makeSetupChecklist()),
  setupStatus = 200,
}: {
  dashboardResponse?: Response;
  setupResponse?: Response;
  setupStatus?: number;
} = {}) =>
  vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    const pathname = new URL(url, 'http://localhost').pathname;
    const method = init?.method ?? 'GET';

    if (pathname === '/api/v1/dashboard/owner' && method === 'GET') {
      return dashboardResponse;
    }

    if (pathname === '/api/v1/setup/checklist' && method === 'GET') {
      if (setupStatus >= 400) {
        return setupResponse;
      }

      return setupResponse;
    }

    throw new Error(`Unexpected fetch in owner dashboard test: ${method} ${url}`);
  });

const renderOwnerDashboardRoute = (
  permissions: string[],
  fetchMock: ReturnType<typeof vi.fn>,
  path = '/owner/dashboard',
  sessionOverrides: Record<string, unknown> = {}
) => {
  clearAuthSession();
  storeAuthSession({
    permissions,
    roles: ['Admin'],
    activeRole: 'Admin',
    ...sessionOverrides,
  });
  vi.stubGlobal('fetch', fetchMock);
  return renderWithRouter(<App />, path);
};

describe('OwnerDashboardPage', () => {
  beforeEach(() => {
    clearAuthSession();
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it('shows a not-authorized state without calling the dashboard API', async () => {
    const fetchMock = vi.fn();
    renderOwnerDashboardRoute([], fetchMock);

    expect(await screen.findByRole('heading', { name: /not authorized/i })).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('loads the default business date and dashboard data for Report.View users', async () => {
    const fetchMock = createOwnerDashboardFetchMock();
    renderOwnerDashboardRoute(['Report.View'], fetchMock);

    expect(await screen.findByRole('heading', { name: /owner dashboard/i })).toBeInTheDocument();
    const businessDateInput = await screen.findByLabelText(/business date/i);

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalled();
    });

    const firstCall = fetchMock.mock.calls[0];
    expect(String(firstCall[0])).toContain('/api/v1/dashboard/owner');
    expect(String(firstCall[0])).toContain(`date=${(businessDateInput as HTMLInputElement).value}`);
    const main = await screen.findByRole('main');
    expect(within(main).getByText(/^Net sales$/i)).toBeInTheDocument();
    expect(within(main).getByText(/^Cash payments$/i)).toBeInTheDocument();
    expect(within(main).getByText(/^Unpaid balance$/i)).toBeInTheDocument();
    expect(within(main).getByRole('heading', { name: /inventory alerts/i })).toBeInTheDocument();
    expect(await within(main).findByRole('heading', { name: /pilot setup/i })).toBeInTheDocument();
  });

  it('reloads when the business date changes', async () => {
    const fetchMock = createOwnerDashboardFetchMock({
      dashboardResponse: createJsonResponse(makeDashboard({ businessDate: '2026-06-13' })),
    });

    renderOwnerDashboardRoute(['Report.View'], fetchMock);

    const businessDateInput = await screen.findByLabelText(/business date/i);
    await waitFor(() => expect(fetchMock.mock.calls.length).toBeGreaterThanOrEqual(2));

    await userEvent.clear(businessDateInput);
    await userEvent.type(businessDateInput, '2026-06-14');

    await waitFor(() => expect(fetchMock.mock.calls.length).toBeGreaterThanOrEqual(2));
    const lastCall = fetchMock.mock.calls[fetchMock.mock.calls.length - 1];
    expect(String(lastCall?.[0])).toContain('date=2026-06-14');
  });

  it('renders alerts, severity badges, and quick links', async () => {
    const fetchMock = createOwnerDashboardFetchMock();
    renderOwnerDashboardRoute(['Report.View'], fetchMock);

    const main = await screen.findByRole('main');

    expect(within(main).getByText(/unpaid bills need follow-up/i)).toBeInTheDocument();
    expect(within(main).getByText(/cancelled activity recorded/i)).toBeInTheDocument();
    expect(within(main).getByText(/receipt reprints detected/i)).toBeInTheDocument();
    expect(within(main).getByText(/closed shift variance needs review/i)).toBeInTheDocument();
    expect(within(main).getByText(/open shifts remain active/i)).toBeInTheDocument();
    const openShiftsCard = within(main).getByText('Open shifts').closest('.summary-card');
    expect(openShiftsCard).not.toBeNull();
    expect(within(openShiftsCard as HTMLElement).getByText('1')).toBeInTheDocument();

    const varianceCard = within(main).getByText('Closed shift variance').closest('.summary-card');
    expect(varianceCard).not.toBeNull();
    expect(within(varianceCard as HTMLElement).getByText(/counted closing cash minus expected cash/i)).toBeInTheDocument();

    expect(within(main).getAllByText(/high/i).length).toBeGreaterThan(0);
    expect(within(main).getAllByText(/medium/i).length).toBeGreaterThan(0);
    expect(within(main).getAllByText(/low/i).length).toBeGreaterThan(0);
    const inventoryCard = within(main).getByRole('heading', { name: /inventory alerts/i }).closest('section');
    expect(inventoryCard).not.toBeNull();
    const inventoryScope = within(inventoryCard as HTMLElement);
    expect(inventoryScope.getByText(/total inventory alerts/i)).toBeInTheDocument();
    expect(inventoryScope.getAllByText(/out of stock/i).length).toBeGreaterThan(0);
    expect(inventoryScope.getAllByText(/low stock/i).length).toBeGreaterThan(0);
    expect(inventoryScope.getAllByRole('cell', { name: /butter/i }).length).toBeGreaterThan(0);
    expect(inventoryScope.getAllByRole('cell', { name: /rice/i }).length).toBeGreaterThan(0);
    expect(inventoryScope.getAllByRole('cell', { name: /oil/i }).length).toBeGreaterThan(0);

    expect(within(main).getByRole('button', { name: /daily report/i })).toBeInTheDocument();
    expect(within(main).getByRole('button', { name: /billing/i })).toBeInTheDocument();
    expect(within(main).getByRole('button', { name: /cashier shifts/i })).toBeInTheDocument();
    expect(within(main).getByRole('button', { name: /kitchen tickets/i })).toBeInTheDocument();
    expect(within(main).getByRole('button', { name: /pos orders/i })).toBeInTheDocument();
    expect(within(main).getByRole('button', { name: /view inventory/i })).toBeInTheDocument();
  });

  it('renders inventory alert counts and critical items', async () => {
    const fetchMock = createOwnerDashboardFetchMock();
    renderOwnerDashboardRoute(['Report.View'], fetchMock);

    const main = await screen.findByRole('main');
    const inventoryCard = within(main).getByRole('heading', { name: /inventory alerts/i }).closest('section');
    expect(inventoryCard).not.toBeNull();

    const inventoryScope = within(inventoryCard as HTMLElement);
    const totalAlertCard = inventoryScope.getByText(/total inventory alerts/i).closest('.summary-card');
    expect(totalAlertCard).not.toBeNull();
    expect(normalizeText(totalAlertCard?.querySelector('.summary-card__value')?.textContent)).toBe('3');
    expect(inventoryScope.getAllByRole('cell', { name: /butter/i }).length).toBeGreaterThan(0);
    expect(inventoryScope.getAllByRole('cell', { name: /rice/i }).length).toBeGreaterThan(0);
    expect(inventoryScope.getAllByRole('cell', { name: /oil/i }).length).toBeGreaterThan(0);
    expect(inventoryScope.getAllByText(/out of stock/i).length).toBeGreaterThan(0);
    expect(inventoryScope.getAllByText(/low stock/i).length).toBeGreaterThan(0);
  });

  it('navigates to inventory with a stock status filter from the dashboard', async () => {
    const fetchMock = createOwnerDashboardFetchMock({
      dashboardResponse: createJsonResponse(
        makeDashboard({
          inventoryAlerts: {
            lowStockCount: 1,
            outOfStockCount: 1,
            totalAlertCount: 2,
            criticalItems: [
              {
                inventoryItemId: 'inventory-1',
                name: 'Butter',
                category: 'Dairy',
                unit: 'kg',
                currentQuantity: 0,
                minimumQuantity: 5,
                status: 'Out of stock',
                lastUpdatedAt: '2026-06-12T08:00:00Z',
              },
              {
                inventoryItemId: 'inventory-2',
                name: 'Rice',
                category: 'Grains',
                unit: 'kg',
                currentQuantity: 3,
                minimumQuantity: 10,
                status: 'Low stock',
                lastUpdatedAt: '2026-06-12T08:00:00Z',
              },
            ],
          },
        })
      ),
    });
    const fetchImpl = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      const pathname = new URL(url, 'http://localhost').pathname;
      const method = init?.method ?? 'GET';

      if (pathname === '/api/v1/setup/checklist' && method === 'GET') {
        return createJsonResponse(makeSetupChecklist());
      }

      if (pathname === '/api/v1/inventory/items' && method === 'GET') {
        return createJsonResponse({
          items: [
            {
              inventoryItemId: 'inventory-1',
              restaurantId: 'restaurant-1',
              branchId: 'branch-1',
              name: 'Butter',
              normalizedName: 'BUTTER',
              category: 'Dairy',
              unitOfMeasure: 'kg',
              lowStockThreshold: 5,
              isActive: true,
              currentStock: 0,
              status: 'Out of stock',
              createdAtUtc: '2026-06-12T08:00:00Z',
              updatedAtUtc: '2026-06-12T08:00:00Z',
            },
            {
              inventoryItemId: 'inventory-2',
              restaurantId: 'restaurant-1',
              branchId: 'branch-1',
              name: 'Rice',
              normalizedName: 'RICE',
              category: 'Grains',
              unitOfMeasure: 'kg',
              lowStockThreshold: 10,
              isActive: true,
              currentStock: 3,
              status: 'Low stock',
              createdAtUtc: '2026-06-12T08:00:00Z',
              updatedAtUtc: '2026-06-12T08:00:00Z',
            },
          ],
        });
      }

      if (pathname === '/api/v1/inventory/summary' && method === 'GET') {
        return createJsonResponse({
          restaurantId: 'restaurant-1',
          branchId: 'branch-1',
          totalItems: 2,
          activeItems: 2,
          inactiveItems: 0,
          lowStockCount: 1,
          outOfStockCount: 1,
          totalCurrentStock: 3,
          recentlyAdjustedCount: 0,
          lowStockItems: [],
          outOfStockItems: [],
        });
      }

      if (pathname.match(/\/api\/v1\/inventory\/items\/[^/]+\/movements$/) && method === 'GET') {
        return createJsonResponse({ items: [] });
      }

      return fetchMock(input, init);
    });

    renderOwnerDashboardRoute(['Report.View', 'Inventory.View'], fetchImpl, '/owner/dashboard', {
      branchId: 'branch-1',
    });
    const user = userEvent.setup();

    expect(await screen.findByRole('heading', { name: /owner dashboard/i })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /view inventory/i }));

    expect(await screen.findByRole('heading', { name: /inventory workspace/i })).toBeInTheDocument();
    const statusSelect = screen.getByLabelText(/stock status/i) as HTMLSelectElement;
    expect(statusSelect.value).toBe('Out of stock');
    expect(screen.getByRole('cell', { name: /butter/i })).toBeInTheDocument();
  });

  it('renders zero metrics and no alerts when the dashboard payload is empty', async () => {
    const fetchMock = createOwnerDashboardFetchMock({
      dashboardResponse: createJsonResponse(
        makeDashboard({
          metrics: {
            grossSales: 0,
            netSales: 0,
            cashPayments: 0,
            nonCashPayments: 0,
            totalAmountPaid: 0,
            totalBalanceDue: 0,
            unpaidBills: 0,
            cancelledBills: 0,
            cancelledPayments: 0,
            receiptReprints: 0,
            cashVarianceTotal: 0,
            openShifts: 0,
          },
          alerts: [],
          inventoryAlerts: {
            lowStockCount: 0,
            outOfStockCount: 0,
            totalAlertCount: 0,
            criticalItems: [],
          },
          quickLinks: [],
        })
      ),
    });

    renderOwnerDashboardRoute(['Report.View'], fetchMock);

    const main = await screen.findByRole('main');
    const summaryCards = Array.from(main.querySelectorAll('.summary-card'));
    expect(summaryCards).toHaveLength(14);

    const expectations = new Map<string, string>([
      ['Net sales', '0.00'],
      ['Cash payments', '0.00'],
      ['Unpaid balance', '0.00'],
      ['Cancelled activity', '0'],
      ['Receipt reprints', '0'],
      ['Closed shift variance', '0.00'],
      ['Open shifts', '0'],
      ['Total outstanding', '0.00'],
      ['Vendors with dues', '0'],
      ['Setup progress', '80%'],
      ['Completed steps', '8 / 10'],
      ['Total inventory alerts', '0'],
      ['Out of stock', '0'],
      ['Low stock', '0'],
    ]);

    for (const [label, expectedValue] of expectations) {
      const card = summaryCards.find(node => node.querySelector('.summary-card__label')?.textContent === label);
      expect(card).toBeTruthy();
      expect(normalizeText(card?.querySelector('.summary-card__value')?.textContent)).toContain(expectedValue);
    }

    expect(within(main).getByRole('heading', { name: /no active alerts/i })).toBeInTheDocument();
    expect(within(main).getByRole('heading', { name: /no inventory alerts/i })).toBeInTheDocument();
    expect(within(main).queryByRole('button', { name: /daily report/i })).not.toBeInTheDocument();
  });

  it('renders the pilot setup card with progress, completed count, and a branch-scoped setup link', async () => {
    const fetchMock = createOwnerDashboardFetchMock({
      setupResponse: createJsonResponse(
        makeSetupChecklist({
          branchName: 'Main Branch',
          completionPercent: 80,
          completedCount: 8,
          totalCount: 10,
        })
      ),
    });

    renderOwnerDashboardRoute(['Report.View'], fetchMock);

    const main = await screen.findByRole('main');
    const setupCard = within(main).getByRole('heading', { name: /pilot setup/i }).closest('section');
    expect(setupCard).not.toBeNull();

    const setupScope = within(setupCard as HTMLElement);
    expect(setupScope.getByText(/complete setup steps before pilot usage/i)).toBeInTheDocument();
    expect(setupScope.getByText('80%')).toBeInTheDocument();
    expect(normalizeText(setupCard?.querySelectorAll('.summary-card__detail')[0]?.textContent)).toBe('8 / 10');
    expect(normalizeText(setupCard?.querySelectorAll('.summary-card__detail')[1]?.textContent)).toBe('10');
    expect(setupScope.getByText(/needs attention/i)).toBeInTheDocument();
    expect(setupScope.getByRole('link', { name: /view setup checklist/i })).toHaveAttribute(
      'href',
      '/setup?branchId=branch-1'
    );
  });

  it('shows a ready badge when the pilot setup checklist is complete', async () => {
    const fetchMock = createOwnerDashboardFetchMock({
      setupResponse: createJsonResponse(
        makeSetupChecklist({
          completionPercent: 100,
          completedCount: 10,
          totalCount: 10,
        })
      ),
    });

    renderOwnerDashboardRoute(['Report.View'], fetchMock);

    const main = await screen.findByRole('main');
    const setupCard = within(main).getByRole('heading', { name: /pilot setup/i }).closest('section');
    expect(setupCard).not.toBeNull();
    expect(within(setupCard as HTMLElement).getByText(/ready for pilot/i)).toBeInTheDocument();
  });

  it('keeps the owner dashboard usable when the setup checklist fails', async () => {
    const fetchMock = createOwnerDashboardFetchMock({
      setupResponse: createJsonResponse(
        {
          title: 'Service Unavailable',
          detail: 'setup unavailable',
        },
        { status: 503 }
      ),
      setupStatus: 503,
    });

    renderOwnerDashboardRoute(['Report.View'], fetchMock);

    const main = await screen.findByRole('main');
    expect(within(main).getByRole('heading', { name: /owner dashboard/i })).toBeInTheDocument();
    const setupCard = within(main).getByRole('heading', { name: /pilot setup/i }).closest('section');
    expect(setupCard).not.toBeNull();
    const setupScope = within(setupCard as HTMLElement);
    expect(setupScope.getByText(/setup status unavailable/i)).toBeInTheDocument();
    expect(setupScope.getByRole('link', { name: /view setup checklist/i })).toHaveAttribute(
      'href',
      '/setup?branchId=branch-1'
    );
  });

  it('sanitizes raw backend errors and does not leak SQL or HTML content', async () => {
    const fetchMock = createOwnerDashboardFetchMock({
      dashboardResponse: createJsonResponse(
        {
          title: 'Bad Request',
          detail: 'Microsoft.Data.SqlClient.SqlException: stack trace <html>token</html>',
        },
        { status: 400 }
      ),
    });
    renderOwnerDashboardRoute(['Report.View'], fetchMock);

    expect(await screen.findByRole('alert')).toHaveTextContent(/unable to load the owner dashboard/i);
    expect(screen.queryByText(/sqlclient/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/stack trace/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/<html>/i)).not.toBeInTheDocument();
  });

  it('does not expose mutation, export, gateway, tax, or inventory controls', async () => {
    const fetchMock = createOwnerDashboardFetchMock();
    renderOwnerDashboardRoute(['Report.View'], fetchMock);

    const main = await screen.findByRole('main');
    expect(within(main).queryByRole('button', { name: /export/i })).not.toBeInTheDocument();
    expect(within(main).queryByRole('button', { name: /delete/i })).not.toBeInTheDocument();
    expect(within(main).queryByRole('button', { name: /print/i })).not.toBeInTheDocument();
    expect(within(main).queryByRole('button', { name: /gateway/i })).not.toBeInTheDocument();
    expect(within(main).queryByRole('button', { name: /tax/i })).not.toBeInTheDocument();
    expect(within(main).queryByRole('button', { name: /save/i })).not.toBeInTheDocument();
    expect(within(main).queryByRole('button', { name: /refund/i })).not.toBeInTheDocument();
  });
});
