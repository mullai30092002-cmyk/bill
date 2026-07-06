import { isApiError } from '../../api/apiErrors';

const maxVisibleLength = 160;

const unsafeErrorPatterns = [
  /microsoft\.data\.sqlclient/i,
  /system\.data\.sqlclient/i,
  /\bsqlexception\b/i,
  /\bexception\b/i,
  /\binner exception\b/i,
  /\binvalid object name\b/i,
  /\binvalid column name\b/i,
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

  if (unsafeErrorPatterns.some(pattern => pattern.test(value))) {
    return false;
  }

  return true;
};

const clipVisibleText = (value: string) => {
  if (value.length <= maxVisibleLength) {
    return value;
  }

  return `${value.slice(0, maxVisibleLength - 1).trimEnd()}…`;
};

export interface BillingErrorMessages {
  sessionExpired: string;
  unauthorized: string;
}

const defaultMessages: BillingErrorMessages = {
  sessionExpired: 'Your session expired. Please sign in again.',
  unauthorized: 'You are not authorized to use Billing.',
};

export const getSafeBillingErrorMessage = (
  error: unknown,
  fallback: string,
  messages: BillingErrorMessages = defaultMessages
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
