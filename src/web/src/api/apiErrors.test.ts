import { describe, expect, it } from 'vitest';

import { ApiError, getSafeApiErrorMessage } from './apiErrors';

describe('getSafeApiErrorMessage', () => {
  it('returns the RFC7807 detail from an ApiError', () => {
    const error = new ApiError('Request failed (400)', {
      status: 400,
      detail: 'SKU already exists. Please enter a unique SKU.',
    });

    expect(getSafeApiErrorMessage(error, 'Fallback message')).toBe(
      'SKU already exists. Please enter a unique SKU.'
    );
  });

  it('parses the RFC7807 detail from a JSON problem details string', () => {
    const problem = JSON.stringify({
      type: 'https://datatracker.ietf.org/doc/html/rfc7807',
      title: 'Bad Request',
      status: 400,
      detail: 'Branch name already exists in this restaurant.',
    });
    const error = new ApiError(problem, {
      status: 400,
      payload: problem,
    });

    expect(getSafeApiErrorMessage(error, 'Fallback message')).toBe(
      'Branch name already exists in this restaurant.'
    );
  });

  it('uses the RFC7807 title when detail is missing', () => {
    const error = new ApiError('Request failed (400)', {
      status: 400,
      title: 'Category already exists.',
    });

    expect(getSafeApiErrorMessage(error, 'Fallback message')).toBe('Category already exists.');
  });

  it('falls back when the RFC7807 payload has no detail or title', () => {
    const error = new ApiError('Request failed (400)', {
      status: 400,
    });

    expect(getSafeApiErrorMessage(error, 'Fallback message')).toBe('Fallback message');
  });

  it('falls back for unknown non-API errors', () => {
    expect(getSafeApiErrorMessage(new Error('Unexpected boom'), 'Fallback message')).toBe(
      'Fallback message'
    );
  });
});
