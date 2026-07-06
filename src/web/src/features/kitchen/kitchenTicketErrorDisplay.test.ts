import { describe, expect, it } from 'vitest';

import { ApiError } from '../../api/apiErrors';
import { getSafeKitchenTicketErrorMessage } from './kitchenTicketErrorDisplay';

describe('getSafeKitchenTicketErrorMessage', () => {
  it('sanitizes SQL-like backend errors', () => {
    const error = new ApiError('Microsoft.Data.SqlClient.SqlException: invalid column name', {
      status: 500,
      title: 'Server Error',
      detail: 'Microsoft.Data.SqlClient.SqlException: invalid column name',
    });

    expect(getSafeKitchenTicketErrorMessage(error, 'Unable to load kitchen tickets. Please refresh or try again.')).toBe(
      'Unable to load kitchen tickets. Please refresh or try again.'
    );
  });

  it('allows safe validation messages from the backend', () => {
    const error = new ApiError('Cancel reason is required.', {
      status: 400,
      title: 'Bad Request',
      detail: 'Cancel reason is required.',
    });

    expect(getSafeKitchenTicketErrorMessage(error, 'Fallback')).toBe('Cancel reason is required.');
  });
});

