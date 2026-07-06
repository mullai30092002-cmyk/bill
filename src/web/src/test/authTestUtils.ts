import { AUTH_SESSION_STORAGE_KEY } from '../features/auth/authStorage';
import type { AuthSession } from '../features/auth/authTypes';

export const createAuthSession = (overrides: Partial<AuthSession> = {}): AuthSession => ({
  accessToken: 'access-token',
  refreshToken: 'refresh-token',
  accessTokenExpiresAtUtc: '2099-06-11T10:15:00Z',
  refreshTokenExpiresAtUtc: '2099-06-18T10:15:00Z',
  userId: 'session-user',
  restaurantId: 'restaurant-1',
  restaurantCode: 'BILL01',
  countryCode: 'IN',
  currencyCode: 'INR',
  timeZoneId: 'Asia/Kolkata',
  branchId: 'branch-1',
  fullName: 'Maya Iyer',
  mobileNumber: '91234567',
  roles: ['Admin'],
  permissions: ['User.Manage'],
  activeRole: 'Admin',
  ...overrides,
});

export const storeAuthSession = (overrides: Partial<AuthSession> = {}) => {
  const session = createAuthSession(overrides);
  localStorage.setItem(AUTH_SESSION_STORAGE_KEY, JSON.stringify(session));
  return session;
};

export const clearAuthSession = () => {
  localStorage.removeItem(AUTH_SESSION_STORAGE_KEY);
};

export const createJsonResponse = (body: unknown, init?: ResponseInit) =>
  new Response(JSON.stringify(body), {
    status: init?.status ?? 200,
    headers: {
      'Content-Type': 'application/json',
      ...init?.headers,
    },
  });
