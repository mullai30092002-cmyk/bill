import type {
  CloseCashierShiftRequest,
  OpenCashierShiftRequest,
} from './cashierShiftTypes';

export interface CashierShiftOpenFormState {
  businessDate: string;
  openingCashAmount: string;
}

export interface CashierShiftCloseFormState {
  declaredClosingCashAmount: string;
  closeNotes: string;
}

export interface CashierShiftOpenValidationErrors {
  businessDate?: string;
  openingCashAmount?: string;
}

export interface CashierShiftCloseValidationErrors {
  declaredClosingCashAmount?: string;
}

export interface CashierShiftValidationMessages {
  businessDateRequired: string;
  openingCashAmountRequired: string;
  openingCashAmountInvalid: string;
  openingCashAmountTooLow: string;
  declaredClosingCashRequired: string;
  declaredClosingCashInvalid: string;
  declaredClosingCashTooLow: string;
}

const defaultMessages: CashierShiftValidationMessages = {
  businessDateRequired: 'Business date is required.',
  openingCashAmountRequired: 'Opening cash amount is required.',
  openingCashAmountInvalid: 'Opening cash amount must be a valid number.',
  openingCashAmountTooLow: 'Opening cash amount must be greater than or equal to 0.',
  declaredClosingCashRequired: 'Declared closing cash is required.',
  declaredClosingCashInvalid: 'Declared closing cash must be a valid number.',
  declaredClosingCashTooLow: 'Declared closing cash must be greater than or equal to 0.',
};

const parseAmount = (value?: string | null) => {
  const trimmed = value?.trim() ?? '';
  if (!trimmed) {
    return Number.NaN;
  }

  if (!/^[+-]?(?:\d+(?:\.\d+)?|\.\d+)$/.test(trimmed)) {
    return Number.NaN;
  }

  return Number.parseFloat(trimmed);
};

export const roundCash = (value: number) => Math.round(value * 100) / 100;

export const normalizeOptionalText = (value: string) => {
  const trimmed = value.trim();
  return trimmed ? trimmed : null;
};

export const buildCashierShiftOpenValidationErrors = (
  form: CashierShiftOpenFormState,
  messages: CashierShiftValidationMessages = defaultMessages
): CashierShiftOpenValidationErrors => {
  const errors: CashierShiftOpenValidationErrors = {};

  if (!/^\d{4}-\d{2}-\d{2}$/.test(form.businessDate.trim())) {
    errors.businessDate = messages.businessDateRequired;
  }

  const amount = parseAmount(form.openingCashAmount);
  if (!Number.isFinite(amount)) {
    errors.openingCashAmount = !form.openingCashAmount.trim()
      ? messages.openingCashAmountRequired
      : messages.openingCashAmountInvalid;
  } else if (amount < 0) {
    errors.openingCashAmount = messages.openingCashAmountTooLow;
  }

  return errors;
};

export const buildCashierShiftCloseValidationErrors = (
  form: CashierShiftCloseFormState,
  messages: CashierShiftValidationMessages = defaultMessages
): CashierShiftCloseValidationErrors => {
  const amount = parseAmount(form.declaredClosingCashAmount);
  if (!Number.isFinite(amount)) {
    return {
      declaredClosingCashAmount: !form.declaredClosingCashAmount.trim()
        ? messages.declaredClosingCashRequired
        : messages.declaredClosingCashInvalid,
    };
  }

  if (amount < 0) {
    return {
      declaredClosingCashAmount: messages.declaredClosingCashTooLow,
    };
  }

  return {};
};

export const buildCashierShiftOpenRequest = (
  branchId: string,
  form: CashierShiftOpenFormState
): OpenCashierShiftRequest => ({
  branchId,
  businessDate: form.businessDate.trim(),
  openingCashAmount: roundCash(Number.parseFloat(form.openingCashAmount.trim())),
});

export const buildCashierShiftCloseRequest = (
  form: CashierShiftCloseFormState
): CloseCashierShiftRequest => ({
  declaredClosingCashAmount: roundCash(Number.parseFloat(form.declaredClosingCashAmount.trim())),
  closeNotes: normalizeOptionalText(form.closeNotes),
});

export const getCashierShiftVariancePreview = (expectedClosingCashAmount: number, declaredClosingCashAmount: string) => {
  const amount = parseAmount(declaredClosingCashAmount);
  if (!Number.isFinite(amount)) {
    return null;
  }

  return roundCash(amount - expectedClosingCashAmount);
};
