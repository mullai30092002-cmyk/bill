import type { AdminUserStatus } from '../adminTypes';
import { privilegedAdminRoleNames } from '../adminTypes';

export interface AdminCreateUserFormState {
  branchId: string | null;
  fullName: string;
  mobileNumber: string;
  email: string;
  initialPassword: string;
  roleNames: string[];
}

export interface AdminProfileFormState {
  fullName: string;
  mobileNumber: string;
  email: string;
  status: AdminUserStatus;
  branchId: string | null;
}

export interface AdminCreateUserFormErrors {
  fullName?: string;
  mobileNumber?: string;
  email?: string;
  initialPassword?: string;
  roleNames?: string;
}

export interface AdminProfileFormErrors {
  fullName?: string;
  mobileNumber?: string;
  email?: string;
  status?: string;
}

export interface AdminResetPasswordFormState {
  newPassword: string;
  confirmPassword: string;
}

export interface AdminResetPasswordFormErrors {
  newPassword?: string;
  confirmPassword?: string;
}

export const emptyCreateUserForm = (): AdminCreateUserFormState => ({
  branchId: null,
  fullName: '',
  mobileNumber: '',
  email: '',
  initialPassword: '',
  roleNames: [],
});

export const emptyProfileForm = (): AdminProfileFormState => ({
  fullName: '',
  mobileNumber: '',
  email: '',
  status: 'Active',
  branchId: null,
});

export const emptyResetPasswordForm = (): AdminResetPasswordFormState => ({
  newPassword: '',
  confirmPassword: '',
});

export const isPrivilegedRole = (roleName: string) =>
  privilegedAdminRoleNames.some(candidate => candidate.toLowerCase() === roleName.toLowerCase());

export const hasPrivilegedSelection = (roleNames: string[]) => roleNames.some(isPrivilegedRole);

export const getPasswordMinimumLength = (roleNames: string[]) =>
  hasPrivilegedSelection(roleNames) ? 12 : 8;

export const getCreatePasswordHelperText = (roleNames: string[], messages?: { privileged: string; regular: string }) =>
  hasPrivilegedSelection(roleNames)
    ? (messages?.privileged ?? 'Minimum 12 characters because one or more privileged roles are selected.')
    : (messages?.regular ?? 'Minimum 8 characters for regular staff accounts.');

export const getResetPasswordHelperText = (roleNames: string[], messages?: { privileged: string; regular: string }) =>
  hasPrivilegedSelection(roleNames)
    ? (messages?.privileged ?? 'Minimum 12 characters because one or more privileged roles are assigned.')
    : (messages?.regular ?? 'Minimum 8 characters for this staff account.');

export const normalizeOptionalText = (value: string) => {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
};

export interface ValidationMessages {
  fullNameRequired: string;
  mobileRequired: string;
  statusRequired: string;
  selectAtLeastOneRole: string;
  initialPasswordMinLength: (n: number) => string;
  newPasswordMinLength: (n: number) => string;
  confirmPassword: string;
  passwordsMismatch: string;
}

const defaultMessages: ValidationMessages = {
  fullNameRequired: 'Full name is required.',
  mobileRequired: 'Mobile number is required.',
  statusRequired: 'Status is required.',
  selectAtLeastOneRole: 'Select at least one role.',
  initialPasswordMinLength: (n: number) => `Initial password must be at least ${n} characters long.`,
  newPasswordMinLength: (n: number) => `New password must be at least ${n} characters long.`,
  confirmPassword: 'Confirm the new password.',
  passwordsMismatch: 'Passwords do not match.',
};

export const buildCreateUserFormErrors = (
  form: AdminCreateUserFormState,
  messages: ValidationMessages = defaultMessages
): AdminCreateUserFormErrors => {
  const errors: AdminCreateUserFormErrors = {};

  if (!form.fullName.trim()) {
    errors.fullName = messages.fullNameRequired;
  }

  if (!form.mobileNumber.trim()) {
    errors.mobileNumber = messages.mobileRequired;
  }

  const minLength = getPasswordMinimumLength(form.roleNames);
  if (!form.initialPassword.trim()) {
    errors.initialPassword = messages.initialPasswordMinLength(minLength);
  } else if (form.initialPassword.trim().length < minLength) {
    errors.initialPassword = messages.initialPasswordMinLength(minLength);
  }

  if (form.roleNames.length === 0) {
    errors.roleNames = messages.selectAtLeastOneRole;
  }

  return errors;
};

export const buildProfileFormErrors = (
  form: AdminProfileFormState,
  messages: ValidationMessages = defaultMessages
): AdminProfileFormErrors => {
  const errors: AdminProfileFormErrors = {};

  if (!form.fullName.trim()) {
    errors.fullName = messages.fullNameRequired;
  }

  if (!form.mobileNumber.trim()) {
    errors.mobileNumber = messages.mobileRequired;
  }

  if (!form.status.trim()) {
    errors.status = messages.statusRequired;
  }

  return errors;
};

export const buildResetPasswordFormErrors = (
  form: AdminResetPasswordFormState,
  roleNames: string[],
  messages: ValidationMessages = defaultMessages
): AdminResetPasswordFormErrors => {
  const errors: AdminResetPasswordFormErrors = {};
  const minimumLength = getPasswordMinimumLength(roleNames);

  if (!form.newPassword.trim()) {
    errors.newPassword = messages.newPasswordMinLength(minimumLength);
  } else if (form.newPassword.length < minimumLength) {
    errors.newPassword = messages.newPasswordMinLength(minimumLength);
  }

  if (!form.confirmPassword.trim()) {
    errors.confirmPassword = messages.confirmPassword;
  } else if (form.confirmPassword !== form.newPassword) {
    errors.confirmPassword = messages.passwordsMismatch;
  }

  return errors;
};
