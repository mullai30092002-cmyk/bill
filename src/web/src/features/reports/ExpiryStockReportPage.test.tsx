import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';

import App from '../../App';
import { clearAuthSession, createJsonResponse, storeAuthSession } from '../../test/authTestUtils';
import { LANGUAGE_STORAGE_KEY } from '../../i18n/translations';
import { renderWithRouter } from '../../test/renderWithRouter';

const buildReportResponse = (overrides: Record<string, unknown> = {}) =>
  createJsonResponse({
    branchId: 'branch-1',
    branchName: 'Main Branch',
    asOfDate: '2026-06-22',
    totals: {
      freshCount: 1,
      nearExpiryCount: 0,
      expiredCount: 0,
      noExpiryCount: 0,
      totalTrackedItems: 1,
    },
    rows: [
      {
        inventoryItemId: 'item-1',
        inventoryItemName: 'Milk',
        unitOfMeasure: 'L',
        sourceType: 'OpeningLot',
        batchReference: 'Opening lot',
        quantity: 6,
        producedOrReceivedAt: '2026-06-10T00:00:00Z',
        expiresAtUtc: null,
        expiryStatus: 'NoExpiry',
        warningReason: null,
        sourceReference: 'LOT-00000000000000000000000000000001',
      },
    ],
    ...overrides,
  });

const renderReportRoute = (language: 'en' | 'ta', fetchMock: ReturnType<typeof vi.fn>) => {
  clearAuthSession();
  localStorage.setItem(LANGUAGE_STORAGE_KEY, language);
  storeAuthSession({
    permissions: ['Report.View'],
    roles: ['Admin'],
    activeRole: 'Admin',
    branchId: 'branch-1',
  });
  vi.stubGlobal('fetch', fetchMock);
  return renderWithRouter(<App />, '/reports/expiry-stock?asOfDate=2026-06-22&branchId=branch-1');
};

afterEach(() => {
  clearAuthSession();
  localStorage.removeItem(LANGUAGE_STORAGE_KEY);
  vi.unstubAllGlobals();
});

describe('ExpiryStockReportPage', () => {
  it('renders the updated lot remaining label and opening lot source', async () => {
    const fetchMock = vi.fn().mockResolvedValue(buildReportResponse());
    renderReportRoute('en', fetchMock);

    expect((await screen.findAllByRole('heading', { name: /^Expiry Stock Report$/ })).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/Lot remaining/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/Opening lot/i).length).toBeGreaterThan(0);
    expect(screen.getByText(/expiry status is based on the selected as-of date/i)).toBeInTheDocument();
  });

  it('renders the updated lot remaining label in Tamil', async () => {
    const fetchMock = vi.fn().mockResolvedValue(buildReportResponse());
    renderReportRoute('ta', fetchMock);

    expect((await screen.findAllByRole('heading', { name: /காலாவதி இருப்பு அறிக்கை/i })).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/குவியல் மீதம்/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/தொடக்க குவியல்/i).length).toBeGreaterThan(0);
    expect(screen.getByText(/தேர்ந்தெடுத்த as-of தேதியை அடிப்படையாகக் கொண்டது/i)).toBeInTheDocument();
  });

  it('renders an empty state safely', async () => {
    const fetchMock = vi.fn().mockResolvedValue(buildReportResponse({ totals: { freshCount: 0, nearExpiryCount: 0, expiredCount: 0, noExpiryCount: 0, totalTrackedItems: 0 }, rows: [] }));
    renderReportRoute('en', fetchMock);

    expect((await screen.findAllByRole('heading', { name: /^Expiry Stock Report$/ })).length).toBeGreaterThan(0);
    expect(screen.getByRole('heading', { name: /No expiry-tracked stock/i })).toBeInTheDocument();
  });
});
