import { render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthProvider } from '../auth/AuthProvider';
import { LanguageProvider } from '../../i18n/LanguageProvider';
import { clearAuthSession, storeAuthSession } from '../../test/authTestUtils';
import type { InventoryItemListItem } from '../inventory/inventoryTypes';
import type { VendorDetail } from './vendorTypes';
import VendorBillOcrDraftPanel from './VendorBillOcrDraftPanel';
import {
  confirmVendorBillOcrDraft,
  getVendorBillOcrDraft,
  listVendorBillOcrDrafts,
  updateVendorBillOcrDraft,
  uploadVendorBillOcrDraft,
} from './vendorBillOcrApi';

vi.mock('./vendorBillOcrApi', () => ({
  confirmVendorBillOcrDraft: vi.fn(),
  getVendorBillOcrDraft: vi.fn(),
  listVendorBillOcrDrafts: vi.fn(),
  updateVendorBillOcrDraft: vi.fn(),
  uploadVendorBillOcrDraft: vi.fn(),
}));

const mockedListVendorBillOcrDrafts = vi.mocked(listVendorBillOcrDrafts);
const mockedGetVendorBillOcrDraft = vi.mocked(getVendorBillOcrDraft);
const mockedUpdateVendorBillOcrDraft = vi.mocked(updateVendorBillOcrDraft);
const mockedUploadVendorBillOcrDraft = vi.mocked(uploadVendorBillOcrDraft);
const mockedConfirmVendorBillOcrDraft = vi.mocked(confirmVendorBillOcrDraft);

const renderVendorBillOcrDraftPanel = () =>
  render(
    <AuthProvider>
      <LanguageProvider>
        <VendorBillOcrDraftPanel
          branchId="branch-1"
          vendors={vendors}
          inventoryItems={inventoryItems}
          canAccess
        />
      </LanguageProvider>
    </AuthProvider>
  );

const vendors: VendorDetail[] = [
  {
    vendorId: 'vendor-1',
    restaurantId: 'restaurant-1',
    branchId: 'branch-1',
    name: 'Fresh Rice',
    normalizedName: 'FRESH RICE',
    vendorType: 'Groceries',
    contactName: null,
    mobileNumber: null,
    address: null,
    notes: null,
    isActive: true,
    createdAtUtc: '2026-06-18T00:00:00Z',
    updatedAtUtc: null,
  },
];

const inventoryItems: InventoryItemListItem[] = [
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
    currentStock: 10,
    status: 'Healthy',
    createdAtUtc: '2026-06-18T00:00:00Z',
    updatedAtUtc: '2026-06-18T00:00:00Z',
  },
];

const baseDraft = {
  vendorBillOcrDraftId: 'draft-1',
  restaurantId: 'restaurant-1',
  branchId: 'branch-1',
  uploadedByUserId: 'user-1',
  originalFileName: 'bill.pdf',
  contentType: 'application/pdf',
  fileSizeBytes: 3,
  status: 'Extracted',
  extractedVendorName: 'Fresh Rice',
  extractedBillNumber: 'OCR-200',
  extractedBillDate: '2026-06-18T00:00:00Z',
  extractedTotalAmount: 10,
  extractedConfidenceScore: 0.8,
  providerWarnings: ['Vendor name was not detected.', 'Image was slightly blurred.'],
  hasDuplicateReceipt: false,
  duplicateReceiptWarning: null,
  canOverrideDuplicateReceipt: false,
  reviewedVendorId: null,
  reviewedBillNumber: null,
  reviewedBillDate: null,
  reviewedTotalAmount: null,
  safeErrorMessage: null,
  confirmedVendorBillId: null,
  createdAtUtc: '2026-06-18T00:00:00Z',
  updatedAtUtc: '2026-06-18T00:00:00Z',
  confirmedAtUtc: null,
  lines: [
    {
      vendorBillOcrDraftLineId: 'line-1',
      lineNumber: 1,
      extractedDescription: 'Rice',
      extractedQuantity: 2,
      extractedUnitCost: 5,
      extractedLineTotal: 10,
      confidenceScore: 0.82,
      selectedInventoryItemId: null,
      isIgnored: false,
      reviewedDescription: null,
      reviewedQuantity: null,
      reviewedUnitCost: null,
      reviewedLineTotal: null,
      createdAtUtc: '2026-06-18T00:00:00Z',
      updatedAtUtc: '2026-06-18T00:00:00Z',
    },
  ],
};

