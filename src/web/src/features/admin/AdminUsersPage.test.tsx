import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import App from '../../App';
import { clearAuthSession, createJsonResponse, storeAuthSession } from '../../test/authTestUtils';
import { renderWithRouter } from '../../test/renderWithRouter';
import type { AuthSession } from '../auth/authTypes';

const adminUsersPath = '/api/v1/admin/users';
const adminRolesPath = '/api/v1/admin/roles';
const adminBranchesPath = '/api/v1/admin/branches';
const userPath = (userId: string) => `/api/v1/admin/users/${userId}`;
const rolesPath = (userId: string) => `/api/v1/admin/users/${userId}/roles`;
const activatePath = (userId: string) => `/api/v1/admin/users/${userId}/activate`;
const deactivatePath = (userId: string) => `/api/v1/admin/users/${userId}/deactivate`;
const resetPasswordPath = (userId: string) => `/api/v1/admin/users/${userId}/reset-password`;

const usersResponse = (items: Array<Record<string, unknown>>) =>
  createJsonResponse({
    items,
    totalCount: items.length,
    page: 1,
    pageSize: 20,
  });

const rolesResponse = (items: Array<Record<string, unknown>>) =>
  createJsonResponse({
    items,
  });

const branchesResponse = (items: Array<Record<string, unknown>>) =>
  createJsonResponse({
    items,
  });

const defaultBranchesResponse = () =>
  branchesResponse([
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
  ]);

const detailResponse = (item: Record<string, unknown>) =>
  createJsonResponse({
    ...item,
    createdAt: '2026-06-11T09:00:00Z',
    updatedAt: '2026-06-11T09:30:00Z',
  });

