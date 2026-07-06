import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { useLocation } from 'react-router-dom';

import App from '../../App';
import { AUTH_SESSION_STORAGE_KEY } from './authStorage';
import { clearAuthSession, createAuthSession, createJsonResponse } from '../../test/authTestUtils';
import { renderWithRouter } from '../../test/renderWithRouter';

const LocationProbe = () => {
  const location = useLocation();
  return <div data-testid="location-probe">{`${location.pathname}${location.search}`}</div>;
};

describe('LoginPage', () => {
  it('renders the restaurant operations copy and shared form helpers', () => {
    clearAuthSession();

    renderWithRouter(<App />, '/login');

    expect(screen.getByRole('heading', { name: /every order tracked, every bill accountable/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /sign in to billsoft/i })).toBeInTheDocument();
    expect(
      screen.getByText(/use your restaurant code and registered staff mobile number/i)
    ).toBeInTheDocument();
    expect(screen.getByLabelText(/restaurant code/i)).toBeInTheDocument();
    expect(screen.getByText(/provided by your restaurant administrator/i)).toBeInTheDocument();
    expect(screen.getByText(/multi-branch access can be selected after sign-in/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/mobile number/i)).toBeInTheDocument();
    expect(screen.getByText(/use your registered staff mobile number/i)).toBeInTheDocument();
    const passwordInput = screen.getByLabelText(/^password$/i, { selector: 'input' });
    expect(passwordInput).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /forgot password/i })).toBeInTheDocument();
    const passwordShell = passwordInput.closest('.password-shell');
    expect(passwordShell).not.toBeNull();
    expect(passwordShell).toContainElement(screen.getByRole('button', { name: /show password/i }));
    expect(passwordShell?.querySelectorAll('input')).toHaveLength(1);
    expect(screen.getByRole('checkbox', { name: /trust this private device/i })).toBeInTheDocument();
    expect(screen.getByText(/do not use on shared billing counters/i)).toBeInTheDocument();
    expect(screen.getByText(/need access\? contact your restaurant admin/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /restaurant owner\? request access/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /sign in/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /google/i })).not.toBeInTheDocument();
    expect(screen.queryByText(/email/i)).not.toBeInTheDocument();
  });

  it('stores the session and redirects to the dashboard after a successful login', async () => {
    clearAuthSession();
    const authSession = createAuthSession();
    const fetchMock = vi.fn().mockResolvedValueOnce(createJsonResponse(authSession));
    vi.stubGlobal('fetch', fetchMock);

    const user = userEvent.setup();
    renderWithRouter(<App />, '/login');

    await user.type(screen.getByLabelText(/restaurant code/i), 'bill01');
    await user.type(screen.getByLabelText(/mobile number/i), '91234567');
    await user.type(screen.getByLabelText(/^password$/i, { selector: 'input' }), 'restaurant-pass');
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /billsoft dashboard/i })).toBeInTheDocument();
    });

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(localStorage.getItem(AUTH_SESSION_STORAGE_KEY)).toBeTruthy();
    expect(JSON.parse(localStorage.getItem(AUTH_SESSION_STORAGE_KEY) ?? '{}')).toMatchObject({
      accessToken: authSession.accessToken,
      userId: authSession.userId,
      fullName: authSession.fullName,
      permissions: authSession.permissions,
    });
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/api/v1/auth/login'),
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({
          restaurantCode: 'bill01',
          mobileNumber: '91234567',
          password: 'restaurant-pass',
        }),
      })
    );
  });

  it('routes a direct owner login to the owner dashboard', async () => {
    clearAuthSession();
    const authSession = createAuthSession({
      roles: ['RestaurantOwner'],
      permissions: ['Report.View'],
      activeRole: 'RestaurantOwner',
    });
    const fetchMock = vi.fn().mockResolvedValueOnce(createJsonResponse(authSession));
    vi.stubGlobal('fetch', fetchMock);

    const user = userEvent.setup();
    renderWithRouter(
      <>
        <App />
        <LocationProbe />
      </>,
      '/login'
    );

    await user.type(screen.getByLabelText(/restaurant code/i), 'bill01');
    await user.type(screen.getByLabelText(/mobile number/i), '91234567');
    await user.type(screen.getByLabelText(/^password$/i, { selector: 'input' }), 'restaurant-pass');
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => {
      expect(screen.getByTestId('location-probe')).toHaveTextContent('/owner/dashboard');
    });
  });

  it('routes a direct accounts login to the daily cash sales report', async () => {
    clearAuthSession();
    const authSession = createAuthSession({
      roles: ['AccountsUser'],
      permissions: ['Report.View'],
      activeRole: 'AccountsUser',
    });
    const fetchMock = vi.fn().mockResolvedValueOnce(createJsonResponse(authSession));
    vi.stubGlobal('fetch', fetchMock);

    const user = userEvent.setup();
    renderWithRouter(
      <>
        <App />
        <LocationProbe />
      </>,
      '/login'
    );

    await user.type(screen.getByLabelText(/restaurant code/i), 'bill01');
    await user.type(screen.getByLabelText(/mobile number/i), '91234567');
    await user.type(screen.getByLabelText(/^password$/i, { selector: 'input' }), 'restaurant-pass');
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => {
      expect(screen.getByTestId('location-probe')).toHaveTextContent('/reports/daily-cash-sales');
    });
  });

  it('routes a direct cashier login to POS orders', async () => {
    clearAuthSession();
    const authSession = createAuthSession({
      roles: ['Cashier'],
      permissions: ['Payment.Record'],
      activeRole: 'Cashier',
    });
    const fetchMock = vi.fn().mockResolvedValueOnce(createJsonResponse(authSession));
    vi.stubGlobal('fetch', fetchMock);

    const user = userEvent.setup();
    renderWithRouter(
      <>
        <App />
        <LocationProbe />
      </>,
      '/login'
    );

    await user.type(screen.getByLabelText(/restaurant code/i), 'bill01');
    await user.type(screen.getByLabelText(/mobile number/i), '91234567');
    await user.type(screen.getByLabelText(/^password$/i, { selector: 'input' }), 'restaurant-pass');
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => {
      expect(screen.getByTestId('location-probe')).toHaveTextContent('/pos/orders');
    });
  });

  it('routes a direct kitchen login to kitchen tickets', async () => {
    clearAuthSession();
    const authSession = createAuthSession({
      roles: ['KitchenUser'],
      permissions: ['KitchenTicket.UpdateStatus'],
      activeRole: 'KitchenUser',
    });
    const fetchMock = vi.fn().mockResolvedValueOnce(createJsonResponse(authSession));
    vi.stubGlobal('fetch', fetchMock);

    const user = userEvent.setup();
    renderWithRouter(
      <>
        <App />
        <LocationProbe />
      </>,
      '/login'
    );

    await user.type(screen.getByLabelText(/restaurant code/i), 'bill01');
    await user.type(screen.getByLabelText(/mobile number/i), '91234567');
    await user.type(screen.getByLabelText(/^password$/i, { selector: 'input' }), 'restaurant-pass');
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => {
      expect(screen.getByTestId('location-probe')).toHaveTextContent('/kitchen/tickets');
    });
  });

  it('keeps a protected return URL ahead of the role landing route', async () => {
    clearAuthSession();
    const authSession = createAuthSession({
      roles: ['RestaurantOwner'],
      permissions: ['Report.View'],
      activeRole: 'RestaurantOwner',
    });
    const fetchMock = vi.fn().mockResolvedValueOnce(createJsonResponse(authSession));
    vi.stubGlobal('fetch', fetchMock);

    const user = userEvent.setup();
    renderWithRouter(
      <>
        <App />
        <LocationProbe />
      </>,
      {
        pathname: '/login',
        state: {
          from: { pathname: '/billing', search: '?branchId=branch-1' },
        },
      }
    );

    await user.type(screen.getByLabelText(/restaurant code/i), 'bill01');
    await user.type(screen.getByLabelText(/mobile number/i), '91234567');
    await user.type(screen.getByLabelText(/^password$/i, { selector: 'input' }), 'restaurant-pass');
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => {
      expect(screen.getByTestId('location-probe')).toHaveTextContent('/billing?branchId=branch-1');
    });
  });

  it.each(['https://evil.com', '//evil.com'])('rejects an unsafe return URL of %s and uses role landing', async unsafeReturnUrl => {
    clearAuthSession();
    const authSession = createAuthSession({
      roles: ['RestaurantOwner'],
      permissions: ['Report.View'],
      activeRole: 'RestaurantOwner',
    });
    const fetchMock = vi.fn().mockResolvedValueOnce(createJsonResponse(authSession));
    vi.stubGlobal('fetch', fetchMock);

    const user = userEvent.setup();
    renderWithRouter(
      <>
        <App />
        <LocationProbe />
      </>,
      {
        pathname: '/login',
        state: {
          from: unsafeReturnUrl,
        },
      }
    );

    await user.type(screen.getByLabelText(/restaurant code/i), 'bill01');
    await user.type(screen.getByLabelText(/mobile number/i), '91234567');
    await user.type(screen.getByLabelText(/^password$/i, { selector: 'input' }), 'restaurant-pass');
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => {
      expect(screen.getByTestId('location-probe')).toHaveTextContent('/owner/dashboard');
    });
  });

  it('shows a generic error and keeps tokens out of storage on failed login', async () => {
    clearAuthSession();
    const fetchMock = vi.fn().mockResolvedValueOnce(
      createJsonResponse({ title: 'Unauthorized', detail: 'Invalid credentials.' }, { status: 401 })
    );
    vi.stubGlobal('fetch', fetchMock);

    const user = userEvent.setup();
    renderWithRouter(<App />, '/login');

    await user.type(screen.getByLabelText(/restaurant code/i), 'bill01');
    await user.type(screen.getByLabelText(/mobile number/i), '91234567');
    await user.type(screen.getByLabelText(/^password$/i, { selector: 'input' }), 'wrong-password');
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent(/sign in failed/i);
    });

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(localStorage.getItem(AUTH_SESSION_STORAGE_KEY)).toBeNull();
    expect(screen.queryByText(/invalid credentials/i)).not.toBeInTheDocument();
  });

  it('submits the form when pressing Enter in the password field', async () => {
    clearAuthSession();
    const authSession = createAuthSession();
    const fetchMock = vi.fn().mockResolvedValueOnce(createJsonResponse(authSession));
    vi.stubGlobal('fetch', fetchMock);

    const user = userEvent.setup();
    renderWithRouter(<App />, '/login');

    await user.type(screen.getByLabelText(/restaurant code/i), 'bill01');
    await user.type(screen.getByLabelText(/mobile number/i), '91234567');
    await user.type(screen.getByLabelText(/^password$/i, { selector: 'input' }), 'restaurant-pass');
    await user.keyboard('{Enter}');

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /billsoft dashboard/i })).toBeInTheDocument();
    });

    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it('toggles password visibility from the keyboard without submitting the form', async () => {
    clearAuthSession();
    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);

    const user = userEvent.setup();
    renderWithRouter(<App />, '/login');

    await user.type(screen.getByLabelText(/restaurant code/i), 'bill01');
    await user.type(screen.getByLabelText(/mobile number/i), '91234567');
    await user.type(screen.getByLabelText(/^password$/i, { selector: 'input' }), 'restaurant-pass');
    screen.getByRole('button', { name: /show password/i }).focus();
    await user.keyboard('{Enter}');

    expect(screen.getByRole('button', { name: /hide password/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/^password$/i, { selector: 'input' })).toHaveAttribute('type', 'text');
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
