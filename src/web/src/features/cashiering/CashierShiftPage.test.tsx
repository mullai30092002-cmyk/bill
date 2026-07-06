import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import App from '../../App';
import { createJsonResponse, clearAuthSession, storeAuthSession } from '../../test/authTestUtils';
import { renderWithRouter } from '../../test/renderWithRouter';
import type { CashierShiftDetail, CashierShiftListItem } from './cashierShiftTypes';

const normalizeText = (value: string | null | undefined) => value?.replace(/\s+/g, ' ').replace(/\u00a0/g, ' ').trim() ?? '';

const branchesPath = '/api/v1/admin/branches';
const cashierOpenPath = '/api/v1/cashier/shifts/open';
const cashierListPath = (branchId: string) => `/api/v1/cashier/shifts?branchId=${branchId}`;
const cashierCurrentLookupPath = (branchId: string) => `/api/v1/cashier/shifts/current?branchId=${branchId}`;
const languageStorageKey = 'billsoft.language';

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

const makeShiftListItem = (overrides: Partial<CashierShiftListItem> = {}): CashierShiftListItem => ({
  cashierShiftId: 'shift-1',
  restaurantId: 'restaurant-1',
  branchId: 'branch-1',
  cashierUserId: 'user-1',
  cashierName: 'Admin User',
  branchName: 'Main Branch',
  businessDate: '2026-06-13T00:00:00',
  status: 'Open',
  openedAtUtc: '2026-06-13T09:00:00Z',
  openingCashAmount: 100,
  closedAtUtc: null,
  declaredClosingCashAmount: null,
  expectedClosingCashAmount: 100,
  cashVarianceAmount: null,
  closeNotes: null,
  createdAtUtc: '2026-06-13T09:00:00Z',
  updatedAtUtc: null,
  ...overrides,
});

const makeShiftDetail = (overrides: Partial<CashierShiftDetail> = {}): CashierShiftDetail => ({
  ...makeShiftListItem(),
  ...overrides,
});

const setupFetch = (responses: Record<string, Response[]>) => {
  const fetchMock = vi.fn(async (input, init) => {
    const method = (init?.method ?? 'GET').toUpperCase();
    const url = new URL(String(input), 'http://localhost');
    const exactKey = `${method} ${url.pathname}${url.search}`;
    let queue = responses[exactKey];

    if ((!queue || queue.length === 0) && method === 'GET' && url.pathname === '/api/v1/cashier/shifts') {
      const branchId = url.searchParams.get('branchId');
      if (branchId) {
        queue = responses[`${method} ${url.pathname}?branchId=${branchId}`];
      }
    }

    if (!queue || queue.length === 0) {
      throw new Error(`Unhandled request: ${exactKey}`);
    }

    return queue.shift()!;
  });

  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
};

const getJsonBody = (call: [RequestInfo | URL, RequestInit?]) =>
  call[1]?.body ? JSON.parse(String(call[1].body)) : undefined;

const renderCashierRoute = (
  permissions: string[],
  responses: Record<string, Response[]>,
  authOverrides: Record<string, unknown> = {}
) => {
  const { language = 'en', ...sessionOverrides } = authOverrides as Record<string, unknown> & { language?: 'en' | 'ta' };
  clearAuthSession();
  storeAuthSession({
    permissions,
    roles: ['Cashier'],
    activeRole: 'Cashier',
    ...sessionOverrides,
  });
  window.localStorage.setItem(languageStorageKey, language);

  const fetchMock = setupFetch(responses);
  const user = userEvent.setup();

  renderWithRouter(<App />, '/cashier/shifts');

  return { fetchMock, user };
};

