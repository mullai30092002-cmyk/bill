import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import App from '../../App';
import { clearAuthSession, createJsonResponse, storeAuthSession } from '../../test/authTestUtils';
import { renderWithRouter } from '../../test/renderWithRouter';
import type { PreparedStockReportResponse } from './preparedStockReportTypes';

const makeReport = (overrides: Partial<PreparedStockReportResponse> = {}): PreparedStockReportResponse => ({
  branchId: 'branch-1',
  branchName: 'Main Branch',
  businessDate: '2026-06-13',
  totals: {
    producedQuantity: 3,
    servedQuantity: 1,
    wastedQuantity: 1,
    remainingQuantity: 1,
    itemCount: 1,
    warningCount: 0,
  },
  rows: [
    {
      menuItemId: 'menu-1',
      menuItemName: 'Idli',
      preparedInventoryItemId: 'stock-1',
      preparedInventoryItemName: 'Idli Prepared',
      unitOfMeasure: 'pcs',
      producedQuantity: 3,
      servedQuantity: 1,
      wastedQuantity: 1,
      remainingQuantity: 1,
      hasWarning: false,
      warningReason: null,
    },
  ],
  ...overrides,
});

const renderReportRoute = (permissions: string[], fetchMock: ReturnType<typeof vi.fn>, path = '/reports/prepared-stock') => {
  clearAuthSession();
  storeAuthSession({
    permissions,
    roles: ['Admin'],
    activeRole: 'Admin',
    branchId: 'branch-1',
  });
  vi.stubGlobal('fetch', fetchMock);
  return renderWithRouter(<App />, path);
};

describe('PreparedStockReportPage', () => {
  beforeEach(() => {
    clearAuthSession();
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it('shows a not-authorized state without calling the report API', async () => {
    const fetchMock = vi.fn();
    renderReportRoute([], fetchMock, '/reports/prepared-stock?businessDate=2026-06-13&branchId=branch-1');

    expect(await screen.findByRole('heading', { name: /not authorized/i })).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('loads the report and sends the business date and branch query when branchId is present', async () => {
    const fetchMock = vi.fn().mockResolvedValue(createJsonResponse(makeReport()));
    renderReportRoute(['Report.View'], fetchMock, '/reports/prepared-stock?businessDate=2026-06-13&branchId=branch-1');

    expect(await screen.findByRole('heading', { name: /^Prepared stock report$/ })).toBeInTheDocument();
    await waitFor(() => expect(fetchMock).toHaveBeenCalled());

    const firstCall = fetchMock.mock.calls[0];
    expect(String(firstCall[0])).toContain('/api/v1/reports/prepared-stock');
    expect(String(firstCall[0])).toContain('businessDate=2026-06-13');
    expect(String(firstCall[0])).toContain('branchId=branch-1');
  });

  it('renders the KPI cards and table columns', async () => {
    const fetchMock = vi.fn().mockResolvedValue(createJsonResponse(makeReport()));
    renderReportRoute(['Report.View'], fetchMock, '/reports/prepared-stock?businessDate=2026-06-13&branchId=branch-1');

    const main = await screen.findByRole('main');
    expect(within(main).getByRole('heading', { name: /^Prepared stock report$/ })).toBeInTheDocument();
    expect(within(main).getAllByText('Prepared stock item').length).toBeGreaterThan(0);
    expect(within(main).getAllByText('Status').length).toBeGreaterThan(0);
    expect(within(main).getAllByText('Idli').length).toBeGreaterThan(0);
    expect(within(main).getAllByText('Idli Prepared').length).toBeGreaterThan(0);
  });

  it('renders warning rows safely', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      createJsonResponse(
        makeReport({
          totals: {
            producedQuantity: 2,
            servedQuantity: 0,
            wastedQuantity: 0,
            remainingQuantity: 2,
            itemCount: 1,
            warningCount: 1,
          },
          rows: [
            {
              menuItemId: 'menu-1',
              menuItemName: 'Idli',
              preparedInventoryItemId: 'stock-1',
              preparedInventoryItemName: 'Idli Prepared',
              unitOfMeasure: 'pcs',
              producedQuantity: 2,
              servedQuantity: 0,
              wastedQuantity: 0,
              remainingQuantity: 2,
              hasWarning: true,
              warningReason: 'Missing prepared stock mapping.',
            },
          ],
        })
      )
    );

    renderReportRoute(['Report.View'], fetchMock, '/reports/prepared-stock?businessDate=2026-06-13&branchId=branch-1');

    expect(await screen.findByRole('heading', { name: /^Prepared stock report$/ })).toBeInTheDocument();
    expect(screen.getAllByText(/^Warning$/).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/missing prepared stock mapping/i).length).toBeGreaterThan(0);
  });

  it('renders empty state safely when there is no activity', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      createJsonResponse(
        makeReport({
          totals: {
            producedQuantity: 0,
            servedQuantity: 0,
            wastedQuantity: 0,
            remainingQuantity: 0,
            itemCount: 0,
            warningCount: 0,
          },
          rows: [],
        })
      )
    );

    renderReportRoute(['Report.View'], fetchMock, '/reports/prepared-stock?businessDate=2026-06-13&branchId=branch-1');

    expect(await screen.findByRole('heading', { name: /^Prepared stock report$/ })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /^No prepared stock activity$/ })).toBeInTheDocument();
  });
});
