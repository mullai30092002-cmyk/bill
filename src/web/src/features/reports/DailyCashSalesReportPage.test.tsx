import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import App from '../../App';
import { clearAuthSession, createJsonResponse, storeAuthSession } from '../../test/authTestUtils';
import { renderWithRouter } from '../../test/renderWithRouter';
import type { DailyCashSalesReportResponse } from './dailyCashSalesReportTypes';

const makeReport = (overrides: Partial<DailyCashSalesReportResponse> = {}): DailyCashSalesReportResponse => ({
  restaurantId: 'restaurant-1',
  restaurantCode: 'DEMO',
  restaurantName: 'Demo Restaurant',
  branchId: 'branch-1',
  branchName: 'Main Branch',
  businessDate: '2026-06-13',
  currencyCode: 'SGD',
  generatedAt: '2026-06-13T10:30:00Z',
  summary: {
    totalBills: 4,
    paidBills: 1,
    partiallyPaidBills: 1,
    unpaidBills: 1,
    cancelledBills: 1,
    grossSales: 44,
    grossBillTotal: 33,
    cancelledBillAmount: 11,
    netSales: 33,
    totalAmountPaid: 16,
    totalBalanceDue: 28,
    cashPayments: 10,
    upiPayments: 0,
    cardPayments: 6,
    otherPayments: 0,
    nonCashPayments: 6,
    openShifts: 1,
    closedShifts: 1,
    openingCashTotal: 150,
    declaredClosingCashTotal: 120,
    expectedCashTotal: 160,
    receiptPrints: 3,
    receiptReprints: 1,
    cashVarianceTotal: 10,
  },
  paymentBreakdown: [
    {
      paymentMode: 'Cash',
      recordedAmount: 10,
      cancelledAmount: 0,
      netAmount: 10,
      paymentCount: 1,
      cancelledCount: 0,
    },
    {
      paymentMode: 'Upi',
      recordedAmount: 0,
      cancelledAmount: 0,
      netAmount: 0,
      paymentCount: 0,
      cancelledCount: 0,
    },
    {
      paymentMode: 'Card',
      recordedAmount: 6,
      cancelledAmount: 0,
      netAmount: 6,
      paymentCount: 1,
      cancelledCount: 0,
    },
    {
      paymentMode: 'Other',
      recordedAmount: 0,
      cancelledAmount: 0,
      netAmount: 0,
      paymentCount: 0,
      cancelledCount: 0,
    },
  ],
  cashShiftSummaries: [
    {
      cashierShiftId: 'shift-1',
      branchId: 'branch-1',
      branchName: 'Main Branch',
      status: 'Closed',
      openedAt: '2026-06-13T02:00:00Z',
      closedAt: '2026-06-13T10:00:00Z',
      openingCashAmount: 100,
      expectedCashAmount: 110,
      countedCashAmount: 120,
      cashVarianceAmount: 10,
      cashMovementTotal: 0,
      cashPaymentTotal: 10,
    },
    {
      cashierShiftId: 'shift-2',
      branchId: 'branch-2',
      branchName: 'North Branch',
      status: 'Open',
      openedAt: '2026-06-13T06:00:00Z',
      closedAt: null,
      openingCashAmount: 50,
      expectedCashAmount: 50,
      countedCashAmount: null,
      cashVarianceAmount: null,
      cashMovementTotal: 0,
      cashPaymentTotal: 0,
    },
  ],
  exceptions: {
    unpaidBills: [
      {
        id: 'bill-1',
        referenceNumber: 'BILL-20260613-0001',
        branchId: 'branch-1',
        branchName: 'Main Branch',
        amount: 12,
        status: 'Unpaid',
        occurredAt: '2026-06-13T08:00:00Z',
        reason: 'Balance due 12.00',
        severity: 'Medium',
        balanceDue: 12,
      },
    ],
    cancelledBills: [
      {
        id: 'bill-2',
        referenceNumber: 'BILL-20260613-0002',
        branchId: 'branch-1',
        branchName: 'Main Branch',
        amount: 11,
        status: 'Cancelled',
        occurredAt: '2026-06-13T08:15:00Z',
        reason: 'Customer cancelled',
        severity: 'Medium',
      },
    ],
    cancelledPayments: [
      {
        id: 'payment-1',
        referenceNumber: 'PAY-20260613-0001',
        branchId: 'branch-1',
        branchName: 'Main Branch',
        amount: 6,
        status: 'Cancelled',
        occurredAt: '2026-06-13T09:00:00Z',
        reason: 'Duplicate payment',
        severity: 'Medium',
      },
    ],
    receiptReprints: [],
    cashVariances: [
      {
        id: 'shift-1',
        referenceNumber: 'shift-1',
        branchId: 'branch-1',
        branchName: 'Main Branch',
        amount: 10,
        status: 'Closed',
        occurredAt: '2026-06-13T10:00:00Z',
        reason: 'Counted closing cash minus expected cash (opening cash + recorded cash payments).',
        severity: 'High',
        expectedCashAmount: 110,
        countedCashAmount: 120,
        varianceAmount: 10,
      },
    ],
    openShifts: [
      {
        id: 'shift-2',
        referenceNumber: 'shift-2',
        branchId: 'branch-2',
        branchName: 'North Branch',
        amount: null,
        status: 'Open',
        occurredAt: '2026-06-13T06:00:00Z',
        reason: 'Shift is still open after the selected business date.',
        severity: 'Medium',
      },
    ],
  },
  ...overrides,
});

