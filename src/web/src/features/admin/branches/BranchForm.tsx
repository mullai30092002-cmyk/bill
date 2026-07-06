import type { Dispatch, FormEvent, SetStateAction } from 'react';
import type { RefObject } from 'react';

import { Button, Input } from '../../../components/ui';
import { useLanguage } from '../../../i18n/LanguageProvider';
import type { BranchFormErrors, BranchFormState } from './branchFormValidation';

export interface BranchFormProps {
  mode: 'create' | 'edit';
  form: BranchFormState;
  errors: BranchFormErrors;
  submitting: boolean;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
  onSecondaryAction: () => void;
  secondaryActionLabel: string;
  nameInputRef?: RefObject<HTMLInputElement | null>;
  setForm: Dispatch<SetStateAction<BranchFormState>>;
}

export const BranchForm = ({
  mode,
  form,
  errors,
  submitting,
  onSubmit,
  onSecondaryAction,
  secondaryActionLabel,
  nameInputRef,
  setForm,
}: BranchFormProps) => {
  const { t } = useLanguage();

  return (
    <form className="admin-form" onSubmit={onSubmit}>
      <div className="admin-form-grid">
        <Input
          label={t('branches.fieldName')}
          ref={nameInputRef}
          value={form.name}
          onChange={event => setForm(current => ({ ...current, name: event.target.value }))}
          error={errors.name}
          placeholder="Main Outlet"
          autoComplete="organization"
        />
        <Input
          label={t('branches.fieldPhone')}
          value={form.phone}
          onChange={event => setForm(current => ({ ...current, phone: event.target.value }))}
          autoComplete="tel"
          inputMode="tel"
          placeholder="60000001"
          helperText={t('branches.phoneHelperText')}
        />
      </div>

      <Input
        label={t('branches.fieldAddress')}
        value={form.address}
        onChange={event => setForm(current => ({ ...current, address: event.target.value }))}
        autoComplete="street-address"
        placeholder="123 Market Street"
        helperText={t('branches.addressHelperText')}
      />

      <div className="admin-form-grid">
        <Input
          label={t('branches.fieldTimezone')}
          value={form.timezone}
          onChange={event => setForm(current => ({ ...current, timezone: event.target.value }))}
          error={errors.timezone}
          placeholder="Asia/Singapore"
          helperText={t('branches.timezoneHelperText')}
        />
        <Input
          label={t('branches.fieldCurrency')}
          value={form.currency}
          onChange={event =>
            setForm(current => ({
              ...current,
              currency: event.target.value.toUpperCase(),
            }))
          }
          error={errors.currency}
          placeholder="INR"
          helperText={t('branches.currencyHelperText')}
          autoCapitalize="characters"
          autoCorrect="off"
          spellCheck={false}
        />
      </div>

      <div className="admin-form-note">
        {mode === 'create'
          ? t('branches.createNote')
          : t('branches.editNote')}
      </div>

      <div className="admin-form-actions">
        <Button type="submit" size="lg" disabled={submitting}>
          {submitting
            ? mode === 'create' ? t('branches.creatingButton') : t('branches.savingButton')
            : mode === 'create' ? t('branches.createBranchButton') : t('branches.saveChangesButton')}
        </Button>
        <Button type="button" variant="secondary" size="lg" onClick={onSecondaryAction} disabled={submitting}>
          {secondaryActionLabel}
        </Button>
      </div>
    </form>
  );
};

export default BranchForm;