const setupAdminFetch = (responses: Record<string, Response[]>) => {
  const fetchMock = vi.fn(async (input, init) => {
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

const renderAdminUsersPage = (
  responses: Record<string, Response[]>,
  authOverrides: Partial<AuthSession> = {}
) => {
  storeAuthSession({
    userId: 'session-user',
    permissions: ['User.Manage', 'Role.Manage'],
    roles: ['Admin'],
    activeRole: 'Admin',
    ...authOverrides,
  });
  const fetchMock = setupAdminFetch({
    [`GET ${adminBranchesPath}`]: [defaultBranchesResponse()],
    ...responses,
  });
  const user = userEvent.setup();

  renderWithRouter(<App />, '/admin/users');

  return { fetchMock, user };
};

const getJsonBody = (call: [RequestInfo | URL, RequestInit?]) =>
  call[1]?.body ? JSON.parse(String(call[1].body)) : undefined;

const getCreateSubmitButton = () => {
  const createButtons = screen.getAllByRole('button', { name: /^create user$/i });
  return createButtons.find(button => button.closest('form')) ?? createButtons[0];
};

const clickEditButton = async (user: ReturnType<typeof userEvent.setup>) => {
  await user.click(screen.getAllByRole('button', { name: /^edit$/i })[0]);
};

describe('AdminUsersPage', () => {
  it('loads users and roles', async () => {
    const { fetchMock } = renderAdminUsersPage({
      [`GET ${adminUsersPath}`]: [
        usersResponse([
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
        ]),
      ],
    [`GET ${adminRolesPath}`]: [
      rolesResponse([
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
          {
            roleId: 'role-waiter',
            restaurantId: null,
            name: 'Waiter',
            description: 'Wait service user',
            isSystemRole: false,
            isAssignable: true,
            assignmentBlockedReason: null,
            permissionCodes: ['Order.View'],
          },
      ]),
    ],
    [`GET ${adminBranchesPath}`]: [
      branchesResponse([
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
        {
          branchId: 'branch-2',
          restaurantId: 'restaurant-1',
          name: 'Side Outlet',
          address: '456 Side Street',
          phone: '60000002',
          timezone: 'Asia/Singapore',
          currency: 'SGD',
          status: 'Inactive',
        },
      ]),
    ],
  });

    expect(await screen.findByRole('heading', { name: /users and roles/i })).toBeInTheDocument();
    await waitFor(() => {
      expect(screen.getAllByText('Asha Kumar').length).toBeGreaterThan(0);
    });
    expect(screen.getByRole('checkbox', { name: /cashier/i })).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: /waiter/i })).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledTimes(3);
  });

  it('loads branches from the backend and shows them in the create branch selector', async () => {
    renderAdminUsersPage({
      [`GET ${adminUsersPath}`]: [usersResponse([])],
      [`GET ${adminRolesPath}`]: [
        rolesResponse([
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
        ]),
      ],
      [`GET ${adminBranchesPath}`]: [
        branchesResponse([
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
          {
            branchId: 'branch-2',
            restaurantId: 'restaurant-1',
            name: 'Side Outlet',
            address: '456 Side Street',
            phone: '60000002',
            timezone: 'Asia/Singapore',
            currency: 'SGD',
            status: 'Inactive',
          },
        ]),
      ],
    });

    expect(await screen.findByRole('heading', { name: /users and roles/i })).toBeInTheDocument();
    const branchSelect = screen.getByLabelText(/branch/i);
    expect(branchSelect).toBeInTheDocument();
    expect(screen.getByRole('option', { name: /no branch assignment/i })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: /main outlet/i })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: /side outlet/i })).not.toBeInTheDocument();
  });

  it('sends the selected branch id when creating a user', async () => {
    const { user, fetchMock } = renderAdminUsersPage({
      [`GET ${adminUsersPath}`]: [
        usersResponse([]),
        usersResponse([
          {
            userId: 'user-2',
            restaurantId: 'restaurant-1',
            branchId: 'branch-1',
            fullName: 'New User',
            mobileNumber: '90001234',
            email: 'new@example.com',
            status: 'Active',
            roleNames: ['Cashier'],
          },
        ]),
      ],
      [`GET ${adminRolesPath}`]: [
        rolesResponse([
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
        ]),
      ],
      [`GET ${adminBranchesPath}`]: [
        branchesResponse([
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
        ]),
      ],
      [`POST ${adminUsersPath}`]: [
        detailResponse({
          userId: 'user-2',
          restaurantId: 'restaurant-1',
          branchId: 'branch-1',
          fullName: 'New User',
          mobileNumber: '90001234',
          email: 'new@example.com',
          status: 'Active',
          roleNames: ['Cashier'],
        }),
      ],
    });

    await screen.findByRole('heading', { name: /users and roles/i });
    await user.type(screen.getByLabelText(/full name/i), 'New User');
    await user.type(screen.getByLabelText(/mobile number/i), '90001234');
    await user.type(screen.getByLabelText(/initial password/i), 'password1');
    await user.click(screen.getByRole('checkbox', { name: /cashier/i }));
    await user.selectOptions(screen.getByLabelText(/branch/i), 'branch-1');
    await user.click(getCreateSubmitButton());

    await waitFor(() => {
      expect(screen.getByText(/created new user/i)).toBeInTheDocument();
    });

    const createCall = fetchMock.mock.calls.find(call => {
      const method = (call[1]?.method ?? 'GET').toUpperCase();
      const path = new URL(String(call[0])).pathname;
      return method === 'POST' && path === adminUsersPath;
    });

    expect(getJsonBody(createCall as [RequestInfo | URL, RequestInit?])).toEqual({
      branchId: 'branch-1',
      fullName: 'New User',
      mobileNumber: '90001234',
      email: null,
      initialPassword: 'password1',
      roleNames: ['Cashier'],
    });
  });

  it('sends branchId null when no branch assignment is selected', async () => {
    const { user, fetchMock } = renderAdminUsersPage({
      [`GET ${adminUsersPath}`]: [
        usersResponse([]),
        usersResponse([
          {
            userId: 'user-2',
            restaurantId: 'restaurant-1',
            branchId: null,
            fullName: 'New User',
            mobileNumber: '90001234',
            email: null,
            status: 'Active',
            roleNames: ['Cashier'],
          },
        ]),
      ],
      [`GET ${adminRolesPath}`]: [
        rolesResponse([
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
        ]),
      ],
      [`GET ${adminBranchesPath}`]: [
        branchesResponse([
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
        ]),
      ],
      [`POST ${adminUsersPath}`]: [
        detailResponse({
          userId: 'user-2',
          restaurantId: 'restaurant-1',
          branchId: null,
          fullName: 'New User',
          mobileNumber: '90001234',
          email: null,
          status: 'Active',
          roleNames: ['Cashier'],
        }),
      ],
    });

    await screen.findByRole('heading', { name: /users and roles/i });
    await user.type(screen.getByLabelText(/full name/i), 'New User');
    await user.type(screen.getByLabelText(/mobile number/i), '90001234');
    await user.type(screen.getByLabelText(/initial password/i), 'password1');
    await user.click(screen.getByRole('checkbox', { name: /cashier/i }));
    await user.selectOptions(screen.getByLabelText(/branch/i), '');
    await user.click(getCreateSubmitButton());

    await waitFor(() => {
      expect(screen.getByText(/created new user/i)).toBeInTheDocument();
    });

    const createCall = fetchMock.mock.calls.find(call => {
      const method = (call[1]?.method ?? 'GET').toUpperCase();
      const path = new URL(String(call[0])).pathname;
      return method === 'POST' && path === adminUsersPath;
    });

    expect(getJsonBody(createCall as [RequestInfo | URL, RequestInit?])).toEqual({
      branchId: null,
      fullName: 'New User',
      mobileNumber: '90001234',
      email: null,
      initialPassword: 'password1',
      roleNames: ['Cashier'],
    });
  });

  it('shows branch names in the list and selected user detail instead of raw ids', async () => {
    const { user } = renderAdminUsersPage({
      [`GET ${adminUsersPath}`]: [
        usersResponse([
          {
            userId: 'user-1',
            restaurantId: 'restaurant-1',
            branchId: 'branch-1',
            fullName: 'Asha Kumar',
            mobileNumber: '90001111',
            email: 'asha@example.com',
            status: 'Active',
            roleNames: ['Cashier'],
          },
        ]),
      ],
      [`GET ${adminRolesPath}`]: [
        rolesResponse([
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
        ]),
      ],
      [`GET ${adminBranchesPath}`]: [
        branchesResponse([
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
        ]),
      ],
      [`GET ${userPath('user-1')}`]: [
        detailResponse({
          userId: 'user-1',
          restaurantId: 'restaurant-1',
          branchId: 'branch-1',
          fullName: 'Asha Kumar',
          mobileNumber: '90001111',
          email: 'asha@example.com',
          status: 'Active',
          roleNames: ['Cashier'],
        }),
      ],
    });

    expect(await screen.findByRole('heading', { name: /users and roles/i })).toBeInTheDocument();
    expect(screen.getAllByText('Main Outlet').length).toBeGreaterThan(0);
    expect(screen.queryByText('branch-1')).not.toBeInTheDocument();

    await clickEditButton(user);
    await screen.findByDisplayValue('Asha Kumar');

    expect(screen.getAllByText('Main Outlet').length).toBeGreaterThan(1);
  });

  it('renders the create user form with required fields', async () => {
    renderAdminUsersPage({
      [`GET ${adminUsersPath}`]: [usersResponse([])],
      [`GET ${adminRolesPath}`]: [
        rolesResponse([
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
        ]),
      ],
    });

    expect(await screen.findByRole('heading', { name: /users and roles/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/full name/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/mobile number/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/email/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/initial password/i)).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: /create user/i }).length).toBeGreaterThan(0);
  });

  it('uses role names from the backend catalog', async () => {
    renderAdminUsersPage({
      [`GET ${adminUsersPath}`]: [usersResponse([])],
      [`GET ${adminRolesPath}`]: [
        rolesResponse([
          {
            roleId: 'role-dining-lead',
            restaurantId: null,
            name: 'DiningLead',
            description: 'Front of house lead',
            isSystemRole: false,
            isAssignable: true,
            assignmentBlockedReason: null,
            permissionCodes: ['Order.View'],
          },
          {
            roleId: 'role-kitchen-lead',
            restaurantId: null,
            name: 'KitchenLead',
            description: 'Kitchen lead',
            isSystemRole: false,
            isAssignable: true,
            assignmentBlockedReason: null,
            permissionCodes: ['Kitchen.View'],
          },
        ]),
      ],
    });

    expect(await screen.findByRole('heading', { name: /users and roles/i })).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: /dininglead/i })).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: /kitchenlead/i })).toBeInTheDocument();
  });

  it('sends the create request body with all required fields', async () => {
    const { user, fetchMock } = renderAdminUsersPage({
      [`GET ${adminUsersPath}`]: [
        usersResponse([]),
        usersResponse([
          {
            userId: 'user-2',
            restaurantId: 'restaurant-1',
            branchId: null,
            fullName: 'New User',
            mobileNumber: '90001234',
            email: 'new@example.com',
            status: 'Active',
            roleNames: ['Cashier'],
          },
        ]),
      ],
      [`GET ${adminRolesPath}`]: [
        rolesResponse([
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
        ]),
      ],
      [`POST ${adminUsersPath}`]: [
        detailResponse({
          userId: 'user-2',
          restaurantId: 'restaurant-1',
          branchId: null,
          fullName: 'New User',
          mobileNumber: '90001234',
          email: 'new@example.com',
          status: 'Active',
          roleNames: ['Cashier'],
        }),
      ],
    });

    await screen.findByRole('heading', { name: /users and roles/i });

    await user.type(screen.getByLabelText(/full name/i), 'New User');
    await user.type(screen.getByLabelText(/mobile number/i), '90001234');
    await user.type(screen.getByLabelText(/email/i), 'new@example.com');
    await user.type(screen.getByLabelText(/initial password/i), 'password1');
    await user.click(screen.getByRole('checkbox', { name: /cashier/i }));
    await user.click(getCreateSubmitButton());

    await waitFor(() => {
      expect(screen.getByText(/created new user/i)).toBeInTheDocument();
    });

    const createCall = fetchMock.mock.calls.find(call => {
      const method = (call[1]?.method ?? 'GET').toUpperCase();
      const path = new URL(String(call[0])).pathname;
      return method === 'POST' && path === adminUsersPath;
    });

    expect(createCall).toBeDefined();
    expect(getJsonBody(createCall as [RequestInfo | URL, RequestInit?])).toEqual({
      branchId: null,
      fullName: 'New User',
      mobileNumber: '90001234',
      email: 'new@example.com',
      initialPassword: 'password1',
      roleNames: ['Cashier'],
    });
  });

  it('populates the edit branch selector and saves a changed branch assignment', async () => {
    const { user, fetchMock } = renderAdminUsersPage({
      [`GET ${adminUsersPath}`]: [
        usersResponse([
          {
            userId: 'user-1',
            restaurantId: 'restaurant-1',
            branchId: 'branch-1',
            fullName: 'Asha Kumar',
            mobileNumber: '90001111',
            email: 'asha@example.com',
            status: 'Active',
            roleNames: ['Cashier'],
          },
        ]),
        usersResponse([
          {
            userId: 'user-1',
            restaurantId: 'restaurant-1',
            branchId: 'branch-2',
            fullName: 'Asha Kumar',
            mobileNumber: '90001111',
            email: 'asha@example.com',
            status: 'Active',
            roleNames: ['Cashier'],
          },
        ]),
      ],
      [`GET ${adminRolesPath}`]: [
        rolesResponse([
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
        ]),
      ],
      [`GET ${adminBranchesPath}`]: [
        branchesResponse([
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
          {
            branchId: 'branch-2',
            restaurantId: 'restaurant-1',
            name: 'Side Outlet',
            address: '456 Side Street',
            phone: '60000002',
            timezone: 'Asia/Singapore',
            currency: 'SGD',
            status: 'Active',
          },
        ]),
      ],
      [`GET ${userPath('user-1')}`]: [
        detailResponse({
          userId: 'user-1',
          restaurantId: 'restaurant-1',
          branchId: 'branch-1',
          fullName: 'Asha Kumar',
          mobileNumber: '90001111',
          email: 'asha@example.com',
          status: 'Active',
          roleNames: ['Cashier'],
        }),
      ],
      [`PUT ${userPath('user-1')}`]: [
        detailResponse({
          userId: 'user-1',
          restaurantId: 'restaurant-1',
          branchId: 'branch-2',
          fullName: 'Asha Kumar',
          mobileNumber: '90001111',
          email: 'asha@example.com',
          status: 'Active',
          roleNames: ['Cashier'],
        }),
      ],
    });

    await screen.findByRole('heading', { name: /users and roles/i });
    await clickEditButton(user);

    const branchSelect = await screen.findByLabelText(/branch assignment/i);
    expect(branchSelect).toHaveValue('branch-1');

    await user.selectOptions(branchSelect, 'branch-2');
    await user.click(screen.getByRole('button', { name: /save profile/i }));

    await waitFor(() => {
      expect(screen.getByText(/saved profile changes for asha kumar/i)).toBeInTheDocument();
    });

    const updateCall = fetchMock.mock.calls.find(call => {
      const method = (call[1]?.method ?? 'GET').toUpperCase();
      const path = new URL(String(call[0])).pathname;
      return method === 'PUT' && path === userPath('user-1');
    });

    expect(getJsonBody(updateCall as [RequestInfo | URL, RequestInit?])).toEqual({
      branchId: 'branch-2',
      fullName: 'Asha Kumar',
      mobileNumber: '90001111',
      email: 'asha@example.com',
      status: 'Active',
    });
  });

  it('preserves an existing branch assignment that is not in the loaded catalog', async () => {
    const { user } = renderAdminUsersPage({
      [`GET ${adminUsersPath}`]: [
        usersResponse([
          {
            userId: 'user-1',
            restaurantId: 'restaurant-1',
            branchId: 'branch-missing',
            fullName: 'Asha Kumar',
            mobileNumber: '90001111',
            email: 'asha@example.com',
            status: 'Active',
            roleNames: ['Cashier'],
          },
        ]),
      ],
      [`GET ${adminRolesPath}`]: [
        rolesResponse([
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
        ]),
      ],
      [`GET ${adminBranchesPath}`]: [
        branchesResponse([
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
        ]),
      ],
      [`GET ${userPath('user-1')}`]: [
        detailResponse({
          userId: 'user-1',
          restaurantId: 'restaurant-1',
          branchId: 'branch-missing',
          fullName: 'Asha Kumar',
          mobileNumber: '90001111',
          email: 'asha@example.com',
          status: 'Active',
          roleNames: ['Cashier'],
        }),
      ],
    });

    await screen.findByRole('heading', { name: /users and roles/i });
    await clickEditButton(user);

    const branchSelect = await screen.findByLabelText(/branch assignment/i);
    expect(branchSelect).toHaveValue('branch-missing');
    expect(screen.getByRole('option', { name: /current branch unavailable/i })).toBeInTheDocument();
  });

  it('disables branch selection when the branch catalog cannot load', async () => {
    renderAdminUsersPage({
      [`GET ${adminUsersPath}`]: [usersResponse([])],
      [`GET ${adminRolesPath}`]: [
        rolesResponse([
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
        ]),
      ],
      [`GET ${adminBranchesPath}`]: [
        createJsonResponse(
          {
            title: 'Server Error',
            detail: 'Branch catalog unavailable.',
          },
          { status: 500 }
        ),
      ],
    });

    expect(await screen.findByRole('heading', { name: /users and roles/i })).toBeInTheDocument();
    expect(screen.getByText(/branches unavailable right now/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/branch assignment/i)).toBeDisabled();
  });

  it('clears the initial password and refreshes the user list after a successful create', async () => {
    const { user } = renderAdminUsersPage({
      [`GET ${adminUsersPath}`]: [
        usersResponse([]),
        usersResponse([
          {
            userId: 'user-2',
            restaurantId: 'restaurant-1',
            branchId: null,
            fullName: 'New User',
            mobileNumber: '90001234',
            email: 'new@example.com',
            status: 'Active',
            roleNames: ['Cashier'],
          },
        ]),
      ],
      [`GET ${adminRolesPath}`]: [
        rolesResponse([
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
        ]),
      ],
      [`POST ${adminUsersPath}`]: [
        detailResponse({
          userId: 'user-2',
          restaurantId: 'restaurant-1',
          branchId: null,
          fullName: 'New User',
          mobileNumber: '90001234',
          email: 'new@example.com',
          status: 'Active',
          roleNames: ['Cashier'],
        }),
      ],
    });

    await screen.findByRole('heading', { name: /users and roles/i });

    await user.type(screen.getByLabelText(/full name/i), 'New User');
    await user.type(screen.getByLabelText(/mobile number/i), '90001234');
    await user.type(screen.getByLabelText(/initial password/i), 'password1');
    await user.click(screen.getByRole('checkbox', { name: /cashier/i }));
    await user.click(getCreateSubmitButton());

    await waitFor(() => {
      expect(screen.getByText(/created new user/i)).toBeInTheDocument();
    });

    expect(screen.getByLabelText(/initial password/i)).toHaveValue('');
    expect(screen.getAllByText('New User').length).toBeGreaterThan(0);
  });

  it('blocks create submission until required fields are filled', async () => {
    const { user, fetchMock } = renderAdminUsersPage({
      [`GET ${adminUsersPath}`]: [usersResponse([])],
      [`GET ${adminRolesPath}`]: [
        rolesResponse([
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
        ]),
      ],
    });

    await screen.findByRole('heading', { name: /users and roles/i });

    await user.click(getCreateSubmitButton());

    expect(screen.getByText(/full name is required/i)).toBeInTheDocument();
    expect(screen.getByText(/mobile number is required/i)).toBeInTheDocument();
    expect(screen.getByText(/initial password must be at least 8 characters long/i)).toBeInTheDocument();
    expect(screen.getByText(/select at least one role/i)).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledTimes(3);
  });

  it('shows the longer password helper for privileged roles', async () => {
    const { user } = renderAdminUsersPage({
      [`GET ${adminUsersPath}`]: [usersResponse([])],
      [`GET ${adminRolesPath}`]: [
        rolesResponse([
          {
            roleId: 'role-admin',
            restaurantId: null,
            name: 'Admin',
            description: 'Administrative access',
            isSystemRole: false,
            isAssignable: true,
            assignmentBlockedReason: null,
            permissionCodes: ['User.Manage'],
          },
        ]),
      ],
    });

    await screen.findByRole('heading', { name: /users and roles/i });
    await user.click(screen.getByRole('checkbox', { name: /admin/i }));

    expect(screen.getByText(/minimum 12 characters because one or more privileged roles are selected/i)).toBeInTheDocument();
  });

  it('keeps non-assignable superadmin roles disabled for non-superadmin sessions', async () => {
    renderAdminUsersPage({
      [`GET ${adminUsersPath}`]: [usersResponse([])],
      [`GET ${adminRolesPath}`]: [
        rolesResponse([
          {
            roleId: 'role-superadmin',
            restaurantId: null,
            name: 'SuperAdmin',
            description: 'Platform owner',
            isSystemRole: true,
            isAssignable: false,
            assignmentBlockedReason: 'SuperAdmin assignment requires a SuperAdmin principal.',
            permissionCodes: ['*'],
          },
        ]),
      ],
    });

    await screen.findByRole('heading', { name: /users and roles/i });

    const checkbox = screen.getByRole('checkbox', { name: /superadmin/i });
    expect(checkbox).toBeDisabled();
    expect(
      screen.getByText(/superadmin assignment requires a superadmin principal/i)
    ).toBeInTheDocument();
  });

  it('loads the selected user into the edit workspace without showing a password field', async () => {
    const { user } = renderAdminUsersPage({
      [`GET ${adminUsersPath}`]: [
        usersResponse([
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
        ]),
      ],
      [`GET ${adminRolesPath}`]: [
        rolesResponse([
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
        ]),
      ],
      [`GET ${userPath('user-1')}`]: [
        detailResponse({
          userId: 'user-1',
          restaurantId: 'restaurant-1',
          branchId: null,
          fullName: 'Asha Kumar',
          mobileNumber: '90001111',
          email: 'asha@example.com',
          status: 'Active',
          roleNames: ['Cashier'],
        }),
      ],
    });

    await screen.findByRole('heading', { name: /users and roles/i });
    await clickEditButton(user);

    expect(await screen.findByDisplayValue('Asha Kumar')).toBeInTheDocument();
    expect(screen.getByDisplayValue('90001111')).toBeInTheDocument();
    expect(screen.getByDisplayValue('asha@example.com')).toBeInTheDocument();
    expect(screen.queryByLabelText(/initial password/i)).not.toBeInTheDocument();
  });

  it('sends the profile update without password or role fields', async () => {
    const { user, fetchMock } = renderAdminUsersPage({
      [`GET ${adminUsersPath}`]: [
        usersResponse([
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
        ]),
        usersResponse([
          {
            userId: 'user-1',
            restaurantId: 'restaurant-1',
            branchId: null,
            fullName: 'Asha Updated',
            mobileNumber: '91112222',
            email: 'asha.updated@example.com',
            status: 'Inactive',
            roleNames: ['Cashier'],
          },
        ]),
      ],
      [`GET ${adminRolesPath}`]: [
        rolesResponse([
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
        ]),
      ],
      [`GET ${userPath('user-1')}`]: [
        detailResponse({
          userId: 'user-1',
          restaurantId: 'restaurant-1',
          branchId: null,
          fullName: 'Asha Kumar',
          mobileNumber: '90001111',
          email: 'asha@example.com',
          status: 'Active',
          roleNames: ['Cashier'],
        }),
      ],
      [`PUT ${userPath('user-1')}`]: [
        detailResponse({
          userId: 'user-1',
          restaurantId: 'restaurant-1',
          branchId: null,
          fullName: 'Asha Updated',
          mobileNumber: '91112222',
          email: 'asha.updated@example.com',
          status: 'Inactive',
          roleNames: ['Cashier'],
        }),
      ],
    });

    await screen.findByRole('heading', { name: /users and roles/i });
    await clickEditButton(user);
    await screen.findByDisplayValue('Asha Kumar');

    await user.clear(screen.getByLabelText(/full name/i));
    await user.type(screen.getByLabelText(/full name/i), 'Asha Updated');
    await user.clear(screen.getByLabelText(/mobile number/i));
    await user.type(screen.getByLabelText(/mobile number/i), '91112222');
    await user.clear(screen.getByLabelText(/email/i));
    await user.type(screen.getByLabelText(/email/i), 'asha.updated@example.com');
    await user.click(screen.getByRole('button', { name: /save profile/i }));

    await waitFor(() => {
      expect(screen.getByText(/saved profile changes for asha updated/i)).toBeInTheDocument();
    });

    const updateCall = fetchMock.mock.calls.find(call => {
      const method = (call[1]?.method ?? 'GET').toUpperCase();
      const path = new URL(String(call[0])).pathname;
      return method === 'PUT' && path === userPath('user-1');
    });

    expect(updateCall).toBeDefined();
    expect(getJsonBody(updateCall as [RequestInfo | URL, RequestInit?])).toEqual({
      branchId: null,
      fullName: 'Asha Updated',
      mobileNumber: '91112222',
      email: 'asha.updated@example.com',
      status: 'Active',
    });
    expect(
      Object.keys(getJsonBody(updateCall as [RequestInfo | URL, RequestInit?]))
    ).not.toContain('initialPassword');
    expect(
      Object.keys(getJsonBody(updateCall as [RequestInfo | URL, RequestInit?]))
    ).not.toContain('roleNames');
  });

  it('sends the role replacement request to the roles endpoint', async () => {
    const { user, fetchMock } = renderAdminUsersPage({
      [`GET ${adminUsersPath}`]: [
        usersResponse([
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
        ]),
        usersResponse([
          {
            userId: 'user-1',
            restaurantId: 'restaurant-1',
            branchId: null,
            fullName: 'Asha Kumar',
            mobileNumber: '90001111',
            email: 'asha@example.com',
            status: 'Active',
            roleNames: ['Cashier', 'Waiter'],
          },
        ]),
      ],
      [`GET ${adminRolesPath}`]: [
        rolesResponse([
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
          {
            roleId: 'role-waiter',
            restaurantId: null,
            name: 'Waiter',
            description: 'Floor service',
            isSystemRole: false,
            isAssignable: true,
            assignmentBlockedReason: null,
            permissionCodes: ['Order.View'],
          },
        ]),
      ],
      [`GET ${userPath('user-1')}`]: [
        detailResponse({
          userId: 'user-1',
          restaurantId: 'restaurant-1',
          branchId: null,
          fullName: 'Asha Kumar',
          mobileNumber: '90001111',
          email: 'asha@example.com',
          status: 'Active',
          roleNames: ['Cashier'],
        }),
      ],
      [`PUT ${rolesPath('user-1')}`]: [
        detailResponse({
          userId: 'user-1',
          restaurantId: 'restaurant-1',
          branchId: null,
          fullName: 'Asha Kumar',
          mobileNumber: '90001111',
          email: 'asha@example.com',
          status: 'Active',
          roleNames: ['Cashier', 'Waiter'],
        }),
      ],
    });

    await screen.findByRole('heading', { name: /users and roles/i });
    await clickEditButton(user);
    await screen.findByDisplayValue('Asha Kumar');

    await user.click(screen.getByRole('checkbox', { name: /waiter/i }));
    await user.click(screen.getByRole('button', { name: /save roles/i }));

    await waitFor(() => {
      expect(screen.getByText(/updated roles for asha kumar/i)).toBeInTheDocument();
    });

    const roleCall = fetchMock.mock.calls.find(call => {
      const method = (call[1]?.method ?? 'GET').toUpperCase();
      const path = new URL(String(call[0])).pathname;
      return method === 'PUT' && path === rolesPath('user-1');
    });

    expect(getJsonBody(roleCall as [RequestInfo | URL, RequestInit?])).toEqual({
      roleNames: ['Cashier', 'Waiter'],
    });
  });

  it('deactivates a user with confirmation and refreshes the list', async () => {
    const { user, fetchMock } = renderAdminUsersPage({
      [`GET ${adminUsersPath}`]: [
        usersResponse([
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
        ]),
        usersResponse([
          {
            userId: 'user-1',
            restaurantId: 'restaurant-1',
            branchId: null,
            fullName: 'Asha Kumar',
            mobileNumber: '90001111',
            email: 'asha@example.com',
            status: 'Inactive',
            roleNames: ['Cashier'],
          },
        ]),
      ],
      [`GET ${adminRolesPath}`]: [
        rolesResponse([
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
        ]),
      ],
      [`GET ${userPath('user-1')}`]: [
        detailResponse({
          userId: 'user-1',
          restaurantId: 'restaurant-1',
          branchId: null,
          fullName: 'Asha Kumar',
          mobileNumber: '90001111',
          email: 'asha@example.com',
          status: 'Active',
          roleNames: ['Cashier'],
        }),
      ],
      [`POST ${deactivatePath('user-1')}`]: [
        detailResponse({
          userId: 'user-1',
          restaurantId: 'restaurant-1',
          branchId: null,
          fullName: 'Asha Kumar',
          mobileNumber: '90001111',
          email: 'asha@example.com',
          status: 'Inactive',
          roleNames: ['Cashier'],
        }),
      ],
    });

    await screen.findByRole('heading', { name: /users and roles/i });
    await clickEditButton(user);
    await screen.findByDisplayValue('Asha Kumar');
    await user.click(screen.getByRole('button', { name: /deactivate user/i }));
    await user.click(screen.getByRole('button', { name: /confirm deactivate/i }));

    await waitFor(() => {
      expect(screen.getByText(/now inactive/i)).toBeInTheDocument();
    });

    const deactivateCall = fetchMock.mock.calls.find(call => {
      const method = (call[1]?.method ?? 'GET').toUpperCase();
      const path = new URL(String(call[0])).pathname;
      return method === 'POST' && path === deactivatePath('user-1');
    });

    expect(deactivateCall).toBeDefined();
    expect(screen.getAllByText('Asha Kumar').length).toBeGreaterThan(0);
  });

  it('opens a reset password dialog for another selected user and sends confirm password payload', async () => {
    const { user, fetchMock } = renderAdminUsersPage({
      [`GET ${adminUsersPath}`]: [
        usersResponse([
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
        ]),
      ],
      [`GET ${adminRolesPath}`]: [
        rolesResponse([
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
        ]),
      ],
      [`GET ${userPath('user-1')}`]: [
        detailResponse({
          userId: 'user-1',
          restaurantId: 'restaurant-1',
          branchId: null,
          fullName: 'Asha Kumar',
          mobileNumber: '90001111',
          email: 'asha@example.com',
          status: 'Active',
          roleNames: ['Cashier'],
        }),
      ],
      [`POST ${resetPasswordPath('user-1')}`]: [
        createJsonResponse({
          userId: 'user-1',
          message: 'Password was reset.',
        }),
      ],
    });

    await screen.findByRole('heading', { name: /users and roles/i });
    await clickEditButton(user);
    await screen.findByDisplayValue('Asha Kumar');

    await user.click(screen.getByRole('button', { name: /reset password/i }));
    const dialog = await screen.findByRole('dialog', { name: /reset password dialog/i });
    expect(within(dialog).getByText(/Asha Kumar/i)).toBeInTheDocument();
    expect(within(dialog).getByText(/90001111/)).toBeInTheDocument();
    expect(
      within(dialog).getByText(/share this password securely\. the user should change it later when self-change is available\./i)
    ).toBeInTheDocument();
    expect(within(dialog).queryByLabelText(/reason/i)).not.toBeInTheDocument();

    await user.type(within(dialog).getByLabelText(/new password/i), 'NewStrongPassword123!');
    await user.type(within(dialog).getByLabelText(/confirm password/i), 'NewStrongPassword123!');
    await user.click(within(dialog).getByRole('button', { name: /submit reset/i }));

    await waitFor(() => {
      expect(
        screen.getByText(/password reset successfully\./i)
      ).toBeInTheDocument();
    });

    const resetCall = fetchMock.mock.calls.find(call => {
      const method = (call[1]?.method ?? 'GET').toUpperCase();
      const path = new URL(String(call[0])).pathname;
      return method === 'POST' && path === resetPasswordPath('user-1');
    });

    expect(getJsonBody(resetCall as [RequestInfo | URL, RequestInit?])).toEqual({
      newPassword: 'NewStrongPassword123!',
      confirmPassword: 'NewStrongPassword123!',
    });
    expect(
      Object.keys(getJsonBody(resetCall as [RequestInfo | URL, RequestInit?]))
    ).not.toContain('restaurantId');
    expect(
      Object.keys(getJsonBody(resetCall as [RequestInfo | URL, RequestInit?]))
    ).not.toContain('passwordHash');
    expect(screen.queryByRole('dialog', { name: /reset password dialog/i })).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/new password/i)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/confirm password/i)).not.toBeInTheDocument();
  }, 20000);

  it('hides reset password for the current signed-in user', async () => {
    const { user } = renderAdminUsersPage(
      {
        [`GET ${adminUsersPath}`]: [
          usersResponse([
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
          ]),
        ],
        [`GET ${adminRolesPath}`]: [
          rolesResponse([
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
          ]),
        ],
        [`GET ${userPath('user-1')}`]: [
          detailResponse({
            userId: 'user-1',
            restaurantId: 'restaurant-1',
            branchId: null,
            fullName: 'Asha Kumar',
            mobileNumber: '90001111',
            email: 'asha@example.com',
            status: 'Active',
            roleNames: ['Cashier'],
          }),
        ],
      },
      { userId: 'user-1' }
    );

    await screen.findByRole('heading', { name: /users and roles/i });
    await clickEditButton(user);
    await screen.findByDisplayValue('Asha Kumar');

    expect(screen.queryByRole('button', { name: /reset password/i })).not.toBeInTheDocument();
  });

  it('blocks password mismatch and sanitizes backend errors in the reset flow', async () => {
    const { user, fetchMock } = renderAdminUsersPage({
      [`GET ${adminUsersPath}`]: [
        usersResponse([
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
        ]),
      ],
      [`GET ${adminRolesPath}`]: [
        rolesResponse([
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
        ]),
      ],
      [`GET ${userPath('user-1')}`]: [
        detailResponse({
          userId: 'user-1',
          restaurantId: 'restaurant-1',
          branchId: null,
          fullName: 'Asha Kumar',
          mobileNumber: '90001111',
          email: 'asha@example.com',
          status: 'Active',
          roleNames: ['Cashier'],
        }),
      ],
      [`POST ${resetPasswordPath('user-1')}`]: [
        createJsonResponse(
          {
            title: 'Server Error',
            detail: '<script>alert("x")</script> Password reset failed. SQL error: token leaked.',
          },
          { status: 500 }
        ),
      ],
    });

    await screen.findByRole('heading', { name: /users and roles/i });
    await clickEditButton(user);
    await screen.findByDisplayValue('Asha Kumar');

    await user.click(screen.getByRole('button', { name: /reset password/i }));
    const dialog = await screen.findByRole('dialog', { name: /reset password dialog/i });
    await user.type(within(dialog).getByLabelText(/new password/i), 'NewStrongPassword123!');
    await user.type(within(dialog).getByLabelText(/confirm password/i), 'DifferentPassword123!');
    await user.click(within(dialog).getByRole('button', { name: /submit reset/i }));

    expect(within(dialog).getByText(/passwords do not match/i)).toBeInTheDocument();

    await user.clear(within(dialog).getByLabelText(/confirm password/i));
    await user.type(within(dialog).getByLabelText(/confirm password/i), 'NewStrongPassword123!');
    await user.click(within(dialog).getByRole('button', { name: /submit reset/i }));

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent(/password reset failed/i);
    });

    expect(screen.getByRole('alert')).not.toHaveTextContent(/<script>/i);
    expect(screen.getByRole('alert')).not.toHaveTextContent(/sql error/i);
    expect(fetchMock).toHaveBeenCalled();
  });

  it('blocks weak reset passwords before submitting', async () => {
    const { user, fetchMock } = renderAdminUsersPage({
      [`GET ${adminUsersPath}`]: [
        usersResponse([
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
        ]),
      ],
      [`GET ${adminRolesPath}`]: [
        rolesResponse([
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
        ]),
      ],
      [`GET ${userPath('user-1')}`]: [
        detailResponse({
          userId: 'user-1',
          restaurantId: 'restaurant-1',
          branchId: null,
          fullName: 'Asha Kumar',
          mobileNumber: '90001111',
          email: 'asha@example.com',
          status: 'Active',
          roleNames: ['Cashier'],
        }),
      ],
    });

    await screen.findByRole('heading', { name: /users and roles/i });
    await clickEditButton(user);
    await screen.findByDisplayValue('Asha Kumar');

    await user.click(screen.getByRole('button', { name: /reset password/i }));
    const dialog = await screen.findByRole('dialog', { name: /reset password dialog/i });
    await user.type(within(dialog).getByLabelText(/new password/i), 'short');
    await user.type(within(dialog).getByLabelText(/confirm password/i), 'short');
    await user.click(within(dialog).getByRole('button', { name: /submit reset/i }));

    expect(screen.getByText(/password must be at least 8 characters long/i)).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledTimes(4);
  });

  it('blocks current-user deactivation using userId even when the mobile number differs', async () => {
    const { user } = renderAdminUsersPage(
      {
        [`GET ${adminUsersPath}`]: [
          usersResponse([
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
          ]),
        ],
        [`GET ${adminRolesPath}`]: [
          rolesResponse([
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
          ]),
        ],
        [`GET ${userPath('user-1')}`]: [
          detailResponse({
            userId: 'user-1',
            restaurantId: 'restaurant-1',
            branchId: null,
            fullName: 'Asha Kumar',
            mobileNumber: '90001111',
            email: 'asha@example.com',
            status: 'Active',
            roleNames: ['Cashier'],
          }),
        ],
      },
      { userId: 'user-1', mobileNumber: '99999999' }
    );

    await screen.findByRole('heading', { name: /users and roles/i });
    await clickEditButton(user);
    await screen.findByDisplayValue('Asha Kumar');

    expect(screen.getByText(/current session/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /deactivate user/i })).toBeDisabled();
  });

  it('does not treat the same mobile number as the current user when userId differs', async () => {
    const { user } = renderAdminUsersPage(
      {
        [`GET ${adminUsersPath}`]: [
          usersResponse([
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
          ]),
        ],
        [`GET ${adminRolesPath}`]: [
          rolesResponse([
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
          ]),
        ],
        [`GET ${userPath('user-1')}`]: [
          detailResponse({
            userId: 'user-1',
            restaurantId: 'restaurant-1',
            branchId: null,
            fullName: 'Asha Kumar',
            mobileNumber: '90001111',
            email: 'asha@example.com',
            status: 'Active',
            roleNames: ['Cashier'],
          }),
        ],
      },
      { userId: 'session-user', mobileNumber: '90001111' }
    );

    await screen.findByRole('heading', { name: /users and roles/i });
    await clickEditButton(user);
    await screen.findByDisplayValue('Asha Kumar');

    expect(screen.queryByText(/current session/i)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /deactivate user/i })).toBeEnabled();
  });

  it('activates an inactive user and refreshes the list', async () => {
    const { user, fetchMock } = renderAdminUsersPage({
      [`GET ${adminUsersPath}`]: [
        usersResponse([
          {
            userId: 'user-2',
            restaurantId: 'restaurant-1',
            branchId: null,
            fullName: 'Arun',
            mobileNumber: '90002222',
            email: 'arun@example.com',
            status: 'Inactive',
            roleNames: ['Cashier'],
          },
        ]),
        usersResponse([
          {
            userId: 'user-2',
            restaurantId: 'restaurant-1',
            branchId: null,
            fullName: 'Arun',
            mobileNumber: '90002222',
            email: 'arun@example.com',
            status: 'Active',
            roleNames: ['Cashier'],
          },
        ]),
      ],
      [`GET ${adminRolesPath}`]: [
        rolesResponse([
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
        ]),
      ],
      [`GET ${userPath('user-2')}`]: [
        detailResponse({
          userId: 'user-2',
          restaurantId: 'restaurant-1',
          branchId: null,
          fullName: 'Arun',
          mobileNumber: '90002222',
          email: 'arun@example.com',
          status: 'Inactive',
          roleNames: ['Cashier'],
        }),
      ],
      [`POST ${activatePath('user-2')}`]: [
        detailResponse({
          userId: 'user-2',
          restaurantId: 'restaurant-1',
          branchId: null,
          fullName: 'Arun',
          mobileNumber: '90002222',
          email: 'arun@example.com',
          status: 'Active',
          roleNames: ['Cashier'],
        }),
      ],
    });

    await screen.findByRole('heading', { name: /users and roles/i });
    await clickEditButton(user);
    await screen.findByDisplayValue('Arun');
    await user.click(screen.getByRole('button', { name: /activate user/i }));

    await waitFor(() => {
      expect(screen.getByText(/now active/i)).toBeInTheDocument();
    });

    const activateCall = fetchMock.mock.calls.find(call => {
      const method = (call[1]?.method ?? 'GET').toUpperCase();
      const path = new URL(String(call[0])).pathname;
      return method === 'POST' && path === activatePath('user-2');
    });

    expect(activateCall).toBeDefined();
  });

  it('shows a not-authorized state when the session lacks User.Manage', async () => {
    clearAuthSession();
    storeAuthSession({ permissions: ['Report.View'], roles: ['Cashier'], activeRole: 'Cashier' });

    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);

    renderWithRouter(<App />, '/admin/users');

    expect(await screen.findByRole('heading', { name: /not authorized/i })).toBeInTheDocument();
    expect(screen.getByText(/user management requires the user\.manage permission/i)).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
