import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';

import App from '../../App';
import { clearAuthSession, createJsonResponse, storeAuthSession } from '../../test/authTestUtils';
import { LANGUAGE_STORAGE_KEY } from '../../i18n/translations';
import { renderWithRouter } from '../../test/renderWithRouter';

const adminUsersPath = '/api/v1/admin/users';
const adminRolesPath = '/api/v1/admin/roles';
const adminBranchesPath = '/api/v1/admin/branches';

const makeUsersResponse = () =>
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
  });

const makeRolesResponse = () =>
  createJsonResponse({
    items: [
      {
        roleId: 'role-cashier',
        restaurantId: null,
        name: 'Cashier',
        description: 'Front counter user',
        isSystemRole: false,
        isAssignable: true,
        assignmentBlockedReason: null,
        permissionCodes: ['Order.View'],
      },
    ],
  });

const makeBranchesResponse = () =>
  createJsonResponse({
    items: [
      {
        branchId: 'branch-1',
        restaurantId: 'restaurant-1',
        name: 'Main Outlet',
        address: '123 Market Street',
        phone: '60000001',
        timezone: 'Asia/Singapore',
        currency: 'SGD',
        status: 'Active',
      },
    ],
  });

const setupFetch = (responses: Record<string, Response[]>) => {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const method = (init?.method ?? 'GET').toUpperCase();
    const path = new URL(String(input)).pathname;
    const key = `${method} ${path}`;
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
  localStorage.removeItem(LANGUAGE_STORAGE_KEY);
  vi.unstubAllGlobals();
});

describe('AdminUsers Tamil chrome', () => {
  it('renders Tamil UI chrome for the admin users page', async () => {
    localStorage.setItem(LANGUAGE_STORAGE_KEY, 'ta');
    storeAuthSession({
      userId: 'session-user',
      permissions: ['User.Manage', 'Role.Manage'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });

    setupFetch({
      [`GET ${adminBranchesPath}`]: [makeBranchesResponse(), makeBranchesResponse()],
      [`GET ${adminUsersPath}`]: [makeUsersResponse(), makeUsersResponse()],
      [`GET ${adminRolesPath}`]: [makeRolesResponse(), makeRolesResponse()],
    });

    renderWithRouter(<App />, '/admin/users');

    expect(await screen.findByRole('heading', { name: /பயனர்கள் மற்றும் பாத்திரங்கள்/i })).toBeInTheDocument();
    expect((await screen.findAllByText('Asha Kumar')).length).toBeGreaterThan(0);
    expect(screen.getAllByRole('button', { name: /திருத்து/i }).length).toBeGreaterThan(0);
  });
});
