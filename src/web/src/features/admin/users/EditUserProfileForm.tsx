import type { Dispatch, FormEvent, SetStateAction } from 'react';

import { Button, Input, Select } from '../../../components/ui';
import { useLanguage } from '../../../i18n/LanguageProvider';
import { BranchSelect } from './BranchSelect';
import type { AdminProfileFormErrors, AdminProfileFormState } from './adminUserFormValidation';
import type { BranchSelectOption } from './adminUserDisplay';

interface EditUserProfileFormProps {
  form: AdminProfileFormState;
  errors: AdminProfileFormErrors;
  branchOptions: BranchSelectOption[];
  branchHelperText: string;
  branchDisabled: boolean;
  submitting: boolean;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
  setForm: Dispatch<SetStateAction<AdminProfileFormState>>;
}

export const EditUserProfileForm = ({
  form,
  errors,
  branchOptions,
  branchHelperText,
  branchDisabled,
  submitting,
  onSubmit,
  setForm,
}: EditUserProfileFormProps) => {
  const { t } = useLanguage();

  return (
    <form className="admin-form" onSubmit={onSubmit}>
      <div className="admin-form-grid">
        <Input
          label={t('adminUsers.fieldFullName')}
          value={form.fullName}
          onChange={event => setForm(current => ({ ...current, fullName: event.target.value }))}
          error={errors.fullName}
          autoComplete="name"
        />
        <Input
          label={t('adminUsers.fieldMobileNumber')}
          value={form.mobileNumber}
          onChange={event => setForm(current => ({ ...current, mobileNumber: event.target.value }))}
          error={errors.mobileNumber}
          autoComplete="tel"
          inputMode="tel"
        />
      </div>

      <Input
        label={t('adminUsers.fieldEmail')}
        value={form.email}
        onChange={event => setForm(current => ({ ...current, email: event.target.value }))}
        error={errors.email}
        helperText={t('adminUsers.emailEditHelperText')}
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

      <Select
        label={t('adminUsers.fieldStatus')}
        value={form.status}
        onChange={event =>
          setForm(current => ({
            ...current,
            status: event.target.value as AdminProfileFormState['status'],
          }))
        }
        error={errors.status}
        helperText={t('adminUsers.statusHelperText')}
      >
        <option value="Active">{t('adminUsers.statusActive')}</option>
        <option value="Inactive">{t('adminUsers.statusInactive')}</option>
        <option value="Locked">{t('adminUsers.statusLocked')}</option>
      </Select>

      <div className="admin-form-actions">
        <Button type="submit" size="lg" disabled={submitting}>
          {submitting ? t('adminUsers.savingButton') : t('adminUsers.saveProfileButton')}
        </Button>
      </div>
    </form>
  );
};

export default EditUserProfileForm;
