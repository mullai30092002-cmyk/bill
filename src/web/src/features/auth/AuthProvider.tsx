import type { ReactNode } from 'react';
import { createContext, useCallback, useContext, useMemo, useState } from 'react';

import { clearAuthSession, isAuthSessionExpired, readAuthSession, writeAuthSession } from './authStorage';
import { login as loginRequest, logout as logoutRequest } from './authApi';
import type { AuthSession, LoginRequest } from './authTypes';

export interface AuthContextValue {
  session: AuthSession | null;
  isAuthenticated: boolean;
  login: (request: LoginRequest) => Promise<AuthSession>;
  logout: () => Promise<void>;
  hasPermission: (permission: string) => boolean;
  hasRole: (role: string) => boolean;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export interface AuthProviderProps {
  children: ReactNode;
}

const normalize = (value: string) => value.trim().toLowerCase();

export const AuthProvider = ({ children }: AuthProviderProps) => {
  const [session, setSession] = useState<AuthSession | null>(() => {
    const stored = readAuthSession();
    return stored && !isAuthSessionExpired(stored) ? stored : null;
  });

  const handleLogin = useCallback(async (request: LoginRequest) => {
    const nextSession = await loginRequest(request);
    writeAuthSession(nextSession);
    setSession(nextSession);
    return nextSession;
  }, []);

  const handleLogout = useCallback(async () => {
    const currentSession = readAuthSession() ?? session;
    try {
      if (currentSession?.refreshToken) {
        await logoutRequest(currentSession.refreshToken);
      }
    } catch {
      // Clear local state even if the server logout request fails.
    } finally {
      clearAuthSession();
      setSession(null);
    }
  }, [session]);

  const value = useMemo<AuthContextValue>(
    () => ({
      session,
      isAuthenticated: Boolean(session),
      login: handleLogin,
      logout: handleLogout,
      hasPermission: permission =>
        Boolean(session?.permissions.some(value => normalize(value) === normalize(permission))),
      hasRole: role => Boolean(session?.roles.some(value => normalize(value) === normalize(role))),
    }),
    [handleLogin, handleLogout, session]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

export const useAuthContext = () => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within AuthProvider');
  }

  return context;
};

export default AuthProvider;
