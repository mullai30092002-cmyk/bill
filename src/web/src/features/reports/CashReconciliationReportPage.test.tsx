import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';

import App from '../../App';
import { clearAuthSession, createJsonResponse, storeAuthSession } from '../../test/authTestUtils';
import { LANGUAGE_STORAGE_KEY } from '../../i18n/translations';
import { renderWithRouter } from '../../test/renderWithRouter';
import { formatCashReconciliationCurrency } from './cashReconciliationReportDisplay';
import type { CashReconciliationReportResponse } from './cashReconciliationReportTypes';

const buildReportResponse = (overrides: Partial<CashReconciliationReportResponse> = {}) =>
  createJsonResponse({
    restaurantId: 'restaurant-1',
    restaurantName: 'Demo Restaurant',
    branchId: 'branch-1',
    branchName: 'Main Branch',
    businessDate: '2026-06-13',
    generatedAtUtc: '2026-06-13T10:30:00Z',
    currencyCode: 'SGD',
    totals: {
      shiftCount: 2,
      openShiftCount: 1,
      closedShiftCount: 1,
      openingCashTotal: 150,
      cashPaymentTotal: 110,
      cashInTotal: 20,
      cashOutTotal: 10,
      adjustmentTotal: 5,
      expectedCashTotal: 275,
      declaredCashTotal: 280,
      varianceTotal: 5,
      majorVarianceCount: 0,
      minorVarianceCount: 1,
      balancedShiftCount: 0,
    },
    shifts: [
      {
        cashierShiftId: 'shift-1',
        branchId: 'branch-1',
        branchName: 'Main Branch',
        cashierUserId: 'user-1',
        cashierName: 'Asha',
        status: 'Closed',
        openedAt: '2026-06-13T02:00:00Z',
        closedAt: '2026-06-13T10:00:00Z',
        openingCashAmount: 100,
        cashPaymentTotal: 90,
        cashInTotal: 20,
        cashOutTotal: 10,
        adjustmentTotal: 5,
        expectedCashAmount: 205,
        declaredClosingCashAmount: 210,
        varianceAmount: 5,
        varianceStatus: 'MinorVariance',
        paymentCount: 2,
        movementCount: 3,
        closingNote: 'Counted at close',
      },
      {
        cashierShiftId: 'shift-2',
        branchId: 'branch-1',
        branchName: 'Main Branch',
        cashierUserId: 'user-2',
        cashierName: 'Mohan',
        status: 'Open',
        openedAt: '2026-06-13T06:00:00Z',
        closedAt: null,
        openingCashAmount: 50,
        cashPaymentTotal: 20,
        cashInTotal: 0,
        cashOutTotal: 0,
        adjustmentTotal: 0,
        expectedCashAmount: 70,
        declaredClosingCashAmount: null,
        varianceAmount: null,
        varianceStatus: 'OpenShift',
        paymentCount: 1,
        movementCount: 0,
        closingNote: null,
      },
    ],
    ...overrides,
  });

afterEach(() => {
  clearAuthSession();
  localStorage.removeItem(LANGUAGE_STORAGE_KEY);
  vi.unstubAllGlobals();
});

