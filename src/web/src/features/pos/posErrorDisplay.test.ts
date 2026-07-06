import { describe, expect, it } from 'vitest';

import { getSafePosErrorMessage } from './posErrorDisplay';
import { ApiError } from '../../api/apiErrors';

describe('getSafePosErrorMessage', () => {
  it('returns the fallback for backend exception text', () => {
    const error = new Error('Microsoft.Data.SqlClient.SqlException: invalid column name');

    expect(getSafePosErrorMessage(error, 'Unable to load recent orders. Please refresh or try again.')).toBe(
      'Unable to load recent orders. Please refresh or try again.'
    );
  });

  it('returns a short clean api validation message', () => {
    const error = new ApiError('Branch must belong to the current restaurant.', {
      status: 400,
    });

    expect(getSafePosErrorMessage(error, 'Fallback')).toBe('Branch must belong to the current restaurant.');
  });
});
