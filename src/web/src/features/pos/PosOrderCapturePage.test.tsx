import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import App from '../../App';
import { createJsonResponse, storeAuthSession } from '../../test/authTestUtils';
import { renderWithRouter } from '../../test/renderWithRouter';

const buildPosWorkspaceResponseMocks = () =>
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
          {
            menuItemId: 'item-2',
            restaurantId: 'restaurant-1',
            menuCategoryId: 'category-1',
            categoryName: 'Breakfast',
            name: 'Parcel Snack',
            description: 'Parcel only snack',
            sku: 'SNACK-01',
            basePrice: 1.75,
            taxRate: 0,
            isVegetarian: false,
            isAvailableForEatIn: false,
            isAvailableForParcel: true,
            status: 'Active',
            createdAt: '2026-06-11T09:00:00Z',
            updatedAt: '2026-06-11T09:30:00Z',
          },
          {
            menuItemId: 'item-3',
            restaurantId: 'restaurant-1',
            menuCategoryId: 'category-1',
            categoryName: 'Breakfast',
            name: 'EatIn Special',
            description: 'Eat-in only item',
            sku: 'SPECIAL-01',
            basePrice: 3,
            taxRate: 0,
            isVegetarian: false,
            isAvailableForEatIn: true,
            isAvailableForParcel: false,
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
    );

const renderCreateModePosWorkspace = () => {
  storeAuthSession({
    permissions: ['Order.Create', 'Order.View', 'Order.Cancel'],
    roles: ['Cashier'],
    activeRole: 'Cashier',
  });

  const fetchMock = buildPosWorkspaceResponseMocks();
  vi.stubGlobal('fetch', fetchMock);
  const user = userEvent.setup();

  renderWithRouter(<App />, '/pos/orders');

  return { fetchMock, user };
};

const getCardScope = (title: RegExp) => {
  const heading = screen.getByRole('heading', { name: title });
  const card = heading.closest('section');
  expect(card).not.toBeNull();

  return within(card as HTMLElement);
};

