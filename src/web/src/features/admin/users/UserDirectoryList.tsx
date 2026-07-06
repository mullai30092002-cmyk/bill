import { Button, EmptyState, ResponsiveDataList, StatusBadge } from '../../../components/ui';
import { useLanguage } from '../../../i18n/LanguageProvider';
import type { UserDirectoryRow } from './adminUserDisplay';

interface UserDirectoryListProps {
  rows: UserDirectoryRow[];
  loading?: boolean;
  error?: string | null;
  onRetry: () => void;
  onEditUser: (userId: string) => void;
}

export const UserDirectoryList = ({ rows, loading, error, onRetry, onEditUser }: UserDirectoryListProps) => {
  const { t } = useLanguage();

  if (loading && rows.length === 0) {
    return (
      <EmptyState
        title={t('adminUsers.loadingUsersTitle')}
        description={t('adminUsers.loadingUsersDescription')}
        tone="admin"
      />
    );
  }

  if (error) {
    return (
      <EmptyState
        title={t('adminUsers.couldNotLoadUsersTitle')}
        description={error}
        tone="admin"
        actionLabel={t('adminUsers.tryAgain')}
        onAction={onRetry}
      />
    );
  }

  return (
    <ResponsiveDataList
      rows={rows}
      columns={[
        { key: 'fullName', label: t('adminUsers.columnUser') },
        { key: 'mobileNumber', label: t('adminUsers.columnMobile') },
        {
          key: 'branchName',
          label: t('adminUsers.columnBranch'),
          render: row => row.branchName,
        },
        {
          key: 'roleNames',
          label: t('adminUsers.columnRoles'),
          render: row => row.roleNames.join(', '),
        },
        {
          key: 'status',
          label: t('adminUsers.columnStatus'),
          render: row => <StatusBadge status={row.status} />,
        },
        {
          key: 'userId',
          label: t('adminUsers.columnActions'),
          render: row => (
            <Button
              type="button"
              variant="secondary"
              size="md"
              fullWidth
              onClick={() => onEditUser(row.userId)}
            >
              {t('adminUsers.editButton')}
            </Button>
          ),
        },
      ]}
      mobileTitle={row => row.fullName}
      mobileDescription={row => row.mobileNumber}
      emptyTitle={t('adminUsers.noUsersFoundTitle')}
      emptyDescription={t('adminUsers.noUsersFoundDescription')}
    />
  );
};

export default UserDirectoryList;
