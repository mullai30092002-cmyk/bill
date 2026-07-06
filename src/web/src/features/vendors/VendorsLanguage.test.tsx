import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';

import App from '../../App';
import { clearAuthSession, createJsonResponse, storeAuthSession } from '../../test/authTestUtils';
import { LANGUAGE_STORAGE_KEY } from '../../i18n/translations';
import { renderWithRouter } from '../../test/renderWithRouter';

const branchesPath = '/api/v1/admin/branches';
const vendorsPath = '/api/v1/vendors';
const vendorBillsPath = '/api/v1/vendor-bills';
const inventoryItemsPath = '/api/v1/inventory/items';
const vendorPayablesPath = '/api/v1/reports/vendor-payables';

const makeBranchesResponse = () =>
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
  });

const makeVendorsResponse = () =>
  createJsonResponse({
    items: [
      {
        vendorId: 'vendor-1',
        restaurantId: 'restaurant-1',
        name: 'ABC Supplies',
        vendorType: 'Supplier',
        contactName: 'Ravi',
        contactMobile: '91000000',
        creditDays: 30,
        creditLimit: 5000,
        status: 'Active',
        createdAtUtc: '2026-06-11T09:00:00Z',
        updatedAtUtc: null,
      },
    ],
  });

const makeEmptyResponse = () => createJsonResponse({ items: [] });

const setupFetch = (responses: Record<string, Response[]>) => {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const method = (init?.method ?? 'GET').toUpperCase();
    const path = new URL(String(input)).pathname;
    const key = `${method} ${path}`;
    const queue = responses[key];
    if (!queue || queue.length === 0) {
      throw new Error(`Unhandled request: ${key}`);
    }
    return queue.shift()!;
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
};

afterEach(() => {
  clearAuthSession();
  localStorage.removeItem(LANGUAGE_STORAGE_KEY);
  vi.unstubAllGlobals();
});

describe('Vendors Tamil chrome', () => {
  it('renders Tamil UI chrome for the vendor workspace', async () => {
    localStorage.setItem(LANGUAGE_STORAGE_KEY, 'ta');
    storeAuthSession({
      userId: 'session-user',
      permissions: ['VendorBill.Confirm', 'VendorPayment.Create', 'Branch.Manage'],
      roles: ['Admin'],
      activeRole: 'Admin',
      branchId: 'branch-1',
    });

    setupFetch({
      [`GET ${branchesPath}`]: [makeBranchesResponse()],
      [`GET ${vendorsPath}`]: [makeEmptyResponse()],
      [`GET ${vendorBillsPath}`]: [makeEmptyResponse()],
      [`GET ${inventoryItemsPath}`]: [makeEmptyResponse()],
    });

    renderWithRouter(<App />, '/vendors');

    expect(await screen.findByRole('heading', { name: /வெண்டர் பணியிடம்/i })).toBeInTheDocument();
    expect(screen.getByText(/இன்னும் வெண்டர்கள் இல்லை/i)).toBeInTheDocument();
  });

  it('renders Tamil UI chrome with a vendor listed', async () => {
    localStorage.setItem(LANGUAGE_STORAGE_KEY, 'ta');
    storeAuthSession({
      userId: 'session-user',
      permissions: ['VendorBill.Confirm', 'VendorPayment.Create', 'Branch.Manage'],
      roles: ['Admin'],
      activeRole: 'Admin',
      branchId: 'branch-1',
    });

    setupFetch({
      [`GET ${branchesPath}`]: [makeBranchesResponse()],
      [`GET ${vendorsPath}`]: [makeVendorsResponse()],
      [`GET ${vendorBillsPath}`]: [makeEmptyResponse()],
      [`GET ${inventoryItemsPath}`]: [makeEmptyResponse()],
    });

    renderWithRouter(<App />, '/vendors');

    expect(await screen.findByRole('heading', { name: /வெண்டர் பணியிடம்/i })).toBeInTheDocument();
    expect((await screen.findAllByText('ABC Supplies')).length).toBeGreaterThan(0);
  });

  it('renders the Tamil vendor payable report and vendor section chrome', async () => {
    localStorage.setItem(LANGUAGE_STORAGE_KEY, 'ta');
    storeAuthSession({
      userId: 'session-user',
      permissions: ['VendorBill.Confirm', 'VendorPayment.Create', 'Branch.Manage', 'Report.View'],
      roles: ['Admin'],
      activeRole: 'Admin',
      branchId: 'branch-1',
    });

    setupFetch({
      [`GET ${branchesPath}`]: [makeBranchesResponse()],
      [`GET ${vendorsPath}`]: [makeEmptyResponse()],
      [`GET ${vendorBillsPath}`]: [makeEmptyResponse()],
      [`GET ${inventoryItemsPath}`]: [makeEmptyResponse()],
      [`GET ${vendorPayablesPath}`]: [
        createJsonResponse({
          restaurantId: 'restaurant-1',
          restaurantCode: 'DEMO',
          restaurantName: 'Demo Restaurant',
          branchId: 'branch-1',
          branchName: 'Main Branch',
          fromDate: '2026-06-01',
          toDate: '2026-06-30',
          currencyCode: 'SGD',
          generatedAt: '2026-06-11T09:00:00Z',
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
        }),
      ],
    });

    renderWithRouter(<App />, '/vendors');

    expect(await screen.findByRole('heading', { name: /வெண்டர் பணியிடம்/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /வெண்டர் பாக்கி அறிக்கை/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /வெண்டர் பாக்கி அறிக்கையை பார்/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /^வெண்டர்கள்$/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /புதிய வெண்டர்/i })).toBeInTheDocument();
  });
});