describe('PosOrderCapturePage', () => {
  it('shows a not-authorized state without calling workspace APIs', () => {
    storeAuthSession({ permissions: [], roles: ['Cashier'], activeRole: 'Cashier' });
    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);

    renderWithRouter(<App />, '/pos/orders');

    expect(screen.getByRole('heading', { name: /not authorized/i })).toBeInTheDocument();
    expect(screen.getByText(/order.create or order.view/i)).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('shows a safe generic message when recent orders fails with backend exception text', async () => {
    storeAuthSession({
      permissions: ['Order.View'],
      roles: ['KitchenUser'],
      activeRole: 'KitchenUser',
    });
    const fetchMock = vi
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
      .mockRejectedValueOnce(new Error('Microsoft.Data.SqlClient.SqlException: invalid column name'));
    vi.stubGlobal('fetch', fetchMock);

    renderWithRouter(<App />, '/pos/orders');

    expect(await screen.findByText(/unable to load recent orders\. please refresh or try again\./i)).toBeInTheDocument();
    expect(screen.queryByText(/microsoft\.data\.sqlclient/i)).not.toBeInTheDocument();
  });

  it('loads a read-only order list for Order.View users', async () => {
    storeAuthSession({ permissions: ['Order.View'], roles: ['KitchenUser'], activeRole: 'KitchenUser' });
    const fetchMock = buildPosWorkspaceResponseMocks();
    vi.stubGlobal('fetch', fetchMock);
    const expectedOrderNumber = 'ORD-20260612-0001';

    renderWithRouter(<App />, '/pos/orders');

    expect(await screen.findByRole('heading', { name: /pos order capture/i })).toBeInTheDocument();
    expect(screen.getByText(/read-only order mode/i)).toBeInTheDocument();
    expect((await screen.findAllByText(expectedOrderNumber)).length).toBeGreaterThan(0);
    expect(screen.queryByRole('button', { name: /create draft/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /confirm order/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /cancel order/i })).not.toBeInTheDocument();
  });

  it('preselects a single active branch and filters menu items by order type', async () => {
    const { user } = renderCreateModePosWorkspace();

    const branchSelect = await screen.findByDisplayValue('Main Branch');
    expect(branchSelect).toHaveValue('branch-1');

    const menuBrowser = getCardScope(/menu browser/i);
    expect(menuBrowser.getByText(/3 active items/i)).toBeInTheDocument();
    expect(menuBrowser.getByRole('button', { name: /eatin special/i })).toBeEnabled();
    expect(menuBrowser.getByRole('button', { name: /parcel snack/i })).toBeDisabled();

    await user.click(screen.getByRole('button', { name: /^parcel$/i }));

    expect(menuBrowser.getByRole('button', { name: /eatin special/i })).toBeDisabled();
    expect(menuBrowser.getByRole('button', { name: /parcel snack/i })).toBeEnabled();
  }, 20000);

  it('adds an available menu item to the cart and accepts line notes', async () => {
    const { user } = renderCreateModePosWorkspace();

    await user.click(screen.getByRole('button', { name: /^parcel$/i }));
    await user.click(screen.getByRole('button', { name: /parcel snack/i }));

    const cart = getCardScope(/^cart$/i);
    expect(cart.queryByText(/cart is empty/i)).not.toBeInTheDocument();
    expect(cart.getByText(/parcel snack/i)).toBeInTheDocument();
    expect(cart.getByLabelText(/quantity/i)).toHaveValue(1);

    const notesInput = cart.getByLabelText(/line notes/i);
    await user.type(notesInput, 'Less spicy');
    expect(notesInput).toHaveValue('Less spicy');
  }, 20000);

  it('allows quantity editing and removes the cart line', async () => {
    const { user } = renderCreateModePosWorkspace();

    await user.click(screen.getByRole('button', { name: /^parcel$/i }));
    await user.click(screen.getByRole('button', { name: /parcel snack/i }));

    const cart = getCardScope(/^cart$/i);
    const quantityInput = cart.getByLabelText(/quantity/i);
    await user.clear(quantityInput);
    await user.type(quantityInput, '2');

    expect(quantityInput).toHaveValue(2);
    expect(cart.getByRole('button', { name: /remove line/i })).toBeInTheDocument();

    await user.click(cart.getByRole('button', { name: /remove line/i }));

    expect(cart.getByText(/cart is empty/i)).toBeInTheDocument();
  });

  it('does not show a manual create kitchen ticket button', async () => {
    renderCreateModePosWorkspace();

    expect(await screen.findByRole('heading', { name: /pos order capture/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /create kitchen ticket/i })).not.toBeInTheDocument();
  });

  it('creates a draft order with backend-owned totals and no restaurantId or orderNumber in the request body', async () => {
    storeAuthSession({
      permissions: ['Order.Create', 'Order.View', 'Order.Cancel'],
      roles: ['Cashier'],
      activeRole: 'Cashier',
    });
    const fetchMock = vi
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
      .mockResolvedValueOnce(createJsonResponse({ items: [] }))
      .mockResolvedValueOnce(
        createJsonResponse({
          posOrderId: 'order-1',
          restaurantId: 'restaurant-1',
          branchId: 'branch-1',
          orderNumber: 'ORD-20260612-0001',
          orderType: 'EatIn',
          status: 'Draft',
          tableName: 'T1',
          customerName: 'Walk-in',
          customerMobile: null,
          notes: null,
          subtotal: 5,
          taxTotal: 0,
          grandTotal: 5,
          confirmedAt: null,
          cancelledAt: null,
          cancelReason: null,
          createdByUserId: null,
          confirmedByUserId: null,
          cancelledByUserId: null,
          createdAt: '2026-06-12T10:00:00Z',
          updatedAt: null,
          lines: [
            {
              posOrderLineId: 'line-1',
              menuItemId: 'item-1',
              menuCategoryId: 'category-1',
              menuItemNameSnapshot: 'Masala Dosa',
              menuCategoryNameSnapshot: 'Breakfast',
              skuSnapshot: 'DOSA-01',
              unitPrice: 2.5,
              taxRate: 0,
              quantity: 2,
              lineSubtotal: 5,
              lineTax: 0,
              lineTotal: 5,
              notes: 'Less spicy',
              displayOrder: 1,
              createdAt: '2026-06-12T10:00:00Z',
              updatedAt: null,
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
      .mockResolvedValueOnce(
        createJsonResponse({
          posOrderId: 'order-1',
          restaurantId: 'restaurant-1',
          branchId: 'branch-1',
          orderNumber: 'ORD-20260612-0001',
          orderType: 'EatIn',
          status: 'Draft',
          tableName: 'T1',
          customerName: 'Walk-in',
          customerMobile: null,
          notes: null,
          subtotal: 2.5,
          taxTotal: 0,
          grandTotal: 2.5,
          confirmedAt: null,
          cancelledAt: null,
          cancelReason: null,
          createdByUserId: null,
          confirmedByUserId: null,
          cancelledByUserId: null,
          createdAt: '2026-06-12T10:00:00Z',
          updatedAt: null,
          lines: [
            {
              posOrderLineId: 'line-1',
              menuItemId: 'item-1',
              menuCategoryId: 'category-1',
              menuItemNameSnapshot: 'Masala Dosa',
              menuCategoryNameSnapshot: 'Breakfast',
              skuSnapshot: 'DOSA-01',
              unitPrice: 2.5,
              taxRate: 0,
              quantity: 1,
              lineSubtotal: 2.5,
              lineTax: 0,
              lineTotal: 2.5,
              notes: null,
              displayOrder: 1,
              createdAt: '2026-06-12T10:00:00Z',
              updatedAt: null,
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
              grandTotal: 5,
              lineCount: 1,
              createdAt: '2026-06-12T10:00:00Z',
            },
          ],
        })
      );
    vi.stubGlobal('fetch', fetchMock);
    const user = userEvent.setup();

    renderWithRouter(<App />, '/pos/orders');

    await user.click(await screen.findByRole('button', { name: /masala dosa/i }));

    const quantityInput = screen.getByLabelText(/quantity/i);
    await user.clear(quantityInput);
    await user.type(quantityInput, '2');

    const notesInput = screen.getByLabelText(/line notes/i);
    await user.type(notesInput, 'Less spicy');

    await user.click(screen.getByRole('button', { name: /create draft/i }));

    expect((await screen.findAllByText('ORD-20260612-0001')).length).toBeGreaterThan(0);
    expect(screen.getByText(/created ord-20260612-0001/i)).toBeInTheDocument();

    const createCall = fetchMock.mock.calls.find(
      call =>
        call[1]?.method === 'POST' &&
        String(call[0]).includes('/api/v1/pos/orders') &&
        !String(call[0]).includes('/confirm') &&
        !String(call[0]).includes('/cancel')
    );
    expect(createCall).toBeDefined();

    const createBody = JSON.parse(String(createCall?.[1]?.body));
    expect(createBody).toEqual(
      expect.objectContaining({
        branchId: 'branch-1',
        orderType: 'EatIn',
        lines: [expect.objectContaining({ menuItemId: 'item-1', quantity: 2, notes: 'Less spicy' })],
      })
    );
    expect(createBody).not.toHaveProperty('restaurantId');
    expect(createBody).not.toHaveProperty('orderNumber');
    expect(createBody.lines[0]).not.toHaveProperty('unitPrice');
    expect(createBody.lines[0]).not.toHaveProperty('taxRate');
  });

  it('loads a draft order into the editor, updates it, confirms it, and then cancels it with a reason', async () => {
    storeAuthSession({
      permissions: ['Order.Create', 'Order.View', 'Order.Cancel'],
      roles: ['Cashier'],
      activeRole: 'Cashier',
    });
    const fetchMock = vi
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
      .mockResolvedValueOnce(
        createJsonResponse({
          posOrderId: 'order-1',
          restaurantId: 'restaurant-1',
          branchId: 'branch-1',
          orderNumber: 'ORD-20260612-0001',
          orderType: 'EatIn',
          status: 'Draft',
          tableName: 'T1',
          customerName: 'Walk-in',
          customerMobile: null,
          notes: null,
          subtotal: 2.5,
          taxTotal: 0,
          grandTotal: 2.5,
          confirmedAt: null,
          cancelledAt: null,
          cancelReason: null,
          createdByUserId: null,
          confirmedByUserId: null,
          cancelledByUserId: null,
          createdAt: '2026-06-12T10:00:00Z',
          updatedAt: null,
          lines: [
            {
              posOrderLineId: 'line-1',
              menuItemId: 'item-1',
              menuCategoryId: 'category-1',
              menuItemNameSnapshot: 'Masala Dosa',
              menuCategoryNameSnapshot: 'Breakfast',
              skuSnapshot: 'DOSA-01',
              unitPrice: 2.5,
              taxRate: 0,
              quantity: 1,
              lineSubtotal: 2.5,
              lineTax: 0,
              lineTotal: 2.5,
              notes: null,
              displayOrder: 1,
              createdAt: '2026-06-12T10:00:00Z',
              updatedAt: null,
            },
          ],
        })
      )
      .mockResolvedValueOnce(
        createJsonResponse({
          posOrderId: 'order-1',
          restaurantId: 'restaurant-1',
          branchId: 'branch-1',
          orderNumber: 'ORD-20260612-0001',
          orderType: 'EatIn',
          status: 'Draft',
          tableName: 'T1',
          customerName: 'Walk-in',
          customerMobile: null,
          notes: null,
          subtotal: 2.5,
          taxTotal: 0,
          grandTotal: 2.5,
          confirmedAt: null,
          cancelledAt: null,
          cancelReason: null,
          createdByUserId: null,
          confirmedByUserId: null,
          cancelledByUserId: null,
          createdAt: '2026-06-12T10:00:00Z',
          updatedAt: null,
          lines: [
            {
              posOrderLineId: 'line-1',
              menuItemId: 'item-1',
              menuCategoryId: 'category-1',
              menuItemNameSnapshot: 'Masala Dosa',
              menuCategoryNameSnapshot: 'Breakfast',
              skuSnapshot: 'DOSA-01',
              unitPrice: 2.5,
              taxRate: 0,
              quantity: 2,
              lineSubtotal: 5,
              lineTax: 0,
              lineTotal: 5,
              notes: 'Less spicy',
              displayOrder: 1,
              createdAt: '2026-06-12T10:00:00Z',
              updatedAt: null,
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
              grandTotal: 5,
              lineCount: 1,
              createdAt: '2026-06-12T10:00:00Z',
            },
          ],
        })
      )
      .mockResolvedValueOnce(
        createJsonResponse({
          posOrderId: 'order-1',
          restaurantId: 'restaurant-1',
          branchId: 'branch-1',
          orderNumber: 'ORD-20260612-0001',
          orderType: 'EatIn',
          status: 'Confirmed',
          tableName: 'T1',
          customerName: 'Walk-in',
          customerMobile: null,
          notes: null,
          subtotal: 2.5,
          taxTotal: 0,
          grandTotal: 2.5,
          confirmedAt: '2026-06-12T10:10:00Z',
          cancelledAt: null,
          cancelReason: null,
          createdByUserId: null,
          confirmedByUserId: 'user-1',
          cancelledByUserId: null,
          createdAt: '2026-06-12T10:00:00Z',
          updatedAt: '2026-06-12T10:10:00Z',
          kitchenTicketId: 'ticket-1',
          kitchenTicketNumber: 'KIT-20260612-0001',
          kitchenTicketStatus: 'Pending',
          lines: [
            {
              posOrderLineId: 'line-1',
              menuItemId: 'item-1',
              menuCategoryId: 'category-1',
              menuItemNameSnapshot: 'Masala Dosa',
              menuCategoryNameSnapshot: 'Breakfast',
              skuSnapshot: 'DOSA-01',
              unitPrice: 2.5,
              taxRate: 0,
              quantity: 2,
              lineSubtotal: 5,
              lineTax: 0,
              lineTotal: 5,
              notes: 'Less spicy',
              displayOrder: 1,
              createdAt: '2026-06-12T10:00:00Z',
              updatedAt: '2026-06-12T10:10:00Z',
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
              status: 'Confirmed',
              tableName: 'T1',
              customerName: 'Walk-in',
              grandTotal: 2.5,
              lineCount: 1,
              createdAt: '2026-06-12T10:00:00Z',
            },
          ],
        })
      )
      .mockResolvedValueOnce(
        createJsonResponse({
          posOrderId: 'order-1',
          restaurantId: 'restaurant-1',
          branchId: 'branch-1',
          orderNumber: 'ORD-20260612-0001',
          orderType: 'EatIn',
          status: 'Cancelled',
          tableName: 'T1',
          customerName: 'Walk-in',
          customerMobile: null,
          notes: null,
          subtotal: 2.5,
          taxTotal: 0,
          grandTotal: 2.5,
          confirmedAt: '2026-06-12T10:10:00Z',
          cancelledAt: '2026-06-12T10:15:00Z',
          cancelReason: 'Customer cancelled',
          createdByUserId: null,
          confirmedByUserId: 'user-1',
          cancelledByUserId: 'user-1',
          createdAt: '2026-06-12T10:00:00Z',
          updatedAt: '2026-06-12T10:15:00Z',
          lines: [
            {
              posOrderLineId: 'line-1',
              menuItemId: 'item-1',
              menuCategoryId: 'category-1',
              menuItemNameSnapshot: 'Masala Dosa',
              menuCategoryNameSnapshot: 'Breakfast',
              skuSnapshot: 'DOSA-01',
              unitPrice: 2.5,
              taxRate: 0,
              quantity: 2,
              lineSubtotal: 5,
              lineTax: 0,
              lineTotal: 5,
              notes: 'Less spicy',
              displayOrder: 1,
              createdAt: '2026-06-12T10:00:00Z',
              updatedAt: '2026-06-12T10:15:00Z',
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
              status: 'Cancelled',
              tableName: 'T1',
              customerName: 'Walk-in',
              grandTotal: 2.5,
              lineCount: 1,
              createdAt: '2026-06-12T10:00:00Z',
            },
          ],
        })
      );
    vi.stubGlobal('fetch', fetchMock);
    const user = userEvent.setup();

    renderWithRouter(<App />, '/pos/orders');

    await user.click(await screen.findByRole('button', { name: 'ORD-20260612-0001' }));

    expect(await screen.findByRole('button', { name: /load into draft/i })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /load into draft/i }));

    const quantityInput = await screen.findByLabelText(/quantity/i);
    await user.clear(quantityInput);
    await user.type(quantityInput, '2');

    await user.click(screen.getByRole('button', { name: /update draft/i }));

    const updateCall = fetchMock.mock.calls.find(
      call =>
        call[1]?.method === 'PUT' &&
        String(call[0]).includes('/api/v1/pos/orders/order-1')
    );
    expect(updateCall).toBeDefined();

    const updateBody = JSON.parse(String(updateCall?.[1]?.body));
    expect(updateBody).toEqual(
      expect.objectContaining({
        orderType: 'EatIn',
        lines: [expect.objectContaining({ menuItemId: 'item-1', quantity: 2 })],
      })
    );
    expect(updateBody).not.toHaveProperty('restaurantId');
    expect(updateBody).not.toHaveProperty('branchId');
    expect(updateBody).not.toHaveProperty('orderNumber');

    // Confirm order: first click reveals inline summary, second click fires the API
    await user.click(screen.getByRole('button', { name: /confirm order/i }));
    expect(screen.getByRole('button', { name: /yes, confirm order/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /no, go back/i })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /yes, confirm order/i }));

    expect(await screen.findByRole('status')).toHaveTextContent(/order confirmed and sent to kitchen as kit-20260612-0001/i);
    expect(fetchMock.mock.calls.some(call => String(call[0]).includes('/api/v1/kitchen/tickets'))).toBe(false);

    // Cancel order: reason input + danger button (no second browser dialog)
    const cancelButton = screen.getByRole('button', { name: /cancel order/i });
    expect(cancelButton).toBeDisabled();

    const cancelReasonInput = screen.getByLabelText(/cancel reason/i);
    await user.type(cancelReasonInput, 'Customer cancelled');
    expect(cancelButton).toBeEnabled();

    await user.click(cancelButton);

    const confirmCall = fetchMock.mock.calls.find(call => String(call[0]).includes('/confirm'));
    const cancelCall = fetchMock.mock.calls.find(call => String(call[0]).includes('/cancel'));
    expect(confirmCall).toBeDefined();
    expect(cancelCall).toBeDefined();

    const cancelBody = JSON.parse(String(cancelCall?.[1]?.body));
    expect(cancelBody).toEqual({ reason: 'Customer cancelled' });
    expect(await screen.findByRole('status')).toHaveTextContent(/ORD-20260612-0001 cancelled/i);
  }, 20000);

  it('shows a safe error when confirm fails and does not show the sent-to-kitchen success message', async () => {
    storeAuthSession({
      permissions: ['Order.Create', 'Order.View', 'Order.Cancel'],
      roles: ['Cashier'],
      activeRole: 'Cashier',
    });
    const fetchMock = vi
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
      .mockResolvedValueOnce(
        createJsonResponse({
          posOrderId: 'order-1',
          restaurantId: 'restaurant-1',
          branchId: 'branch-1',
          orderNumber: 'ORD-20260612-0001',
          orderType: 'EatIn',
          status: 'Draft',
          tableName: 'T1',
          customerName: 'Walk-in',
          customerMobile: null,
          notes: null,
          subtotal: 2.5,
          taxTotal: 0,
          grandTotal: 2.5,
          confirmedAt: null,
          cancelledAt: null,
          cancelReason: null,
          createdByUserId: null,
          confirmedByUserId: null,
          cancelledByUserId: null,
          createdAt: '2026-06-12T10:00:00Z',
          updatedAt: null,
          lines: [
            {
              posOrderLineId: 'line-1',
              menuItemId: 'item-1',
              menuCategoryId: 'category-1',
              menuItemNameSnapshot: 'Masala Dosa',
              menuCategoryNameSnapshot: 'Breakfast',
              skuSnapshot: 'DOSA-01',
              unitPrice: 2.5,
              taxRate: 0,
              quantity: 1,
              lineSubtotal: 2.5,
              lineTax: 0,
              lineTotal: 2.5,
              notes: null,
              displayOrder: 1,
              createdAt: '2026-06-12T10:00:00Z',
              updatedAt: null,
            },
          ],
        })
      )
      .mockResolvedValueOnce(
        createJsonResponse(
          {
            title: 'Bad Request',
            detail: 'Unable to confirm order and send it to kitchen. Please try again.',
            status: 400,
          },
          { status: 400 }
        )
      );
    vi.stubGlobal('fetch', fetchMock);
    const user = userEvent.setup();

    renderWithRouter(<App />, '/pos/orders');

    await user.click(await screen.findByRole('button', { name: 'ORD-20260612-0001' }));

    // First click shows the inline confirmation step
    await user.click(await screen.findByRole('button', { name: /confirm order/i }));
    expect(screen.getByRole('button', { name: /yes, confirm order/i })).toBeInTheDocument();

    // Second click fires the API
    await user.click(screen.getByRole('button', { name: /yes, confirm order/i }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/unable to confirm order and send it to kitchen/i);
    expect(screen.queryByText(/sent to kitchen as kit-/i)).not.toBeInTheDocument();
  });

  it('selecting a recent order shows the selected order detail panel', async () => {
    const fetchMock = buildPosWorkspaceResponseMocks()
      .mockResolvedValueOnce(
        createJsonResponse({
          posOrderId: 'order-1',
          restaurantId: 'restaurant-1',
          branchId: 'branch-1',
          orderNumber: 'ORD-20260612-0001',
          orderType: 'EatIn',
          status: 'Draft',
          tableName: null,
          customerName: null,
          customerMobile: null,
          notes: null,
          subtotal: 2.5,
          taxTotal: 0,
          grandTotal: 2.5,
          confirmedAt: null,
          cancelledAt: null,
          cancelReason: null,
          createdByUserId: null,
          confirmedByUserId: null,
          cancelledByUserId: null,
          createdAt: '2026-06-12T10:00:00Z',
          updatedAt: null,
          lines: [],
        })
      );
    vi.stubGlobal('fetch', fetchMock);
    const user = userEvent.setup();

    storeAuthSession({
      permissions: ['Order.Create', 'Order.View', 'Order.Cancel'],
      roles: ['Cashier'],
      activeRole: 'Cashier',
    });
    renderWithRouter(<App />, '/pos/orders');

    await user.click(await screen.findByRole('button', { name: 'ORD-20260612-0001' }));

    expect(await screen.findByRole('button', { name: /load into draft/i })).toBeInTheDocument();
    expect(screen.getAllByText('ORD-20260612-0001').length).toBeGreaterThan(0);
  }, 20000);

  it('the selected order row shows a selected state after clicking View', async () => {
    const fetchMock = buildPosWorkspaceResponseMocks()
      .mockResolvedValueOnce(
        createJsonResponse({
          posOrderId: 'order-1',
          restaurantId: 'restaurant-1',
          branchId: 'branch-1',
          orderNumber: 'ORD-20260612-0001',
          orderType: 'EatIn',
          status: 'Draft',
          tableName: null,
          customerName: null,
          customerMobile: null,
          notes: null,
          subtotal: 2.5,
          taxTotal: 0,
          grandTotal: 2.5,
          confirmedAt: null,
          cancelledAt: null,
          cancelReason: null,
          createdByUserId: null,
          confirmedByUserId: null,
          cancelledByUserId: null,
          createdAt: '2026-06-12T10:00:00Z',
          updatedAt: null,
          lines: [],
        })
      );
    vi.stubGlobal('fetch', fetchMock);
    const user = userEvent.setup();

    storeAuthSession({
      permissions: ['Order.Create', 'Order.View', 'Order.Cancel'],
      roles: ['Cashier'],
      activeRole: 'Cashier',
    });
    renderWithRouter(<App />, '/pos/orders');

    const orderBtn = await screen.findByRole('button', { name: 'ORD-20260612-0001' });
    await user.click(orderBtn);

    // Wait for the order detail to load (confirms the selection completed)
    await screen.findByRole('button', { name: /load into draft/i });

    // After selection the order number button has aria-pressed="true" (selected state)
    expect(screen.getByRole('button', { name: 'ORD-20260612-0001' })).toHaveAttribute('aria-pressed', 'true');
  }, 20000);

  it('selected order detail panel is rendered outside the sticky cart column', async () => {
    storeAuthSession({
      permissions: ['Order.Create', 'Order.View', 'Order.Cancel'],
      roles: ['Cashier'],
      activeRole: 'Cashier',
    });
    const fetchMock = buildPosWorkspaceResponseMocks()
      .mockResolvedValueOnce(
        createJsonResponse({
          posOrderId: 'order-1',
          restaurantId: 'restaurant-1',
          branchId: 'branch-1',
          orderNumber: 'ORD-20260612-0001',
          orderType: 'EatIn',
          status: 'Draft',
          tableName: null,
          customerName: null,
          customerMobile: null,
          notes: null,
          subtotal: 2.5,
          taxTotal: 0,
          grandTotal: 2.5,
          confirmedAt: null,
          cancelledAt: null,
          cancelReason: null,
          createdByUserId: null,
          confirmedByUserId: null,
          cancelledByUserId: null,
          createdAt: '2026-06-12T10:00:00Z',
          updatedAt: null,
          lines: [],
        })
      );
    vi.stubGlobal('fetch', fetchMock);
    const user = userEvent.setup();
    renderWithRouter(<App />, '/pos/orders');

    await user.click(await screen.findByRole('button', { name: 'ORD-20260612-0001' }));

    await screen.findByRole('button', { name: /load into draft/i });

    // The selected order panel must NOT be inside the sticky cart column
    const selectedOrderPanel = document.querySelector('[data-testid="pos-selected-order-panel"]');
    expect(selectedOrderPanel).not.toBeNull();
    const stickyCartColumn = document.querySelector('.pos-workspace__cart');
    expect(stickyCartColumn).not.toBeNull();
    expect(stickyCartColumn!.contains(selectedOrderPanel)).toBe(false);
  }, 20000);

  it('cart, compact totals, and draft actions remain in the active order workflow after selecting an order', async () => {
    const { user } = renderCreateModePosWorkspace();

    await screen.findByDisplayValue('Main Branch');

    // All three active-order-workflow elements are present in the cart column
    const cartColumn = document.querySelector('.pos-workspace__cart');
    expect(cartColumn).not.toBeNull();

    // Cart heading is inside the cart column
    const cartHeading = screen.getByRole('heading', { name: /^cart$/i });
    expect(cartColumn!.contains(cartHeading)).toBe(true);

    // Draft actions (create/clear draft buttons) are inside the cart column
    const createDraftBtn = screen.getByRole('button', { name: /create draft/i });
    expect(cartColumn!.contains(createDraftBtn)).toBe(true);

    // Compact totals bar is inside the cart column (not Recent Orders or Selected Order sections)
    const totalsBar = cartColumn!.querySelector('.pos-totals-bar');
    expect(totalsBar).not.toBeNull();
  }, 20000);

  it('clicking the Order ID button selects the recent order', async () => {
    const { user } = renderCreateModePosWorkspace();

    await screen.findByDisplayValue('Main Branch');

    // The order list renders; no detail panel shown yet
    expect(screen.queryByRole('button', { name: /load into draft/i })).not.toBeInTheDocument();

    // Order number is a clickable button in the recent orders list
    const orderNumberBtn = await screen.findByRole('button', { name: 'ORD-20260612-0001' });
    expect(orderNumberBtn).toBeInTheDocument();
  }, 20000);

  it('separate View button is not rendered in the recent orders list', async () => {
    const { user: _user } = renderCreateModePosWorkspace();

    await screen.findByDisplayValue('Main Branch');

    // Confirm the order list has loaded (order number visible)
    expect(await screen.findByRole('button', { name: 'ORD-20260612-0001' })).toBeInTheDocument();

    // No separate "View" button column
    expect(screen.queryByRole('button', { name: /^view$/i })).not.toBeInTheDocument();
  }, 20000);

  it('selected recent order row has aria-pressed selected state', async () => {
    const fetchMock = buildPosWorkspaceResponseMocks()
      .mockResolvedValueOnce(
        createJsonResponse({
          posOrderId: 'order-1',
          restaurantId: 'restaurant-1',
          branchId: 'branch-1',
          orderNumber: 'ORD-20260612-0001',
          orderType: 'EatIn',
          status: 'Draft',
          tableName: null,
          customerName: null,
          customerMobile: null,
          notes: null,
          subtotal: 2.5,
          taxTotal: 0,
          grandTotal: 2.5,
          confirmedAt: null,
          cancelledAt: null,
          cancelReason: null,
          createdByUserId: null,
          confirmedByUserId: null,
          cancelledByUserId: null,
          createdAt: '2026-06-12T10:00:00Z',
          updatedAt: null,
          lines: [],
        })
      );
    vi.stubGlobal('fetch', fetchMock);
    const user = userEvent.setup();
    storeAuthSession({
      permissions: ['Order.Create', 'Order.View', 'Order.Cancel'],
      roles: ['Cashier'],
      activeRole: 'Cashier',
    });
    renderWithRouter(<App />, '/pos/orders');

    const orderBtn = await screen.findByRole('button', { name: 'ORD-20260612-0001' });
    expect(orderBtn).toHaveAttribute('aria-pressed', 'false');

    await user.click(orderBtn);
    await screen.findByRole('button', { name: /load into draft/i });

    expect(screen.getByRole('button', { name: 'ORD-20260612-0001' })).toHaveAttribute('aria-pressed', 'true');
  }, 20000);

  it('Branch column is not rendered in the recent orders table', async () => {
    const { user: _user } = renderCreateModePosWorkspace();

    await screen.findByDisplayValue('Main Branch');
    await screen.findByRole('button', { name: 'ORD-20260612-0001' });

    // Branch column header should not exist in the recent orders table
    const recentOrdersSection = document.querySelector('.pos-order-list');
    expect(recentOrdersSection).not.toBeNull();
    const thElements = recentOrdersSection!.querySelectorAll('th');
    const columnLabels = Array.from(thElements).map(th => th.textContent?.trim());
    expect(columnLabels).not.toContain('Branch');
  }, 20000);

  it('Eat-in and Parcel order type controls render icon + text and remain selectable', async () => {
    const { user } = renderCreateModePosWorkspace();

    await screen.findByDisplayValue('Main Branch');

    // Both options present with correct labels
    const eatInBtn = screen.getByRole('button', { name: /^eat-in$/i });
    const parcelBtn = screen.getByRole('button', { name: /^parcel$/i });
    expect(eatInBtn).toBeInTheDocument();
    expect(parcelBtn).toBeInTheDocument();

    // Eat-in active by default
    expect(eatInBtn).toHaveAttribute('aria-pressed', 'true');
    expect(parcelBtn).toHaveAttribute('aria-pressed', 'false');

    // Each button contains an SVG icon (icon + text layout)
    expect(eatInBtn.querySelector('svg')).not.toBeNull();
    expect(parcelBtn.querySelector('svg')).not.toBeNull();

    // Switching to Parcel updates the pressed state
    await user.click(parcelBtn);
    expect(parcelBtn).toHaveAttribute('aria-pressed', 'true');
    expect(eatInBtn).toHaveAttribute('aria-pressed', 'false');
  }, 20000);

  it('page subtitle describes confirm sending to kitchen, not "without side effects"', async () => {
    renderCreateModePosWorkspace();

    await screen.findByDisplayValue('Main Branch');
    expect(screen.getByText(/without billing, kitchen, or inventory side effects/i)).toBeInTheDocument();
    expect(screen.queryByText(/confirm and send to the kitchen/i)).not.toBeInTheDocument();
  }, 20000);

  it('menu search filters items by name and shows a no-match message', async () => {
    const { user } = renderCreateModePosWorkspace();

    await screen.findByDisplayValue('Main Branch');

    const searchInput = screen.getByLabelText(/search items/i);
    await user.type(searchInput, 'dosa');

    const menuBrowser = getCardScope(/menu browser/i);
    expect(menuBrowser.getByText(/masala dosa/i)).toBeInTheDocument();
    expect(menuBrowser.queryByText(/eatin special/i)).not.toBeInTheDocument();

    await user.clear(searchInput);
    await user.type(searchInput, 'zzznotexist');
    expect(menuBrowser.getByText(/no items match/i)).toBeInTheDocument();
  }, 20000);

  it('confirm order shows inline summary step before firing the API', async () => {
    const fetchMock = buildPosWorkspaceResponseMocks()
      .mockResolvedValueOnce(
        createJsonResponse({
          posOrderId: 'order-1',
          restaurantId: 'restaurant-1',
          branchId: 'branch-1',
          orderNumber: 'ORD-20260612-0001',
          orderType: 'EatIn',
          status: 'Draft',
          tableName: 'T1',
          customerName: null,
          customerMobile: null,
          notes: null,
          subtotal: 2.5,
          taxTotal: 0,
          grandTotal: 2.5,
          confirmedAt: null,
          cancelledAt: null,
          cancelReason: null,
          createdByUserId: null,
          confirmedByUserId: null,
          cancelledByUserId: null,
          createdAt: '2026-06-12T10:00:00Z',
          updatedAt: null,
          lines: [
            {
              posOrderLineId: 'line-1',
              menuItemId: 'item-1',
              menuCategoryId: 'category-1',
              menuItemNameSnapshot: 'Masala Dosa',
              menuCategoryNameSnapshot: 'Breakfast',
              skuSnapshot: 'DOSA-01',
              unitPrice: 2.5,
              taxRate: 0,
              quantity: 1,
              lineSubtotal: 2.5,
              lineTax: 0,
              lineTotal: 2.5,
              notes: null,
              displayOrder: 1,
              createdAt: '2026-06-12T10:00:00Z',
              updatedAt: null,
            },
          ],
        })
      );
    vi.stubGlobal('fetch', fetchMock);
    const user = userEvent.setup();
    storeAuthSession({ permissions: ['Order.Create', 'Order.View', 'Order.Cancel'], roles: ['Cashier'], activeRole: 'Cashier' });

    renderWithRouter(<App />, '/pos/orders');

    await user.click(await screen.findByRole('button', { name: 'ORD-20260612-0001' }));

    // First click: shows the inline summary — no API call yet
    await user.click(await screen.findByRole('button', { name: /confirm order/i }));
    expect(screen.getByRole('button', { name: /yes, confirm order/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /no, go back/i })).toBeInTheDocument();

    // "No, go back" cancels without firing API
    const callsBefore = fetchMock.mock.calls.length;
    await user.click(screen.getByRole('button', { name: /no, go back/i }));
    expect(fetchMock.mock.calls.length).toBe(callsBefore);

    // "Confirm order" button is restored
    expect(screen.getByRole('button', { name: /confirm order/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /yes, confirm order/i })).not.toBeInTheDocument();
  }, 20000);

  it('clear draft on a non-empty cart shows inline confirmation before clearing', async () => {
    const { user } = renderCreateModePosWorkspace();

    await screen.findByDisplayValue('Main Branch');
    await user.click(screen.getByRole('button', { name: /masala dosa/i }));

    // Cart has one line — "Clear draft" should show confirmation step
    await user.click(screen.getByRole('button', { name: /clear draft/i }));
    expect(screen.getByRole('button', { name: /yes, clear/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /no, keep/i })).toBeInTheDocument();

    // "No, keep" cancels without clearing
    await user.click(screen.getByRole('button', { name: /no, keep/i }));
    expect(screen.getByRole('button', { name: /clear draft/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /yes, clear/i })).not.toBeInTheDocument();

    // "Yes, clear" clears the cart (both desktop card heading and compact mobile message match)
    await user.click(screen.getByRole('button', { name: /clear draft/i }));
    await user.click(screen.getByRole('button', { name: /yes, clear/i }));
    expect(screen.getAllByText(/cart is empty/i).length).toBeGreaterThan(0);
  }, 20000);

  it('clear draft on an empty cart clears immediately without confirmation', async () => {
    const { user } = renderCreateModePosWorkspace();

    await screen.findByDisplayValue('Main Branch');
    // Cart starts empty — clicking "Clear draft" should clear immediately
    await user.click(screen.getByRole('button', { name: /clear draft/i }));
    expect(screen.queryByRole('button', { name: /yes, clear/i })).not.toBeInTheDocument();
    expect(screen.getByText(/draft cleared/i)).toBeInTheDocument();
  }, 20000);

  it('empty cart renders a compact mobile message and a full desktop card simultaneously', async () => {
    renderCreateModePosWorkspace();

    await screen.findByDisplayValue('Main Branch');

    // jsdom has no viewport so both elements are in the DOM; CSS controls which is visible.
    // Compact mobile message (aria-label on <p>)
    expect(screen.getByLabelText(/cart is empty/i)).toBeInTheDocument();

    // Full EmptyState heading for desktop
    expect(screen.getByRole('heading', { name: /cart is empty/i })).toBeInTheDocument();
  }, 20000);

  it('eat-in: table field is visible in the context bar without expanding the details section', async () => {
    const { user } = renderCreateModePosWorkspace();

    await screen.findByDisplayValue('Main Branch');

    // EatIn is the default order type — the Table input must be in the context bar (always visible)
    const tableInput = screen.getByLabelText(/^table name$/i);
    expect(tableInput).toBeInTheDocument();

    // Typing in the context-bar table input updates the field
    await user.type(tableInput, 'T5');
    expect(tableInput).toHaveValue('T5');
  }, 20000);

  it('eat-in: no-table warning appears when table is blank', async () => {
    renderCreateModePosWorkspace();

    await screen.findByDisplayValue('Main Branch');

    // By default the table field is empty → warning is shown
    expect(screen.getByText(/no table set/i)).toBeInTheDocument();
  }, 20000);

  it('eat-in: no-table warning disappears once a table name is entered', async () => {
    const { user } = renderCreateModePosWorkspace();

    await screen.findByDisplayValue('Main Branch');
    expect(screen.getByText(/no table set/i)).toBeInTheDocument();

    const tableInput = screen.getByLabelText(/^table name$/i);
    await user.type(tableInput, 'T3');
    expect(screen.queryByText(/no table set/i)).not.toBeInTheDocument();
  }, 20000);

  it('parcel: table field and no-table warning are not visible in the context bar', async () => {
    const { user } = renderCreateModePosWorkspace();

    await screen.findByDisplayValue('Main Branch');
    await user.click(screen.getByRole('button', { name: /^parcel$/i }));

    // Context-bar table input and its warning must not appear for Parcel orders
    const contextBar = document.querySelector('.pos-context-bar');
    expect(contextBar).not.toBeNull();
    expect(within(contextBar as HTMLElement).queryByLabelText(/^table name$/i)).not.toBeInTheDocument();
    expect(within(contextBar as HTMLElement).queryByText(/no table set/i)).not.toBeInTheDocument();
  }, 20000);

  it('eat-in: details section does not render a second Table input', async () => {
    const { user } = renderCreateModePosWorkspace();

    await screen.findByDisplayValue('Main Branch');

    // Open the details section
    const detailsSummary = screen.getByText(/customer \/ notes/i);
    await user.click(detailsSummary);

    // Only one "Table" input exists — the one in the context bar
    const tableInputs = screen.queryAllByLabelText(/^table(?: name)?$/i);
    expect(tableInputs).toHaveLength(1);

    // The details summary itself reflects the renamed label (no "Table /" prefix for EatIn)
    expect(screen.getByText(/customer \/ notes/i)).toBeInTheDocument();
    expect(screen.queryByText(/table \/ customer \/ notes/i)).not.toBeInTheDocument();
  }, 20000);

  it('confirm step shows table name when EatIn order has a table set', async () => {
    const fetchMock = buildPosWorkspaceResponseMocks()
      .mockResolvedValueOnce(
        createJsonResponse({
          posOrderId: 'order-1',
          restaurantId: 'restaurant-1',
          branchId: 'branch-1',
          orderNumber: 'ORD-20260612-0001',
          orderType: 'EatIn',
          status: 'Draft',
          tableName: 'T1',
          customerName: null,
          customerMobile: null,
          notes: null,
          subtotal: 2.5,
          taxTotal: 0,
          grandTotal: 2.5,
          confirmedAt: null,
          cancelledAt: null,
          cancelReason: null,
          createdByUserId: null,
          confirmedByUserId: null,
          cancelledByUserId: null,
          createdAt: '2026-06-12T10:00:00Z',
          updatedAt: null,
          lines: [
            {
              posOrderLineId: 'line-1',
              menuItemId: 'item-1',
              menuCategoryId: 'category-1',
              menuItemNameSnapshot: 'Masala Dosa',
              menuCategoryNameSnapshot: 'Breakfast',
              skuSnapshot: 'DOSA-01',
              unitPrice: 2.5,
              taxRate: 0,
              quantity: 1,
              lineSubtotal: 2.5,
              lineTax: 0,
              lineTotal: 2.5,
              notes: null,
              displayOrder: 1,
              createdAt: '2026-06-12T10:00:00Z',
              updatedAt: null,
            },
          ],
        })
      );
    vi.stubGlobal('fetch', fetchMock);
    const user = userEvent.setup();
    storeAuthSession({ permissions: ['Order.Create', 'Order.View', 'Order.Cancel'], roles: ['Cashier'], activeRole: 'Cashier' });

    renderWithRouter(<App />, '/pos/orders');
    await user.click(await screen.findByRole('button', { name: 'ORD-20260612-0001' }));
    await user.click(await screen.findByRole('button', { name: /confirm order/i }));

    // Confirm step is shown — table name must appear
    const confirmRegion = screen.getByRole('region', { name: /confirm order/i });
    expect(within(confirmRegion).getByText(/T1/)).toBeInTheDocument();
    // "No table set" warning must NOT appear
    expect(within(confirmRegion).queryByText(/no table set/i)).not.toBeInTheDocument();
  }, 20000);

  it('confirm step shows "No table set" warning when EatIn order has no table', async () => {
    const fetchMock = buildPosWorkspaceResponseMocks()
      .mockResolvedValueOnce(
        createJsonResponse({
          posOrderId: 'order-1',
          restaurantId: 'restaurant-1',
          branchId: 'branch-1',
          orderNumber: 'ORD-20260612-0001',
          orderType: 'EatIn',
          status: 'Draft',
          tableName: null,
          customerName: null,
          customerMobile: null,
          notes: null,
          subtotal: 2.5,
          taxTotal: 0,
          grandTotal: 2.5,
          confirmedAt: null,
          cancelledAt: null,
          cancelReason: null,
          createdByUserId: null,
          confirmedByUserId: null,
          cancelledByUserId: null,
          createdAt: '2026-06-12T10:00:00Z',
          updatedAt: null,
          lines: [
            {
              posOrderLineId: 'line-1',
              menuItemId: 'item-1',
              menuCategoryId: 'category-1',
              menuItemNameSnapshot: 'Masala Dosa',
              menuCategoryNameSnapshot: 'Breakfast',
              skuSnapshot: 'DOSA-01',
              unitPrice: 2.5,
              taxRate: 0,
              quantity: 1,
              lineSubtotal: 2.5,
              lineTax: 0,
              lineTotal: 2.5,
              notes: null,
              displayOrder: 1,
              createdAt: '2026-06-12T10:00:00Z',
              updatedAt: null,
            },
          ],
        })
      );
    vi.stubGlobal('fetch', fetchMock);
    const user = userEvent.setup();
    storeAuthSession({ permissions: ['Order.Create', 'Order.View', 'Order.Cancel'], roles: ['Cashier'], activeRole: 'Cashier' });

    renderWithRouter(<App />, '/pos/orders');
    await user.click(await screen.findByRole('button', { name: 'ORD-20260612-0001' }));
    await user.click(await screen.findByRole('button', { name: /confirm order/i }));

    const confirmRegion = screen.getByRole('region', { name: /confirm order/i });
    expect(within(confirmRegion).getByText(/no table set/i)).toBeInTheDocument();
  }, 20000);

  it('confirmed order cancellation shows a kitchen-already-notified warning', async () => {
    const fetchMock = buildPosWorkspaceResponseMocks()
      .mockResolvedValueOnce(
        createJsonResponse({
          posOrderId: 'order-1',
          restaurantId: 'restaurant-1',
          branchId: 'branch-1',
          orderNumber: 'ORD-20260612-0001',
          orderType: 'EatIn',
          status: 'Confirmed',
          tableName: 'T1',
          customerName: null,
          customerMobile: null,
          notes: null,
          subtotal: 2.5,
          taxTotal: 0,
          grandTotal: 2.5,
          confirmedAt: '2026-06-12T10:10:00Z',
          cancelledAt: null,
          cancelReason: null,
          createdByUserId: null,
          confirmedByUserId: 'user-1',
          cancelledByUserId: null,
          createdAt: '2026-06-12T10:00:00Z',
          updatedAt: '2026-06-12T10:10:00Z',
          kitchenTicketId: 'ticket-1',
          kitchenTicketNumber: 'KIT-20260612-0001',
          kitchenTicketStatus: 'Pending',
          lines: [],
        })
      );
    vi.stubGlobal('fetch', fetchMock);
    const user = userEvent.setup();
    storeAuthSession({ permissions: ['Order.Create', 'Order.View', 'Order.Cancel'], roles: ['Cashier'], activeRole: 'Cashier' });

    renderWithRouter(<App />, '/pos/orders');
    await user.click(await screen.findByRole('button', { name: 'ORD-20260612-0001' }));

    // Cancel panel appears for Confirmed orders; warning must be present
    expect(await screen.findByText(/already have been sent to kitchen/i)).toBeInTheDocument();
    expect(screen.getByText(/cancel only after informing kitchen/i)).toBeInTheDocument();
  }, 20000);

  it('does not use window.confirm anywhere in the POS order flow', async () => {
    const windowConfirmSpy = vi.spyOn(window, 'confirm');
    const { user } = renderCreateModePosWorkspace();

    await screen.findByDisplayValue('Main Branch');

    // Trigger all interactive flows that previously used window.confirm
    await user.click(screen.getByRole('button', { name: /masala dosa/i }));
    await user.click(screen.getByRole('button', { name: /clear draft/i }));
    await user.click(screen.getByRole('button', { name: /yes, clear/i }));

    expect(windowConfirmSpy).not.toHaveBeenCalled();
    windowConfirmSpy.mockRestore();
  }, 20000);

  it('shows "Confirm and send to kitchen" button in draft actions after a draft is saved', async () => {
    storeAuthSession({ permissions: ['Order.Create', 'Order.View', 'Order.Cancel'], roles: ['Cashier'], activeRole: 'Cashier' });
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(createJsonResponse({ items: [{ branchId: 'branch-1', restaurantId: 'restaurant-1', name: 'Main Branch', address: null, phone: null, timezone: 'Asia/Singapore', currency: 'SGD', status: 'Active', createdAt: '2026-06-11T09:00:00Z', updatedAt: null }] }))
      .mockResolvedValueOnce(createJsonResponse({ items: [{ menuCategoryId: 'category-1', restaurantId: 'restaurant-1', name: 'Breakfast', displayOrder: 1, status: 'Active', createdAt: '2026-06-11T09:00:00Z', updatedAt: null }] }))
      .mockResolvedValueOnce(createJsonResponse({ items: [{ menuItemId: 'item-1', restaurantId: 'restaurant-1', menuCategoryId: 'category-1', categoryName: 'Breakfast', name: 'Masala Dosa', description: null, sku: 'DOSA-01', basePrice: 2.5, taxRate: 0, isVegetarian: true, isAvailableForEatIn: true, isAvailableForParcel: true, status: 'Active', createdAt: '2026-06-11T09:00:00Z', updatedAt: null }] }))
      .mockResolvedValueOnce(createJsonResponse({ items: [] }))
      .mockResolvedValueOnce(createJsonResponse({ posOrderId: 'order-1', restaurantId: 'restaurant-1', branchId: 'branch-1', orderNumber: 'ORD-20260612-0001', orderType: 'EatIn', status: 'Draft', tableName: null, customerName: null, customerMobile: null, notes: null, subtotal: 2.5, taxTotal: 0, grandTotal: 2.5, confirmedAt: null, cancelledAt: null, cancelReason: null, createdByUserId: null, confirmedByUserId: null, cancelledByUserId: null, createdAt: '2026-06-12T10:00:00Z', updatedAt: null, lines: [{ posOrderLineId: 'line-1', menuItemId: 'item-1', menuCategoryId: 'category-1', menuItemNameSnapshot: 'Masala Dosa', menuCategoryNameSnapshot: 'Breakfast', skuSnapshot: 'DOSA-01', unitPrice: 2.5, taxRate: 0, quantity: 1, lineSubtotal: 2.5, lineTax: 0, lineTotal: 2.5, notes: null, displayOrder: 1, createdAt: '2026-06-12T10:00:00Z', updatedAt: null }] }))
      .mockResolvedValueOnce(createJsonResponse({ items: [{ posOrderId: 'order-1', branchId: 'branch-1', orderNumber: 'ORD-20260612-0001', orderType: 'EatIn', status: 'Draft', tableName: null, customerName: null, grandTotal: 2.5, lineCount: 1, createdAt: '2026-06-12T10:00:00Z' }] }));
    vi.stubGlobal('fetch', fetchMock);
    const user = userEvent.setup();

    renderWithRouter(<App />, '/pos/orders');

    // "Confirm and send to kitchen" should not appear before a draft is saved
    await screen.findByDisplayValue('Main Branch');
    expect(screen.queryByRole('button', { name: /confirm and send to kitchen/i })).not.toBeInTheDocument();

    // Add an item and save the draft
    await user.click(screen.getByRole('button', { name: /masala dosa/i }));
    await user.click(screen.getByRole('button', { name: /create draft/i }));

    // After save, "Confirm and send to kitchen" appears in the draft actions
    expect(await screen.findByRole('button', { name: /confirm and send to kitchen/i })).toBeInTheDocument();
  }, 20000);
});
