import { Button, EmptyState, Input, ResponsiveDataList, Select, StatusBadge } from '../../../components/ui';
import { useLanguage } from '../../../i18n/LanguageProvider';
import type { AdminBranchListItem, AdminBranchStatus } from '../adminTypes';
import { buildBranchDirectoryRows, formatBranchTimestamp } from './branchDisplay';

interface BranchDirectoryListProps {
  branches: AdminBranchListItem[];
  loading?: boolean;
  error?: string | null;
  search: string;
  statusFilter: 'All' | AdminBranchStatus;
  onSearchChange: (value: string) => void;
  onStatusFilterChange: (value: 'All' | AdminBranchStatus) => void;
  onRetry: () => void;
  onSelectBranch: (branchId: string) => void;
}

export const BranchDirectoryList = ({
  branches,
  loading,
  error,
  search,
  statusFilter,
  onSearchChange,
  onStatusFilterChange,
  onRetry,
  onSelectBranch,
}: BranchDirectoryListProps) => {
  const { t } = useLanguage();

  if (loading && branches.length === 0) {
    return (
      <EmptyState
        title={t('branches.loadingBranchesTitle')}
        description={t('branches.loadingBranchesDescription')}
        tone="admin"
      />
    );
  }

  if (error) {
    return (
      <EmptyState
        title={t('branches.couldNotLoadBranchesTitle')}
        description={error}
        tone="admin"
        actionLabel={t('branches.tryAgain')}
        onAction={onRetry}
      />
    );
  }

  const rows = buildBranchDirectoryRows(branches);

  return (
    <div className="admin-controls">
      <div className="admin-form-grid">
        <Input
          label={t('branches.searchBranchesLabel')}
          value={search}
          onChange={event => onSearchChange(event.target.value)}
          placeholder="Name, address, or phone"
          helperText={t('branches.searchBranchesHelper')}
        />
        <Select
          label={t('branches.statusFilterLabel')}
          value={statusFilter}
          onChange={event => onStatusFilterChange(event.target.value as 'All' | AdminBranchStatus)}
          helperText={t('branches.statusFilterHelper')}
        >
          <option value="All">{t('branches.statusAll')}</option>
          <option value="Active">{t('branches.statusActive')}</option>
          <option value="Inactive">{t('branches.statusInactive')}</option>
        </Select>
      </div>

      <ResponsiveDataList
        rows={rows}
        columns={[
          { key: 'name', label: t('branches.columnBranch') },
          {
            key: 'status',
            label: t('branches.columnStatus'),
            render: row => <StatusBadge status={row.status} />,
          },
          { key: 'address', label: t('branches.columnAddress'), render: row => row.address ?? t('branches.notProvided') },
          { key: 'phone', label: t('branches.columnPhone'), render: row => row.phone ?? t('branches.notProvided') },
          { key: 'timezone', label: t('branches.columnTimezone') },
          { key: 'currency', label: t('branches.columnCurrency') },
          {
            key: 'updatedAt',
            label: t('branches.columnUpdated'),
            render: row => formatBranchTimestamp(row.updatedAt),
          },
          {
            key: 'branchId',
            label: t('branches.columnActions'),
            render: row => (
              <Button
                type="button"
                variant="secondary"
                size="md"
                fullWidth
                onClick={() => onSelectBranch(row.branchId)}
              >
                {t('branches.editButton')}
              </Button>
            ),
          },
        ]}
        mobileTitle={row => row.name}
        mobileDescription={row => row.address ?? row.phone ?? row.timezone}
        emptyTitle={t('branches.noBranchesFoundTitle')}
        emptyDescription={t('branches.noBranchesFoundDescription')}
      />
    </div>
  );
};

export default BranchDirectoryList;