describe('VendorBillOcrDraftPanel', () => {
  beforeEach(() => {
    clearAuthSession();
    storeAuthSession({
      countryCode: 'IN',
      currencyCode: 'INR',
      timeZoneId: 'Asia/Kolkata',
      permissions: ['Vendor.View'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });
    mockedListVendorBillOcrDrafts.mockReset();
    mockedGetVendorBillOcrDraft.mockReset();
    mockedUpdateVendorBillOcrDraft.mockReset();
    mockedUploadVendorBillOcrDraft.mockReset();
    mockedConfirmVendorBillOcrDraft.mockReset();
  });

  afterEach(() => {
    clearAuthSession();
  });

  it('renders provider warnings and low confidence as review-required', async () => {
    mockedListVendorBillOcrDrafts.mockResolvedValue({ items: [baseDraft] });
    mockedGetVendorBillOcrDraft.mockResolvedValue(baseDraft);
    mockedUpdateVendorBillOcrDraft.mockResolvedValue(baseDraft);
    mockedUploadVendorBillOcrDraft.mockResolvedValue(baseDraft);

    renderVendorBillOcrDraftPanel();

    expect(await screen.findByText(/^Review required$/i)).toBeInTheDocument();
    expect(screen.getByText(/^Low confidence$/i)).toBeInTheDocument();
    expect(screen.getByText(/vendor name was not detected/i)).toBeInTheDocument();
  });

  it('shows a safe extraction failure message without raw provider text', async () => {
    mockedListVendorBillOcrDrafts.mockResolvedValue({ items: [baseDraft] });
    mockedGetVendorBillOcrDraft.mockResolvedValue({
      ...baseDraft,
      status: 'ExtractionFailed',
      safeErrorMessage: 'OCR service is temporarily unavailable.',
      providerWarnings: [],
    });
    mockedUpdateVendorBillOcrDraft.mockResolvedValue(baseDraft);
    mockedUploadVendorBillOcrDraft.mockResolvedValue(baseDraft);

    renderVendorBillOcrDraftPanel();

    expect(await screen.findByText(/ocr service is temporarily unavailable/i)).toBeInTheDocument();
    expect(screen.queryByText(/raw-azure/i)).not.toBeInTheDocument();
  });

  it('shows a duplicate receipt warning and blocks confirm when override is unavailable', async () => {
    const duplicateDraft = {
      ...baseDraft,
      hasDuplicateReceipt: true,
      duplicateReceiptWarning: 'A matching vendor bill already exists for this receipt.',
      canOverrideDuplicateReceipt: false,
    };

    mockedListVendorBillOcrDrafts.mockResolvedValue({ items: [duplicateDraft] });
    mockedGetVendorBillOcrDraft.mockResolvedValue(duplicateDraft);
    mockedUpdateVendorBillOcrDraft.mockResolvedValue(duplicateDraft);
    mockedUploadVendorBillOcrDraft.mockResolvedValue(duplicateDraft);

    renderVendorBillOcrDraftPanel();

    expect(await screen.findByText(/matching vendor bill already exists/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /create vendor bill/i })).toBeDisabled();
  });

  it('renders ignored draft lines as ignored in the review summary', async () => {
    const ignoredDraft = {
      ...baseDraft,
      lines: [
        {
          ...baseDraft.lines[0],
          isIgnored: true,
          selectedInventoryItemId: null,
        },
      ],
    };

    mockedListVendorBillOcrDrafts.mockResolvedValue({ items: [ignoredDraft] });
    mockedGetVendorBillOcrDraft.mockResolvedValue(ignoredDraft);
    mockedUpdateVendorBillOcrDraft.mockResolvedValue(ignoredDraft);
    mockedUploadVendorBillOcrDraft.mockResolvedValue(ignoredDraft);

    renderVendorBillOcrDraftPanel();

    expect((await screen.findAllByText(/^Ignored$/i)).length).toBeGreaterThan(0);
  });
});
