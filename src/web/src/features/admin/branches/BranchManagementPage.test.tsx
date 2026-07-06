import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';

import App from '../../../App';
import { clearAuthSession, createAuthSession, createJsonResponse, storeAuthSession } from '../../../test/authTestUtils';
import { renderWithRouter } from '../../../test/renderWithRouter';

const adminBranchesPath = '/api/v1/admin/branches';
const branchPath = (branchId: string) => `/api/v1/admin/branches/${branchId}`;
const activatePath = (branchId: string) => `/api/v1/admin/branches/${branchId}/activate`;
const deactivatePath = (branchId: string) => `/api/v1/admin/branches/${branchId}/deactivate`;

const branchListResponse = (items: Array<Record<string, unknown>>) =>
  createJsonResponse({
    items,
  });

const branchDetailResponse = (item: Record<string, unknown>) =>
  createJsonResponse({
    ...item,
    createdAt: '2026-06-11T09:00:00Z',
    updatedAt: '2026-06-11T09:30:00Z',
  });

const problemJsonResponse = (body: Record<string, unknown>, status = 400) =>
  new Response(JSON.stringify(body), {
    status,
    headers: {
      'Content-Type': 'application/problem+json',
    },
  });

const defaultBranchList = () =>
  branchListResponse([
    {
      branchId: 'branch-1',
      restaurantId: 'restaurant-1',
      name: 'Main Branch',
      address: '123 Market Street',
      phone: '60000001',
      timezone: 'Asia/Singapore',
      currency: 'INR',
      status: 'Active',
      createdAt: '2026-06-11T09:00:00Z',
      updatedAt: '2026-06-11T09:30:00Z',
    },
    {
      branchId: 'branch-2',
      restaurantId: 'restaurant-1',
      name: 'Side Branch',
      address: '456 Side Street',
      phone: '60000002',
      timezone: 'Asia/Singapore',
      currency: 'INR',
      status: 'Inactive',
      createdAt: '2026-06-11T08:00:00Z',
      updatedAt: '2026-06-11T08:30:00Z',
    },
  ]);

const setupFetch = (responses: Record<string, Response[]>) => {
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

const mockScrollIntoView = () => {
  const scrollIntoViewMock = vi.fn();
  Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', {
    configurable: true,
    writable: true,
    value: scrollIntoViewMock,
  });
  return scrollIntoViewMock;
};

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

const getJsonBody = (call: [RequestInfo | URL, RequestInit?]) =>
  call[1]?.body ? JSON.parse(String(call[1].body)) : undefined;

const renderBranchPage = (
  responses: Record<string, Response[]> = {},
  authOverrides: Parameters<typeof createAuthSession>[0] = {}
) => {
  clearAuthSession();
  storeAuthSession({
    permissions: ['Branch.Manage'],
    roles: ['Admin'],
    activeRole: 'Admin',
    ...authOverrides,
  });

  const fetchMock = setupFetch({
    [`GET ${adminBranchesPath}`]: [defaultBranchList()],
    ...responses,
  });

  const user = userEvent.setup();
  renderWithRouter(<App />, '/admin/branches');

  return { fetchMock, user };
};

const selectBranch = async (user: ReturnType<typeof userEvent.setup>) => {
  const editButtons = screen.getAllByRole('button', { name: /edit/i });
  await user.click(editButtons[0]);
};

