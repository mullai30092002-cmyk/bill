export interface BillingPaymentValidationForm {
  amount?: string | null;
  paymentMode?: string | null;
  referenceNumber?: string | null;
}

export interface BillingPaymentValidationErrors {
  amount?: string;
  referenceNumber?: string;
}

export interface BillingPaymentValidationMessages {
  amountRequired: string;
  amountInvalid: string;
  amountTooLow: string;
  amountTooHigh: string;
  referenceNumberRequired: string;
}

const defaultMessages: BillingPaymentValidationMessages = {
  amountRequired: 'Amount is required.',
  amountInvalid: 'Amount must be a valid number.',
  amountTooLow: 'Amount must be greater than 0.',
  amountTooHigh: 'Amount must not exceed the selected bill balance due.',
  referenceNumberRequired: 'Reference number is required for UPI and Card payments.',
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

export const buildBillingPaymentValidationErrors = (
  form: BillingPaymentValidationForm,
  balanceDue: number,
  messages: BillingPaymentValidationMessages = defaultMessages
): BillingPaymentValidationErrors => {
  const amount = parseAmount(form.amount);
  if (!Number.isFinite(amount)) {
    return {
      amount: !form.amount?.trim() ? messages.amountRequired : messages.amountInvalid,
    };
  }

  if (amount <= 0) {
    return {
      amount: messages.amountTooLow,
    };
  }

  // Keep the local guard aligned with the backend's exact over-balance rule.
  if (amount > balanceDue) {
    return {
      amount: messages.amountTooHigh,
    };
  }

  const paymentMode = form.paymentMode?.trim().toLowerCase() ?? '';
  if ((paymentMode === 'upi' || paymentMode === 'card') && !form.referenceNumber?.trim()) {
    return {
      referenceNumber: messages.referenceNumberRequired,
    };
  }

  return {};
};
