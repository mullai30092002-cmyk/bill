import { describe, expect, it } from 'vitest';

import {
  DEFAULT_API_BASE_URL,
  buildApiUrl,
  resolveApiBaseUrl,
  resolveConfiguredApiBaseUrl
} from './apiConfig';

describe('apiConfig', () => {
  it('defaults to the local API URL when the env value is missing or blank', () => {
    expect(resolveApiBaseUrl()).toBe(DEFAULT_API_BASE_URL);
    expect(resolveApiBaseUrl('')).toBe(DEFAULT_API_BASE_URL);
    expect(resolveApiBaseUrl('   ')).toBe(DEFAULT_API_BASE_URL);
  });

  it('trims whitespace and removes trailing slashes from the base URL', () => {
    expect(resolveApiBaseUrl('  http://localhost:5000/  ')).toBe('http://localhost:5000');
  });

  it('builds API URLs from paths with or without a leading slash', () => {
    expect(buildApiUrl('/api/v1/auth/login', 'http://localhost:5000')).toBe(
      'http://localhost:5000/api/v1/auth/login'
    );
    expect(buildApiUrl('api/v1/auth/login', 'http://localhost:5000')).toBe(
      'http://localhost:5000/api/v1/auth/login'
    );
  });

  it('removes trailing slashes from the base URL when building URLs', () => {
    expect(buildApiUrl('/api/v1/auth/login', 'http://localhost:5000///')).toBe(
      'http://localhost:5000/api/v1/auth/login'
    );
  });

  it('prefers the runtime API base URL when one is provided', () => {
    expect(resolveConfiguredApiBaseUrl('https://api-billsoft-dev.azurewebsites.net/', 'http://localhost:5000')).toBe(
      'https://api-billsoft-dev.azurewebsites.net'
    );
  });

  it('falls back to the environment API base URL when runtime config is missing', () => {
    expect(resolveConfiguredApiBaseUrl(undefined, 'https://api-billsoft-dev.azurewebsites.net/')).toBe(
      'https://api-billsoft-dev.azurewebsites.net'
    );
  });
});
