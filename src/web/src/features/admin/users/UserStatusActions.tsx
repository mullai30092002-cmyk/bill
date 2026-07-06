import { Button, StatusBadge } from '../../../components/ui';
import { useLanguage } from '../../../i18n/LanguageProvider';
import type { AdminUserStatus } from '../adminTypes';

interface UserStatusActionsProps {
  status: AdminUserStatus;
  isCurrentSession: boolean;
  confirmDeactivate: boolean;
  submitting: boolean;
  onActivate: () => void;
  onRequestDeactivate: () => void;
  onConfirmDeactivate: () => void;
  onCancelDeactivate: () => void;
}

export const UserStatusActions = ({
  status,
  isCurrentSession,
  confirmDeactivate,
  submitting,
  onActivate,
  onRequestDeactivate,
  onConfirmDeactivate,
  onCancelDeactivate,
}: UserStatusActionsProps) => {
  const { t } = useLanguage();

  return confirmDeactivate ? (
    <div className="admin-confirmation">
      <div className="admin-confirmation__copy">
        <strong>{t('adminUsers.deactivateConfirmTitle')}</strong>
        <p>{t('adminUsers.deactivateConfirmDescription')}</p>
      </div>
      <div className="admin-confirmation__actions">
        <Button
          variant="danger"
          size="lg"
          onClick={onConfirmDeactivate}
          disabled={submitting || isCurrentSession}
        >
          {submitting ? t('adminUsers.deactivatingButton') : t('adminUsers.confirmDeactivateButton')}
        </Button>
        <Button type="button" variant="secondary" size="lg" onClick={onCancelDeactivate} disabled={submitting}>
          {t('adminUsers.cancelButton')}
        </Button>
      </div>
    </div>
  ) : (
    <div className="admin-status-actions">
      <div className="admin-status-actions__summary">
        <StatusBadge status={status} />
        {isCurrentSession ? (
          <div className="admin-status-actions__note">
            {t('adminUsers.cannotDeactivateSelf')}
          </div>
        ) : null}
      </div>
      {status === 'Active' ? (
        <Button
          type="button"
          variant="danger"
          size="lg"
          onClick={onRequestDeactivate}
          disabled={submitting || isCurrentSession}
        >
          {t('adminUsers.deactivateUserButton')}
        </Button>
      ) : (
        <Button type="button" size="lg" onClick={onActivate} disabled={submitting}>
          {submitting ? t('adminUsers.activatingButton') : t('adminUsers.activateUserButton')}
        </Button>
      )}
    </div>
  );
};

export default UserStatusActions;
