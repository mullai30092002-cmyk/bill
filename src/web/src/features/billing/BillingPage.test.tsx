import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import App from '../../App';
import { createJsonResponse, clearAuthSession, storeAuthSession } from '../../test/authTestUtils';
import { renderWithRouter } from '../../test/renderWithRouter';
import type { CashierShiftDetail } from '../cashiering/cashierShiftTypes';
import type { BillDetail, BillListItem, BillLineDetail, PaymentDetail } from './billingTypes';
import type { BillReceiptLine, BillReceiptPayment, BillReceiptResponse } from './billReceiptTypes';
import type { PosOrderListItem } from '../pos/posTypes';

const makeBillListItem = (overrides: Partial<BillListItem> = {}): BillListItem => ({
  billId: 'bill-1',
  branchId: 'branch-1',
  posOrderId: 'order-1',
  billNumber: 'BILL-20260612-0001',
  businessDate: '2026-06-12T00:00:00Z',
  status: 'Unpaid',
  grandTotal: 25,
  amountPaid: 0,
  balanceDue: 25,
  createdAt: '2026-06-12T09:50:00Z',
  ...overrides,
});

const makeBillLine = (overrides: Partial<BillLineDetail> = {}): BillLineDetail => ({
  billLineId: 'bill-line-1',
  posOrderLineId: 'pos-line-1',
  menuItemId: 'item-1',
  menuCategoryId: 'category-1',
  menuItemNameSnapshot: 'Masala Dosa',
  menuCategoryNameSnapshot: 'Breakfast',
  skuSnapshot: 'DOSA-01',
  unitPrice: 25,
  taxRate: 0,
  quantity: 1,
  lineSubtotal: 25,
  lineTax: 0,
  lineTotal: 25,
  notes: null,
  displayOrder: 1,
  createdAt: '2026-06-12T09:50:00Z',
  ...overrides,
});

const makePayment = (overrides: Partial<PaymentDetail> = {}): PaymentDetail => ({
  paymentId: 'payment-1',
  billId: 'bill-1',
  branchId: 'branch-1',
  paymentNumber: 'PAY-20260612-0001',
  paymentMode: 'Cash',
  status: 'Recorded',
  amount: 10,
  referenceNumber: null,
  notes: null,
  recordedByUserId: 'user-1',
  cancelledByUserId: null,
  cancelledAt: null,
  cancelReason: null,
  createdAt: '2026-06-12T10:00:00Z',
  updatedAt: null,
  ...overrides,
});

const makeReceiptLine = (overrides: Partial<BillReceiptLine> = {}): BillReceiptLine => ({
  displayOrder: 1,
  menuItemNameSnapshot: 'Masala Dosa',
  menuCategoryNameSnapshot: 'Breakfast',
  skuSnapshot: 'DOSA-01',
  quantity: 1,
  notes: 'Less spicy',
  unitPrice: 25,
  lineSubtotal: 25,
  lineTax: 0,
  lineTotal: 25,
  ...overrides,
});

const makeReceiptPayment = (overrides: Partial<BillReceiptPayment> = {}): BillReceiptPayment => ({
  paymentNumber: 'PAY-20260612-0001',
  paymentMode: 'Cash',
  status: 'Recorded',
  amount: 25,
  referenceNumber: null,
  notes: null,
  recordedByUserId: 'user-1',
  recordedByUserLabel: 'Maya Iyer',
  createdAt: '2026-06-12T10:00:00Z',
  ...overrides,
});

const makeCurrentShift = (overrides: Partial<CashierShiftDetail> = {}): CashierShiftDetail => ({
  cashierShiftId: 'shift-1',
  restaurantId: 'restaurant-1',
  branchId: 'branch-1',
  cashierUserId: 'user-1',
  cashierName: 'Maya Iyer',
  branchName: 'Main Branch',
  businessDate: '2026-06-12',
  status: 'Open',
  openedAtUtc: '2026-06-12T09:00:00Z',
  openingCashAmount: 20,
  closedAtUtc: null,
  declaredClosingCashAmount: null,
  expectedClosingCashAmount: 20,
  cashVarianceAmount: null,
  closeNotes: null,
  createdAtUtc: '2026-06-12T09:00:00Z',
  updatedAtUtc: null,
  ...overrides,
});

