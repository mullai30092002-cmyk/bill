import { Button, StatusBadge } from '../../../components/ui';
import { useLanguage } from '../../../i18n/LanguageProvider';
import type { AdminBranchStatus } from '../adminTypes';

interface BranchStatusActionsProps {
  status: AdminBranchStatus;
  confirmDeactivate: boolean;
  submitting: boolean;
  onActivate: () => void;
  onRequestDeactivate: () => void;
  onConfirmDeactivate: () => void;
  onCancelDeactivate: () => void;
}

export const BranchStatusActions = ({
  status,
  confirmDeactivate,
  submitting,
  onActivate,
  onRequestDeactivate,
  onConfirmDeactivate,
  onCancelDeactivate,
}: BranchStatusActionsProps) => {
  const { t } = useLanguage();

  return confirmDeactivate ? (
    <div className="admin-confirmation">
      <div className="admin-confirmation__copy">
        <strong>{t('branches.deactivateConfirmTitle')}</strong>
        <p>{t('branches.deactivateConfirmDescription')}</p>
      </div>
      <div className="admin-confirmation__actions">
        <Button variant="danger" size="lg" onClick={onConfirmDeactivate} disabled={submitting}>
          {submitting ? t('branches.deactivatingButton') : t('branches.confirmDeactivateButton')}
        </Button>
        <Button type="button" variant="secondary" size="lg" onClick={onCancelDeactivate} disabled={submitting}>
          {t('branches.cancelButton')}
        </Button>
      </div>
    </div>
  ) : (
    <div className="admin-status-actions">
      <div className="admin-status-actions__summary">
        <StatusBadge status={status} />
        <div className="admin-status-actions__note">
          {t('branches.branchStatusNote')}
        </div>
      </div>
      {status === 'Active' ? (
        <Button type="button" variant="danger" size="lg" onClick={onRequestDeactivate} disabled={submitting}>
          {t('branches.deactivateBranchButton')}
        </Button>
      ) : (
        <Button type="button" size="lg" onClick={onActivate} disabled={submitting}>
          {submitting ? t('branches.activatingButton') : t('branches.activateBranchButton')}
        </Button>
      )}
    </div>
  );
};

export default BranchStatusActions;
