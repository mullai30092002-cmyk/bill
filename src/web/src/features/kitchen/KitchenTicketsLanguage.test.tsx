import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import App from '../../App';
import { clearAuthSession, createJsonResponse, storeAuthSession } from '../../test/authTestUtils';
import { LANGUAGE_STORAGE_KEY } from '../../i18n/translations';
import { renderWithRouter } from '../../test/renderWithRouter';

const buildKitchenTicketsResponse = () =>
  createJsonResponse({
    items: [
      {
        kitchenTicketId: 'ticket-1',
        branchId: 'branch-1',
        posOrderId: 'order-1',
        ticketNumber: 'KIT-20260613-0001',
        orderNumberSnapshot: 'ORD-20260613-0001',
        orderTypeSnapshot: 'EatIn',
        tableNameSnapshot: 'Table 3',
        customerNameSnapshot: null,
        orderNotesSnapshot: null,
        status: 'Pending',
        lineCount: 2,
        createdAt: '2026-06-13T08:00:00Z',
        updatedAt: '2026-06-13T08:02:00Z',
        cancelledAt: null,
        cancelReason: null,
      },
    ],
  });

const buildKitchenTicketDetailResponse = () =>
  createJsonResponse({
    kitchenTicketId: 'ticket-1',
    restaurantId: 'restaurant-1',
    branchId: 'branch-1',
    posOrderId: 'order-1',
    ticketNumber: 'KIT-20260613-0001',
    orderNumberSnapshot: 'ORD-20260613-0001',
    orderTypeSnapshot: 'EatIn',
    tableNameSnapshot: 'Table 3',
    customerNameSnapshot: null,
    orderNotesSnapshot: null,
    status: 'Pending',
    createdByUserId: 'user-1',
    lastStatusChangedByUserId: 'user-1',
    cancelledByUserId: null,
    cancelledAt: null,
    cancelReason: null,
    createdAt: '2026-06-13T08:00:00Z',
    updatedAt: '2026-06-13T08:02:00Z',
    preparingAt: null,
    readyAt: null,
    servedAt: null,
    inventoryDeductionStatus: 'NotDeducted',
    lines: [
      {
        kitchenTicketLineId: 'line-1',
        posOrderLineId: 'pos-line-1',
        menuItemId: 'item-1',
        menuCategoryId: 'category-1',
        menuItemNameSnapshot: 'Masala Dosa',
        menuCategoryNameSnapshot: 'Breakfast',
        skuSnapshot: 'DOSA-01',
        quantity: 2,
        notes: 'Less spicy',
        displayOrder: 1,
        createdAt: '2026-06-13T08:00:00Z',
      },
    ],
  });

const buildDeductionPreviewResponse = () =>
  createJsonResponse({
    ticketId: 'ticket-1',
    canComplete: true,
    lines: [
      {
        menuItemName: 'Masala Dosa',
        inventoryItemName: 'Rice Flour',
        requiredQuantity: 2,
        availableQuantity: 10,
        resultingQuantity: 8,
        status: 'Sufficient',
      },
    ],
  });

afterEach(() => {
  clearAuthSession();
  localStorage.removeItem(LANGUAGE_STORAGE_KEY);
  vi.unstubAllGlobals();
});

describe('KitchenTickets Tamil chrome', () => {
  it('renders Tamil kitchen display chrome for the ticket workflow', async () => {
    localStorage.setItem(LANGUAGE_STORAGE_KEY, 'ta');
    storeAuthSession({
      permissions: ['KitchenTicket.View', 'KitchenTicket.Manage'],
      roles: ['KitchenUser'],
      activeRole: 'KitchenUser',
    });

    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(buildKitchenTicketsResponse())
      .mockResolvedValueOnce(buildKitchenTicketDetailResponse())
      .mockResolvedValueOnce(buildDeductionPreviewResponse());
    vi.stubGlobal('fetch', fetchMock);

    const user = userEvent.setup();
    renderWithRouter(<App />, '/kitchen/tickets');

    expect(await screen.findByRole('heading', { name: /சமையல் காட்சி/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /புதுப்பி/i })).toBeInTheDocument();

    await user.click(await screen.findByRole('button', { name: /kit-20260613-0001/i }));

    expect(await screen.findByRole('button', { name: /தயாரிக்க தொடங்கு/i })).toBeInTheDocument();
  });
});
