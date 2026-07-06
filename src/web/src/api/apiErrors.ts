export interface ApiErrorOptions {
  status: number;
  title?: string;
  detail?: string;
  payload?: unknown;
}

export class ApiError extends Error {
  status: number;

  title?: string;

  detail?: string;

  payload?: unknown;

  constructor(message: string, options: ApiErrorOptions) {
    super(message);
    this.name = 'ApiError';
    this.status = options.status;
    this.title = options.title;
    this.detail = options.detail;
    this.payload = options.payload;
  }
}

export const isApiError = (error: unknown): error is ApiError => error instanceof ApiError;

const maxVisibleLength = 160;

const unsafeErrorPatterns = [
  /microsoft\.data\.sqlclient/i,
  /system\.data\.sqlclient/i,
  /\bsqlexception\b/i,
  /\binvalid object name\b/i,
  /\binvalid column name\b/i,
  /\bexception\b/i,
  /\binner exception\b/i,
  /\bstack trace\b/i,
  /\bat\s+\S+\.\S+\(/i,
  /<!doctype/i,
  /<html/i,
  /<\?xml/i,
  /\bbearer\s+[a-z0-9._-]+\b/i,
  /\beyj[a-z0-9._-]+\.[a-z0-9._-]+\.[a-z0-9._-]+\b/i,
];

const cleanVisibleText = (value: string) => value.replace(/\s+/g, ' ').trim();

const isSafeVisibleText = (value: string) => {
  if (!value) {
    return false;
  }

  return !unsafeErrorPatterns.some(pattern => pattern.test(value));
};

const clipVisibleText = (value: string) => {
  if (value.length <= maxVisibleLength) {
    return value;
  }

  return `${value.slice(0, maxVisibleLength - 1).trimEnd()}…`;
};

const isJsonLikeText = (value: string) => /^[\[{]/.test(value.trim());

const extractSafeText = (value: unknown, allowPlainText: boolean): string | undefined => {
  if (value == null) {
    return undefined;
  }

  if (typeof value === 'string') {
    const cleaned = cleanVisibleText(value);
    if (!cleaned) {
      return undefined;
    }

    if (isJsonLikeText(cleaned)) {
      try {
        return extractSafeText(JSON.parse(cleaned) as unknown, true);
      } catch {
        return undefined;
      }
    }

    if (!allowPlainText || !isSafeVisibleText(cleaned)) {
      return undefined;
    }

    return clipVisibleText(cleaned);
  }

  if (typeof value === 'object') {
    const typedValue = value as { detail?: unknown; title?: unknown };

    if (typedValue.detail !== undefined) {
      return extractSafeText(typedValue.detail, true);
    }

    if (typedValue.title !== undefined) {
      return extractSafeText(typedValue.title, true);
    }
  }

  return undefined;
};

export interface SafeApiErrorMessages {
  sessionExpired: string;
  unauthorized: string;
}

const defaultMessages: SafeApiErrorMessages = {
  sessionExpired: 'Your session expired. Please sign in again.',
  unauthorized: 'You are not authorized to make this change.',
};

export const getSafeApiErrorMessage = (
  error: unknown,
  fallback: string,
  messages: SafeApiErrorMessages = defaultMessages
): string => {
  if (isApiError(error)) {
    if (error.status === 401) {
      return messages.sessionExpired;
    }

    if (error.status === 403) {
      return messages.unauthorized;
    }

    const detail = extractSafeText(error.detail, true);
    if (detail) {
      return detail;
    }

    const payload = extractSafeText(error.payload, false);
    if (payload) {
      return payload;
    }

    const title = extractSafeText(error.title, true);
    if (title) {
      return title;
    }

    const message = extractSafeText(error.message, false);
    if (message) {
      return message;
    }

    return fallback;
  }

  const structuredMessage =
    error instanceof Error
      ? extractSafeText(error.message, false)
      : typeof error === 'string'
        ? extractSafeText(error, false)
        : undefined;

  return structuredMessage ?? fallback;
};
