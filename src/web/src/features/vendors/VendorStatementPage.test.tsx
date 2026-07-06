import { screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import App from '../../App';
import { clearAuthSession, createJsonResponse, storeAuthSession } from '../../test/authTestUtils';
import { renderWithRouter } from '../../test/renderWithRouter';

const branchesPath = '/api/v1/admin/branches';
const vendorsPath = '/api/v1/vendors';
const statementPath = '/api/v1/vendors/vendor-1/statement';

const setupFetch = (responses: Record<string, Response[]>) => {
  const fetchMock = vi.fn(async (input, init) => {
    const method = (init?.method ?? 'GET').toUpperCase();
    const url = new URL(String(input));
    const key = `${method} ${url.pathname}`;
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
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe('VendorStatementPage', () => {
  it('shows readable bill numbers instead of internal ids', async () => {
    clearAuthSession();
    storeAuthSession({
      permissions: ['VendorBill.Confirm', 'VendorPayment.Create', 'Branch.Manage'],
      roles: ['Admin'],
      activeRole: 'Admin',
      branchId: 'branch-1',
    });

    setupFetch({
      [`GET ${branchesPath}`]: [
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
        }),
      ],
      [`GET ${vendorsPath}`]: [
        createJsonResponse({
          items: [
            {
              vendorId: 'vendor-1',
              restaurantId: 'restaurant-1',
              branchId: 'branch-1',
              name: 'Fresh Rice',
              normalizedName: 'FRESH RICE',
              vendorType: 'Groceries',
              contactName: 'Kumar',
              mobileNumber: '90010001',
              address: 'Market Road',
              notes: null,
              isActive: true,
              createdAtUtc: '2026-06-11T09:00:00Z',
              updatedAtUtc: null,
            },
          ],
        }),
      ],
      [`GET ${statementPath}`]: [
        createJsonResponse({
          restaurantId: 'restaurant-1',
          branchId: 'branch-1',
          branchName: 'Main Branch',
          vendorId: 'vendor-1',
          vendorName: 'Fresh Rice',
          vendorType: 'Groceries',
          currencyCode: 'SGD',
          fromDate: '2026-06-01',
          toDate: '2026-06-30',
          generatedAt: '2026-06-11T09:00:00Z',
          openingOutstandingAmount: 0,
          currentOutstandingAmount: 60,
          summary: {
            totalBillAmount: 100,
            totalSettlementAmount: 40,
            payableBillCount: 1,
            settlementCount: 1,
            overdueBillCount: 0,
          },
          payableBills: [
            {
              vendorBillId: 'bill-1',
              branchId: 'branch-1',
              branchName: 'Main Branch',
              billNumber: 'VB-010',
              billDate: '2026-06-11T00:00:00Z',
              dueDate: null,
              status: 'PartiallyPaid',
              totalAmount: 100,
              paidAmount: 40,
              outstandingAmount: 60,
              notes: null,
              createdAtUtc: '2026-06-11T09:00:00Z',
            },
          ],
          settlements: [
            {
              vendorSettlementId: 'settlement-1',
              vendorBillId: 'bill-1',
              branchId: 'branch-1',
              branchName: 'Main Branch',
              billNumber: 'VB-010',
              paidAtUtc: '2026-06-11T09:30:00Z',
              paymentMode: 'Cash',
              amount: 40,
              referenceNumberMasked: null,
              notes: null,
              previousOutstandingAmount: 100,
              newOutstandingAmount: 60,
              status: 'Active',
            },
          ],
          timeline: [
            {
              entryType: 'Settlement',
              timestampUtc: '2026-06-11T09:30:00Z',
              billNumber: 'VB-010',
              reference: null,
              description: 'Settlement recorded',
              debitAmount: 0,
              creditAmount: 40,
              runningBalance: 60,
              paymentMode: 'Cash',
              status: 'Active',
            },
          ],
        }),
      ],
    });

    renderWithRouter(<App />, '/vendors/statement');

    expect(await screen.findByRole('heading', { name: /vendor statement/i })).toBeInTheDocument();
    expect((await screen.findAllByText(/VB-010/)).length).toBeGreaterThan(0);
    expect(screen.queryByText('bill-1')).not.toBeInTheDocument();
  });
});
