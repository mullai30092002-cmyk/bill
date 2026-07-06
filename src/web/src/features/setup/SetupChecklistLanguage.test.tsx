import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';

import App from '../../App';
import { clearAuthSession, createJsonResponse, storeAuthSession } from '../../test/authTestUtils';
import { LANGUAGE_STORAGE_KEY } from '../../i18n/translations';
import { renderWithRouter } from '../../test/renderWithRouter';

const buildChecklistResponse = () =>
  createJsonResponse({
    restaurantId: 'restaurant-1',
    restaurantName: 'Demo Restaurant',
    branchId: 'branch-1',
    branchName: 'Main Branch',
    businessType: 'Restaurant',
    completionPercent: 80,
    completedCount: 8,
    totalCount: 10,
    items: [
      { key: 'restaurantProfile', title: 'Restaurant profile ready', description: 'Confirm the restaurant name and code before pilot usage.', status: 'Complete', priority: 'Required', actionLabel: 'View setup', actionHref: '/owner/dashboard', count: 1, warningCount: null },
      { key: 'branchCreated', title: 'Branch created', description: 'At least one active branch is available for pilot usage.', status: 'Complete', priority: 'Required', actionLabel: 'Add branch', actionHref: '/admin/branches', count: 1, warningCount: null },
      { key: 'staffUsersAdded', title: 'Staff users added', description: 'Active users are ready for restaurant operations.', status: 'Complete', priority: 'Recommended', actionLabel: 'Add users', actionHref: '/admin/users', count: 2, warningCount: null },
      { key: 'menuCategoriesAdded', title: 'Menu categories added', description: 'Add at least one active menu category before loading items.', status: 'Complete', priority: 'Required', actionLabel: 'Add menu', actionHref: '/admin/menu', count: 4, warningCount: null },
      { key: 'menuItemsAdded', title: 'Menu items added', description: 'Create active menu items so POS orders and billing can use the catalog.', status: 'Complete', priority: 'Required', actionLabel: 'Add menu', actionHref: '/admin/menu', count: 14, warningCount: null },
      { key: 'inventoryItemsAdded', title: 'Inventory items added', description: 'Add at least one active inventory item for the selected branch.', status: 'Complete', priority: 'Recommended', actionLabel: 'Add inventory', actionHref: '/inventory', count: 9, warningCount: null },
      { key: 'recipesOrStockMappingsConfigured', title: 'Recipes or stock mappings configured', description: 'Some menu items still need recipe or stock mappings before pilot usage.', status: 'Complete', priority: 'Recommended', actionLabel: 'Add menu', actionHref: '/admin/menu', count: 8, warningCount: null },
      { key: 'vendorsAdded', title: 'Vendors added', description: 'Add at least one active vendor before recording purchases or settlements.', status: 'Complete', priority: 'Recommended', actionLabel: 'Add vendors', actionHref: '/vendors', count: 3, warningCount: null },
      { key: 'firstPosOrderCompleted', title: 'First test POS order completed', description: 'A confirmed POS order exists for the selected branch.', status: 'Complete', priority: 'Required', actionLabel: 'Create test order', actionHref: '/pos/orders', count: 1, warningCount: null },
      { key: 'firstBillPaymentCompleted', title: 'First bill/payment completed', description: 'A paid bill or recorded payment exists for the selected branch.', status: 'Complete', priority: 'Required', actionLabel: 'Complete first bill', actionHref: '/billing', count: 1, warningCount: null },
    ],
  });

afterEach(() => {
  clearAuthSession();
  localStorage.removeItem(LANGUAGE_STORAGE_KEY);
  vi.unstubAllGlobals();
});

describe('Setup checklist Tamil chrome', () => {
  it('renders Tamil setup checklist chrome', async () => {
    localStorage.setItem(LANGUAGE_STORAGE_KEY, 'ta');
    storeAuthSession({
      permissions: ['Report.View'],
      roles: ['RestaurantOwner'],
      activeRole: 'RestaurantOwner',
      branchId: 'branch-1',
    });
    vi.stubGlobal('fetch', vi.fn(async () => buildChecklistResponse()));

    renderWithRouter(<App />, '/setup');

    expect(await screen.findByRole('heading', { name: /அமைப்பு சரிபார்ப்பு பட்டியல்/i })).toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: /வணிக வகை/i })).not.toBeInTheDocument();
    expect(screen.getByText(/சுயவிவரம்: உணவகம்/i)).toBeInTheDocument();
    expect(screen.getByText(/^பார்வை மட்டும்$/i)).toBeInTheDocument();
    expect(screen.getByText(/இந்த கிளையை பைலட் பயன்பாட்டுக்கு தயார் செய்யவும்/i)).toBeInTheDocument();
    expect(screen.getByText(/முன்னேற்றம்/i)).toBeInTheDocument();
    expect(screen.getByText(/முடிந்த படிகள்/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /கிளையைச் சேர்/i })).toHaveAttribute('href', '/admin/branches');
    expect(screen.getByRole('link', { name: /பயனர்களைச் சேர்/i })).toHaveAttribute('href', '/admin/users');
    expect(screen.getAllByRole('link', { name: /மெனுவைச் சேர்/i })).toHaveLength(3);
    screen.getAllByRole('link', { name: /மெனுவைச் சேர்/i }).forEach(link => {
      expect(link).toHaveAttribute('href', '/admin/menu');
    });
    expect(screen.getByRole('link', { name: /கையிருப்பைச் சேர்/i })).toHaveAttribute('href', '/inventory');
    expect(screen.getByRole('link', { name: /விற்பனையாளர்களைச் சேர்/i })).toHaveAttribute('href', '/vendors');
    expect(screen.getByRole('link', { name: /சோதனை ஆர்டரை உருவாக்கு/i })).toHaveAttribute('href', '/pos/orders');
    expect(screen.getByRole('link', { name: /முதல் பில்லை முடி/i })).toHaveAttribute('href', '/billing');
  });
});