describe('BranchManagementPage', () => {
  it('renders the route for a branch manager and shows the branch nav item', async () => {
    renderBranchPage();

    expect(await screen.findByRole('heading', { name: /branch management/i })).toBeInTheDocument();
    expect(screen.getAllByText('Branches', { selector: '.responsive-nav__link-label' }).length).toBeGreaterThan(0);
  });

  it('shows a not-authorized state without Branch.Manage and does not call branch APIs', async () => {
    clearAuthSession();
    storeAuthSession({
      permissions: ['User.Manage'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });

    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);

    renderWithRouter(<App />, '/admin/branches');

    expect(await screen.findByRole('heading', { name: /not authorized/i })).toBeInTheDocument();
    expect(
      screen.getByText(/branch management requires the branch\.manage permission/i)
    ).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('loads branches from the backend and displays names and statuses', async () => {
    renderBranchPage();

    expect(await screen.findByRole('heading', { name: /branch management/i })).toBeInTheDocument();
    expect(screen.getAllByText(/main branch/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/side branch/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/active/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/inactive/i).length).toBeGreaterThan(0);
    expect(screen.queryByText('branch-1')).not.toBeInTheDocument();
  });

  it('renders the create form fields', async () => {
    renderBranchPage();

    expect(await screen.findByRole('heading', { name: /branch management/i })).toBeInTheDocument();
    expect(screen.getByPlaceholderText('Main Outlet')).toBeInTheDocument();
    expect(screen.getByPlaceholderText('123 Market Street')).toBeInTheDocument();
    expect(screen.getByPlaceholderText('60000001')).toBeInTheDocument();
    expect(screen.getByPlaceholderText('Asia/Singapore')).toBeInTheDocument();
    expect(screen.getByPlaceholderText('INR')).toBeInTheDocument();
  });

  it('blocks create submission when name is missing', async () => {
    const { user, fetchMock } = renderBranchPage();

    expect(await screen.findByRole('heading', { name: /branch management/i })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /create branch/i }));

    await waitFor(() => {
      expect(screen.getByText(/please fix the form fields before saving the branch/i)).toBeInTheDocument();
    });

    expect(screen.getByText(/name is required/i)).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it('sends create requests without restaurantId and clears the form after success', async () => {
    const { user, fetchMock } = renderBranchPage({
      [`POST ${adminBranchesPath}`]: [
        branchDetailResponse({
          branchId: 'branch-3',
          restaurantId: 'restaurant-1',
          name: 'New Branch',
          address: '789 Fresh Street',
          phone: '60000003',
          timezone: 'Asia/Singapore',
          currency: 'SGD',
          status: 'Active',
        }),
      ],
      [`GET ${adminBranchesPath}`]: [
        defaultBranchList(),
        branchListResponse([
          {
            branchId: 'branch-3',
            restaurantId: 'restaurant-1',
            name: 'New Branch',
            address: '789 Fresh Street',
            phone: '60000003',
            timezone: 'Asia/Singapore',
            currency: 'SGD',
            status: 'Active',
          },
          {
            branchId: 'branch-1',
            restaurantId: 'restaurant-1',
            name: 'Main Branch',
            address: '123 Market Street',
            phone: '60000001',
            timezone: 'Asia/Singapore',
            currency: 'SGD',
            status: 'Active',
          },
          {
            branchId: 'branch-2',
            restaurantId: 'restaurant-1',
            name: 'Side Branch',
            address: '456 Side Street',
            phone: '60000002',
            timezone: 'Asia/Singapore',
            currency: 'SGD',
            status: 'Inactive',
          },
        ]),
      ],
    });

    expect(await screen.findByRole('heading', { name: /branch management/i })).toBeInTheDocument();
    await user.type(screen.getByPlaceholderText('Main Outlet'), '  New Branch  ');
    await user.type(screen.getByPlaceholderText('123 Market Street'), ' 789 Fresh Street ');
    await user.type(screen.getByPlaceholderText('60000001'), ' 60000003 ');
    await user.clear(screen.getByPlaceholderText('Asia/Singapore'));
    await user.type(screen.getByPlaceholderText('Asia/Singapore'), ' Asia/Singapore ');
    await user.clear(screen.getByPlaceholderText('INR'));
    await user.type(screen.getByPlaceholderText('INR'), ' inr ');
    await user.click(screen.getByRole('button', { name: /create branch/i }));

    await waitFor(() => {
      expect(screen.getByText(/created new branch/i)).toBeInTheDocument();
    });

    const createCall = fetchMock.mock.calls.find(call => {
      const method = (call[1]?.method ?? 'GET').toUpperCase();
      const path = new URL(String(call[0])).pathname;
      return method === 'POST' && path === adminBranchesPath;
    });

    expect(getJsonBody(createCall as [RequestInfo | URL, RequestInit?])).toEqual({
      name: 'New Branch',
      address: '789 Fresh Street',
      phone: '60000003',
      timezone: 'Asia/Singapore',
      currency: 'INR',
    });
    expect(screen.getByPlaceholderText('Main Outlet')).toHaveValue('');
    expect(screen.getAllByText(/new branch/i).length).toBeGreaterThan(0);
  }, 20000);

  it('shows a clean duplicate-branch error when create fails with RFC7807 JSON', async () => {
    const { user } = renderBranchPage({
      [`POST ${adminBranchesPath}`]: [
        problemJsonResponse(
          {
            type: 'https://datatracker.ietf.org/doc/html/rfc7807',
            title: 'Bad Request',
            status: 400,
            detail: 'Branch name already exists in this restaurant.',
          },
          400
        ),
      ],
    });

    expect(await screen.findByRole('heading', { name: /branch management/i })).toBeInTheDocument();
    await user.type(screen.getByPlaceholderText('Main Outlet'), '  Main Branch  ');
    await user.click(screen.getByRole('button', { name: /create branch/i }));

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent(
        'Branch name already exists in this restaurant.'
      );
    });

    expect(screen.queryByText(/datatracker\.ietf\.org/i)).not.toBeInTheDocument();
  });

  it('shows a clean duplicate-mobile error when create fails with RFC7807 JSON', async () => {
    const { user } = renderBranchPage({
      [`POST ${adminBranchesPath}`]: [
        problemJsonResponse(
          {
            type: 'https://datatracker.ietf.org/doc/html/rfc7807',
            title: 'Bad Request',
            status: 400,
            detail: 'Branch mobile number already exists.',
          },
          400
        ),
      ],
    });

    expect(await screen.findByRole('heading', { name: /branch management/i })).toBeInTheDocument();
    await user.type(screen.getByPlaceholderText('Main Outlet'), 'Main Outlet');
    await user.type(screen.getByPlaceholderText('60000001'), '60000001');
    await user.click(screen.getByRole('button', { name: /create branch/i }));

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent('Branch mobile number already exists.');
    });

    expect(screen.queryByText(/datatracker\.ietf\.org/i)).not.toBeInTheDocument();
  });

  it('scrolls and focuses the branch form when clicking New Branch', async () => {
    const scrollIntoViewMock = mockScrollIntoView();
    const { user } = renderBranchPage();

    expect(await screen.findByRole('heading', { name: /branch management/i })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /new branch/i }));

    await waitFor(() => {
      expect(scrollIntoViewMock).toHaveBeenCalled();
    });

    expect(screen.getByPlaceholderText('Main Outlet')).toHaveFocus();
  });

  it('loads a selected branch into the edit workspace', async () => {
    const { user } = renderBranchPage({
      [`GET ${branchPath('branch-1')}`]: [
        branchDetailResponse({
          branchId: 'branch-1',
          restaurantId: 'restaurant-1',
          name: 'Main Branch',
          address: '123 Market Street',
          phone: '60000001',
          timezone: 'Asia/Singapore',
          currency: 'SGD',
          status: 'Active',
        }),
      ],
    });

    expect(await screen.findByRole('heading', { name: /branch management/i })).toBeInTheDocument();
    await selectBranch(user);

    await screen.findByDisplayValue('Main Branch');
    expect(screen.getByDisplayValue('123 Market Street')).toBeInTheDocument();
    expect(screen.getByDisplayValue('60000001')).toBeInTheDocument();
  });

  it('sends update requests without restaurantId or status', async () => {
    const { user, fetchMock } = renderBranchPage({
      [`GET ${branchPath('branch-1')}`]: [
        branchDetailResponse({
          branchId: 'branch-1',
          restaurantId: 'restaurant-1',
          name: 'Main Branch',
          address: '123 Market Street',
          phone: '60000001',
          timezone: 'Asia/Singapore',
          currency: 'SGD',
          status: 'Active',
        }),
      ],
      [`GET ${adminBranchesPath}`]: [
        defaultBranchList(),
        branchListResponse([
          {
            branchId: 'branch-1',
            restaurantId: 'restaurant-1',
            name: 'Updated Branch',
            address: '456 Updated Street',
            phone: '60000002',
            timezone: 'Asia/Singapore',
            currency: 'SGD',
            status: 'Active',
          },
          {
            branchId: 'branch-2',
            restaurantId: 'restaurant-1',
            name: 'Side Branch',
            address: '456 Side Street',
            phone: '60000002',
            timezone: 'Asia/Singapore',
            currency: 'SGD',
            status: 'Inactive',
          },
        ]),
      ],
      [`PUT ${branchPath('branch-1')}`]: [
        branchDetailResponse({
          branchId: 'branch-1',
          restaurantId: 'restaurant-1',
          name: 'Updated Branch',
          address: '456 Updated Street',
          phone: '60000002',
          timezone: 'Asia/Singapore',
          currency: 'SGD',
          status: 'Active',
        }),
      ],
    });

    expect(await screen.findByRole('heading', { name: /branch management/i })).toBeInTheDocument();
    await selectBranch(user);
    await screen.findByDisplayValue('Main Branch');

    const nameField = screen.getByDisplayValue('Main Branch');
    const addressField = screen.getByDisplayValue('123 Market Street');
    const phoneField = screen.getByDisplayValue('60000001');
    const timezoneField = screen.getByDisplayValue('Asia/Singapore');
    const currencyField = screen.getByDisplayValue('SGD');

    await user.clear(nameField);
    await user.type(nameField, ' Updated Branch ');
    await user.clear(addressField);
    await user.type(addressField, ' 456 Updated Street ');
    await user.clear(phoneField);
    await user.type(phoneField, ' 60000002 ');
    expect(timezoneField).toHaveValue('Asia/Singapore');
    expect(currencyField).toHaveValue('SGD');
    await user.click(screen.getByRole('button', { name: /save changes/i }));

    await waitFor(() => {
      expect(screen.getByText(/saved changes for updated branch/i)).toBeInTheDocument();
    });

    const updateCall = fetchMock.mock.calls.find(call => {
      const method = (call[1]?.method ?? 'GET').toUpperCase();
      const path = new URL(String(call[0])).pathname;
      return method === 'PUT' && path === branchPath('branch-1');
    });

    expect(getJsonBody(updateCall as [RequestInfo | URL, RequestInit?])).toEqual({
      name: 'Updated Branch',
      address: '456 Updated Street',
      phone: '60000002',
      timezone: 'Asia/Singapore',
      currency: 'SGD',
    });
    expect(
      Object.keys(getJsonBody(updateCall as [RequestInfo | URL, RequestInit?]))
    ).not.toContain('restaurantId');
    expect(
      Object.keys(getJsonBody(updateCall as [RequestInfo | URL, RequestInit?]))
    ).not.toContain('status');
  }, 20000);

  it('activates an inactive branch', async () => {
    const { user, fetchMock } = renderBranchPage({
      [`GET ${branchPath('branch-2')}`]: [
        branchDetailResponse({
          branchId: 'branch-2',
          restaurantId: 'restaurant-1',
          name: 'Side Branch',
          address: '456 Side Street',
          phone: '60000002',
          timezone: 'Asia/Singapore',
          currency: 'SGD',
          status: 'Inactive',
        }),
      ],
      [`GET ${adminBranchesPath}`]: [
        defaultBranchList(),
        branchListResponse([
          {
            branchId: 'branch-1',
            restaurantId: 'restaurant-1',
            name: 'Main Branch',
            address: '123 Market Street',
            phone: '60000001',
            timezone: 'Asia/Singapore',
            currency: 'SGD',
            status: 'Active',
          },
          {
            branchId: 'branch-2',
            restaurantId: 'restaurant-1',
            name: 'Side Branch',
            address: '456 Side Street',
            phone: '60000002',
            timezone: 'Asia/Singapore',
            currency: 'SGD',
            status: 'Active',
          },
        ]),
      ],
      [`POST ${activatePath('branch-2')}`]: [
        branchDetailResponse({
          branchId: 'branch-2',
          restaurantId: 'restaurant-1',
          name: 'Side Branch',
          address: '456 Side Street',
          phone: '60000002',
          timezone: 'Asia/Singapore',
          currency: 'SGD',
          status: 'Active',
        }),
      ],
    });

    expect(await screen.findByRole('heading', { name: /branch management/i })).toBeInTheDocument();
    await user.click(screen.getAllByRole('button', { name: /edit/i })[1]);
    await screen.findByDisplayValue('Side Branch');
    await user.click(screen.getByRole('button', { name: /activate branch/i }));

    await waitFor(() => {
      expect(screen.getByText(/side branch is now active/i)).toBeInTheDocument();
    });

    const activateCall = fetchMock.mock.calls.find(call => {
      const method = (call[1]?.method ?? 'GET').toUpperCase();
      const path = new URL(String(call[0])).pathname;
      return method === 'POST' && path === activatePath('branch-2');
    });

    expect(activateCall).toBeDefined();
  });

  it('requires confirmation before deactivating a branch', async () => {
    const { user, fetchMock } = renderBranchPage({
      [`GET ${branchPath('branch-1')}`]: [
        branchDetailResponse({
          branchId: 'branch-1',
          restaurantId: 'restaurant-1',
          name: 'Main Branch',
          address: '123 Market Street',
          phone: '60000001',
          timezone: 'Asia/Singapore',
          currency: 'SGD',
          status: 'Active',
        }),
      ],
      [`GET ${adminBranchesPath}`]: [
        defaultBranchList(),
        branchListResponse([
          {
            branchId: 'branch-1',
            restaurantId: 'restaurant-1',
            name: 'Main Branch',
            address: '123 Market Street',
            phone: '60000001',
            timezone: 'Asia/Singapore',
            currency: 'SGD',
            status: 'Inactive',
          },
          {
            branchId: 'branch-2',
            restaurantId: 'restaurant-1',
            name: 'Side Branch',
            address: '456 Side Street',
            phone: '60000002',
            timezone: 'Asia/Singapore',
            currency: 'SGD',
            status: 'Inactive',
          },
        ]),
      ],
      [`POST ${deactivatePath('branch-1')}`]: [
        branchDetailResponse({
          branchId: 'branch-1',
          restaurantId: 'restaurant-1',
          name: 'Main Branch',
          address: '123 Market Street',
          phone: '60000001',
          timezone: 'Asia/Singapore',
          currency: 'SGD',
          status: 'Inactive',
        }),
      ],
    });

    expect(await screen.findByRole('heading', { name: /branch management/i })).toBeInTheDocument();
    await selectBranch(user);
    await screen.findByDisplayValue('Main Branch');

    expect(screen.queryByRole('button', { name: /^confirm deactivate$/i })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /deactivate branch/i }));
    expect(screen.getByRole('button', { name: /^confirm deactivate$/i })).toBeInTheDocument();
    expect(
      fetchMock.mock.calls.some(call => {
        const method = (call[1]?.method ?? 'GET').toUpperCase();
        const path = new URL(String(call[0])).pathname;
        return method === 'POST' && path === deactivatePath('branch-1');
      })
    ).toBe(false);

    await user.click(screen.getByRole('button', { name: /^confirm deactivate$/i }));

    await waitFor(() => {
      expect(screen.getByText(/main branch is now inactive/i)).toBeInTheDocument();
    });

    const deactivateCall = fetchMock.mock.calls.find(call => {
      const method = (call[1]?.method ?? 'GET').toUpperCase();
      const path = new URL(String(call[0])).pathname;
      return method === 'POST' && path === deactivatePath('branch-1');
    });

    expect(deactivateCall).toBeDefined();
  });

  it('shows the active-user guard error safely when deactivation is rejected', async () => {
    const { user } = renderBranchPage({
      [`GET ${branchPath('branch-1')}`]: [
        branchDetailResponse({
          branchId: 'branch-1',
          restaurantId: 'restaurant-1',
          name: 'Main Branch',
          address: '123 Market Street',
          phone: '60000001',
          timezone: 'Asia/Singapore',
          currency: 'SGD',
          status: 'Active',
        }),
      ],
      [`POST ${deactivatePath('branch-1')}`]: [
        createJsonResponse(
          {
            title: 'Conflict',
            detail: 'Branch cannot be deactivated while active users are assigned.',
          },
          { status: 409 }
        ),
      ],
    });

    expect(await screen.findByRole('heading', { name: /branch management/i })).toBeInTheDocument();
    await selectBranch(user);
    await screen.findByDisplayValue('Main Branch');
    await user.click(screen.getByRole('button', { name: /deactivate branch/i }));
    await user.click(screen.getByRole('button', { name: /^confirm deactivate$/i }));

    await waitFor(() => {
      expect(
        screen.getByText(/branch cannot be deactivated while active users are assigned/i)
      ).toBeInTheDocument();
    });
  });

  it('shows a clean deactivate error when branch deactivation fails with RFC7807 JSON', async () => {
    const { user } = renderBranchPage({
      [`GET ${branchPath('branch-1')}`]: [
        branchDetailResponse({
          branchId: 'branch-1',
          restaurantId: 'restaurant-1',
          name: 'Main Branch',
          address: '123 Market Street',
          phone: '60000001',
          timezone: 'Asia/Singapore',
          currency: 'SGD',
          status: 'Active',
        }),
      ],
      [`POST ${deactivatePath('branch-1')}`]: [
        problemJsonResponse(
          {
            type: 'https://datatracker.ietf.org/doc/html/rfc7807',
            title: 'Conflict',
            status: 409,
            detail: 'Branch cannot be deactivated while active users are assigned.',
          },
          409
        ),
      ],
    });

    expect(await screen.findByRole('heading', { name: /branch management/i })).toBeInTheDocument();
    await selectBranch(user);
    await screen.findByDisplayValue('Main Branch');
    await user.click(screen.getByRole('button', { name: /deactivate branch/i }));
    await user.click(screen.getByRole('button', { name: /^confirm deactivate$/i }));

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent(
        'Branch cannot be deactivated while active users are assigned.'
      );
    });

    expect(screen.queryByText(/datatracker\.ietf\.org/i)).not.toBeInTheDocument();
  });

  it('does not render a delete action', async () => {
    renderBranchPage();

    expect(await screen.findByRole('heading', { name: /branch management/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /delete/i })).not.toBeInTheDocument();
  });
});