describe('CashierShiftPage', () => {
  beforeEach(() => {
    clearAuthSession();
    window.localStorage.removeItem(languageStorageKey);
  });

  afterEach(() => {
    clearAuthSession();
    window.localStorage.removeItem(languageStorageKey);
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it('renders Tamil labels for the cashier shifts workspace', async () => {
    renderCashierRoute(
      ['CashShift.Manage'],
      {
        [`GET ${branchesPath}`]: [createJsonResponse({ items: [makeBranch()] })],
        [`GET ${cashierCurrentLookupPath('branch-1')}`]: [
          createJsonResponse(
            makeShiftDetail({
              cashierShiftId: 'shift-opened',
              openingCashAmount: 100,
              expectedClosingCashAmount: 100,
              status: 'Open',
            })
          ),
        ],
        [`GET ${cashierListPath('branch-1')}`]: [
          createJsonResponse({
            items: [
              makeShiftListItem({
                cashierShiftId: 'shift-opened',
                openingCashAmount: 100,
                expectedClosingCashAmount: 100,
                status: 'Open',
              }),
            ],
          }),
        ],
      }, { language: 'ta' });

    expect(await screen.findByRole('heading', { name: /காசாளர் ஷிப்ட்கள்/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /ஷிப்டை மூடு/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /ஷிப்ட் வரலாறு/i })).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: /நிலை/i })).toBeInTheDocument();
    expect(screen.getAllByText('திறந்த').length).toBeGreaterThan(0);
  });

  it('renders an empty view-only state when there is no active shift', async () => {
    renderCashierRoute(
      ['CashShift.View'],
      {
        [`GET ${branchesPath}`]: [createJsonResponse({ items: [makeBranch()] })],
        [`GET ${cashierCurrentLookupPath('branch-1')}`]: [createJsonResponse(null)],
        [`GET ${cashierListPath('branch-1')}`]: [createJsonResponse({ items: [] })],
      }
    );

    expect(await screen.findByRole('heading', { name: /cashier shifts/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /no active shift/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /open shift/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /close shift/i })).not.toBeInTheDocument();
  });

  it('validates opening cash before submitting a shift open request', async () => {
    const { user, fetchMock } = renderCashierRoute(
      ['CashShift.Manage'],
      {
        [`GET ${branchesPath}`]: [createJsonResponse({ items: [makeBranch()] })],
        [`GET ${cashierCurrentLookupPath('branch-1')}`]: [createJsonResponse(null)],
        [`GET ${cashierListPath('branch-1')}`]: [createJsonResponse({ items: [] })],
      }
    );

    const openingCashAmount = await screen.findByLabelText(/opening cash amount/i);
    expect(screen.getByRole('heading', { name: /no active shift/i })).toBeInTheDocument();
    await user.clear(openingCashAmount);
    await user.type(openingCashAmount, '-10');
    await user.click(screen.getByRole('button', { name: /open shift/i }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/opening cash amount must be greater than or equal to 0/i);
    expect(fetchMock.mock.calls.filter(call => call[1]?.method === 'POST' && String(call[0]).includes(cashierOpenPath))).toHaveLength(0);
  });

  it('opens a shift and shows the active shift card', async () => {
    const { fetchMock, user } = renderCashierRoute(
      ['CashShift.Manage'],
      {
        [`GET ${branchesPath}`]: [createJsonResponse({ items: [makeBranch()] })],
        [`GET ${cashierCurrentLookupPath('branch-1')}`]: [
          createJsonResponse(null),
          createJsonResponse(
            makeShiftDetail({
              cashierShiftId: 'shift-opened',
              openingCashAmount: 50,
              expectedClosingCashAmount: 50,
              status: 'Open',
            })
          ),
        ],
        [`GET ${cashierListPath('branch-1')}`]: [
          createJsonResponse({ items: [] }),
          createJsonResponse({
            items: [
              makeShiftListItem({
                cashierShiftId: 'shift-opened',
                openingCashAmount: 50,
                expectedClosingCashAmount: 50,
                status: 'Open',
              }),
            ],
          }),
        ],
        [`POST ${cashierOpenPath}`]: [
          createJsonResponse(
            makeShiftDetail({
              cashierShiftId: 'shift-opened',
              openingCashAmount: 50,
              expectedClosingCashAmount: 50,
              status: 'Open',
            })
          ),
        ],
      }
    );

    const openingCashAmount = await screen.findByLabelText(/opening cash amount/i);
    const businessDateInput = (await screen.findByLabelText(/business date/i)) as HTMLInputElement;
    await user.clear(openingCashAmount);
    await user.type(openingCashAmount, '50');
    await user.click(screen.getByRole('button', { name: /open shift/i }));

    expect(await screen.findByRole('heading', { name: /active shift/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /open shift/i })).not.toBeInTheDocument();

    const openCall = fetchMock.mock.calls.find(call => call[1]?.method === 'POST' && String(call[0]).includes(cashierOpenPath));
    expect(getJsonBody(openCall as [RequestInfo | URL, RequestInit?])).toEqual({
      branchId: 'branch-1',
      businessDate: businessDateInput.value,
      openingCashAmount: 50,
    });
  });

  it('validates declared closing cash in the close shift dialog', async () => {
    const { user } = renderCashierRoute(
      ['CashShift.Manage'],
      {
        [`GET ${branchesPath}`]: [createJsonResponse({ items: [makeBranch()] })],
        [`GET ${cashierCurrentLookupPath('branch-1')}`]: [
          createJsonResponse(
            makeShiftDetail({
              cashierShiftId: 'shift-opened',
              openingCashAmount: 100,
              expectedClosingCashAmount: 100,
              status: 'Open',
            })
          ),
        ],
        [`GET ${cashierListPath('branch-1')}`]: [
          createJsonResponse({
            items: [
              makeShiftListItem({
                cashierShiftId: 'shift-opened',
                openingCashAmount: 100,
                expectedClosingCashAmount: 100,
                status: 'Open',
              }),
            ],
          }),
        ],
      }
    );

    await screen.findByRole('heading', { name: /active shift/i });
    await user.click(screen.getByRole('button', { name: /close shift/i }));
    const dialog = await screen.findByRole('dialog', { name: /close shift dialog/i });
    const declaredCash = within(dialog).getByLabelText(/declared closing cash/i);

    await user.clear(declaredCash);
    await user.type(declaredCash, '-1');
    await user.click(within(dialog).getByRole('button', { name: /^close shift$/i }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      /declared closing cash must be greater than or equal to 0/i
    );
  });

  it('shows a variance preview before closing the shift', async () => {
    const { user } = renderCashierRoute(
      ['CashShift.Manage'],
      {
        [`GET ${branchesPath}`]: [createJsonResponse({ items: [makeBranch()] })],
        [`GET ${cashierCurrentLookupPath('branch-1')}`]: [
          createJsonResponse(
            makeShiftDetail({
              cashierShiftId: 'shift-opened',
              openingCashAmount: 100,
              expectedClosingCashAmount: 100,
              status: 'Open',
            })
          ),
        ],
        [`GET ${cashierListPath('branch-1')}`]: [
          createJsonResponse({
            items: [
              makeShiftListItem({
                cashierShiftId: 'shift-opened',
                openingCashAmount: 100,
                expectedClosingCashAmount: 100,
                status: 'Open',
              }),
            ],
          }),
        ],
      }
    );

    await screen.findByRole('heading', { name: /active shift/i });
    await user.click(screen.getByRole('button', { name: /close shift/i }));
    const dialog = await screen.findByRole('dialog', { name: /close shift dialog/i });
    const declaredCash = within(dialog).getByLabelText(/declared closing cash/i);

    await user.clear(declaredCash);
    await user.type(declaredCash, '130');

    expect(within(dialog).getByText(/variance preview/i)).toBeInTheDocument();
    expect(within(dialog).getByText(/\+.*30\.00/)).toBeInTheDocument();
  });

  it('moves a closed shift into history after close', async () => {
    const { user } = renderCashierRoute(
      ['CashShift.Manage'],
      {
        [`GET ${branchesPath}`]: [createJsonResponse({ items: [makeBranch()] })],
        [`GET ${cashierCurrentLookupPath('branch-1')}`]: [
          createJsonResponse(
            makeShiftDetail({
              cashierShiftId: 'shift-opened',
              openingCashAmount: 100,
              expectedClosingCashAmount: 100,
              status: 'Open',
            })
          ),
          createJsonResponse(null),
        ],
        [`GET ${cashierListPath('branch-1')}`]: [
          createJsonResponse({
            items: [
              makeShiftListItem({
                cashierShiftId: 'shift-opened',
                openingCashAmount: 100,
                expectedClosingCashAmount: 100,
                status: 'Open',
              }),
            ],
          }),
          createJsonResponse({
            items: [
              makeShiftListItem({
                cashierShiftId: 'shift-opened',
                openingCashAmount: 100,
                expectedClosingCashAmount: 100,
                status: 'Closed',
                closedAtUtc: '2026-06-13T12:00:00Z',
                declaredClosingCashAmount: 130,
                cashVarianceAmount: 30,
                closeNotes: 'End of day',
              }),
            ],
          }),
        ],
        [`POST ${cashierOpenPath}`]: [],
        [`POST /api/v1/cashier/shifts/shift-opened/close`]: [
          createJsonResponse(
            makeShiftDetail({
              cashierShiftId: 'shift-opened',
              openingCashAmount: 100,
              expectedClosingCashAmount: 100,
              status: 'Closed',
              closedAtUtc: '2026-06-13T12:00:00Z',
              declaredClosingCashAmount: 130,
              cashVarianceAmount: 30,
              closeNotes: 'End of day',
            })
          ),
        ],
      }
    );

    await screen.findByRole('heading', { name: /active shift/i });
    await user.click(screen.getByRole('button', { name: /close shift/i }));
    const dialog = await screen.findByRole('dialog', { name: /close shift dialog/i });
    await user.clear(within(dialog).getByLabelText(/declared closing cash/i));
    await user.type(within(dialog).getByLabelText(/declared closing cash/i), '130');
    await user.click(within(dialog).getByRole('button', { name: /^close shift$/i }));

    await waitFor(() => expect(screen.queryByRole('dialog', { name: /close shift dialog/i })).not.toBeInTheDocument());
    expect(screen.getAllByText('Closed').find(node => node.closest('tbody'))).toBeTruthy();

    const historyTable = screen.getByRole('table');
    const closedRow = Array.from(historyTable.querySelectorAll('tbody tr')).find(row =>
      Array.from(row.querySelectorAll('td')).some(cell => normalizeText(cell.textContent) === 'Closed')
    );

    expect(closedRow).toBeTruthy();

    const cells = closedRow!.querySelectorAll('td');
    expect(normalizeText(cells[6]?.textContent)).toContain('130.00');
    expect(normalizeText(cells[8]?.textContent)).toContain('+');
    expect(normalizeText(cells[8]?.textContent)).toContain('30.00');
  });

  it('does not call the legacy open lookup when loading the cashier workspace', async () => {
    const { fetchMock } = renderCashierRoute(
      ['CashShift.View'],
      {
        [`GET ${branchesPath}`]: [createJsonResponse({ items: [makeBranch()] })],
        [`GET ${cashierCurrentLookupPath('branch-1')}`]: [createJsonResponse(null)],
        [`GET ${cashierListPath('branch-1')}`]: [createJsonResponse({ items: [] })],
      }
    );

    expect(await screen.findByRole('heading', { name: /cashier shifts/i })).toBeInTheDocument();
    await waitFor(() => expect(fetchMock).toHaveBeenCalled());
    expect(
      fetchMock.mock.calls.some(
        call => String(call[0]).includes('/api/v1/cashier/shifts/open') && (call[1]?.method ?? 'GET').toUpperCase() === 'GET'
      )
    ).toBe(false);
  });
});
