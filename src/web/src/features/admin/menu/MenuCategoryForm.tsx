import type { Dispatch, FormEvent, SetStateAction } from 'react';
import type { RefObject } from 'react';

import { Button, Input } from '../../../components/ui';
import { useLanguage } from '../../../i18n/LanguageProvider';
import type { MenuCategoryFormErrors, MenuCategoryFormState } from './menuFormValidation';

export interface MenuCategoryFormProps {
  mode: 'create' | 'edit';
  form: MenuCategoryFormState;
  errors: MenuCategoryFormErrors;
  submitting: boolean;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
  onSecondaryAction: () => void;
  secondaryActionLabel: string;
  nameInputRef?: RefObject<HTMLInputElement | null>;
  setForm: Dispatch<SetStateAction<MenuCategoryFormState>>;
}

export const MenuCategoryForm = ({
  mode,
  form,
  errors,
  submitting,
  onSubmit,
  onSecondaryAction,
  secondaryActionLabel,
  nameInputRef,
  setForm,
}: MenuCategoryFormProps) => {
  const { t } = useLanguage();

  return (
    <form className="admin-form" onSubmit={onSubmit}>
      <Input
        label={t('menu.categoryNameLabel')}
        ref={nameInputRef}
        value={form.name}
        onChange={event => setForm(current => ({ ...current, name: event.target.value }))}
        error={errors.name}
        placeholder="Breakfast"
        autoComplete="organization"
        helperText={t('menu.categoryNameHelper')}
      />

      <Input
        label={t('menu.displayOrderLabel')}
        type="number"
        min={0}
        step={1}
        inputMode="numeric"
        value={form.displayOrder}
        onChange={event => setForm(current => ({ ...current, displayOrder: event.target.value }))}
        error={errors.displayOrder}
        placeholder="0"
        helperText={t('menu.displayOrderHelper')}
      />

      <div className="admin-form-note">
        {mode === 'create'
          ? t('menu.categoryCreateNote')
          : t('menu.categoryEditNote')}
      </div>

      <div className="admin-form-actions">
        <Button type="submit" size="lg" disabled={submitting}>
          {submitting
            ? t('menu.creatingButton')
            : mode === 'create' ? t('menu.createCategoryButton') : t('menu.saveChangesButton')}
        </Button>
        <Button type="button" variant="secondary" size="lg" onClick={onSecondaryAction} disabled={submitting}>
          {secondaryActionLabel}
        </Button>
      </div>
    </form>
  );
};

export default MenuCategoryForm;
