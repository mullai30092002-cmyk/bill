import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';

import App from '../../App';
import { clearAuthSession, createJsonResponse, storeAuthSession } from '../../test/authTestUtils';
import { LANGUAGE_STORAGE_KEY } from '../../i18n/translations';
import { renderWithRouter } from '../../test/renderWithRouter';

const buildDashboardResponse = () =>
  createJsonResponse({
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
    alerts: [],
    quickLinks: [],
    vendorDues: {
      totalVendorOutstanding: 0,
      overdueVendorCount: 0,
      vendorsWithOutstandingCount: 0,
      criticalVendors: [],
    },
    inventoryAlerts: {
      lowStockCount: 0,
      outOfStockCount: 0,
      totalAlertCount: 0,
      criticalItems: [],
    },
  });

const buildSetupChecklistResponse = () =>
  createJsonResponse({
    restaurantId: 'restaurant-1',
    restaurantName: 'Demo Restaurant',
    businessType: 'Restaurant',
    branchId: 'branch-1',
    branchName: 'Main Branch',
    completionPercent: 80,
    completedCount: 8,
    totalCount: 10,
    items: [],
  });

afterEach(() => {
  clearAuthSession();
  localStorage.removeItem(LANGUAGE_STORAGE_KEY);
  vi.unstubAllGlobals();
});

describe('Owner dashboard Tamil chrome', () => {
  it('renders Tamil owner dashboard chrome', async () => {
    localStorage.setItem(LANGUAGE_STORAGE_KEY, 'ta');
    storeAuthSession({
      permissions: ['Report.View'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });
    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: RequestInfo | URL) => {
        const url = String(input);
        const pathname = new URL(url, 'http://localhost').pathname;

        if (pathname === '/api/v1/dashboard/owner') {
          return buildDashboardResponse();
        }

        if (pathname === '/api/v1/setup/checklist') {
          return buildSetupChecklistResponse();
        }

        throw new Error(`Unexpected fetch in owner dashboard Tamil test: ${url}`);
      })
    );

    renderWithRouter(<App />, '/owner/dashboard');

    expect(await screen.findByRole('heading', { name: /உரிமையாளர் டாஷ்போர்டு/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /டாஷ்போர்டை புதுப்பி/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /பைலட் அமைப்பு/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /அமைப்பு சரிபார்ப்பு பட்டியலைப் பார்க்க/i })).toBeInTheDocument();
  });
});
