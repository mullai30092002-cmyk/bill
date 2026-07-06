import { render, screen, within } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';

import { AuthProvider } from '../auth/AuthProvider';
import { storeAuthSession, clearAuthSession } from '../../test/authTestUtils';
import { LANGUAGE_STORAGE_KEY } from '../../i18n/translations';
import { LanguageProvider } from '../../i18n/LanguageProvider';
import PosMenuBrowser from './PosMenuBrowser';
import PosOrderListPanel from './PosOrderListPanel';
import PosOrderTypeToggle from './PosOrderTypeToggle';
import type { MenuCategory, MenuItem } from '../admin/adminTypes';
import type { PosOrderListItem } from './posTypes';

const renderPosChrome = () =>
  render(
    <AuthProvider>
      <LanguageProvider>
        <div>
          <PosOrderTypeToggle value="EatIn" onChange={vi.fn()} />
          <PosMenuBrowser
            categories={[
              {
                menuCategoryId: 'category-1',
                restaurantId: 'restaurant-1',
                name: 'Breakfast',
                displayOrder: 1,
                status: 'Active',
                createdAt: '2026-06-11T09:00:00Z',
                updatedAt: '2026-06-11T09:30:00Z',
              } satisfies MenuCategory,
            ]}
            items={[
              {
                menuItemId: 'item-1',
                restaurantId: 'restaurant-1',
                menuCategoryId: 'category-1',
                categoryName: 'Breakfast',
                name: 'Masala Dosa',
                description: 'Crisp rice crepe',
                sku: 'DOSA-01',
                basePrice: 125,
                taxRate: 5,
                isVegetarian: true,
                isAvailableForEatIn: true,
                isAvailableForParcel: true,
                inventoryDeductionMode: 'RecipeOnServe',
                stockInventoryItemId: null,
                stockInventoryItemName: null,
                status: 'Active',
                createdAt: '2026-06-11T09:00:00Z',
                updatedAt: '2026-06-11T09:30:00Z',
              } satisfies MenuItem,
            ]}
            selectedCategoryId=""
            selectedOrderType="EatIn"
            canCreate={true}
            onCategorySelect={vi.fn()}
            onAddItem={vi.fn()}
          />
          <PosOrderListPanel
            orders={[
              {
                posOrderId: 'order-1',
                branchId: 'branch-1',
                orderNumber: 'ORD-20260612-0001',
                orderType: 'EatIn',
                status: 'Draft',
                tableName: 'T1',
                customerName: null,
                grandTotal: 125,
                lineCount: 1,
                createdAt: '2026-06-12T10:00:00Z',
              } satisfies PosOrderListItem,
            ]}
            loading={false}
            selectedOrderId={null}
            onRetry={vi.fn()}
            onSelectOrder={vi.fn()}
          />
        </div>
      </LanguageProvider>
    </AuthProvider>
  );

describe('POS language chrome', () => {
  beforeEach(() => {
    clearAuthSession();
    localStorage.setItem(LANGUAGE_STORAGE_KEY, 'ta');
    storeAuthSession({
      permissions: ['Order.Create', 'Order.View'],
      roles: ['Cashier'],
      activeRole: 'Cashier',
    });
  });

  afterEach(() => {
    clearAuthSession();
    localStorage.removeItem(LANGUAGE_STORAGE_KEY);
  });

  it('renders Tamil labels for POS order type, menu browser, and recent orders chrome', () => {
    renderPosChrome();

    expect(screen.getByRole('group', { name: 'ஆர்டர் வகை' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'உட்கார்ந்து உணவு' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'பார்சல்' })).toBeInTheDocument();

    expect(screen.getByRole('heading', { name: 'மெனு உலாவி' })).toBeInTheDocument();
    expect(screen.getByLabelText('பொருட்களைத் தேடு')).toBeInTheDocument();

    expect(screen.getAllByLabelText('சமீபத்திய ஆர்டர்கள்').length).toBeGreaterThan(0);

    const table = screen.getByRole('table');
    expect(within(table).getByText('ஆர்டர்')).toBeInTheDocument();
    expect(within(table).getByText('வகை')).toBeInTheDocument();
    expect(within(table).getByText('நிலை')).toBeInTheDocument();
    expect(within(table).getByText('வரைவு')).toBeInTheDocument();
  });
});
