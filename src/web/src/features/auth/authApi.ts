import { requestJson, requestVoid } from '../../api/apiClient';
import type { AuthSession, AuthUserContext, LoginRequest } from './authTypes';

export const login = (request: LoginRequest) =>
  requestJson<AuthSession>('/api/v1/auth/login', {
    method: 'POST',
    body: request,
  });

export const logout = (refreshToken: string) =>
  requestVoid('/api/v1/auth/logout', {
    method: 'POST',
    body: { refreshToken },
  });

export const me = () => requestJson<AuthUserContext>('/api/v1/auth/me');