const renderReportRoute = (permissions: string[], fetchMock: ReturnType<typeof vi.fn>, path = '/reports/daily-cash-sales') => {
  clearAuthSession();
  storeAuthSession({
    permissions,
    roles: ['Admin'],
    activeRole: 'Admin',
  });
  vi.stubGlobal('fetch', fetchMock);
  return renderWithRouter(<App />, path);
};

describe('DailyCashSalesReportPage', () => {
  beforeEach(() => {
    clearAuthSession();
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it('shows a not-authorized state without calling the report API', async () => {
    const fetchMock = vi.fn();
    renderReportRoute([], fetchMock);

    expect(await screen.findByRole('heading', { name: /not authorized/i })).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('loads the report and sends the date and branch query when branchId is present', async () => {
    const fetchMock = vi.fn().mockResolvedValue(createJsonResponse(makeReport()));
    renderReportRoute(['Report.View'], fetchMock, '/reports/daily-cash-sales?date=2026-06-13&branchId=branch-1');

    expect(await screen.findByRole('heading', { name: /daily cash sales report/i })).toBeInTheDocument();
    await waitFor(() => expect(fetchMock).toHaveBeenCalled());

    const firstCall = fetchMock.mock.calls[0];
    expect(String(firstCall[0])).toContain('/api/v1/reports/daily-cash-sales');
    expect(String(firstCall[0])).toContain('date=2026-06-13');
    expect(String(firstCall[0])).toContain('branchId=branch-1');
  });

  it('reloads when the business date changes and preserves the branch scope in the query string', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(createJsonResponse(makeReport({ businessDate: '2026-06-13' })))
      .mockResolvedValueOnce(createJsonResponse(makeReport({ businessDate: '2026-06-14' })));

    renderReportRoute(['Report.View'], fetchMock, '/reports/daily-cash-sales?date=2026-06-13&branchId=branch-1');

    const businessDateInput = await screen.findByLabelText(/business date/i);
    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1));

    fireEvent.change(businessDateInput, { target: { value: '2026-06-14' } });

    await waitFor(() => expect(fetchMock.mock.calls.length).toBeGreaterThanOrEqual(2));
    const lastCall = fetchMock.mock.calls[fetchMock.mock.calls.length - 1];
    expect(String(lastCall?.[0])).toContain('date=2026-06-14');
    expect(String(lastCall?.[0])).toContain('branchId=branch-1');
  });

  it('renders the business totals, payment breakdown, shift summary, and required exceptions', async () => {
    const fetchMock = vi.fn().mockResolvedValue(createJsonResponse(makeReport()));
    renderReportRoute(['Report.View'], fetchMock, '/reports/daily-cash-sales?date=2026-06-13&branchId=branch-1');

    expect(await screen.findByText(/gross bill total/i)).toBeInTheDocument();
    expect(screen.getByText(/33\.00/)).toBeInTheDocument();
    expect(screen.getByText(/paid total/i)).toBeInTheDocument();
    expect(screen.getByText(/16\.00/)).toBeInTheDocument();
    const summaryCards = Array.from((await screen.findByRole('main')).querySelectorAll('.summary-card'));
    const varianceCard = summaryCards.find(node => node.querySelector('.summary-card__label')?.textContent === 'Closed shift variance total');
    expect(varianceCard).toBeTruthy();
    expect(within(varianceCard as HTMLElement).getByText(/counted closing cash minus expected cash/i)).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /payment breakdown/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /cashier shift summary/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /unpaid bills/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /cancelled bills/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /closed shift variances/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /open shifts/i })).toBeInTheDocument();

    const main = await screen.findByRole('main');
    expect(within(main).getAllByText(/^Cash$/).length).toBeGreaterThan(0);
    expect(within(main).getAllByText(/^UPI$/).length).toBeGreaterThan(0);
    expect(within(main).getAllByText(/^Card$/).length).toBeGreaterThan(0);
    expect(within(main).getAllByText(/^Other$/).length).toBeGreaterThan(0);
    expect(within(main).getByText('Scope: Main Branch · 2026-06-13')).toBeInTheDocument();
  });

  it('renders empty states safely when the report has no data', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      createJsonResponse(
        makeReport({
          summary: {
            totalBills: 0,
            paidBills: 0,
            partiallyPaidBills: 0,
            unpaidBills: 0,
            cancelledBills: 0,
            grossSales: 0,
            grossBillTotal: 0,
            cancelledBillAmount: 0,
            netSales: 0,
            totalAmountPaid: 0,
            totalBalanceDue: 0,
            cashPayments: 0,
            upiPayments: 0,
            cardPayments: 0,
            otherPayments: 0,
            nonCashPayments: 0,
            openShifts: 0,
            closedShifts: 0,
            openingCashTotal: 0,
            declaredClosingCashTotal: 0,
            expectedCashTotal: 0,
            receiptPrints: 0,
            receiptReprints: 0,
            cashVarianceTotal: 0,
          },
          paymentBreakdown: [],
          cashShiftSummaries: [],
          exceptions: {
            unpaidBills: [],
            cancelledBills: [],
            cancelledPayments: [],
            receiptReprints: [],
            cashVariances: [],
            openShifts: [],
          },
        })
      )
    );

    renderReportRoute(['Report.View'], fetchMock);

    expect(await screen.findByRole('heading', { name: /daily cash sales report/i })).toBeInTheDocument();
    expect(screen.getAllByText(/0\.00/).length).toBeGreaterThan(0);
    expect(screen.getByText(/no cashier shifts/i)).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /no unpaid bills/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /no cancelled bills/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /no closed shift variances/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /no open shifts/i })).toBeInTheDocument();
  });

  it('sanitizes raw backend errors and does not leak SQL or HTML content', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      createJsonResponse(
        {
          title: 'Bad Request',
          detail: 'Microsoft.Data.SqlClient.SqlException: stack trace <html>token</html>',
        },
        { status: 400 }
      )
    );
    renderReportRoute(['Report.View'], fetchMock);

    expect(await screen.findByRole('alert')).toHaveTextContent(/unable to load the daily cash sales report/i);
    expect(screen.queryByText(/sqlclient/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/stack trace/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/<html>/i)).not.toBeInTheDocument();
  });

  it('does not expose mutation, export, gateway, tax, inventory, or kitchen controls', async () => {
    const fetchMock = vi.fn().mockResolvedValue(createJsonResponse(makeReport()));
    renderReportRoute(['Report.View'], fetchMock);

    const main = await screen.findByRole('main');
    expect(within(main).queryByRole('button', { name: /export/i })).not.toBeInTheDocument();
    expect(within(main).queryByRole('button', { name: /delete/i })).not.toBeInTheDocument();
    expect(within(main).queryByRole('button', { name: /print/i })).not.toBeInTheDocument();
    expect(within(main).queryByRole('button', { name: /gateway/i })).not.toBeInTheDocument();
    expect(within(main).queryByRole('button', { name: /tax/i })).not.toBeInTheDocument();
    expect(within(main).queryByRole('button', { name: /inventory/i })).not.toBeInTheDocument();
    expect(within(main).queryByRole('button', { name: /kitchen/i })).not.toBeInTheDocument();
    expect(within(main).queryByRole('link', { name: /gateway/i })).not.toBeInTheDocument();
    expect(within(main).queryByRole('link', { name: /tax/i })).not.toBeInTheDocument();
    expect(within(main).queryByRole('link', { name: /inventory/i })).not.toBeInTheDocument();
    expect(within(main).queryByRole('link', { name: /kitchen/i })).not.toBeInTheDocument();
  });
});
