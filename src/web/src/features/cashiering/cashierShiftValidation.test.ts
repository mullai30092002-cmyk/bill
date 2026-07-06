import { describe, expect, it } from 'vitest';

import {
  buildCashierShiftCloseRequest,
  buildCashierShiftCloseValidationErrors,
  buildCashierShiftOpenRequest,
  buildCashierShiftOpenValidationErrors,
  getCashierShiftVariancePreview,
} from './cashierShiftValidation';

describe('cashierShiftValidation', () => {
  it('validates the open shift form', () => {
    const errors = buildCashierShiftOpenValidationErrors({ businessDate: '', openingCashAmount: '-1' });

    expect(errors.businessDate).toBe('Business date is required.');
    expect(errors.openingCashAmount).toBe('Opening cash amount must be greater than or equal to 0.');
  });

  it('builds the open shift request payload', () => {
    expect(
      buildCashierShiftOpenRequest('branch-1', {
        businessDate: '2026-06-13',
        openingCashAmount: ' 100.25 ',
      })
    ).toEqual({
      branchId: 'branch-1',
      businessDate: '2026-06-13',
      openingCashAmount: 100.25,
    });
  });

  it('validates the close shift form', () => {
    const errors = buildCashierShiftCloseValidationErrors({ declaredClosingCashAmount: '', closeNotes: '' });

    expect(errors.declaredClosingCashAmount).toBe('Declared closing cash is required.');
  });

  it('builds the close shift request payload and variance preview', () => {
    expect(
      buildCashierShiftCloseRequest({
        declaredClosingCashAmount: ' 130.10 ',
        closeNotes: ' End of day ',
      })
    ).toEqual({
      declaredClosingCashAmount: 130.1,
      closeNotes: 'End of day',
    });

    expect(getCashierShiftVariancePreview(100, '130.10')).toBe(30.1);
    expect(getCashierShiftVariancePreview(100, '')).toBeNull();
  });
});
