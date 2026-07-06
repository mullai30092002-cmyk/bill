import type { AuthSession } from './authTypes';

export const AUTH_SESSION_STORAGE_KEY = 'billsoft.auth.session.v1';

const isStringArray = (value: unknown): value is string[] =>
  Array.isArray(value) && value.every(entry => typeof entry === 'string');

const isAuthSessionShape = (value: unknown): value is AuthSession => {
  if (!value || typeof value !== 'object') {
    return false;
  }

  const session = value as Record<string, unknown>;
  return (
    typeof session.accessToken === 'string' &&
    typeof session.refreshToken === 'string' &&
    typeof session.accessTokenExpiresAtUtc === 'string' &&
    typeof session.refreshTokenExpiresAtUtc === 'string' &&
    typeof session.userId === 'string' &&
    typeof session.restaurantId === 'string' &&
    typeof session.restaurantCode === 'string' &&
    (typeof session.countryCode === 'string' || session.countryCode === undefined) &&
    (typeof session.currencyCode === 'string' || session.currencyCode === undefined) &&
    (typeof session.timeZoneId === 'string' || session.timeZoneId === undefined) &&
    (session.branchId === null || typeof session.branchId === 'string') &&
    typeof session.fullName === 'string' &&
    typeof session.mobileNumber === 'string' &&
    isStringArray(session.roles) &&
    isStringArray(session.permissions) &&
    typeof session.activeRole === 'string'
  );
};

export const isAuthSessionExpired = (session: AuthSession, now = Date.now()) =>
  Number.isNaN(Date.parse(session.accessTokenExpiresAtUtc)) ||
  Date.parse(session.accessTokenExpiresAtUtc) <= now;

export const readAuthSession = (): AuthSession | null => {
  try {
    const raw = localStorage.getItem(AUTH_SESSION_STORAGE_KEY);
    if (!raw) {
      return null;
    }

    const parsed: unknown = JSON.parse(raw);
    if (!isAuthSessionShape(parsed)) {
      clearAuthSession();
      return null;
    }

    if (isAuthSessionExpired(parsed)) {
      clearAuthSession();
      return null;
    }

    return parsed;
  } catch {
    clearAuthSession();
    return null;
  }
};

export const writeAuthSession = (session: AuthSession) => {
  localStorage.setItem(AUTH_SESSION_STORAGE_KEY, JSON.stringify(session));
};

export const clearAuthSession = () => {
  localStorage.removeItem(AUTH_SESSION_STORAGE_KEY);
};
