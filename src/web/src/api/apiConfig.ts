export const DEFAULT_API_BASE_URL = 'http://localhost:5000';

const trimTrailingSlashes = (value: string) => value.replace(/\/+$/, '');

export interface BillSoftRuntimeConfig {
  apiBaseUrl?: string;
}

declare global {
  interface Window {
    __BILLSOFT_RUNTIME_CONFIG__?: BillSoftRuntimeConfig;
  }
}

export const resolveApiBaseUrl = (value?: string | null) => {
  const trimmed = value?.trim();
  return trimmed ? trimTrailingSlashes(trimmed) : DEFAULT_API_BASE_URL;
};

const readRuntimeApiBaseUrl = () => {
  if (typeof window === 'undefined') {
    return undefined;
  }

  return window.__BILLSOFT_RUNTIME_CONFIG__?.apiBaseUrl;
};

export const resolveConfiguredApiBaseUrl = (
  runtimeApiBaseUrl?: string | null,
  envApiBaseUrl?: string | null
) => resolveApiBaseUrl(runtimeApiBaseUrl ?? envApiBaseUrl);

export const API_BASE_URL = resolveConfiguredApiBaseUrl(
  readRuntimeApiBaseUrl(),
  import.meta.env.VITE_BILLSOFT_API_BASE_URL
);

export const buildApiUrl = (path: string, baseUrl = API_BASE_URL) => {
  const normalizedPath = path.startsWith('/') ? path : `/${path}`;
  return `${resolveApiBaseUrl(baseUrl)}${normalizedPath}`;
};
