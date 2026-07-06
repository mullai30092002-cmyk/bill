import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import App from '../../App';
import { clearAuthSession, createJsonResponse, storeAuthSession } from '../../test/authTestUtils';
import { renderWithRouter } from '../../test/renderWithRouter';
import type { VendorPayablesReportResponse } from './vendorPayablesReportTypes';

const makeReport = (overrides: Partial<VendorPayablesReportResponse> = {}): VendorPayablesReportResponse => ({
  restaurantId: 'restaurant-1',
  restaurantCode: 'DEMO',
  restaurantName: 'Demo Restaurant',
  branchId: 'branch-1',
  branchName: 'Main Branch',
  fromDate: '2026-06-01',
  toDate: '2026-06-30',
  currencyCode: 'SGD',
  generatedAt: '2026-06-18T10:30:00Z',
  summary: {
    totalVendorBills: 4,
    totalPurchaseAmount: 400,
    totalPaidAmount: 170,
    totalOutstandingAmount: 230,
    unpaidBillCount: 1,
    partiallyPaidBillCount: 1,
    paidBillCount: 1,
    cancelledBillCount: 1,
    overdueBillCount: 2,
  },
  vendorBalances: [
    {
      vendorId: 'vendor-1',
      vendorName: 'Fresh Rice',
      vendorType: 'Groceries',
      totalBills: 2,
      purchaseAmount: 200,
      paidAmount: 90,
      outstandingAmount: 110,
      unpaidCount: 1,
      partiallyPaidCount: 1,
      overdueCount: 1,
    },
  ],
  overdueBills: [
    {
      billNumber: 'VB-001',
      vendorName: 'Fresh Rice',
      vendorType: 'Groceries',
      branchName: 'Main Branch',
      billDate: '2026-06-11T00:00:00Z',
      dueDate: '2026-06-12T00:00:00Z',
      totalAmount: 100,
      paidAmount: 30,
      outstandingAmount: 70,
      status: 'PartiallyPaid',
    },
  ],
  recentSettlements: [
    {
      vendorName: 'Fresh Rice',
      billNumber: 'VB-001',
      branchName: 'Main Branch',
      paidAtUtc: '2026-06-12T09:15:00Z',
      amount: 30,
      paymentMode: 'UPI',
      referenceNumberMasked: '****3456',
    },
  ],
  inventoryPurchaseTotals: [
    {
      inventoryItemName: 'Rice',
      quantity: 10,
      amount: 100,
    },
  ],
  ...overrides,
});

const renderReportRoute = (permissions: string[], fetchMock: ReturnType<typeof vi.fn>, path = '/reports/vendor-payables') => {
  clearAuthSession();
  storeAuthSession({
    permissions,
    roles: ['Admin'],
    activeRole: 'Admin',
  });
  vi.stubGlobal('fetch', fetchMock);
  return renderWithRouter(<App />, path);
};

describe('VendorPayablesReportPage', () => {
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

  it('loads the report and sends the branch and date filters when branchId is present', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(
        createJsonResponse({
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
        })
      )
      .mockResolvedValueOnce(createJsonResponse(makeReport()));

    renderReportRoute(['Report.View', 'Branch.Manage'], fetchMock, '/reports/vendor-payables?branchId=branch-1&fromDate=2026-06-01&toDate=2026-06-30');

    expect(await screen.findByRole('heading', { name: /vendor payables report/i })).toBeInTheDocument();
    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2));

    const reportCall = fetchMock.mock.calls[1];
    expect(String(reportCall[0])).toContain('/api/v1/reports/vendor-payables');
    expect(String(reportCall[0])).toContain('branchId=branch-1');
    expect(String(reportCall[0])).toContain('fromDate=2026-06-01');
    expect(String(reportCall[0])).toContain('toDate=2026-06-30');
  });

  it('renders the summary cards, vendor balances, overdue bills, settlement history, and inventory totals', async () => {
    const fetchMock = vi.fn().mockResolvedValue(createJsonResponse(makeReport()));
    renderReportRoute(['Report.View'], fetchMock, '/reports/vendor-payables?fromDate=2026-06-01&toDate=2026-06-30');

    expect((await screen.findAllByText(/purchase total/i)).length).toBeGreaterThan(0);
    expect(screen.getByText(/400\.00/)).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /vendor balances/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /overdue vendor bills/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /settlement history/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /inventory purchase totals/i })).toBeInTheDocument();
    expect(screen.getAllByText(/fresh rice/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/vb-001/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/^rice$/i).length).toBeGreaterThan(0);
    expect(within(await screen.findByRole('main')).getByText(/scope: demo restaurant · main branch · 2026-06-01 to 2026-06-30/i)).toBeInTheDocument();
  });

  it('renders empty states safely when the report has no data', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      createJsonResponse(
        makeReport({
          branchId: null,
          branchName: null,
          summary: {
            totalVendorBills: 0,
            totalPurchaseAmount: 0,
            totalPaidAmount: 0,
            totalOutstandingAmount: 0,
            unpaidBillCount: 0,
            partiallyPaidBillCount: 0,
            paidBillCount: 0,
            cancelledBillCount: 0,
            overdueBillCount: 0,
          },
          vendorBalances: [],
          overdueBills: [],
          recentSettlements: [],
          inventoryPurchaseTotals: [],
        })
      )
    );

    renderReportRoute(['Report.View'], fetchMock);

    expect(await screen.findByRole('heading', { name: /vendor payables report/i })).toBeInTheDocument();
    expect(screen.getAllByText(/0\.00/).length).toBeGreaterThan(0);
    expect(screen.getByRole('heading', { name: /no vendor balances/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /no overdue bills/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /no settlements/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /no inventory-linked purchases/i })).toBeInTheDocument();
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

    expect(await screen.findByRole('alert')).toHaveTextContent(/unable to load the vendor payables report/i);
    expect(screen.queryByText(/sqlclient/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/stack trace/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/<html>/i)).not.toBeInTheDocument();
  });

  it('reloads when the date range changes', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(createJsonResponse(makeReport({ fromDate: '2026-06-01', toDate: '2026-06-30' })))
      .mockResolvedValueOnce(createJsonResponse(makeReport({ fromDate: '2026-06-01', toDate: '2026-06-29' })));

    renderReportRoute(['Report.View'], fetchMock, '/reports/vendor-payables?fromDate=2026-06-01&toDate=2026-06-30');

    const toDateInput = await screen.findByLabelText(/to date/i);
    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1));

    fireEvent.change(toDateInput, { target: { value: '2026-06-29' } });

    await waitFor(() => expect(fetchMock.mock.calls.length).toBeGreaterThanOrEqual(2));
    const lastCall = fetchMock.mock.calls[fetchMock.mock.calls.length - 1];
    expect(String(lastCall?.[0])).toContain('toDate=2026-06-29');
  });
});
