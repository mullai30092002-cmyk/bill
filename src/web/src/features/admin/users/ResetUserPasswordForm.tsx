import type { Dispatch, FormEvent, SetStateAction } from 'react';

import { Button, Card, Input } from '../../../components/ui';
import { useLanguage } from '../../../i18n/LanguageProvider';
import type {
  AdminResetPasswordFormErrors,
  AdminResetPasswordFormState,
} from './adminUserFormValidation';
import { getResetPasswordHelperText } from './adminUserFormValidation';

interface ResetUserPasswordFormProps {
  form: AdminResetPasswordFormState;
  errors: AdminResetPasswordFormErrors;
  targetUserName: string;
  targetUserMobile: string;
  roleNames: string[];
  submitting: boolean;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
  onCancel: () => void;
  setForm: Dispatch<SetStateAction<AdminResetPasswordFormState>>;
}

export const ResetUserPasswordForm = ({
  form,
  errors,
  targetUserName,
  targetUserMobile,
  roleNames,
  submitting,
  onSubmit,
  onCancel,
  setForm,
}: ResetUserPasswordFormProps) => {
  const { t } = useLanguage();

  const passwordHelperText = getResetPasswordHelperText(roleNames, {
    privileged: t('adminUsers.resetPasswordMinLengthPrivileged'),
    regular: t('adminUsers.resetPasswordMinLengthRegular'),
  });

  return (
    <div className="admin-password-reset-dialog" role="dialog" aria-modal="true" aria-label={t('adminUsers.resetPasswordDialogAria')}>
      <Card
        title={t('adminUsers.resetPasswordCardTitle')}
        description={t('adminUsers.resetPasswordCardDescription')}
        tone="admin"
      >
        <form className="admin-form" onSubmit={onSubmit}>
          <div className="admin-form-note">
            <div>
              <strong>{targetUserName}</strong>
            </div>
            <div>{targetUserMobile}</div>
          </div>

          <div className="admin-form-note">
            {t('adminUsers.resetPasswordShareNote')}
          </div>

          <div className="admin-form-grid">
            <Input
              label={t('adminUsers.fieldNewPassword')}
              type="password"
              value={form.newPassword}
              onChange={event => setForm(current => ({ ...current, newPassword: event.target.value }))}
              error={errors.newPassword}
              helperText={passwordHelperText}
              autoComplete="new-password"
            />
            <Input
              label={t('adminUsers.fieldConfirmPassword')}
              type="password"
              value={form.confirmPassword}
              onChange={event => setForm(current => ({ ...current, confirmPassword: event.target.value }))}
              error={errors.confirmPassword}
              helperText={t('adminUsers.confirmPasswordHelperText')}
              autoComplete="new-password"
            />
          </div>

          <div className="admin-form-actions">
            <Button type="submit" size="lg" disabled={submitting}>
              {submitting ? t('adminUsers.resettingButton') : t('adminUsers.submitResetButton')}
            </Button>
            <Button type="button" variant="secondary" size="lg" onClick={onCancel} disabled={submitting}>
              {t('adminUsers.cancelButton')}
            </Button>
          </div>
        </form>
      </Card>
    </div>
  );
};

export default ResetUserPasswordForm;
