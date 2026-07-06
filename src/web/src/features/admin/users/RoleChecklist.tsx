import { Badge } from '../../../components/ui';
import { useLanguage } from '../../../i18n/LanguageProvider';
import type { AdminRoleListItem } from '../adminTypes';

interface RoleChecklistProps {
  roles: AdminRoleListItem[];
  selectedRoleNames: string[];
  onToggleRole: (roleName: string) => void;
  disabled?: boolean;
  label: string;
}

export const RoleChecklist = ({
  roles,
  selectedRoleNames,
  onToggleRole,
  disabled,
  label,
}: RoleChecklistProps) => {
  const { t } = useLanguage();

  return (
    <div className="admin-role-checklist" role="group" aria-label={label}>
      {roles.map(role => {
        const selected = selectedRoleNames.includes(role.name);
        const blocked = !role.isAssignable;

        return (
          <label
            key={role.roleId}
            className={[
              'admin-role-card',
              selected && 'admin-role-card--selected',
              blocked && 'admin-role-card--blocked',
            ]
              .filter(Boolean)
              .join(' ')}
          >
            <input
              type="checkbox"
              checked={selected}
              disabled={disabled || blocked}
              onChange={() => onToggleRole(role.name)}
              className="admin-role-card__input"
            />
            <div className="admin-role-card__copy">
              <div className="admin-role-card__title-row">
                <strong>{role.name}</strong>
                <div className="admin-role-card__badges">
                  {role.isSystemRole ? <Badge tone="neutral" label={t('adminUsers.roleBadgeSystem')} /> : null}
                  {role.isAssignable ? (
                    <Badge tone="success" label={t('adminUsers.roleBadgeAssignable')} />
                  ) : (
                    <Badge tone="warning" label={t('adminUsers.roleBadgeLocked')} />
                  )}
                </div>
              </div>
              <div className="admin-role-card__description">
                {role.description || t('adminUsers.roleNoDescription')}
              </div>
              {role.assignmentBlockedReason ? (
                <div className="admin-role-card__blocked-reason">{role.assignmentBlockedReason}</div>
              ) : null}
            </div>
          </label>
        );
      })}
    </div>
  );
};

export default RoleChecklist;
