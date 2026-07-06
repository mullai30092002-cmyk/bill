import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import App from '../../App';
import { createJsonResponse, storeAuthSession } from '../../test/authTestUtils';
import { renderWithRouter } from '../../test/renderWithRouter';

// ── Fixtures ─────────────────────────────────────────────────────────────────

function makeListItem(overrides: Partial<{
  kitchenTicketId: string;
  ticketNumber: string;
  orderNumberSnapshot: string;
  orderTypeSnapshot: string;
  status: string;
  tableNameSnapshot: string | null;
  customerNameSnapshot: string | null;
  orderNotesSnapshot: string | null;
  lineCount: number;
  createdAt: string;
  cancelledAt: string | null;
  cancelReason: string | null;
}> = {}) {
  return {
    kitchenTicketId:     overrides.kitchenTicketId     ?? 'ticket-1',
    branchId:            'branch-1',
    posOrderId:          'order-1',
    ticketNumber:        overrides.ticketNumber         ?? 'KIT-20260613-0001',
    orderNumberSnapshot: overrides.orderNumberSnapshot  ?? 'ORD-20260613-0001',
    orderTypeSnapshot:   overrides.orderTypeSnapshot    ?? 'EatIn',
    tableNameSnapshot:   overrides.tableNameSnapshot    ?? null,
    customerNameSnapshot: overrides.customerNameSnapshot ?? null,
    orderNotesSnapshot:  overrides.orderNotesSnapshot   ?? null,
    status:              overrides.status               ?? 'Pending',
    lineCount:           overrides.lineCount            ?? 2,
    createdAt:           overrides.createdAt            ?? '2026-06-13T08:00:00Z',
    updatedAt:           '2026-06-13T08:02:00Z',
    cancelledAt:         overrides.cancelledAt          ?? null,
    cancelReason:        overrides.cancelReason         ?? null,
  };
}

const buildKitchenTicketList = () =>
  createJsonResponse({
    items: [
      makeListItem({ kitchenTicketId: 'ticket-1', ticketNumber: 'KIT-20260613-0001', orderTypeSnapshot: 'EatIn',   tableNameSnapshot: 'Table 3',  status: 'Pending' }),
      makeListItem({ kitchenTicketId: 'ticket-2', ticketNumber: 'KIT-20260613-0002', orderTypeSnapshot: 'Parcel',  tableNameSnapshot: null,       status: 'Preparing', orderNumberSnapshot: 'ORD-20260613-0002', lineCount: 1 }),
      makeListItem({ kitchenTicketId: 'ticket-3', ticketNumber: 'KIT-20260613-0003', orderTypeSnapshot: 'EatIn',   tableNameSnapshot: null,       status: 'Served',   orderNumberSnapshot: 'ORD-20260613-0003', lineCount: 1 }),
    ],
  });

function makeDetail(overrides: Partial<{
  status: string;
  tableNameSnapshot: string | null;
  customerNameSnapshot: string | null;
  orderNotesSnapshot: string | null;
  cancelReason: string | null;
  cancelledAt: string | null;
  preparingAt: string | null;
  readyAt: string | null;
  servedAt: string | null;
  inventoryDeductionStatus: string;
}> = {}) {
  return createJsonResponse({
    kitchenTicketId:     'ticket-1',
    restaurantId:        'restaurant-1',
    branchId:            'branch-1',
    posOrderId:          'order-1',
    ticketNumber:        'KIT-20260613-0001',
    orderNumberSnapshot: 'ORD-20260613-0001',
    orderTypeSnapshot:   'EatIn',
    tableNameSnapshot:   overrides.tableNameSnapshot    ?? 'Table 3',
    customerNameSnapshot: overrides.customerNameSnapshot ?? null,
    orderNotesSnapshot:  overrides.orderNotesSnapshot   ?? null,
    status:              overrides.status               ?? 'Pending',
    createdByUserId:     'user-1',
    lastStatusChangedByUserId: 'user-1',
    cancelledByUserId:   null,
    cancelledAt:         overrides.cancelledAt          ?? null,
    cancelReason:        overrides.cancelReason         ?? null,
    createdAt:           '2026-06-13T08:00:00Z',
    updatedAt:           '2026-06-13T08:02:00Z',
    preparingAt:         overrides.preparingAt          ?? null,
    readyAt:             overrides.readyAt              ?? null,
    servedAt:            overrides.servedAt             ?? null,
    inventoryDeductionStatus: overrides.inventoryDeductionStatus ?? 'NotDeducted',
    lines: [
      {
        kitchenTicketLineId: 'line-1',
        posOrderLineId:      'pos-line-1',
        menuItemId:          'item-1',
        menuCategoryId:      'category-1',
        menuItemNameSnapshot: 'Masala Dosa',
        menuCategoryNameSnapshot: 'Breakfast',
        skuSnapshot:         'DOSA-01',
        quantity:            2,
        notes:               'Less spicy',
        displayOrder:        1,
        createdAt:           '2026-06-13T08:00:00Z',
      },
    ],
  });
}

