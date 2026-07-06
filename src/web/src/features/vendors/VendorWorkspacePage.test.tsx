import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';

import App from '../../App';
import { clearAuthSession, createJsonResponse, storeAuthSession } from '../../test/authTestUtils';
import { renderWithRouter } from '../../test/renderWithRouter';

const branchesPath = '/api/v1/admin/branches';
const vendorsPath = '/api/v1/vendors';
const vendorBillsPath = '/api/v1/vendor-bills';
const inventoryItemsPath = '/api/v1/inventory/items';

const timestamp = '2026-06-11T09:00:00Z';

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

const defaultBranchesResponse = () =>
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
        createdAt: timestamp,
        updatedAt: timestamp,
      },
    ],
  });

const defaultEmptyVendorResponse = () => createJsonResponse({ items: [] });

const defaultEmptyBillResponse = () => createJsonResponse({ items: [] });

const defaultInventoryResponse = () => createJsonResponse({ items: [] });

const problemJsonResponse = (detail: string, status = 400) =>
  new Response(JSON.stringify({
    type: 'https://datatracker.ietf.org/doc/html/rfc7807',
    title: 'Bad Request',
    status,
    detail,
  }), {
    status,
    headers: {
      'Content-Type': 'application/problem+json',
    },
  });

const mockScrollIntoView = () => {
  const originalDescriptor = Object.getOwnPropertyDescriptor(HTMLElement.prototype, 'scrollIntoView');
  const scrollIntoViewMock = vi.fn();
  Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', {
    configurable: true,
    writable: true,
    value: scrollIntoViewMock,
  });
  restoreScrollIntoView = () => {
    if (originalDescriptor) {
      Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', originalDescriptor);
    } else {
      delete (HTMLElement.prototype as { scrollIntoView?: unknown }).scrollIntoView;
    }
  };
  return scrollIntoViewMock;
};

let restoreScrollIntoView: (() => void) | null = null;

const renderVendorWorkspace = (
  responses: Record<string, Response[]> = {},
  permissions: string[] = ['VendorBill.Confirm', 'VendorPayment.Create', 'Branch.Manage', 'Inventory.Adjust']
) => {
  clearAuthSession();
  storeAuthSession({
    permissions,
    roles: ['Admin'],
    activeRole: 'Admin',
    branchId: 'branch-1',
  });

  const fetchMock = setupFetch({
    [`GET ${branchesPath}`]: [defaultBranchesResponse()],
    [`GET ${vendorsPath}`]: [defaultEmptyVendorResponse()],
    [`GET ${vendorBillsPath}`]: [defaultEmptyBillResponse()],
    [`GET ${inventoryItemsPath}`]: [defaultInventoryResponse()],
    ...responses,
  });
  const user = userEvent.setup();

  renderWithRouter(<App />, '/vendors');

  return { fetchMock, user };
};

const getSelectByFieldLabel = (label: string) =>
  screen.getAllByRole('combobox').find(select =>
    select.closest('.ui-field')?.querySelector('.ui-field__label')?.textContent?.trim() === label
  );