const makeBillDetail = (overrides: Partial<BillDetail> = {}): BillDetail => ({
  billId: 'bill-1',
  restaurantId: 'restaurant-1',
  branchId: 'branch-1',
  posOrderId: 'order-1',
  billNumber: 'BILL-20260612-0001',
  businessDate: '2026-06-12T00:00:00Z',
  status: 'Unpaid',
  subtotal: 25,
  taxTotal: 0,
  grandTotal: 25,
  amountPaid: 0,
  balanceDue: 25,
  createdByUserId: 'user-1',
  cancelledByUserId: null,
  cancelledAt: null,
  cancelReason: null,
  createdAt: '2026-06-12T09:50:00Z',
  updatedAt: '2026-06-12T09:50:00Z',
  lines: [makeBillLine()],
  payments: [],
  ...overrides,
});

const makeBillReceipt = (overrides: Partial<BillReceiptResponse> = {}): BillReceiptResponse => ({
  billId: 'bill-1',
  restaurantId: 'restaurant-1',
  branchId: 'branch-1',
  restaurantCode: 'DEMO',
  countryCode: 'SG',
  currencyCode: 'SGD',
  timeZoneId: 'Asia/Singapore',
  restaurantName: 'Demo Restaurant',
  branchName: 'Main Branch',
  branchAddress: '12 Market Street, Singapore',
  posOrderId: 'order-1',
  businessDate: '2026-06-12T00:00:00Z',
  orderNumberSnapshot: 'ORD-20260612-0001',
  orderTypeSnapshot: 'EatIn',
  orderTableNameSnapshot: 'Table 12',
  orderCustomerNameSnapshot: 'Walk-in customer',
  orderCustomerMobileSnapshot: '90000000',
  billNumber: 'BILL-20260612-0001',
  status: 'Unpaid',
  createdByUserId: 'user-1',
  createdByUserLabel: 'Maya Iyer',
  createdAt: '2026-06-12T09:50:00Z',
  updatedAt: '2026-06-12T09:50:00Z',
  printedAt: '2026-06-12T10:05:00Z',
  cancelledByUserId: null,
  cancelledAt: null,
  cancelReason: null,
  subtotal: 25,
  taxTotal: 0,
  grandTotal: 25,
  amountPaid: 0,
  balanceDue: 25,
  printCount: 0,
  isReprint: false,
  lines: [makeReceiptLine()],
  payments: [],
  ...overrides,
});

const makeConfirmedOrder = (overrides: Partial<PosOrderListItem> = {}): PosOrderListItem => ({
  posOrderId: 'order-1',
  branchId: 'branch-1',
  orderNumber: 'ORD-20260612-0001',
  orderType: 'EatIn',
  status: 'Confirmed',
  tableName: 'T1',
  customerName: 'Walk-in',
  grandTotal: 25,
  lineCount: 1,
  createdAt: '2026-06-12T09:40:00Z',
  ...overrides,
});

const stubBillingBootstrap = (bills: BillListItem[], orders: PosOrderListItem[]) =>
  vi
    .fn()
    .mockResolvedValueOnce(createJsonResponse({ items: bills }))
    .mockResolvedValueOnce(createJsonResponse({ items: orders }));

const renderBillingRoute = (permissions: string[], fetchMock: ReturnType<typeof vi.fn>) => {
  storeAuthSession({ permissions, roles: ['Cashier'], activeRole: 'Cashier' });
  vi.stubGlobal('fetch', fetchMock);
  return renderWithRouter(<App />, '/billing');
};

