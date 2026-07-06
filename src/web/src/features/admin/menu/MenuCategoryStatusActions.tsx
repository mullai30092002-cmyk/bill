import { Button, StatusBadge } from '../../../components/ui';
import { useLanguage } from '../../../i18n/LanguageProvider';
import type { MenuCategoryStatus } from '../adminTypes';

interface MenuCategoryStatusActionsProps {
  status: MenuCategoryStatus;
  confirmDeactivate: boolean;
  submitting: boolean;
  onActivate: () => void;
  onRequestDeactivate: () => void;
  onConfirmDeactivate: () => void;
  onCancelDeactivate: () => void;
}

export const MenuCategoryStatusActions = ({
  status,
  confirmDeactivate,
  submitting,
  onActivate,
  onRequestDeactivate,
  onConfirmDeactivate,
  onCancelDeactivate,
}: MenuCategoryStatusActionsProps) => {
  const { t } = useLanguage();

  return confirmDeactivate ? (
    <div className="admin-confirmation">
      <div className="admin-confirmation__copy">
        <strong>{t('menu.deactivateCategoryConfirmTitle')}</strong>
        <p>{t('menu.deactivateCategoryConfirmDescription')}</p>
      </div>
      <div className="admin-confirmation__actions">
        <Button variant="danger" size="lg" onClick={onConfirmDeactivate} disabled={submitting}>
          {submitting ? t('menu.deactivatingButton') : t('menu.confirmDeactivateButton')}
        </Button>
        <Button type="button" variant="secondary" size="lg" onClick={onCancelDeactivate} disabled={submitting}>
          {t('menu.cancelButton')}
        </Button>
      </div>
    </div>
  ) : (
    <div className="admin-status-actions">
      <div className="admin-status-actions__summary">
        <StatusBadge status={status} />
        <div className="admin-status-actions__note">
          {t('menu.categoryStatusNote')}
        </div>
      </div>
      {status === 'Active' ? (
        <Button type="button" variant="danger" size="lg" onClick={onRequestDeactivate} disabled={submitting}>
          {t('menu.deactivateCategoryButton')}
        </Button>
      ) : (
        <Button type="button" size="lg" onClick={onActivate} disabled={submitting}>
          {submitting ? t('menu.activatingButton') : t('menu.activateCategoryButton')}
        </Button>
      )}
    </div>
  );
};

export default MenuCategoryStatusActions;
