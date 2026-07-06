import { screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import App from './App';
import { createJsonResponse, clearAuthSession, storeAuthSession } from './test/authTestUtils';
import { renderWithRouter } from './test/renderWithRouter';

describe('App routes', () => {
  beforeEach(() => {
    clearAuthSession();
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it.each([
    ['/', 'BillSoft dashboard'],
    ['/orders-preview', 'Order workspace preview'],
    ['/inventory', 'Inventory workspace'],
    ['/vendors', 'Vendor workspace'],
    ['/admin-preview', 'Admin users preview'],
  ])('renders %s route', (path, heading) => {
    storeAuthSession();

    renderWithRouter(<App />, path);

    expect(screen.getByRole('heading', { name: heading })).toBeInTheDocument();
  });

  it('renders the daily cash sales report route with backend data', async () => {
    storeAuthSession({ permissions: ['Report.View'], roles: ['Admin'], activeRole: 'Admin' });

    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValueOnce(
        createJsonResponse({
          restaurantId: 'restaurant-1',
          restaurantCode: 'DEMO',
          restaurantName: 'Demo Restaurant',
          branchId: null,
          branchName: null,
          businessDate: '2026-06-13',
          currencyCode: 'SGD',
          generatedAt: '2026-06-13T10:30:00Z',
          summary: {
            totalBills: 1,
            paidBills: 1,
            partiallyPaidBills: 0,
            unpaidBills: 0,
            cancelledBills: 0,
            grossSales: 12,
            cancelledBillAmount: 0,
            netSales: 12,
            totalAmountPaid: 12,
            totalBalanceDue: 0,
            cashPayments: 12,
            nonCashPayments: 0,
            receiptPrints: 1,
            receiptReprints: 0,
            cashVarianceTotal: 0,
          },
          paymentBreakdown: [
            {
              paymentMode: 'Cash',
              recordedAmount: 12,
              cancelledAmount: 0,
              netAmount: 12,
              paymentCount: 1,
              cancelledCount: 0,
            },
            {
              paymentMode: 'Card',
              recordedAmount: 0,
              cancelledAmount: 0,
              netAmount: 0,
              paymentCount: 0,
              cancelledCount: 0,
            },
            {
              paymentMode: 'Upi',
              recordedAmount: 0,
              cancelledAmount: 0,
              netAmount: 0,
              paymentCount: 0,
              cancelledCount: 0,
            },
            {
              paymentMode: 'Other',
              recordedAmount: 0,
              cancelledAmount: 0,
              netAmount: 0,
              paymentCount: 0,
              cancelledCount: 0,
            },
          ],
          cashShiftSummaries: [],
          exceptions: {
            unpaidBills: [],
            cancelledBills: [],
            cancelledPayments: [],
            receiptReprints: [],
            cashVariances: [],
            openShifts: [],
          },
        })
      )
    );

    renderWithRouter(<App />, '/reports/daily-cash-sales');

    expect(await screen.findByRole('heading', { name: /daily cash sales report/i })).toBeInTheDocument();
    expect(screen.getByText(/gross bill total/i)).toBeInTheDocument();
  });

  it('renders the vendor payables report route with backend data', async () => {
    storeAuthSession({ permissions: ['Report.View'], roles: ['Admin'], activeRole: 'Admin' });

    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValueOnce(
        createJsonResponse({
          restaurantId: 'restaurant-1',
          restaurantCode: 'DEMO',
          restaurantName: 'Demo Restaurant',
          branchId: null,
          branchName: null,
          fromDate: '2026-06-01',
          toDate: '2026-06-30',
          currencyCode: 'SGD',
          generatedAt: '2026-06-13T10:30:00Z',
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

    renderWithRouter(<App />, '/reports/vendor-payables');

    expect(await screen.findByRole('heading', { name: /vendor payables report/i })).toBeInTheDocument();
    expect(screen.getAllByText(/^purchase total$/i).length).toBeGreaterThan(0);
  });

  it('renders the admin users route with backend data', async () => {
    storeAuthSession({ permissions: ['User.Manage', 'Role.Manage'], roles: ['Admin'], activeRole: 'Admin' });

    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValueOnce(
          createJsonResponse({
            items: [
              {
                userId: 'user-1',
                restaurantId: 'restaurant-1',
                branchId: null,
                fullName: 'Asha Kumar',
                mobileNumber: '90001111',
                email: 'asha@example.com',
                status: 'Active',
                roleNames: ['Cashier'],
              },
            ],
            totalCount: 1,
            page: 1,
            pageSize: 20,
          })
        )
        .mockResolvedValueOnce(
          createJsonResponse({
            items: [
              {
                roleId: 'role-1',
                restaurantId: null,
                name: 'Cashier',
                description: 'Front counter user',
                isSystemRole: false,
                isAssignable: true,
                assignmentBlockedReason: null,
                permissionCodes: ['Order.View'],
              },
            ],
          })
        )
    );

    renderWithRouter(<App />, '/admin/users');

    expect(await screen.findByRole('heading', { name: /users and roles/i })).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: /create user/i }).length).toBeGreaterThan(0);
  });

  it('renders the vendor workspace route with backend data', async () => {
    storeAuthSession({
      permissions: ['VendorBill.Confirm', 'VendorPayment.Create', 'Branch.Manage', 'Inventory.Adjust'],
      roles: ['Admin'],
      activeRole: 'Admin',
      branchId: 'branch-1',
    });

    vi.stubGlobal(
      'fetch',
      vi
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
        .mockResolvedValueOnce(createJsonResponse({ items: [] }))
        .mockResolvedValueOnce(createJsonResponse({ items: [] }))
        .mockResolvedValueOnce(createJsonResponse({ items: [] }))
    );

    renderWithRouter(<App />, '/vendors');

    expect(await screen.findByRole('heading', { name: /vendor workspace/i })).toBeInTheDocument();
  });

  it('renders the menu management route with backend data', async () => {
    storeAuthSession({
      permissions: ['MenuItem.View'],
      roles: ['Cashier'],
      activeRole: 'Cashier',
    });

    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValueOnce(
          createJsonResponse({
            items: [
              {
                menuCategoryId: 'category-1',
                restaurantId: 'restaurant-1',
                name: 'Breakfast',
                displayOrder: 1,
                status: 'Active',
                createdAt: '2026-06-11T09:00:00Z',
                updatedAt: '2026-06-11T09:30:00Z',
              },
            ],
          })
        )
        .mockResolvedValueOnce(
          createJsonResponse({
            items: [
              {
                menuItemId: 'item-1',
                restaurantId: 'restaurant-1',
                menuCategoryId: 'category-1',
                categoryName: 'Breakfast',
                name: 'Masala Dosa',
                description: 'Crisp rice crepe',
                sku: 'DOSA-01',
                basePrice: 2.5,
                taxRate: 0,
                isVegetarian: true,
                isAvailableForEatIn: true,
                isAvailableForParcel: true,
                status: 'Active',
                createdAt: '2026-06-11T09:00:00Z',
                updatedAt: '2026-06-11T09:30:00Z',
              },
            ],
          })
        )
    );

    renderWithRouter(<App />, '/admin/menu');

    expect(await screen.findByRole('heading', { name: /menu management/i })).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: /view details/i }).length).toBeGreaterThan(0);
  });

  it('renders the POS orders route with backend data', async () => {
    storeAuthSession({
      permissions: ['Order.Create', 'Order.View'],
      roles: ['Cashier'],
      activeRole: 'Cashier',
    });

    vi.stubGlobal(
      'fetch',
      vi
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
        .mockResolvedValueOnce(
          createJsonResponse({
            items: [
              {
                menuCategoryId: 'category-1',
                restaurantId: 'restaurant-1',
                name: 'Breakfast',
                displayOrder: 1,
                status: 'Active',
                createdAt: '2026-06-11T09:00:00Z',
                updatedAt: '2026-06-11T09:30:00Z',
              },
            ],
          })
        )
        .mockResolvedValueOnce(
          createJsonResponse({
            items: [
              {
                menuItemId: 'item-1',
                restaurantId: 'restaurant-1',
                menuCategoryId: 'category-1',
                categoryName: 'Breakfast',
                name: 'Masala Dosa',
                description: 'Crisp rice crepe',
                sku: 'DOSA-01',
                basePrice: 2.5,
                taxRate: 0,
                isVegetarian: true,
                isAvailableForEatIn: true,
                isAvailableForParcel: true,
                status: 'Active',
                createdAt: '2026-06-11T09:00:00Z',
                updatedAt: '2026-06-11T09:30:00Z',
              },
            ],
          })
        )
        .mockResolvedValueOnce(
          createJsonResponse({
            items: [
              {
                posOrderId: 'order-1',
                branchId: 'branch-1',
                orderNumber: 'ORD-20260612-0001',
                orderType: 'EatIn',
                status: 'Draft',
                tableName: 'T1',
                customerName: 'Walk-in',
                grandTotal: 2.5,
                lineCount: 1,
                createdAt: '2026-06-12T10:00:00Z',
              },
            ],
          })
        )
    );

    renderWithRouter(<App />, '/pos/orders');

    expect(await screen.findByRole('heading', { name: /pos order capture/i })).toBeInTheDocument();
    // Order number appears in both the desktop table button and the mobile card
    expect((await screen.findAllByText('ORD-20260612-0001')).length).toBeGreaterThanOrEqual(1);
  });

  it.each([
    ['CashShift.View', /cashier shifts/i],
    ['CashShift.Manage', /cashier shifts/i],
  ])('renders the cashier shifts route for %s users', async (permission, heading) => {
    storeAuthSession({
      permissions: [permission],
      roles: ['Cashier'],
      activeRole: 'Cashier',
    });

    vi.stubGlobal(
      'fetch',
      vi
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
        .mockResolvedValueOnce(
          createJsonResponse(null)
        )
        .mockResolvedValueOnce(createJsonResponse({ items: [] }))
    );

    renderWithRouter(<App />, '/cashier/shifts');

    expect(await screen.findByRole('heading', { name: heading })).toBeInTheDocument();
  });

  it('redirects unknown routes to the dashboard', async () => {
    storeAuthSession();

    renderWithRouter(<App />, '/not-a-real-route');

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /billsoft dashboard/i })).toBeInTheDocument();
    });
  });

  it('redirects unauthenticated users to the login screen', async () => {
    clearAuthSession();

    renderWithRouter(<App />, '/');

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /sign in to billsoft/i })).toBeInTheDocument();
    });
  });

  it('redirects unauthenticated owner dashboard users to the login screen', async () => {
    clearAuthSession();

    renderWithRouter(<App />, '/owner/dashboard');

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /sign in to billsoft/i })).toBeInTheDocument();
    });
  });
});