describe('BillingPage', () => {
  beforeEach(() => {
    clearAuthSession();
    vi.restoreAllMocks();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it.each([
    ['Billing.View', 'Billing.View'],
    ['Billing.Manage', 'Billing.Manage'],
  ])('renders the billing workspace for %s users', async (permission, label) => {
    const fetchMock = stubBillingBootstrap([makeBillListItem()], [makeConfirmedOrder({ posOrderId: 'order-2', orderNumber: 'ORD-20260612-0002' })]);
    renderBillingRoute([permission], fetchMock);

    expect(await screen.findByRole('heading', { name: /billing workspace/i })).toBeInTheDocument();
    expect(screen.getByText(/select a bill to view details or create a bill from a confirmed order\./i)).toBeInTheDocument();
    if (label === 'Billing.Manage') {
      expect((await screen.findAllByRole('button', { name: /create bill/i })).length).toBeGreaterThan(0);
    } else {
      expect(screen.queryByRole('button', { name: /create bill/i })).not.toBeInTheDocument();
    }
  });

  it('shows a not-authorized state without calling billing or POS APIs', async () => {
    const fetchMock = vi.fn();
    renderBillingRoute([], fetchMock);

    expect(await screen.findByRole('heading', { name: /not authorized/i })).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it.each(['Billing.View', 'Billing.Manage', 'Payment.Record'])('shows the Billing nav item for %s', async permission => {
    const fetchMock = stubBillingBootstrap([], []);
    renderBillingRoute([permission], fetchMock);

    expect(await screen.findByRole('link', { name: /^billing$/i })).toBeInTheDocument();
  });

  it('does not authorize the page for Payment.View-only users', async () => {
    const fetchMock = vi.fn();
    renderBillingRoute(['Payment.View'], fetchMock);

    expect(await screen.findByRole('heading', { name: /not authorized/i })).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('loads the bill list and keeps the detail pane empty until a bill is selected', async () => {
    const fetchMock = stubBillingBootstrap(
      [makeBillListItem(), makeBillListItem({ billId: 'bill-2', billNumber: 'BILL-20260612-0002', createdAt: '2026-06-12T10:00:00Z' })],
      [makeConfirmedOrder()]
    ).mockResolvedValueOnce(
      createJsonResponse({
        billId: 'bill-1',
        restaurantId: 'restaurant-1',
        branchId: 'branch-1',
        posOrderId: 'order-1',
        billNumber: 'BILL-20260612-0001',
        status: 'Unpaid',
        subtotal: 25,
        taxTotal: 0,
        grandTotal: 25,
        amountPaid: 0,
        balanceDue: 25,
        createdByUserId: 'user-1',
        cancelledByUserId: null,
        cancelledAt: null,
        cancelReason: null,
        createdAt: '2026-06-12T09:50:00Z',
        updatedAt: '2026-06-12T09:50:00Z',
        lines: [makeBillLine()],
        payments: [],
      })
    );
    renderBillingRoute(['Billing.View'], fetchMock);
    const user = userEvent.setup();

    expect(await screen.findByText(/select a bill to view details or create a bill from a confirmed order\./i)).toBeInTheDocument();
    expect(screen.getAllByText('BILL-20260612-0001').length).toBeGreaterThan(0);

    await user.click(screen.getAllByRole('button', { name: /^view$/i })[0]);

    expect(await screen.findByText(/masala dosa/i)).toBeInTheDocument();
    expect(screen.getAllByText(/balance due/i).length).toBeGreaterThan(0);
  });

  it('queries bills by branch and business date from the billing filters card', async () => {
    const fetchMock = stubBillingBootstrap([makeBillListItem()], [makeConfirmedOrder()]);
    renderBillingRoute(['Billing.View'], fetchMock);

    await waitFor(() => expect(fetchMock.mock.calls.some(entry => String(entry[0]).includes('/api/v1/billing/bills?'))).toBe(true));
    const billListCall = fetchMock.mock.calls.find(entry => String(entry[0]).includes('/api/v1/billing/bills?'));

    expect(billListCall).toBeDefined();
    expect(String(billListCall![0])).toContain('branchId=branch-1');
    expect(String(billListCall![0])).toContain('businessDate=');
    expect(screen.getByDisplayValue(/\d{4}-\d{2}-\d{2}/)).toBeInTheDocument();
  });

  it('loads selected bill details after a row is clicked', async () => {
    const fetchMock = stubBillingBootstrap([makeBillListItem()], [makeConfirmedOrder()]).mockResolvedValueOnce(
      createJsonResponse(makeBillDetail())
    );
    renderBillingRoute(['Billing.View'], fetchMock);
    const user = userEvent.setup();

    expect((await screen.findAllByText('BILL-20260612-0001')).length).toBeGreaterThan(0);
    await user.click(screen.getAllByRole('button', { name: /^view$/i })[0]);

    expect(await screen.findByText(/masala dosa/i)).toBeInTheDocument();
    expect(screen.getAllByText(/amount paid/i).length).toBeGreaterThan(0);
  });

  it('sends only posOrderId when creating a bill and selects the created bill', async () => {
    const fetchMock = stubBillingBootstrap([makeBillListItem()], [makeConfirmedOrder({ posOrderId: 'order-2', orderNumber: 'ORD-20260612-0002' })]).mockResolvedValueOnce(
      createJsonResponse(makeBillDetail({
        billNumber: 'BILL-20260612-0002',
        posOrderId: 'order-2',
        billId: 'bill-2',
      }))
    ).mockResolvedValueOnce(
      createJsonResponse({
        items: [
          makeBillListItem(),
          makeBillListItem({ billId: 'bill-2', billNumber: 'BILL-20260612-0002', posOrderId: 'order-2' }),
        ],
      })
    ).mockResolvedValueOnce(createJsonResponse({ items: [] }));
    renderBillingRoute(['Billing.Manage'], fetchMock);
    const user = userEvent.setup();

    await user.click((await screen.findAllByRole('button', { name: /create bill/i }))[0]);

    const createCall = fetchMock.mock.calls.find(call => String(call[0]).includes('/api/v1/billing/bills') && call[1]?.method === 'POST');
    expect(createCall).toBeDefined();
    expect(JSON.parse(String(createCall?.[1]?.body))).toEqual({ posOrderId: 'order-2' });
    expect((await screen.findAllByText(/bill-20260612-0002/i)).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/balance due/i).length).toBeGreaterThan(0);
  });

  it('hides the payment form for paid bills', async () => {
    const fetchMock = stubBillingBootstrap([makeBillListItem({ status: 'Paid', amountPaid: 25, balanceDue: 0 })], [makeConfirmedOrder()]).mockResolvedValueOnce(
      createJsonResponse(
        makeBillDetail({
          status: 'Paid',
          amountPaid: 25,
          balanceDue: 0,
          payments: [makePayment({ amount: 25 })],
          updatedAt: '2026-06-12T10:15:00Z',
        })
      )
    ).mockResolvedValueOnce(
      createJsonResponse(makeCurrentShift())
    );
    renderBillingRoute(['Payment.Record'], fetchMock);
    const user = userEvent.setup();

    await user.click((await screen.findAllByRole('button', { name: /^view$/i }))[0]);

    expect((await screen.findAllByText(/paid/i)).length).toBeGreaterThan(0);
    expect(screen.queryByRole('button', { name: /record payment/i })).not.toBeInTheDocument();
  });

  it('blocks payment validation when amount is not positive or exceeds the balance due', async () => {
    const fetchMock = stubBillingBootstrap([makeBillListItem()], []).mockResolvedValueOnce(
      createJsonResponse(makeBillDetail({ balanceDue: 25 }))
    ).mockResolvedValueOnce(
      createJsonResponse(makeCurrentShift())
    );
    renderBillingRoute(['Payment.Record'], fetchMock);
    const user = userEvent.setup();

    await user.click((await screen.findAllByRole('button', { name: /^view$/i }))[0]);

    const amountInput = await screen.findByLabelText(/^amount$/i);
    const submitButton = screen.getByRole('button', { name: /record payment/i });

    await user.clear(amountInput);
    await user.type(amountInput, '0');
    await user.click(submitButton);
    expect(screen.getAllByText(/amount must be greater than 0\./i).length).toBeGreaterThan(0);

    await user.clear(amountInput);
    await user.type(amountInput, '30');
    await user.click(submitButton);
    expect(screen.getAllByText(/amount must not exceed the selected bill balance due\./i).length).toBeGreaterThan(0);
    expect(fetchMock.mock.calls.filter(call => call[1]?.method === 'POST')).toHaveLength(0);
  });

  it('shows the active-shift warning when cash is selected but no current cashier shift exists', async () => {
    const fetchMock = stubBillingBootstrap([makeBillListItem()], [makeConfirmedOrder()]).mockResolvedValueOnce(
      createJsonResponse(makeBillDetail())
    ).mockResolvedValueOnce(
      new Response(null, { status: 204 })
    );
    renderBillingRoute(['Payment.Record'], fetchMock);
    const user = userEvent.setup();

    await user.click((await screen.findAllByRole('button', { name: /^view$/i }))[0]);

    expect(await screen.findByText(/cash payments require an active cashier shift/i)).toBeInTheDocument();
  });

  it('requires a reference number for UPI and Card payments in the billing entry UI', async () => {
    const fetchMock = stubBillingBootstrap([makeBillListItem()], [makeConfirmedOrder()]).mockResolvedValueOnce(
      createJsonResponse(makeBillDetail())
    ).mockResolvedValueOnce(
      createJsonResponse(makeCurrentShift())
    );
    renderBillingRoute(['Payment.Record'], fetchMock);
    const user = userEvent.setup();

    await user.click((await screen.findAllByRole('button', { name: /^view$/i }))[0]);

    const paymentMode = await screen.findByLabelText(/payment mode/i);
    await user.selectOptions(paymentMode, 'Upi');
    await user.clear(await screen.findByLabelText(/^amount$/i));
    await user.type(await screen.findByLabelText(/^amount$/i), '10');
    await user.click(screen.getByRole('button', { name: /record payment/i }));

    expect((await screen.findAllByText(/reference number is required for upi and card payments\./i)).length).toBeGreaterThan(1);
  });

  it('does not expose Other in the billing entry payment mode select but still renders historical Other payments', async () => {
    const fetchMock = stubBillingBootstrap([makeBillListItem()], [makeConfirmedOrder()]).mockResolvedValueOnce(
      createJsonResponse(
        makeBillDetail({
          payments: [makePayment({ paymentMode: 'Other', referenceNumber: 'SYS-1' })],
        })
      )
    ).mockResolvedValueOnce(
      createJsonResponse(makeCurrentShift())
    );
    renderBillingRoute(['Payment.Record'], fetchMock);
    const user = userEvent.setup();

    await user.click((await screen.findAllByRole('button', { name: /^view$/i }))[0]);

    expect(screen.getByText('Other')).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: /^other$/i })).not.toBeInTheDocument();
  });

  it('shows cancel bill controls only when the bill is unpaid with no recorded payments and updates after cancel', async () => {
    const fetchMock = stubBillingBootstrap([makeBillListItem()], [makeConfirmedOrder()]).mockResolvedValueOnce(
      createJsonResponse(makeBillDetail())
    ).mockResolvedValueOnce(
      createJsonResponse({
        billId: 'bill-1',
        restaurantId: 'restaurant-1',
        branchId: 'branch-1',
        posOrderId: 'order-1',
        billNumber: 'BILL-20260612-0001',
        businessDate: '2026-06-12T00:00:00Z',
        status: 'Cancelled',
        subtotal: 25,
        taxTotal: 0,
        grandTotal: 25,
        amountPaid: 0,
        balanceDue: 25,
        createdByUserId: 'user-1',
        cancelledByUserId: 'user-1',
        cancelledAt: '2026-06-12T10:20:00Z',
        cancelReason: 'Customer requested',
        createdAt: '2026-06-12T09:50:00Z',
        updatedAt: '2026-06-12T10:20:00Z',
        lines: [makeBillLine()],
        payments: [],
      })
    ).mockResolvedValueOnce(createJsonResponse({ items: [] }));
    renderBillingRoute(['Billing.Manage'], fetchMock);
    const user = userEvent.setup();

    await user.click((await screen.findAllByRole('button', { name: /^view$/i }))[0]);

    const cancelReasonInput = await screen.findByLabelText(/cancellation reason/i);
    const cancelButton = screen.getByRole('button', { name: /cancel bill/i });
    expect(cancelButton).toBeDisabled();

    await user.type(cancelReasonInput, 'Customer requested');
    expect(cancelButton).toBeEnabled();
    await user.click(cancelButton);

    const cancelCall = fetchMock.mock.calls.find(call => String(call[0]).includes('/cancel') && call[1]?.method === 'POST');
    expect(JSON.parse(String(cancelCall?.[1]?.body))).toEqual({ reason: 'Customer requested' });
    expect((await screen.findAllByText(/cancelled/i)).length).toBeGreaterThan(0);
  });

  it('shows cancel payment controls only for recorded payments and updates the selected bill after cancel', async () => {
    const fetchMock = stubBillingBootstrap([makeBillListItem({ status: 'PartiallyPaid', amountPaid: 10, balanceDue: 15 })], []).mockResolvedValueOnce(
      createJsonResponse(
        makeBillDetail({
          status: 'PartiallyPaid',
          amountPaid: 10,
          balanceDue: 15,
          payments: [makePayment()],
        })
      )
    ).mockResolvedValueOnce(
      createJsonResponse(
        makeBillDetail({
          status: 'Unpaid',
          amountPaid: 0,
          balanceDue: 25,
          payments: [],
        })
      )
    );
    renderBillingRoute(['Billing.View', 'Payment.Cancel'], fetchMock);
    const user = userEvent.setup();

    await user.click((await screen.findAllByRole('button', { name: /^view$/i }))[0]);

    const cancelPaymentButton = await screen.findByRole('button', { name: /cancel payment/i });
    await user.click(cancelPaymentButton);

    const reasonInput = await screen.findByLabelText(/cancel payment reason/i);
    await user.type(reasonInput, 'Duplicate payment');
    expect(screen.getByRole('button', { name: /confirm cancel/i })).toBeEnabled();
    await user.click(screen.getByRole('button', { name: /confirm cancel/i }));

    const cancelCall = fetchMock.mock.calls.find(call => String(call[0]).includes('/api/v1/billing/payments/payment-1/cancel'));
    expect(JSON.parse(String(cancelCall?.[1]?.body))).toEqual({ reason: 'Duplicate payment' });
    expect((await screen.findAllByText(/unpaid/i)).length).toBeGreaterThan(0);
  });

  it('does not show delete, gateway, cash drawer, or kitchen controls in the billing workspace', async () => {
    const fetchMock = stubBillingBootstrap([makeBillListItem()], [makeConfirmedOrder()]).mockResolvedValueOnce(
      createJsonResponse(makeBillDetail())
    );
    renderBillingRoute(['Billing.View'], fetchMock);
    const user = userEvent.setup();

    await user.click((await screen.findAllByRole('button', { name: /^view$/i }))[0]);
    const main = screen.getByRole('main');

    expect(within(main).queryByRole('button', { name: /delete/i })).not.toBeInTheDocument();
    expect(within(main).queryByText(/gateway/i)).not.toBeInTheDocument();
    expect(within(main).queryByText(/cash drawer/i)).not.toBeInTheDocument();
    expect(within(main).queryByText(/kitchen/i)).not.toBeInTheDocument();
    expect(within(main).queryByText(/inventory/i)).not.toBeInTheDocument();
  });

  it('loads the receipt preview with bill, line snapshots, totals, and payments', async () => {
  const fetchMock = stubBillingBootstrap([makeBillListItem()], [makeConfirmedOrder()])
      .mockResolvedValueOnce(createJsonResponse(makeBillDetail()))
      .mockResolvedValueOnce(
        createJsonResponse(
          makeBillReceipt({
            payments: [makeReceiptPayment()],
          })
        )
      );
    renderBillingRoute(['Billing.View'], fetchMock);
    const user = userEvent.setup();

    await user.click((await screen.findAllByRole('button', { name: /^view$/i }))[0]);
    await user.click(screen.getByRole('button', { name: /view receipt/i }));

    expect(await screen.findByRole('heading', { name: /receipt preview/i })).toBeInTheDocument();
    expect(screen.getAllByText(/12 market street/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/business date/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/table 12/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/walk-in customer/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/90000000/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/masala dosa/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/bill-20260612-0001/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/ord-20260612-0001/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/25\.00/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/pay-20260612-0001/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/thank you\. visit again\./i).length).toBeGreaterThan(0);
    const receiptBody = document.querySelector('.billing-receipt');
    expect(receiptBody).not.toBeNull();
    expect(within(receiptBody as HTMLElement).queryByRole('button', { name: /print receipt/i })).not.toBeInTheDocument();
  });

  it('prints only after the print-event call succeeds and keeps the first copy original before reprint', async () => {
    const fetchMock = stubBillingBootstrap([makeBillListItem()], [makeConfirmedOrder()])
      .mockResolvedValueOnce(createJsonResponse(makeBillDetail()))
      .mockResolvedValueOnce(
        createJsonResponse(
          makeBillReceipt({
            printCount: 1,
            isReprint: false,
            printedAt: '2026-06-12T10:10:00Z',
          })
        )
      )
      .mockResolvedValueOnce(
        createJsonResponse(
          makeBillReceipt({
            printCount: 2,
            isReprint: true,
            printedAt: '2026-06-12T10:11:00Z',
          })
        )
      );
    renderBillingRoute(['Billing.View'], fetchMock);
    const printMock = vi.spyOn(window, 'print').mockImplementation(() => undefined);
    const user = userEvent.setup();

    await user.click((await screen.findAllByRole('button', { name: /^view$/i }))[0]);
    await user.click(screen.getByRole('button', { name: /print receipt/i }));

    const printCall = fetchMock.mock.calls.find(call => String(call[0]).includes('/receipt/print-events'));
    expect(printCall).toBeDefined();
    expect(printCall?.[1]?.method).toBe('POST');
    expect(printCall?.[1]?.body).toBeUndefined();
    expect((await screen.findAllByText(/original copy/i)).length).toBeGreaterThan(0);
    await waitFor(() => expect(printMock).toHaveBeenCalledTimes(1));

    await user.click(screen.getByRole('button', { name: /print receipt/i }));

    expect((await screen.findAllByText(/reprint copy/i)).length).toBeGreaterThan(0);
    await waitFor(() => expect(printMock).toHaveBeenCalledTimes(2));
    expect(fetchMock.mock.calls.filter(call => String(call[0]).includes('/receipt/print-events') && call[1]?.method === 'POST')).toHaveLength(2);
  });

  it('labels cancelled receipts as cancelled copies instead of normal paid receipts', async () => {
    const fetchMock = stubBillingBootstrap([makeBillListItem({ status: 'Cancelled' })], [makeConfirmedOrder()])
      .mockResolvedValueOnce(
        createJsonResponse(
          makeBillDetail({
            status: 'Cancelled',
            cancelledByUserId: 'user-1',
            cancelledAt: '2026-06-12T10:20:00Z',
            cancelReason: 'Customer requested',
          })
        )
      )
      .mockResolvedValueOnce(
        createJsonResponse(
          makeBillReceipt({
            status: 'Cancelled',
            cancelledByUserId: 'user-1',
            cancelledAt: '2026-06-12T10:20:00Z',
            cancelReason: 'Customer requested',
            printCount: 0,
            isReprint: false,
          })
        )
      );
    renderBillingRoute(['Billing.View'], fetchMock);
    const user = userEvent.setup();

    await user.click((await screen.findAllByRole('button', { name: /^view$/i }))[0]);
    await user.click(screen.getByRole('button', { name: /view receipt/i }));

    expect((await screen.findAllByText(/cancelled copy/i)).length).toBeGreaterThan(0);
    expect(screen.getByRole('button', { name: /print cancelled copy/i })).toBeInTheDocument();
    expect((await screen.findAllByText(/customer requested/i)).length).toBeGreaterThan(0);
    expect(screen.queryByText(/original copy/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/reprint copy/i)).not.toBeInTheDocument();
  });

  it('does not call window.print when the print-event request fails and keeps the payload safe', async () => {
    const fetchMock = stubBillingBootstrap([makeBillListItem()], [makeConfirmedOrder()])
      .mockResolvedValueOnce(createJsonResponse(makeBillDetail()))
      .mockResolvedValueOnce(
        createJsonResponse(
          {
            title: 'Bad Request',
            detail: 'Microsoft.Data.SqlClient.SqlException: stack trace from SQL Server',
          },
          { status: 400 }
        )
      );
    renderBillingRoute(['Billing.View'], fetchMock);
    const printMock = vi.spyOn(window, 'print').mockImplementation(() => undefined);
    const user = userEvent.setup();

    await user.click((await screen.findAllByRole('button', { name: /^view$/i }))[0]);
    await user.click(screen.getByRole('button', { name: /print receipt/i }));

    expect(printMock).not.toHaveBeenCalled();
    expect(await screen.findByText(/unable to record receipt print/i)).toBeInTheDocument();
    expect(screen.queryByText(/sqlserver/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/stack trace/i)).not.toBeInTheDocument();
  });

  it('does not expose internal ids in the receipt preview', async () => {
    const fetchMock = stubBillingBootstrap([makeBillListItem()], [makeConfirmedOrder()])
      .mockResolvedValueOnce(createJsonResponse(makeBillDetail()))
      .mockResolvedValueOnce(
        createJsonResponse(
          makeBillReceipt({
            printCount: 1,
            isReprint: true,
            payments: [makeReceiptPayment()],
          })
        )
      );
    renderBillingRoute(['Billing.View'], fetchMock);
    const user = userEvent.setup();

    await user.click((await screen.findAllByRole('button', { name: /^view$/i }))[0]);
    await user.click(screen.getByRole('button', { name: /view receipt/i }));

    expect((await screen.findAllByText(/reprint copy/i)).length).toBeGreaterThan(0);
    expect(screen.queryByText('bill-1')).not.toBeInTheDocument();
    expect(screen.queryByText('restaurant-1')).not.toBeInTheDocument();
    expect(screen.queryByText('user-1')).not.toBeInTheDocument();
  });
});