afterEach(() => {
  restoreScrollIntoView?.();
  restoreScrollIntoView = null;
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe('VendorWorkspacePage', () => {
  it('renders the empty workspace and does not show OCR controls', async () => {
    renderVendorWorkspace();

    expect(await screen.findByRole('heading', { name: /vendor workspace/i })).toBeInTheDocument();
    expect(screen.getByText(/no vendors yet/i)).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /no outstanding payables/i })).toBeInTheDocument();
    expect(screen.queryByText(/\bocr\b/i)).not.toBeInTheDocument();
  });

  it('shows a lightweight vendor payable report shortcut when report access is available', async () => {
    renderVendorWorkspace(
      {
        [`GET /api/v1/reports/vendor-payables`]: [
          createJsonResponse({
            restaurantId: 'restaurant-1',
            restaurantCode: 'DEMO',
            restaurantName: 'Demo Restaurant',
            branchId: 'branch-1',
            branchName: 'Main Branch',
            fromDate: '2026-06-01',
            toDate: '2026-06-30',
            currencyCode: 'SGD',
            generatedAt: timestamp,
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
      },
      ['VendorBill.Confirm', 'VendorPayment.Create', 'Branch.Manage', 'Inventory.Adjust', 'Report.View']
    );

    expect(await screen.findByRole('heading', { name: /vendor workspace/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /vendor payable report/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /view vendor payable report/i })).toBeInTheDocument();
    expect(screen.getAllByText(/0\.00/).length).toBeGreaterThan(0);
  });

  it('shows the upload vendor bill OCR review action and warning text', async () => {
    renderVendorWorkspace(
      {
        [`GET /api/v1/vendor-bill-ocr/drafts`]: [
          createJsonResponse({
            items: [],
          }),
        ],
      },
      ['VendorBill.Upload', 'VendorBill.ReviewOcr', 'VendorBill.OverrideOcr', 'VendorBill.Confirm', 'VendorPayment.Create', 'Branch.Manage', 'Inventory.Adjust', 'Report.View']
    );

    expect(await screen.findByRole('heading', { name: /vendor workspace/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /upload vendor bill/i })).toBeInTheDocument();
    expect(screen.getByText(/ocr may be wrong/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /review extracted bill/i })).toBeInTheDocument();
  });

  it('validates unsupported file type before upload and keeps the draft workflow safe', async () => {
    const { user } = renderVendorWorkspace(
      {
        [`GET /api/v1/vendor-bill-ocr/drafts`]: [
          createJsonResponse({
            items: [],
          }),
        ],
      },
      ['VendorBill.Upload', 'VendorBill.ReviewOcr', 'VendorBill.OverrideOcr', 'VendorBill.Confirm', 'VendorPayment.Create', 'Branch.Manage', 'Inventory.Adjust', 'Report.View']
    );

    expect(await screen.findByRole('heading', { name: /vendor workspace/i })).toBeInTheDocument();
    const fileInput = screen.getByLabelText(/vendor bill file/i);
    const file = new File(['bad'], 'vendor.txt', { type: 'text/plain' });
    fireEvent.change(fileInput, { target: { files: [file] } });

    expect(screen.getByRole('alert')).toHaveTextContent(/only jpeg, png, or pdf files are allowed/i);
  });

  it('validates the vendor form before create and reports duplicate-name errors safely', async () => {
    const { user } = renderVendorWorkspace({
      [`POST ${vendorsPath}`]: [
        createJsonResponse(
          {
            title: 'Bad Request',
            detail: 'Vendor name already exists in this scope.',
          },
          { status: 400 }
        ),
      ],
    });

    expect(await screen.findByRole('heading', { name: /vendor workspace/i })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /create vendor/i }));

    await waitFor(() => {
      expect(screen.getByText(/please fix the vendor form before saving/i)).toBeInTheDocument();
    });
    expect(screen.getByText(/vendor name is required/i)).toBeInTheDocument();

    await user.type(screen.getByPlaceholderText('Fresh Rice'), 'Fresh Rice');
    await user.selectOptions(getSelectByFieldLabel('Vendor type')!, 'Groceries');
    await user.clear(screen.getByPlaceholderText('90010001'));
    await user.type(screen.getByPlaceholderText('90010001'), '90010002');
    await user.click(screen.getByRole('button', { name: /create vendor/i }));

    await waitFor(() => {
      expect(screen.getByText(/vendor name already exists in this scope/i)).toBeInTheDocument();
    });
  });

  it('requires vendor mobile before saving and shows duplicate mobile errors safely', async () => {
    const { user } = renderVendorWorkspace({
      [`POST ${vendorsPath}`]: [
        problemJsonResponse('Vendor mobile number already exists.'),
      ],
    });

    expect(await screen.findByRole('heading', { name: /vendor workspace/i })).toBeInTheDocument();
    await user.type(screen.getByPlaceholderText('Fresh Rice'), 'Fresh Rice');
    await user.selectOptions(getSelectByFieldLabel('Vendor type')!, 'Groceries');
    await user.click(screen.getByRole('button', { name: /create vendor/i }));

    await waitFor(() => {
      expect(screen.getByText(/mobile number is required\./i)).toBeInTheDocument();
    });

    await user.type(screen.getByPlaceholderText('90010001'), '90010001');
    await user.click(screen.getByRole('button', { name: /create vendor/i }));

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent(/vendor mobile number already exists\./i);
    });

    const pageText = document.body.textContent ?? '';
    expect(pageText).not.toMatch(/datatracker\.ietf\.org/i);
    expect(pageText).not.toMatch(/detail:/i);
    expect(pageText).not.toMatch(/type:/i);
  });

  it('validates the vendor bill form and shows stock-in linkage in bill detail', async () => {
    const { user } = renderVendorWorkspace(
      {
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
                createdAtUtc: timestamp,
                updatedAtUtc: null,
              },
            ],
          }),
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
                createdAtUtc: timestamp,
                updatedAtUtc: null,
              },
            ],
          }),
        ],
        [`GET ${vendorBillsPath}`]: [
          createJsonResponse({
            items: [
              {
                vendorBillId: 'bill-1',
                vendorId: 'vendor-1',
                branchId: 'branch-1',
                vendorName: 'Fresh Rice',
                vendorType: 'Groceries',
                billNumber: 'VB-001',
                billDate: '2026-06-11T00:00:00Z',
                status: 'PartiallyPaid',
                totalAmount: 100,
                paidAmount: 40,
                balanceAmount: 60,
                createdAtUtc: timestamp,
              },
            ],
          }),
        ],
        [`GET ${inventoryItemsPath}`]: [
          createJsonResponse({
            items: [
              {
                inventoryItemId: 'inventory-1',
                restaurantId: 'restaurant-1',
                branchId: 'branch-1',
                name: 'Rice',
                normalizedName: 'RICE',
                category: 'Grains',
                unitOfMeasure: 'kg',
                lowStockThreshold: 5,
                isActive: true,
                currentStock: 15,
                status: 'Healthy',
                createdAtUtc: timestamp,
                updatedAtUtc: timestamp,
              },
            ],
          }),
        ],
        [`GET ${vendorBillsPath}/bill-1`]: [
          createJsonResponse({
            vendorBillId: 'bill-1',
            restaurantId: 'restaurant-1',
            branchId: 'branch-1',
            vendorId: 'vendor-1',
            vendorName: 'Fresh Rice',
            vendorType: 'Groceries',
            billNumber: 'VB-001',
            billDate: '2026-06-11T00:00:00Z',
            dueDate: null,
            status: 'PartiallyPaid',
            totalAmount: 100,
            paidAmount: 40,
            balanceAmount: 60,
            notes: 'Morning purchase',
            cancelledAtUtc: null,
            cancelledByUserId: null,
            cancellationReason: null,
            createdAtUtc: timestamp,
            updatedAtUtc: null,
            lines: [
              {
                vendorBillLineId: 'line-1',
                inventoryItemId: 'inventory-1',
                inventoryItemName: 'Rice',
                inventoryMovementId: 'movement-1',
                description: 'Rice',
                quantity: 10,
                unitCost: 10,
                lineTotal: 100,
                createdAtUtc: timestamp,
                updatedAtUtc: null,
              },
            ],
            settlements: [
              {
                vendorSettlementId: 'settlement-1',
                paymentMode: 'Cash',
                status: 'Active',
                amount: 40,
                referenceNumber: null,
                paidAtUtc: timestamp,
                recordedByUserId: 'user-1',
                createdAtUtc: timestamp,
                updatedAtUtc: null,
                cancelledAtUtc: null,
                cancelledByUserId: null,
                cancellationReason: null,
              },
            ],
          }),
        ],
      }
    );

    expect(await screen.findByRole('heading', { name: /vendor workspace/i })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /create bill/i }));

    await waitFor(() => {
      expect(screen.getByText(/please fix the bill lines before saving/i)).toBeInTheDocument();
    });
    expect(screen.getByText(/vendor is required/i)).toBeInTheDocument();
    expect(screen.getByText(/each bill line needs a description/i)).toBeInTheDocument();

    await user.selectOptions(getSelectByFieldLabel('Vendor')!, 'vendor-1');
    await user.selectOptions(getSelectByFieldLabel('Inventory item')!, 'inventory-1');
    expect(screen.getByText(/stock-in linked/i)).toBeInTheDocument();
    expect(screen.getAllByText(/partiallypaid/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/settlement history/i).length).toBeGreaterThan(0);
  });

  it('shows readable bill numbers in payables, bill details, and settlement dialog and scrolls to the details section on view', async () => {
    const scrollIntoViewMock = mockScrollIntoView();
    const { user } = renderVendorWorkspace(
      {
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
                createdAtUtc: timestamp,
                updatedAtUtc: null,
              },
            ],
          }),
        ],
        [`GET ${vendorBillsPath}`]: [
          createJsonResponse({
            items: [
              {
                vendorBillId: 'bill-1',
                vendorId: 'vendor-1',
                branchId: 'branch-1',
                vendorName: 'Fresh Rice',
                vendorType: 'Groceries',
                billNumber: 'VB-010',
                billDate: '2026-06-11T00:00:00Z',
                status: 'PartiallyPaid',
                totalAmount: 100,
                paidAmount: 40,
                balanceAmount: 60,
                createdAtUtc: timestamp,
              },
            ],
          }),
        ],
        [`GET ${vendorBillsPath}/bill-1`]: [
          createJsonResponse({
            vendorBillId: 'bill-1',
            restaurantId: 'restaurant-1',
            branchId: 'branch-1',
            vendorId: 'vendor-1',
            vendorName: 'Fresh Rice',
            vendorType: 'Groceries',
            billNumber: 'VB-010',
            billDate: '2026-06-11T00:00:00Z',
            dueDate: null,
            status: 'PartiallyPaid',
            totalAmount: 100,
            paidAmount: 40,
            balanceAmount: 60,
            notes: 'Morning purchase',
            cancelledAtUtc: null,
            cancelledByUserId: null,
            cancellationReason: null,
            createdAtUtc: timestamp,
            updatedAtUtc: null,
            lines: [
              {
                vendorBillLineId: 'line-1',
                inventoryItemId: null,
                inventoryItemName: null,
                inventoryMovementId: null,
                description: 'Rice',
                quantity: 10,
                unitCost: 10,
                lineTotal: 100,
                createdAtUtc: timestamp,
                updatedAtUtc: null,
              },
            ],
            settlements: [],
          }),
        ],
      }
    );

    expect(await screen.findByRole('heading', { name: /vendor workspace/i })).toBeInTheDocument();
    const billRow = screen.getByRole('row', { name: /VB-010 Fresh Rice/ });
    expect(billRow).toBeInTheDocument();
    expect(within(billRow).queryByText('bill-1')).not.toBeInTheDocument();

    await user.click(within(billRow).getByRole('button', { name: /^view$/i }));

    await waitFor(() => {
      expect(scrollIntoViewMock).toHaveBeenCalled();
    });

    expect(await screen.findByRole('heading', { name: /bill detail: vb-010/i })).toBeInTheDocument();
    expect(screen.queryByText('bill-1')).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /record settlement/i }));
    const dialog = await screen.findByRole('dialog', { name: /record vendor settlement/i });

    expect(within(dialog).getByText('VB-010')).toBeInTheDocument();
    expect(within(dialog).queryByText('bill-1')).not.toBeInTheDocument();
  });

  it('keeps the bill form usable after a duplicate bill number error', async () => {
    const { user } = renderVendorWorkspace(
      {
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
                createdAtUtc: timestamp,
                updatedAtUtc: null,
              },
            ],
          }),
        ],
        [`POST ${vendorBillsPath}`]: [
          problemJsonResponse('This bill number already exists for the selected vendor.'),
          createJsonResponse(
            {
              vendorBillId: 'bill-2',
              restaurantId: 'restaurant-1',
              branchId: 'branch-1',
              vendorId: 'vendor-1',
              vendorName: 'Fresh Rice',
              vendorType: 'Groceries',
              billNumber: 'VB-002',
              billDate: '2026-06-11T00:00:00Z',
              dueDate: null,
              status: 'Unpaid',
              totalAmount: 100,
              paidAmount: 0,
              balanceAmount: 100,
              notes: 'Morning purchase',
              cancelledAtUtc: null,
              cancelledByUserId: null,
              cancellationReason: null,
              createdAtUtc: timestamp,
              updatedAtUtc: null,
              lines: [
                {
                  vendorBillLineId: 'line-2',
                  inventoryItemId: null,
                  inventoryItemName: null,
                  inventoryMovementId: null,
                  description: 'Rice',
                  quantity: 10,
                  unitCost: 10,
                  lineTotal: 100,
                  createdAtUtc: timestamp,
                  updatedAtUtc: null,
                },
              ],
              settlements: [],
            },
            { status: 201 }
          ),
        ],
        [`GET ${vendorBillsPath}`]: [
          createJsonResponse({ items: [] }),
          createJsonResponse({
            items: [
              {
                vendorBillId: 'bill-2',
                vendorId: 'vendor-1',
                branchId: 'branch-1',
                vendorName: 'Fresh Rice',
                vendorType: 'Groceries',
                billNumber: 'VB-002',
                billDate: '2026-06-11T00:00:00Z',
                status: 'Unpaid',
                totalAmount: 100,
                paidAmount: 0,
                balanceAmount: 100,
                createdAtUtc: timestamp,
              },
            ],
          }),
        ],
        [`GET ${vendorBillsPath}/bill-2`]: [
          createJsonResponse({
            vendorBillId: 'bill-2',
            restaurantId: 'restaurant-1',
            branchId: 'branch-1',
            vendorId: 'vendor-1',
            vendorName: 'Fresh Rice',
            vendorType: 'Groceries',
            billNumber: 'VB-002',
            billDate: '2026-06-11T00:00:00Z',
            dueDate: null,
            status: 'Unpaid',
            totalAmount: 100,
            paidAmount: 0,
            balanceAmount: 100,
            notes: 'Morning purchase',
            cancelledAtUtc: null,
            cancelledByUserId: null,
            cancellationReason: null,
            createdAtUtc: timestamp,
            updatedAtUtc: null,
            lines: [
              {
                vendorBillLineId: 'line-2',
                inventoryItemId: null,
                inventoryItemName: null,
                inventoryMovementId: null,
                description: 'Rice',
                quantity: 10,
                unitCost: 10,
                lineTotal: 100,
                createdAtUtc: timestamp,
                updatedAtUtc: null,
              },
            ],
            settlements: [],
          }),
        ],
        [`GET ${inventoryItemsPath}`]: [
          defaultInventoryResponse(),
          defaultInventoryResponse(),
        ],
      }
    );

    expect(await screen.findByRole('heading', { name: /vendor workspace/i })).toBeInTheDocument();
    await user.selectOptions(getSelectByFieldLabel('Vendor')!, 'vendor-1');
    await user.type(screen.getByLabelText(/^Bill number$/i), 'VB-001');
    await user.type(screen.getByLabelText(/^Description$/i), 'Rice');
    await user.type(screen.getByLabelText(/^Quantity$/i), '10');
    await user.type(screen.getByLabelText(/^Unit cost$/i), '10');
    await user.click(screen.getByRole('button', { name: /create bill/i }));

    await waitFor(() => {
      expect(screen.getByText(/this bill number already exists for the selected vendor\./i)).toBeInTheDocument();
    });

    await user.clear(screen.getByPlaceholderText('VB-001'));
    await user.type(screen.getByPlaceholderText('VB-001'), 'VB-002');
    await user.click(screen.getByRole('button', { name: /create bill/i }));

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /bill detail: vb-002/i })).toBeInTheDocument();
    });
    expect(
      screen.getAllByRole('status').some(node => /created bill vb-002\./i.test(node.textContent ?? ''))
    ).toBe(true);
    expect(screen.queryByText('bill-2')).not.toBeInTheDocument();
  });

  it('validates settlement overpayment and reference number rules', async () => {
    const { user } = renderVendorWorkspace(
      {
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
                createdAtUtc: timestamp,
                updatedAtUtc: null,
              },
            ],
          }),
        ],
        [`GET ${vendorBillsPath}`]: [
          createJsonResponse({
            items: [
              {
                vendorBillId: 'bill-1',
                vendorId: 'vendor-1',
                branchId: 'branch-1',
                vendorName: 'Fresh Rice',
                vendorType: 'Groceries',
                billNumber: 'VB-010',
                billDate: '2026-06-11T00:00:00Z',
                status: 'PartiallyPaid',
                totalAmount: 100,
                paidAmount: 40,
                balanceAmount: 60,
                createdAtUtc: timestamp,
              },
            ],
          }),
        ],
        [`GET ${vendorBillsPath}/bill-1`]: [
          createJsonResponse({
            vendorBillId: 'bill-1',
            restaurantId: 'restaurant-1',
            branchId: 'branch-1',
            vendorId: 'vendor-1',
            vendorName: 'Fresh Rice',
            vendorType: 'Groceries',
            billNumber: 'VB-010',
            billDate: '2026-06-11T00:00:00Z',
            dueDate: null,
            status: 'PartiallyPaid',
            totalAmount: 100,
            paidAmount: 40,
            balanceAmount: 60,
            notes: null,
            cancelledAtUtc: null,
            cancelledByUserId: null,
            cancellationReason: null,
            createdAtUtc: timestamp,
            updatedAtUtc: null,
            lines: [],
            settlements: [],
          }),
        ],
      }
    );

    expect(await screen.findByRole('heading', { name: /vendor workspace/i })).toBeInTheDocument();
    const billRow = screen.getByRole('row', { name: /VB-010 Fresh Rice/ });
    await user.click(within(billRow).getByRole('button', { name: /^view$/i }));
    await user.click(await screen.findByRole('button', { name: /record settlement/i }));
    const dialog = await screen.findByRole('dialog', { name: /record vendor settlement/i });
    expect(within(dialog).getByText(/^Current outstanding$/i)).toBeInTheDocument();

    const amountInput = within(dialog).getByRole('spinbutton', { name: /^amount$/i });
    await user.clear(amountInput);
    await user.type(amountInput, '61');
    await user.click(within(dialog).getByRole('button', { name: /confirm settlement/i }));

    await waitFor(() => {
      expect(within(dialog).getByText(/settlement amount cannot exceed the current balance/i)).toBeInTheDocument();
    });

    await user.clear(amountInput);
    await user.type(amountInput, '40');
    await user.selectOptions(within(dialog).getByLabelText(/payment mode/i), 'UPI');
    await user.click(within(dialog).getByRole('button', { name: /confirm settlement/i }));

    await waitFor(() => {
      expect(within(dialog).getByText(/reference number is required for this payment mode/i)).toBeInTheDocument();
    });
  });

  it('strips SQL exception details from plain-text 500 responses and shows only a safe error title', async () => {
    const sqlErrorBody =
      "Microsoft.Data.SqlClient.SqlException (0x80131904): Invalid object name 'Vendors'.\r\n   at System.Data.Entity.Core.EntityClient.Internal.EntityCommandDefinition.ExecuteStoreCommands\r\nStack trace:\r\n   at Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync";

    setupFetch({
      [`GET ${branchesPath}`]: [defaultBranchesResponse()],
      [`GET ${vendorsPath}`]: [
        new Response(sqlErrorBody, {
          status: 500,
          headers: { 'Content-Type': 'text/plain' },
        }),
      ],
      [`GET ${vendorBillsPath}`]: [defaultEmptyBillResponse()],
      [`GET ${inventoryItemsPath}`]: [defaultInventoryResponse()],
    });

    clearAuthSession();
    storeAuthSession({
      permissions: ['VendorBill.Confirm', 'VendorPayment.Create', 'Branch.Manage', 'Inventory.Adjust'],
      roles: ['Admin'],
      activeRole: 'Admin',
      branchId: 'branch-1',
    });
    renderWithRouter(<App />, '/vendors');

    await waitFor(() => {
      expect(screen.getByText(/could not load vendors/i)).toBeInTheDocument();
    });

    const pageText = document.body.textContent ?? '';
    expect(pageText).not.toMatch(/SqlException/i);
    expect(pageText).not.toMatch(/Stack trace/i);
    expect(pageText).not.toMatch(/EntityCommand/i);
    expect(pageText).not.toMatch(/0x80131904/i);
    expect(pageText).not.toMatch(/ExecuteSqlRaw/i);
  });

  it('strips HTML tags from HTML error page responses and shows only a safe error title', async () => {
    const htmlErrorBody =
      '<!DOCTYPE html><html><head><title>503 Service Unavailable</title></head><body><h1>Service Unavailable</h1><p>The server is temporarily unable to service your request.</p></body></html>';

    setupFetch({
      [`GET ${branchesPath}`]: [defaultBranchesResponse()],
      [`GET ${vendorsPath}`]: [
        new Response(htmlErrorBody, {
          status: 503,
          headers: { 'Content-Type': 'text/html' },
        }),
      ],
      [`GET ${vendorBillsPath}`]: [defaultEmptyBillResponse()],
      [`GET ${inventoryItemsPath}`]: [defaultInventoryResponse()],
    });

    clearAuthSession();
    storeAuthSession({
      permissions: ['VendorBill.Confirm', 'VendorPayment.Create', 'Branch.Manage', 'Inventory.Adjust'],
      roles: ['Admin'],
      activeRole: 'Admin',
      branchId: 'branch-1',
    });
    renderWithRouter(<App />, '/vendors');

    await waitFor(() => {
      expect(screen.getByText(/could not load vendors/i)).toBeInTheDocument();
    });

    const pageText = document.body.textContent ?? '';
    expect(pageText).not.toMatch(/<!doctype/i);
    expect(pageText).not.toMatch(/<html>/i);
    expect(pageText).not.toMatch(/temporarily unable to service/i);
  });

  it('empty vendor and bill lists render without token-like or encoded text', async () => {
    renderVendorWorkspace();

    await waitFor(() => {
      expect(screen.getByText(/no vendors yet/i)).toBeInTheDocument();
      expect(screen.getByRole('heading', { name: /no outstanding payables/i })).toBeInTheDocument();
    });

    const pageText = document.body.textContent ?? '';
    expect(pageText).not.toMatch(/eyJ[A-Za-z0-9_-]{20,}/);
    expect(pageText).not.toMatch(/Bearer\s+[A-Za-z0-9._-]{20,}/i);
    expect(pageText).not.toMatch(/access.?token/i);
    expect(pageText).not.toMatch(/SqlException/i);
    expect(pageText).not.toMatch(/Stack trace/i);
  });
});
