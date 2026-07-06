import { describe, expect, it } from 'vitest';

import { roundMoney } from './billingDisplay';
import { ApiError } from '../../api/apiErrors';
import { buildBillingPaymentValidationErrors } from './billingValidation';
import { getSafeBillingErrorMessage } from './billingErrorDisplay';

describe('buildBillingPaymentValidationErrors', () => {
  it('rejects empty, invalid, and non-positive payment amounts', () => {
    expect(buildBillingPaymentValidationErrors({ amount: '' }, 125).amount).toBe('Amount is required.');
    expect(buildBillingPaymentValidationErrors({ amount: 'abc' }, 125).amount).toBe('Amount must be a valid number.');
    expect(buildBillingPaymentValidationErrors({ amount: '0' }, 125).amount).toBe('Amount must be greater than 0.');
    expect(buildBillingPaymentValidationErrors({ amount: '-5' }, 125).amount).toBe('Amount must be greater than 0.');
  });

  it('rejects payment amounts that exceed the selected balance due', () => {
    expect(buildBillingPaymentValidationErrors({ amount: '125.01' }, 125).amount).toBe(
      'Amount must not exceed the selected bill balance due.'
    );
  });

  it('accepts decimal values that normalize to an in-range cent amount', () => {
    expect(buildBillingPaymentValidationErrors({ amount: '1.005' }, 1.01)).toEqual({});
  });

  it('blocks values that are still above the balance due before any cent rounding can help', () => {
    expect(buildBillingPaymentValidationErrors({ amount: '10.004' }, 10)).toMatchObject({
      amount: 'Amount must not exceed the selected bill balance due.',
    });
  });

  it('requires a reference number for UPI and Card payments', () => {
    expect(
      buildBillingPaymentValidationErrors(
        { amount: '25', paymentMode: 'Upi', referenceNumber: '' } as never,
        25
      ).referenceNumber
    ).toBe('Reference number is required for UPI and Card payments.');

    expect(
      buildBillingPaymentValidationErrors(
        { amount: '25', paymentMode: 'Card', referenceNumber: '   ' } as never,
        25
      ).referenceNumber
    ).toBe('Reference number is required for UPI and Card payments.');
  });
});

describe('roundMoney', () => {
  it('rounds money using half-away-from-zero semantics for display helpers', () => {
    expect(roundMoney(1.005)).toBe(1.01);
    expect(roundMoney(-1.005)).toBe(-1.01);
    expect(roundMoney(2.335)).toBe(2.34);
    expect(roundMoney(10.075)).toBe(10.08);
    expect(roundMoney(1e-7)).toBe(0);
    expect(roundMoney(1e3)).toBe(1000);
  });
});

describe('getSafeBillingErrorMessage', () => {
  it('returns a safe short validation message from a 4xx api error', () => {
    const error = new ApiError('Amount must be greater than zero.', {
      status: 400,
    });

    expect(getSafeBillingErrorMessage(error, 'Fallback')).toBe('Amount must be greater than zero.');
  });

  it('returns the auth-specific fallback for 401 and 403 responses', () => {
    expect(getSafeBillingErrorMessage(new ApiError('Unauthorized', { status: 401 }), 'Fallback')).toBe(
      'Your session expired. Please sign in again.'
    );
    expect(getSafeBillingErrorMessage(new ApiError('Forbidden', { status: 403 }), 'Fallback')).toBe(
      'You are not authorized to use Billing.'
    );
  });

  it('clips long but safe validation messages', () => {
    const longMessage = `Payment note accepted ${'x'.repeat(220)}`;
    const message = getSafeBillingErrorMessage(new ApiError(longMessage, { status: 400 }), 'Fallback');

    expect(message.length).toBeLessThanOrEqual(160);
    expect(message.endsWith('…')).toBe(true);
  });

  it('returns the fallback for sql-like exception text', () => {
    const error = new Error('Microsoft.Data.SqlClient.SqlException: invalid column name');

    expect(getSafeBillingErrorMessage(error, 'Unable to save payment. Please try again.')).toBe(
      'Unable to save payment. Please try again.'
    );
  });

  it('returns the fallback for generic exception-style text', () => {
    const error = new Error('Unhandled exception while saving payment');

    expect(getSafeBillingErrorMessage(error, 'Unable to save payment. Please try again.')).toBe(
      'Unable to save payment. Please try again.'
    );
  });
});