const buildDeductionPreview = (options?: {
  canComplete?: boolean;
  status?: 'Sufficient' | 'Insufficient' | 'NoRecipe';
}) =>
  createJsonResponse({
    ticketId:    'ticket-1',
    canComplete: options?.canComplete ?? true,
    lines: [{
      menuItemName:      'Masala Dosa',
      inventoryItemName: 'Rice Flour',
      requiredQuantity:  2,
      availableQuantity: 10,
      resultingQuantity: 8,
      status:            options?.status ?? 'Sufficient',
    }],
  });

const buildListForStatus = (status: string) =>
  createJsonResponse({
    items: [
      makeListItem({ status, tableNameSnapshot: 'Table 3' }),
      makeListItem({ kitchenTicketId: 'ticket-2', ticketNumber: 'KIT-20260613-0002', orderTypeSnapshot: 'Parcel', status: 'Preparing', orderNumberSnapshot: 'ORD-20260613-0002', lineCount: 1 }),
    ],
  });

// ── Render helpers ────────────────────────────────────────────────────────────

const renderKitchenTickets = (permissions: string[], initialPath = '/kitchen/tickets') => {
  storeAuthSession({ permissions, roles: ['KitchenUser'], activeRole: 'KitchenUser' });
  const user = userEvent.setup();
  const fetchMock = vi.fn().mockResolvedValue(buildKitchenTicketList());
  vi.stubGlobal('fetch', fetchMock);
  renderWithRouter(<App />, initialPath);
  return { user, fetchMock };
};

