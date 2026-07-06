import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';

import App from '../../App';
import { clearAuthSession, createJsonResponse, storeAuthSession } from '../../test/authTestUtils';
import { LANGUAGE_STORAGE_KEY } from '../../i18n/translations';
import { renderWithRouter } from '../../test/renderWithRouter';

const buildReportResponse = () =>
  createJsonResponse({
    restaurantId: 'restaurant-1',
    restaurantCode: 'DEMO',
    restaurantName: 'Demo Restaurant',
    branchId: 'branch-1',
    branchName: 'Main Branch',
    businessDate: '2026-06-13',
    currencyCode: 'SGD',
    generatedAt: '2026-06-13T10:30:00Z',
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
  });

afterEach(() => {
  clearAuthSession();
  localStorage.removeItem(LANGUAGE_STORAGE_KEY);
  vi.unstubAllGlobals();
});

describe('Daily cash sales report Tamil chrome', () => {
  it('renders Tamil daily report chrome', async () => {
    localStorage.setItem(LANGUAGE_STORAGE_KEY, 'ta');
    storeAuthSession({
      permissions: ['Report.View'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });
    vi.stubGlobal('fetch', vi.fn(async () => buildReportResponse()));

    renderWithRouter(<App />, '/reports/daily-cash-sales');

    expect(await screen.findByRole('heading', { name: /தினசரி பண விற்பனை அறிக்கை/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/வணிக தேதி/i)).toBeInTheDocument();
  });
});