describe('CashReconciliationReportPage', () => {
  it('renders the report chrome, summary cards, table, and navigation link', async () => {
    storeAuthSession({
      permissions: ['Report.View', 'Branch.Manage'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.includes('/api/v1/admin/branches')) {
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
              createdAt: '2026-06-01T00:00:00Z',
              updatedAt: '2026-06-13T00:00:00Z',
            },
          ],
        });
      }

      return buildReportResponse();
    }));

    renderWithRouter(<App />, '/reports/cash-reconciliation?businessDate=2026-06-13&branchId=branch-1');

    expect(await screen.findByRole('heading', { level: 1, name: /cash reconciliation/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/branch/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/business date/i)).toBeInTheDocument();
    expect(screen.getAllByRole('link', { name: /cash reconciliation/i }).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/opening cash/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/cash payments/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/expected cash/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/declared cash/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/variance/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/open shifts/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/major cash variance/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/cashier/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/movements/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/asha/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/mohan/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/minor variance/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/open shift/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(formatCashReconciliationCurrency(205, 'SGD')).length).toBeGreaterThan(0);
  });

  it('renders the empty state when no shifts are returned', async () => {
    storeAuthSession({
      permissions: ['Report.View'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });
    vi.stubGlobal('fetch', vi.fn(async () =>
      buildReportResponse({
        totals: {
          shiftCount: 0,
          openShiftCount: 0,
          closedShiftCount: 0,
          openingCashTotal: 0,
          cashPaymentTotal: 0,
          cashInTotal: 0,
          cashOutTotal: 0,
          adjustmentTotal: 0,
          expectedCashTotal: 0,
          declaredCashTotal: 0,
          varianceTotal: 0,
          majorVarianceCount: 0,
          minorVarianceCount: 0,
          balancedShiftCount: 0,
        },
        shifts: [],
      })
    ));

    renderWithRouter(<App />, '/reports/cash-reconciliation?businessDate=2026-06-13');

    expect(await screen.findByRole('heading', { level: 1, name: /cash reconciliation/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /no cashier shifts found/i })).toBeInTheDocument();
  });

  it('renders safe errors without leaking backend details', async () => {
    storeAuthSession({
      permissions: ['Report.View'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });
    vi.stubGlobal('fetch', vi.fn(async () =>
      createJsonResponse(
        {
          title: 'Bad Request',
          detail: 'Microsoft.Data.SqlClient.SqlException: stack trace <html>token</html>',
        },
        { status: 400 }
      )
    ));

    renderWithRouter(<App />, '/reports/cash-reconciliation?businessDate=2026-06-13');

    expect(await screen.findByRole('alert')).toHaveTextContent(/unable to load the cash reconciliation report/i);
    expect(screen.queryByText(/sqlclient/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/stack trace/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/<html>/i)).not.toBeInTheDocument();
  });

  it('shows variance status labels for the four status types', async () => {
    storeAuthSession({
      permissions: ['Report.View'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });
    vi.stubGlobal('fetch', vi.fn(async () =>
      buildReportResponse({
        totals: {
          shiftCount: 4,
          openShiftCount: 1,
          closedShiftCount: 3,
          openingCashTotal: 200,
          cashPaymentTotal: 100,
          cashInTotal: 0,
          cashOutTotal: 0,
          adjustmentTotal: 0,
          expectedCashTotal: 300,
          declaredCashTotal: 301,
          varianceTotal: 1,
          majorVarianceCount: 1,
          minorVarianceCount: 1,
          balancedShiftCount: 1,
        },
        shifts: [
          {
            cashierShiftId: 'shift-balanced',
            branchId: 'branch-1',
            branchName: 'Main Branch',
            cashierUserId: 'user-1',
            cashierName: 'Balanced',
            status: 'Closed',
            openedAt: '2026-06-13T02:00:00Z',
            closedAt: '2026-06-13T10:00:00Z',
            openingCashAmount: 100,
            cashPaymentTotal: 0,
            cashInTotal: 0,
            cashOutTotal: 0,
            adjustmentTotal: 0,
            expectedCashAmount: 100,
            declaredClosingCashAmount: 100,
            varianceAmount: 0,
            varianceStatus: 'Balanced',
            paymentCount: 0,
            movementCount: 0,
            closingNote: null,
          },
          {
            cashierShiftId: 'shift-minor',
            branchId: 'branch-1',
            branchName: 'Main Branch',
            cashierUserId: 'user-2',
            cashierName: 'Minor',
            status: 'Closed',
            openedAt: '2026-06-13T03:00:00Z',
            closedAt: '2026-06-13T10:30:00Z',
            openingCashAmount: 100,
            cashPaymentTotal: 0,
            cashInTotal: 0,
            cashOutTotal: 0,
            adjustmentTotal: 0,
            expectedCashAmount: 100,
            declaredClosingCashAmount: 101,
            varianceAmount: 1,
            varianceStatus: 'MinorVariance',
            paymentCount: 0,
            movementCount: 0,
            closingNote: null,
          },
          {
            cashierShiftId: 'shift-major',
            branchId: 'branch-1',
            branchName: 'Main Branch',
            cashierUserId: 'user-3',
            cashierName: 'Major',
            status: 'Closed',
            openedAt: '2026-06-13T04:00:00Z',
            closedAt: '2026-06-13T11:00:00Z',
            openingCashAmount: 100,
            cashPaymentTotal: 0,
            cashInTotal: 0,
            cashOutTotal: 0,
            adjustmentTotal: 0,
            expectedCashAmount: 100,
            declaredClosingCashAmount: 220,
            varianceAmount: 120,
            varianceStatus: 'MajorVariance',
            paymentCount: 0,
            movementCount: 0,
            closingNote: null,
          },
          {
            cashierShiftId: 'shift-open',
            branchId: 'branch-1',
            branchName: 'Main Branch',
            cashierUserId: 'user-4',
            cashierName: 'Open',
            status: 'Open',
            openedAt: '2026-06-13T05:00:00Z',
            closedAt: null,
            openingCashAmount: 100,
            cashPaymentTotal: 0,
            cashInTotal: 0,
            cashOutTotal: 0,
            adjustmentTotal: 0,
            expectedCashAmount: 100,
            declaredClosingCashAmount: null,
            varianceAmount: null,
            varianceStatus: 'OpenShift',
            paymentCount: 0,
            movementCount: 0,
            closingNote: null,
          },
        ],
      })
    ));

    renderWithRouter(<App />, '/reports/cash-reconciliation?businessDate=2026-06-13');

    expect(await screen.findByRole('heading', { level: 1, name: /cash reconciliation/i })).toBeInTheDocument();
    expect(screen.getAllByText(/^Balanced$/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/minor variance/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/major variance/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/open shift/i).length).toBeGreaterThan(0);
  });

  it('renders Tamil chrome', async () => {
    localStorage.setItem('billsoft.language', 'ta');
    storeAuthSession({
      permissions: ['Report.View'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });
    vi.stubGlobal('fetch', vi.fn(async () => buildReportResponse({ shifts: [] })));

    renderWithRouter(<App />, '/reports/cash-reconciliation?businessDate=2026-06-13');

    expect(await screen.findByRole('heading', { level: 1, name: /பணம் சமனாக்குதல்/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/வணிக தேதி/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /புதுப்பி/i })).toBeInTheDocument();
  });
});
