import { Button } from '../../../components/ui';
import { useLanguage } from '../../../i18n/LanguageProvider';
import type { AdminRoleListItem } from '../adminTypes';
import { RoleChecklist } from './RoleChecklist';

interface UserRoleAssignmentPanelProps {
  roles: AdminRoleListItem[];
  selectedRoleNames: string[];
  onToggleRole: (roleName: string) => void;
  onSave: () => void;
  submitting: boolean;
}

export const UserRoleAssignmentPanel = ({
  roles,
  selectedRoleNames,
  onToggleRole,
  onSave,
  submitting,
}: UserRoleAssignmentPanelProps) => {
  const { t } = useLanguage();

  return (
    <div className="admin-form">
      <RoleChecklist
        roles={roles}
        selectedRoleNames={selectedRoleNames}
        onToggleRole={onToggleRole}
        disabled={submitting}
        label="Selected user roles"
      />
      <div className="admin-form-note">
        {t('adminUsers.roleAssignmentNote')}
      </div>
      <div className="admin-form-actions">
        <Button type="button" size="lg" onClick={onSave} disabled={submitting}>
          {submitting ? t('adminUsers.savingRolesButton') : t('adminUsers.saveRolesButton')}
        </Button>
      </div>
    </div>
  );
};

export default UserRoleAssignmentPanel;
