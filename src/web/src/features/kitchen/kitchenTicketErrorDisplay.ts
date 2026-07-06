import { isApiError } from '../../api/apiErrors';

const maxVisibleLength = 160;

const unsafeErrorPatterns = [
  /microsoft\.data\.sqlclient/i,
  /system\.data\.sqlclient/i,
  /\bsqlexception\b/i,
  /\binvalid object name\b/i,
  /\binvalid column name\b/i,
  /\bstack trace\b/i,
  /\bexception\b/i,
  /\binner exception\b/i,
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

export interface KitchenTicketErrorMessages {
  sessionExpired: string;
  unauthorized: string;
}

const defaultMessages: KitchenTicketErrorMessages = {
  sessionExpired: 'Your session expired. Please sign in again.',
  unauthorized: 'You are not authorized to use kitchen tickets.',
};

export const getSafeKitchenTicketErrorMessage = (
  error: unknown,
  fallback: string,
  messages: KitchenTicketErrorMessages = defaultMessages
): string => {
  if (isApiError(error)) {
    if (error.status === 401) {
      return messages.sessionExpired;
    }

    if (error.status === 403) {
      return messages.unauthorized;
    }

    if (error.status >= 400 && error.status < 500) {
      const candidate = cleanVisibleText(error.detail ?? error.message ?? '');
      if (!candidate || /^Request failed \(\d+\)$/i.test(candidate)) {
        return fallback;
      }

      if (isSafeVisibleText(candidate)) {
        return clipVisibleText(candidate);
      }
    }

    return fallback;
  }

  const candidate =
    typeof error === 'string'
      ? cleanVisibleText(error)
      : error instanceof Error
        ? cleanVisibleText(error.message)
        : '';

  if (!candidate || !isSafeVisibleText(candidate)) {
    return fallback;
  }

  return clipVisibleText(candidate);
};
