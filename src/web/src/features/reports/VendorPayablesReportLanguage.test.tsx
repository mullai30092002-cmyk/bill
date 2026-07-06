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
    branchId: null,
    branchName: null,
    fromDate: '2026-06-01',
    toDate: '2026-06-30',
    currencyCode: 'SGD',
    generatedAt: '2026-06-18T10:30:00Z',
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
  });

afterEach(() => {
  clearAuthSession();
  localStorage.removeItem(LANGUAGE_STORAGE_KEY);
  vi.unstubAllGlobals();
});

describe('Vendor payables report Tamil chrome', () => {
  it('renders Tamil vendor report chrome', async () => {
    localStorage.setItem(LANGUAGE_STORAGE_KEY, 'ta');
    storeAuthSession({
      permissions: ['Report.View'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });
    vi.stubGlobal('fetch', vi.fn(async () => buildReportResponse()));

    renderWithRouter(<App />, '/reports/vendor-payables');

    expect(await screen.findByRole('heading', { name: /வெண்டர் பாக்கிகள் அறிக்கை/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /அறிக்கையை புதுப்பி/i })).toBeInTheDocument();
  });
});
