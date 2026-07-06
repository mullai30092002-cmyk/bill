import { Button, StatusBadge } from '../../../components/ui';
import { useLanguage } from '../../../i18n/LanguageProvider';
import type { MenuItemStatus } from '../adminTypes';

interface MenuItemStatusActionsProps {
  status: MenuItemStatus;
  confirmDeactivate: boolean;
  submitting: boolean;
  onActivate: () => void;
  onRequestDeactivate: () => void;
  onConfirmDeactivate: () => void;
  onCancelDeactivate: () => void;
}

export const MenuItemStatusActions = ({
  status,
  confirmDeactivate,
  submitting,
  onActivate,
  onRequestDeactivate,
  onConfirmDeactivate,
  onCancelDeactivate,
}: MenuItemStatusActionsProps) => {
  const { t } = useLanguage();

  return confirmDeactivate ? (
    <div className="admin-confirmation">
      <div className="admin-confirmation__copy">
        <strong>{t('menu.deactivateItemConfirmTitle')}</strong>
        <p>{t('menu.deactivateItemConfirmDescription')}</p>
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
          {t('menu.itemStatusNote')}
        </div>
      </div>
      {status === 'Active' ? (
        <Button type="button" variant="danger" size="lg" onClick={onRequestDeactivate} disabled={submitting}>
          {t('menu.deactivateItemButton')}
        </Button>
      ) : (
        <Button type="button" size="lg" onClick={onActivate} disabled={submitting}>
          {submitting ? t('menu.activatingButton') : t('menu.activateItemButton')}
        </Button>
      )}
    </div>
  );
};

export default MenuItemStatusActions;