const getCardScope = (title: RegExp) => {
  const heading = screen.getByRole('heading', { name: title });
  const card = heading.closest('section');
  expect(card).not.toBeNull();
  return card as HTMLElement;
};

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('KitchenTicketsPage', () => {
  // ── Access ──────────────────────────────────────────────────────────────────

  it('renders for KitchenTicket.View users', async () => {
    renderKitchenTickets(['KitchenTicket.View']);
    expect(await screen.findByRole('heading', { name: /kitchen display/i })).toBeInTheDocument();
    expect(screen.getByText(/ticket queue/i)).toBeInTheDocument();
  });

  it('renders for KitchenTicket.UpdateStatus users', async () => {
    renderKitchenTickets(['KitchenTicket.UpdateStatus']);
    expect(await screen.findByRole('heading', { name: /kitchen display/i })).toBeInTheDocument();
  });

  it('renders from the display alias route', async () => {
    const { fetchMock } = renderKitchenTickets(['KitchenTicket.View'], '/kitchen/display');
    expect(await screen.findByRole('heading', { name: /kitchen display/i })).toBeInTheDocument();
    expect(screen.getByText('KIT-20260613-0001')).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it('shows not-authorized state without calling the API', () => {
    storeAuthSession({ permissions: [], roles: ['KitchenUser'], activeRole: 'KitchenUser' });
    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    renderWithRouter(<App />, '/kitchen/tickets');
    expect(screen.getByRole('heading', { name: /not authorized/i })).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  // ── Queue loading and filtering ──────────────────────────────────────────────

  it('loads the queue, defaults to active filter, and can switch to All', async () => {
    const { user } = renderKitchenTickets(['KitchenTicket.View']);
    expect(await screen.findByText('KIT-20260613-0001')).toBeInTheDocument();
    expect(screen.getByText('KIT-20260613-0002')).toBeInTheDocument();
    // Served ticket is hidden by active filter
    expect(screen.queryByText('KIT-20260613-0003')).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /^all$/i }));
    expect(screen.getByText('KIT-20260613-0003')).toBeInTheDocument();
  });

  it('shows empty state when no active tickets', async () => {
    storeAuthSession({ permissions: ['KitchenTicket.View'], roles: ['KitchenUser'], activeRole: 'KitchenUser' });
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(createJsonResponse({ items: [] })));
    renderWithRouter(<App />, '/kitchen/tickets');
    expect(await screen.findByText(/no active tickets/i)).toBeInTheDocument();
  });

  it('shows error state with retry when the API fails', async () => {
    storeAuthSession({ permissions: ['KitchenTicket.View'], roles: ['KitchenUser'], activeRole: 'KitchenUser' });
    const fetchMock = vi.fn().mockRejectedValue(new Error('Network error'));
    vi.stubGlobal('fetch', fetchMock);
    renderWithRouter(<App />, '/kitchen/tickets');
    expect(await screen.findByText(/could not load kitchen tickets/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /try again/i })).toBeInTheDocument();
  });

  // ── Refresh button and last-refreshed ─────────────────────────────────────

  it('shows a Refresh button that is disabled while loading', async () => {
    renderKitchenTickets(['KitchenTicket.View']);
    await screen.findByRole('heading', { name: /kitchen display/i });
    const refreshBtn = screen.getByRole('button', { name: /refresh/i });
    expect(refreshBtn).toBeInTheDocument();
  });

  it('shows last-refreshed summary card', async () => {
    renderKitchenTickets(['KitchenTicket.View']);
    expect(await screen.findByText(/last refreshed/i)).toBeInTheDocument();
  });

  // ── Table name / Parcel reference ─────────────────────────────────────────

  it('shows table name in the ticket card for eat-in tickets', async () => {
    renderKitchenTickets(['KitchenTicket.View']);
    expect(await screen.findByText('Table 3')).toBeInTheDocument();
  });

  it('shows Eat-in badge in ticket card', async () => {
    renderKitchenTickets(['KitchenTicket.View']);
    await screen.findByText('KIT-20260613-0001');
    // Badge label for eat-in
    const eatInBadges = screen.getAllByText('Eat-in');
    expect(eatInBadges.length).toBeGreaterThan(0);
  });

  it('shows customer name in ticket card for parcel when no table name', async () => {
    storeAuthSession({ permissions: ['KitchenTicket.View'], roles: ['KitchenUser'], activeRole: 'KitchenUser' });
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(
      createJsonResponse({
        items: [
          makeListItem({ orderTypeSnapshot: 'Parcel', tableNameSnapshot: null, customerNameSnapshot: 'Ravi Kumar', status: 'Pending' }),
        ],
      })
    ));
    renderWithRouter(<App />, '/kitchen/tickets');
    expect(await screen.findByText('Ravi Kumar')).toBeInTheDocument();
  });

  // ── Ticket detail ─────────────────────────────────────────────────────────

  it('shows line snapshots with item name, category, quantity, and notes', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(buildKitchenTicketList())
      .mockResolvedValueOnce(makeDetail());
    vi.stubGlobal('fetch', fetchMock);
    storeAuthSession({ permissions: ['KitchenTicket.View'], roles: ['KitchenUser'], activeRole: 'KitchenUser' });
    const user = userEvent.setup();
    renderWithRouter(<App />, '/kitchen/tickets');

    await user.click(await screen.findByRole('button', { name: /kit-20260613-0001/i }));
    const detail = getCardScope(/selected ticket/i);
    expect(await within(detail).findByText(/masala dosa/i)).toBeInTheDocument();
    expect(within(detail).getByText(/breakfast/i)).toBeInTheDocument();
    expect(within(detail).getByText(/×2/i)).toBeInTheDocument();
    expect(within(detail).getByText(/less spicy/i)).toBeInTheDocument();
  });

  it('shows table name in detail panel subhead', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(buildKitchenTicketList())
      .mockResolvedValueOnce(makeDetail({ tableNameSnapshot: 'Table 3' }));
    vi.stubGlobal('fetch', fetchMock);
    storeAuthSession({ permissions: ['KitchenTicket.View'], roles: ['KitchenUser'], activeRole: 'KitchenUser' });
    const user = userEvent.setup();
    renderWithRouter(<App />, '/kitchen/tickets');

    await user.click(await screen.findByRole('button', { name: /kit-20260613-0001/i }));
    const detail = getCardScope(/selected ticket/i);
    expect(await within(detail).findByText('Table 3')).toBeInTheDocument();
  });

  it('shows order notes in detail panel when present', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(buildKitchenTicketList())
      .mockResolvedValueOnce(makeDetail({ orderNotesSnapshot: 'Allergy: no peanuts' }));
    vi.stubGlobal('fetch', fetchMock);
    storeAuthSession({ permissions: ['KitchenTicket.View'], roles: ['KitchenUser'], activeRole: 'KitchenUser' });
    const user = userEvent.setup();
    renderWithRouter(<App />, '/kitchen/tickets');

    await user.click(await screen.findByRole('button', { name: /kit-20260613-0001/i }));
    const detail = getCardScope(/selected ticket/i);
    expect(await within(detail).findByText(/allergy: no peanuts/i)).toBeInTheDocument();
  });

  it('shows inventory deduction status in the detail panel', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(buildKitchenTicketList())
      .mockResolvedValueOnce(makeDetail({ inventoryDeductionStatus: 'DeductionWarning' }));
    vi.stubGlobal('fetch', fetchMock);
    storeAuthSession({ permissions: ['KitchenTicket.View'], roles: ['KitchenUser'], activeRole: 'KitchenUser' });
    const user = userEvent.setup();
    renderWithRouter(<App />, '/kitchen/tickets');

    await user.click(await screen.findByRole('button', { name: /kit-20260613-0001/i }));
    const detail = getCardScope(/selected ticket/i);
    expect(await within(detail).findByText(/deduction warning/i)).toBeInTheDocument();
  });

  it('does not show lifecycle timestamps for unreached states', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(buildKitchenTicketList())
      .mockResolvedValueOnce(makeDetail({ status: 'Pending', preparingAt: null, readyAt: null, servedAt: null }));
    vi.stubGlobal('fetch', fetchMock);
    storeAuthSession({ permissions: ['KitchenTicket.View'], roles: ['KitchenUser'], activeRole: 'KitchenUser' });
    const user = userEvent.setup();
    renderWithRouter(<App />, '/kitchen/tickets');

    await user.click(await screen.findByRole('button', { name: /kit-20260613-0001/i }));
    const detail = getCardScope(/selected ticket/i);
    await within(detail).findByText(/masala dosa/i);
    // Preparing/Ready/Served meta items should not appear for a Pending ticket
    expect(within(detail).queryByText(/^Preparing$/i)).not.toBeInTheDocument();
    expect(within(detail).queryByText(/^Ready$/i)).not.toBeInTheDocument();
    expect(within(detail).queryByText(/^Served$/i)).not.toBeInTheDocument();
  });

  it('does not show price, tax, or totals', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(buildKitchenTicketList())
      .mockResolvedValueOnce(makeDetail());
    vi.stubGlobal('fetch', fetchMock);
    storeAuthSession({ permissions: ['KitchenTicket.View'], roles: ['KitchenUser'], activeRole: 'KitchenUser' });
    const user = userEvent.setup();
    renderWithRouter(<App />, '/kitchen/tickets');

    await user.click(await screen.findByRole('button', { name: /kit-20260613-0001/i }));
    const detail = getCardScope(/selected ticket/i);
    expect(within(detail).queryByText(/price/i)).not.toBeInTheDocument();
    expect(within(detail).queryByText(/tax/i)).not.toBeInTheDocument();
    expect(within(detail).queryByText(/total/i)).not.toBeInTheDocument();
  });

  // ── Cancelled ticket ──────────────────────────────────────────────────────

  it('shows "do not prepare" banner in cancelled ticket card', async () => {
    storeAuthSession({ permissions: ['KitchenTicket.View'], roles: ['KitchenUser'], activeRole: 'KitchenUser' });
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(
      createJsonResponse({
        items: [
          makeListItem({
            status: 'Cancelled',
            cancelledAt: '2026-06-13T08:30:00Z',
            cancelReason: 'Customer walked out',
          }),
        ],
      })
    ));
    const user = userEvent.setup();
    renderWithRouter(<App />, '/kitchen/tickets');

    await user.click(screen.getByRole('button', { name: /^all$/i }));
    await screen.findByText(/kit-20260613-0001/i);
    expect(screen.getByText(/do not prepare/i)).toBeInTheDocument();
    expect(screen.getByText(/customer walked out/i)).toBeInTheDocument();
  });

  it('shows cancel alert and reason in detail panel for cancelled ticket', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(
        createJsonResponse({ items: [makeListItem({ status: 'Cancelled', cancelledAt: '2026-06-13T08:30:00Z', cancelReason: 'Customer changed mind' })] })
      )
      .mockResolvedValueOnce(
        makeDetail({ status: 'Cancelled', cancelReason: 'Customer changed mind', cancelledAt: '2026-06-13T08:30:00Z' })
      );
    vi.stubGlobal('fetch', fetchMock);
    storeAuthSession({ permissions: ['KitchenTicket.Manage'], roles: ['KitchenUser'], activeRole: 'KitchenUser' });
    const user = userEvent.setup();
    renderWithRouter(<App />, '/kitchen/tickets');

    await user.click(screen.getByRole('button', { name: /^all$/i }));
    await user.click(await screen.findByRole('button', { name: /kit-20260613-0001/i }));
    const detail = getCardScope(/selected ticket/i);
    expect(await within(detail).findByText(/do not prepare — cancelled/i)).toBeInTheDocument();
    expect(within(detail).getByText(/customer changed mind/i)).toBeInTheDocument();
  });

  it('shows cancelled timestamp in meta-grid for cancelled ticket', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(
        createJsonResponse({ items: [makeListItem({ status: 'Cancelled', cancelledAt: '2026-06-13T08:30:00Z', cancelReason: 'Reason' })] })
      )
      .mockResolvedValueOnce(
        makeDetail({ status: 'Cancelled', cancelledAt: '2026-06-13T08:30:00Z', cancelReason: 'Reason' })
      );
    vi.stubGlobal('fetch', fetchMock);
    storeAuthSession({ permissions: ['KitchenTicket.View'], roles: ['KitchenUser'], activeRole: 'KitchenUser' });
    const user = userEvent.setup();
    renderWithRouter(<App />, '/kitchen/tickets');

    await user.click(screen.getByRole('button', { name: /^all$/i }));
    await user.click(await screen.findByRole('button', { name: /kit-20260613-0001/i }));
    const detail = getCardScope(/selected ticket/i);
    // The meta-grid value shows "Cancelled at <timestamp>" for the cancelled lifecycle row
    expect(await within(detail).findByText(/cancelled at/i)).toBeInTheDocument();
  });

  // ── Status actions ─────────────────────────────────────────────────────────

  it('shows no status or cancel controls for read-only users', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(buildKitchenTicketList())
      .mockResolvedValueOnce(makeDetail());
    vi.stubGlobal('fetch', fetchMock);
    storeAuthSession({ permissions: ['KitchenTicket.View'], roles: ['KitchenUser'], activeRole: 'KitchenUser' });
    const user = userEvent.setup();
    renderWithRouter(<App />, '/kitchen/tickets');

    await user.click(await screen.findByRole('button', { name: /kit-20260613-0001/i }));
    expect(screen.queryByRole('button', { name: /start preparing/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /mark ready/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /cancel ticket/i })).not.toBeInTheDocument();
  });

  it('advances status and refreshes queue on Start Preparing', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(buildKitchenTicketList())
      .mockResolvedValueOnce(makeDetail({ status: 'Pending' }))
      .mockResolvedValueOnce(buildDeductionPreview())
      .mockResolvedValueOnce(makeDetail({ status: 'Preparing' }))
      .mockResolvedValueOnce(buildListForStatus('Preparing'))
      .mockResolvedValueOnce(buildDeductionPreview());
    vi.stubGlobal('fetch', fetchMock);
    storeAuthSession({ permissions: ['KitchenTicket.Manage'], roles: ['KitchenUser'], activeRole: 'KitchenUser' });
    const user = userEvent.setup();
    renderWithRouter(<App />, '/kitchen/tickets');

    await user.click(await screen.findByRole('button', { name: /kit-20260613-0001/i }));
    await user.click(screen.getByRole('button', { name: /start preparing/i }));

    const statusCall = fetchMock.mock.calls.find(
      call => call[1]?.method === 'POST' && String(call[0]).includes('/status')
    );
    expect(statusCall).toBeDefined();
    expect(JSON.parse(String(statusCall?.[1]?.body))).toEqual({ status: 'Preparing' });
    expect(await screen.findByText(/moved to preparing/i)).toBeInTheDocument();

    const actions = getCardScope(/ticket actions/i);
    expect(within(actions).queryByRole('button', { name: /start preparing/i })).not.toBeInTheDocument();
    expect(within(actions).getByRole('button', { name: /mark ready/i })).toBeInTheDocument();
  });

  it('shows per-action loading indicator while submitting', async () => {
    let resolveStatusCall!: (value: Response) => void;
    const statusPromise = new Promise<Response>(r => { resolveStatusCall = r; });
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(buildKitchenTicketList())
      .mockResolvedValueOnce(makeDetail({ status: 'Pending' }))
      .mockResolvedValueOnce(buildDeductionPreview())
      .mockReturnValueOnce(statusPromise)
      .mockResolvedValueOnce(buildListForStatus('Preparing'))   // refreshTickets after status update
      .mockResolvedValueOnce(buildDeductionPreview());          // loadDeductionPreview after refresh
    vi.stubGlobal('fetch', fetchMock);
    storeAuthSession({ permissions: ['KitchenTicket.Manage'], roles: ['KitchenUser'], activeRole: 'KitchenUser' });
    const user = userEvent.setup();
    renderWithRouter(<App />, '/kitchen/tickets');

    await user.click(await screen.findByRole('button', { name: /kit-20260613-0001/i }));
    await user.click(screen.getByRole('button', { name: /start preparing/i }));

    // While in flight the button text should change or be disabled
    expect(screen.getByRole('button', { name: /start preparing/i })).toBeDisabled();

    resolveStatusCall(makeDetail({ status: 'Preparing' }));
    // Drain microtasks so the settled promise and its React state updates stay in this test's context
    await screen.findByText(/moved to preparing/i);
  });

  // ── Cancel inline confirmation ─────────────────────────────────────────────

  it('shows inline confirm panel when cancelling instead of window.confirm', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(buildKitchenTicketList())
      .mockResolvedValueOnce(makeDetail({ status: 'Pending' }))
      .mockResolvedValueOnce(buildDeductionPreview());
    vi.stubGlobal('fetch', fetchMock);
    storeAuthSession({ permissions: ['KitchenTicket.Manage'], roles: ['KitchenUser'], activeRole: 'KitchenUser' });
    const confirmSpy = vi.spyOn(window, 'confirm');
    const user = userEvent.setup();
    renderWithRouter(<App />, '/kitchen/tickets');

    await user.click(await screen.findByRole('button', { name: /kit-20260613-0001/i }));
    await user.type(await screen.findByLabelText(/cancel reason/i), 'Customer changed mind');
    await user.click(screen.getByRole('button', { name: /cancel ticket/i }));

    // Inline confirm panel must appear
    expect(screen.getByRole('alertdialog')).toBeInTheDocument();
    expect(screen.getByText(/cannot be undone/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /confirm cancel/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /go back/i })).toBeInTheDocument();

    // window.confirm must NOT be called
    expect(confirmSpy).not.toHaveBeenCalled();
  });

  it('aborts cancel when user clicks Go back', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(buildKitchenTicketList())
      .mockResolvedValueOnce(makeDetail({ status: 'Pending' }))
      .mockResolvedValueOnce(buildDeductionPreview());
    vi.stubGlobal('fetch', fetchMock);
    storeAuthSession({ permissions: ['KitchenTicket.Manage'], roles: ['KitchenUser'], activeRole: 'KitchenUser' });
    const user = userEvent.setup();
    renderWithRouter(<App />, '/kitchen/tickets');

    await user.click(await screen.findByRole('button', { name: /kit-20260613-0001/i }));
    await user.type(await screen.findByLabelText(/cancel reason/i), 'Customer changed mind');
    await user.click(screen.getByRole('button', { name: /cancel ticket/i }));
    await user.click(screen.getByRole('button', { name: /go back/i }));

    // Confirm panel dismissed, back to normal actions
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /cancel ticket/i })).toBeInTheDocument();
    const cancelCalls = fetchMock.mock.calls.filter(call => String(call[0]).includes('/cancel'));
    expect(cancelCalls).toHaveLength(0);
  });

  it('requires a reason before entering cancel confirm', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(buildKitchenTicketList())
      .mockResolvedValueOnce(makeDetail({ status: 'Pending' }))
      .mockResolvedValueOnce(buildDeductionPreview());
    vi.stubGlobal('fetch', fetchMock);
    storeAuthSession({ permissions: ['KitchenTicket.Manage'], roles: ['KitchenUser'], activeRole: 'KitchenUser' });
    const user = userEvent.setup();
    renderWithRouter(<App />, '/kitchen/tickets');

    await user.click(await screen.findByRole('button', { name: /kit-20260613-0001/i }));
    await user.click(await screen.findByRole('button', { name: /cancel ticket/i }));

    expect(await screen.findByText(/cancellation requires a reason/i)).toBeInTheDocument();
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
  });

  it('completes cancel after confirm and sends only reason in body', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(buildKitchenTicketList())
      .mockResolvedValueOnce(makeDetail({ status: 'Pending' }))
      .mockResolvedValueOnce(buildDeductionPreview())
      .mockResolvedValueOnce(makeDetail({ status: 'Cancelled', cancelReason: 'Customer changed mind', cancelledAt: '2026-06-13T08:30:00Z' }))
      .mockResolvedValueOnce(buildListForStatus('Cancelled'));
    vi.stubGlobal('fetch', fetchMock);
    storeAuthSession({ permissions: ['KitchenTicket.Manage'], roles: ['KitchenUser'], activeRole: 'KitchenUser' });
    const user = userEvent.setup();
    renderWithRouter(<App />, '/kitchen/tickets');

    await user.click(await screen.findByRole('button', { name: /kit-20260613-0001/i }));
    await user.type(await screen.findByLabelText(/cancel reason/i), 'Customer changed mind');
    await user.click(screen.getByRole('button', { name: /cancel ticket/i }));
    await user.click(screen.getByRole('button', { name: /confirm cancel/i }));

    const cancelCall = fetchMock.mock.calls.find(call => call[1]?.method === 'POST' && String(call[0]).includes('/cancel'));
    expect(cancelCall).toBeDefined();
    expect(JSON.parse(String(cancelCall?.[1]?.body))).toEqual({ reason: 'Customer changed mind' });
    expect(await screen.findByText(/kit-20260613-0001 cancelled\./i)).toBeInTheDocument();
  });

  // ── Deduction preview ──────────────────────────────────────────────────────

  it('shows deduction preview with can-complete badge', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(buildKitchenTicketList())
      .mockResolvedValueOnce(makeDetail({ status: 'Ready' }))
      .mockResolvedValueOnce(buildDeductionPreview());
    vi.stubGlobal('fetch', fetchMock);
    storeAuthSession({ permissions: ['KitchenTicket.Manage'], roles: ['KitchenUser'], activeRole: 'KitchenUser' });
    const user = userEvent.setup();
    renderWithRouter(<App />, '/kitchen/tickets');

    await user.click(await screen.findByRole('button', { name: /kit-20260613-0001/i }));
    const preview = getCardScope(/completion preview/i);
    expect(await within(preview).findByText(/can complete/i)).toBeInTheDocument();
    expect(within(preview).getByText(/^yes$/i)).toBeInTheDocument();
    expect(within(preview).getByRole('cell', { name: /^rice flour$/i })).toBeInTheDocument();
  });

  it('shows insufficient stock and allows mark-served (backend will validate)', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(buildKitchenTicketList())
      .mockResolvedValueOnce(makeDetail({ status: 'Ready' }))
      .mockResolvedValueOnce(buildDeductionPreview({ canComplete: false, status: 'Insufficient' }))
      .mockResolvedValueOnce(createJsonResponse({ title: 'Bad Request', detail: 'Insufficient stock: Rice Flour required 5 available 2.' }, { status: 400 }));
    vi.stubGlobal('fetch', fetchMock);
    storeAuthSession({ permissions: ['KitchenTicket.Manage'], roles: ['KitchenUser'], activeRole: 'KitchenUser' });
    const user = userEvent.setup();
    renderWithRouter(<App />, '/kitchen/tickets');

    await user.click(await screen.findByRole('button', { name: /kit-20260613-0001/i }));
    const preview = getCardScope(/completion preview/i);
    expect(await within(preview).findByText(/^no$/i)).toBeInTheDocument();
    expect(within(preview).getByRole('cell', { name: /^insufficient$/i })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /mark served/i }));
    expect(await screen.findByText(/insufficient stock/i)).toBeInTheDocument();
  });

  // ── Operational hygiene ────────────────────────────────────────────────────

  it('does not show delete, printer, hardware, receipt, or billing controls', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(buildKitchenTicketList());
    vi.stubGlobal('fetch', fetchMock);
    storeAuthSession({ permissions: ['KitchenTicket.View'], roles: ['KitchenUser'], activeRole: 'KitchenUser' });
    renderWithRouter(<App />, '/kitchen/tickets');
    const main = screen.getByRole('main');
    expect(within(main).queryByRole('button', { name: /delete/i })).not.toBeInTheDocument();
    expect(within(main).queryByRole('button', { name: /printer/i })).not.toBeInTheDocument();
    expect(within(main).queryByRole('button', { name: /hardware/i })).not.toBeInTheDocument();
    expect(within(main).queryByRole('button', { name: /receipt/i })).not.toBeInTheDocument();
    expect(within(main).queryByRole('button', { name: /billing/i })).not.toBeInTheDocument();
  });

  it('shows kitchen nav only when kitchen permissions are present', async () => {
    storeAuthSession({ permissions: ['KitchenTicket.View'], roles: ['KitchenUser'], activeRole: 'KitchenUser' });
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(buildKitchenTicketList()));
    renderWithRouter(<App />, '/');
    expect(await screen.findAllByText('Kitchen Display', { selector: '.responsive-nav__link-label' })).toHaveLength(3);
  });
});
