import { getSafeApiErrorMessage, type SafeApiErrorMessages } from '../../api/apiErrors';

const defaultMessages: SafeApiErrorMessages = {
  sessionExpired: 'Your session expired. Please sign in again.',
  unauthorized: 'You are not authorized to make this change.',
};

export const getSafeInventoryErrorMessage = (
  error: unknown,
  fallback: string,
  messages: SafeApiErrorMessages = defaultMessages
) => getSafeApiErrorMessage(error, fallback, messages);
