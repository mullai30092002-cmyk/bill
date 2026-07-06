import { buildApiUrl } from './apiConfig';
import { ApiError } from './apiErrors';
import { readAuthSession } from '../features/auth/authStorage';

export interface ApiRequestOptions extends Omit<RequestInit, 'body' | 'headers'> {
  body?: unknown;
  headers?: HeadersInit;
}

const isJsonBody = (body: unknown) =>
  body !== undefined &&
  body !== null &&
  typeof body !== 'string' &&
  !(body instanceof FormData) &&
  !(body instanceof Blob) &&
  !(body instanceof URLSearchParams);

const parseResponsePayload = async (response: Response) => {
  const contentType = response.headers.get('content-type') ?? '';
  if (response.status === 204) {
    return undefined;
  }

  if (contentType.includes('application/json')) {
    return response.json();
  }

  const text = await response.text();
  return text ? text : undefined;
};

const UNSAFE_TEXT_PATTERN =
  /<[a-z][\s\S]*?>/i;

const isSafeShortText = (value: string) =>
  value.length <= 200 &&
  !UNSAFE_TEXT_PATTERN.test(value) &&
  !/\bat\s+\w+[\w.$]+\s*\(/m.test(value) &&
  !/SqlException|SqlError|EntityFramework|Microsoft\.Data/i.test(value) &&
  !/Stack trace/i.test(value);

const resolveErrorMessage = (status: number, payload: unknown) => {
  if (payload && typeof payload === 'object') {
    const typedPayload = payload as { title?: string; detail?: string; message?: string };
    return typedPayload.detail || typedPayload.title || typedPayload.message || `Request failed (${status})`;
  }

  if (typeof payload === 'string') {
    const trimmed = payload.trim();
    if (trimmed && isSafeShortText(trimmed)) {
      return trimmed;
    }
  }

  return `Request failed (${status})`;
};

const buildHeaders = (headers?: HeadersInit, body?: unknown) => {
  const resolvedHeaders = new Headers(headers ?? {});

  if (!resolvedHeaders.has('Accept')) {
    resolvedHeaders.set('Accept', 'application/json');
  }

  if (isJsonBody(body) && !resolvedHeaders.has('Content-Type')) {
    resolvedHeaders.set('Content-Type', 'application/json');
  }

  const accessToken = readAuthSession()?.accessToken;
  if (accessToken && !resolvedHeaders.has('Authorization')) {
    resolvedHeaders.set('Authorization', `Bearer ${accessToken}`);
  }

  return resolvedHeaders;
};

export const requestJson = async <T>(path: string, options: ApiRequestOptions = {}): Promise<T> => {
  const response = await fetch(buildApiUrl(path), {
    ...options,
    headers: buildHeaders(options.headers, options.body),
    body: isJsonBody(options.body) ? JSON.stringify(options.body) : (options.body as BodyInit | null | undefined),
  });

  const payload = await parseResponsePayload(response);
  if (!response.ok) {
    throw new ApiError(resolveErrorMessage(response.status, payload), {
      status: response.status,
      title: response.statusText || 'Request failed',
      detail:
        payload && typeof payload === 'object'
          ? (payload as { detail?: string }).detail
          : undefined,
      payload,
    });
  }

  return payload as T;
};

export const requestVoid = async (path: string, options: ApiRequestOptions = {}): Promise<void> => {
  await requestJson(path, options);
};
