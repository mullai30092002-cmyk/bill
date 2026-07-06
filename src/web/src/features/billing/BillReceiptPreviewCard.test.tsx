import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { LanguageProvider } from '../../i18n/LanguageProvider';
import { BillReceiptPreviewCard } from './BillReceiptPreviewCard';
import type { BillDetail } from './billingTypes';
import type { BillReceiptResponse } from './billReceiptTypes';

const makeBill = (overrides: Partial<BillDetail> = {}): BillDetail => ({
  billId: 'bill-1',
  restaurantId: 'rest-1',
  branchId: 'branch-1',
  posOrderId: 'order-1',
  billNumber: 'B001',
  businessDate: '2026-01-01',
  status: 'Paid',
  subtotal: 90,
  taxTotal: 10,
  grandTotal: 100,
  amountPaid: 100,
  balanceDue: 0,
  createdByUserId: null,
  cancelledByUserId: null,
  cancelledAt: null,
  cancelReason: null,
  createdAt: '2026-01-01T10:00:00Z',
  updatedAt: null,
  lines: [],
  payments: [],
  ...overrides,
});

const makeReceipt = (overrides: Partial<BillReceiptResponse> = {}): BillReceiptResponse => ({
  billId: 'bill-1',
  restaurantId: 'rest-1',
  branchId: 'branch-1',
  restaurantCode: 'BILL01',
  countryCode: 'SG',
  currencyCode: 'SGD',
  timeZoneId: 'Asia/Singapore',
  restaurantName: 'Test Restaurant',
  branchName: 'Main Branch',
  branchAddress: null,
  posOrderId: 'order-1',
  businessDate: '2026-01-01',
  orderNumberSnapshot: 'ORD001',
  orderTypeSnapshot: 'EatIn',
  orderTableNameSnapshot: null,
  orderCustomerNameSnapshot: null,
  orderCustomerMobileSnapshot: null,
  billNumber: 'B001',
  status: 'Paid',
  createdByUserId: 'user-1',
  createdByUserLabel: 'Maya Iyer',
  createdAt: '2026-01-01T10:00:00Z',
  updatedAt: null,
  printedAt: '2026-01-01T10:05:00Z',
  cancelledByUserId: null,
  cancelledAt: null,
  cancelReason: null,
  subtotal: 90,
  taxTotal: 10,
  grandTotal: 100,
  amountPaid: 100,
  balanceDue: 0,
  printCount: 1,
  isReprint: false,
  lines: [],
  payments: [],
  ...overrides,
});

const renderCard = (bill: BillDetail | null, receipt: BillReceiptResponse | null) =>
  render(
    <LanguageProvider>
      <BillReceiptPreviewCard
        bill={bill}
        receipt={receipt}
        loading={false}
        printing={false}
        error={null}
        canViewReceipt={true}
        canPrintReceipt={true}
        onViewReceipt={vi.fn()}
        onPrintReceipt={vi.fn()}
      />
    </LanguageProvider>
  );

describe('BillReceiptPreviewCard – receipt footer', () => {
  it('renders the Intelsoft receipt footer when a receipt is loaded', () => {
    renderCard(makeBill(), makeReceipt());
    expect(screen.getByText('Powered by BillSoft · © 2026 Intelsoft')).toBeInTheDocument();
  });

  it('does not render the receipt footer when no receipt is loaded', () => {
    renderCard(makeBill(), null);
    expect(screen.queryByText('Powered by BillSoft · © 2026 Intelsoft')).not.toBeInTheDocument();
  });
});
