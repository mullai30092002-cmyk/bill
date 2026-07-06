import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';

import App from '../../App';
import { clearAuthSession, createJsonResponse, storeAuthSession } from '../../test/authTestUtils';
import { LANGUAGE_STORAGE_KEY } from '../../i18n/translations';
import { renderWithRouter } from '../../test/renderWithRouter';

const buildReportResponse = () =>
  createJsonResponse({
    restaurantId: 'restaurant-1',
    restaurantName: 'Demo Restaurant',
    branchId: 'branch-1',
    branchName: 'Main Branch',
    businessDate: '2026-06-13',
    generatedAtUtc: '2026-06-13T10:30:00Z',
    currencyCode: 'SGD',
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
  });

afterEach(() => {
  clearAuthSession();
  localStorage.removeItem(LANGUAGE_STORAGE_KEY);
  vi.unstubAllGlobals();
});

describe('Cash reconciliation report Tamil chrome', () => {
  it('renders Tamil cash reconciliation chrome', async () => {
    localStorage.setItem(LANGUAGE_STORAGE_KEY, 'ta');
    storeAuthSession({
      permissions: ['Report.View'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });
    vi.stubGlobal('fetch', vi.fn(async () => buildReportResponse()));

    renderWithRouter(<App />, '/reports/cash-reconciliation?businessDate=2026-06-13');

    expect(await screen.findByRole('heading', { level: 1, name: /பணம் சமனாக்குதல்/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/வணிக தேதி/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /புதுப்பி/i })).toBeInTheDocument();
  });
});
