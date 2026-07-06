import type { Dispatch, FormEvent, SetStateAction } from 'react';

import { Badge, Button, Input } from '../../../components/ui';
import { useLanguage } from '../../../i18n/LanguageProvider';
import { BranchSelect } from './BranchSelect';
import { RoleChecklist } from './RoleChecklist';
import {
  getCreatePasswordHelperText,
  type AdminCreateUserFormErrors,
  type AdminCreateUserFormState,
} from './adminUserFormValidation';
import type { AdminRoleListItem } from '../adminTypes';
import type { BranchSelectOption } from './adminUserDisplay';

interface CreateUserFormProps {
  form: AdminCreateUserFormState;
  errors: AdminCreateUserFormErrors;
  roles: AdminRoleListItem[];
  branchOptions: BranchSelectOption[];
  branchHelperText: string;
  branchDisabled: boolean;
  submitting: boolean;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
  onClear: () => void;
  setForm: Dispatch<SetStateAction<AdminCreateUserFormState>>;
  onToggleRole: (roleName: string) => void;
}

export const CreateUserForm = ({
  form,
  errors,
  roles,
  branchOptions,
  branchHelperText,
  branchDisabled,
  submitting,
  onSubmit,
  onClear,
  setForm,
  onToggleRole,
}: CreateUserFormProps) => {
  const { t } = useLanguage();

  const passwordHelperText = getCreatePasswordHelperText(form.roleNames, {
    privileged: t('adminUsers.passwordMinLengthPrivileged'),
    regular: t('adminUsers.passwordMinLengthRegular'),
  });

  return (
    <form className="admin-form" onSubmit={onSubmit}>
      <div className="admin-form-grid">
        <Input
          label={t('adminUsers.fieldFullName')}
          value={form.fullName}
          onChange={event => setForm(current => ({ ...current, fullName: event.target.value }))}
          error={errors.fullName}
          placeholder="Asha Kumar"
          autoComplete="name"
        />
        <Input
          label={t('adminUsers.fieldMobileNumber')}
          value={form.mobileNumber}
          onChange={event => setForm(current => ({ ...current, mobileNumber: event.target.value }))}
          error={errors.mobileNumber}
          placeholder="91234567"
          autoComplete="tel"
          inputMode="tel"
        />
      </div>

      <Input
        label={t('adminUsers.fieldEmail')}
        value={form.email}
        onChange={event => setForm(current => ({ ...current, email: event.target.value }))}
        error={errors.email}
        helperText={t('adminUsers.emailHelperText')}
        placeholder="asha@example.com"
        autoComplete="email"
      />

      <BranchSelect
        label={t('adminUsers.fieldBranchAssignment')}
        value={form.branchId ?? ''}
        onChange={event =>
          setForm(current => ({
            ...current,
            branchId: event.target.value || null,
          }))
        }
        helperText={branchHelperText}
        disabled={branchDisabled}
        options={branchOptions}
      />

      <Input
        label={t('adminUsers.fieldInitialPassword')}
        type="password"
        value={form.initialPassword}
        onChange={event => setForm(current => ({ ...current, initialPassword: event.target.value }))}
        error={errors.initialPassword}
        helperText={passwordHelperText}
        autoComplete="new-password"
      />

      <div className="admin-form-section">
        <div className="admin-form-section__title-row">
          <div>
            <div className="admin-form-section__title">{t('adminUsers.rolesTitle')}</div>
            <div className="admin-form-section__description">
              {t('adminUsers.rolesDescription')}
            </div>
          </div>
          <Badge tone="neutral" label={t('adminUsers.backendRolesBadge')} />
        </div>

        <RoleChecklist
          roles={roles}
          selectedRoleNames={form.roleNames}
          onToggleRole={onToggleRole}
          disabled={submitting}
          label="Create user roles"
        />
        {errors.roleNames ? <div className="admin-form-error">{errors.roleNames}</div> : null}
      </div>

      <div className="admin-form-actions">
        <Button type="submit" size="lg" disabled={submitting}>
          {submitting ? t('adminUsers.creatingButton') : t('adminUsers.createUser')}
        </Button>
        <Button type="button" variant="secondary" size="lg" onClick={onClear} disabled={submitting}>
          {t('adminUsers.clearButton')}
        </Button>
      </div>
    </form>
  );
};

export default CreateUserForm;
