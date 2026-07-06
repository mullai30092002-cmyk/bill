import type { CreateAdminBranchRequest, UpdateAdminBranchRequest } from '../adminTypes';

export interface BranchFormState {
  name: string;
  address: string;
  phone: string;
  timezone: string;
  currency: string;
}

export interface BranchFormErrors {
  name?: string;
  timezone?: string;
  currency?: string;
}

export const emptyBranchForm = (): BranchFormState => ({
  name: '',
  address: '',
  phone: '',
  timezone: 'Asia/Singapore',
  currency: 'INR',
});

export const normalizeOptionalText = (value: string) => {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
};

export const buildBranchFormErrors = (form: BranchFormState): BranchFormErrors => {
  const errors: BranchFormErrors = {};

  if (!form.name.trim()) {
    errors.name = 'Name is required.';
  }

  if (!form.timezone.trim()) {
    errors.timezone = 'Timezone is required.';
  }

  if (!form.currency.trim()) {
    errors.currency = 'Currency is required.';
  }

  return errors;
};

export const buildCreateBranchRequest = (form: BranchFormState): CreateAdminBranchRequest => ({
  name: form.name.trim(),
  address: normalizeOptionalText(form.address),
  phone: normalizeOptionalText(form.phone),
  timezone: form.timezone.trim(),
  currency: form.currency.trim().toUpperCase(),
});

export const buildUpdateBranchRequest = (form: BranchFormState): UpdateAdminBranchRequest => ({
  name: form.name.trim(),
  address: normalizeOptionalText(form.address),
  phone: normalizeOptionalText(form.phone),
  timezone: form.timezone.trim(),
  currency: form.currency.trim().toUpperCase(),
});
