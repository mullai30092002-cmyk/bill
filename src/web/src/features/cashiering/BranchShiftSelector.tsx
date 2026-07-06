import { useLanguage } from '../../i18n/LanguageProvider';
import { Badge, Button, EmptyState, Select } from '../../components/ui';
import type { AdminBranchListItem } from '../admin/adminTypes';

export interface BranchShiftSelectorProps {
  branches: AdminBranchListItem[];
  selectedBranchId: string;
  loading?: boolean;
  error?: string | null;
  helperText?: string;
  onChange: (branchId: string) => void;
  onRetry?: () => void;
}

export const BranchShiftSelector = ({
  branches,
  selectedBranchId,
  loading,
  error,
  helperText,
  onChange,
  onRetry,
}: BranchShiftSelectorProps) => {
  const { t } = useLanguage();

  if (loading && branches.length === 0) {
    return (
      <EmptyState
        title={t('cashier.loadingBranchesTitle')}
        description={t('cashier.loadingBranchesDescription')}
        tone="orders"
      />
    );
  }

  if (error) {
    return (
      <EmptyState
        title={t('cashier.couldNotLoadBranchesTitle')}
        description={error}
        tone="orders"
        actionLabel={onRetry ? t('cashier.tryAgain') : undefined}
        onAction={onRetry}
      />
    );
  }

  if (branches.length === 0) {
    return (
      <EmptyState
        title={t('cashier.noActiveBranchesTitle')}
        description={t('cashier.noActiveBranchesDescription')}
        tone="orders"
      />
    );
  }

  return (
    <div className="admin-controls">
      <div className="admin-form-grid">
        <Select
          label={t('cashier.branchLabel')}
          value={selectedBranchId}
          onChange={event => onChange(event.target.value)}
          helperText={helperText ?? t('cashier.branchSelectionHelper')}
        >
          <option value="">{t('cashier.selectBranchPlaceholder')}</option>
          {branches.map(branch => (
            <option key={branch.branchId} value={branch.branchId}>
              {branch.name}
            </option>
          ))}
        </Select>
      </div>

      <div className="admin-form-note">
        <Badge tone="info" label={t('cashier.activeBranchesCount', { count: branches.length })} />{' '}
        {t('cashier.branchSelectionNote')}
      </div>

      {selectedBranchId ? (
        <div className="admin-form-note">
          {t('cashier.branchSelectionDriveHint')}
        </div>
      ) : null}

      {onRetry ? (
        <div className="admin-form-actions">
          <Button type="button" variant="secondary" onClick={onRetry}>
            {t('cashier.refreshBranches')}
          </Button>
        </div>
      ) : null}
    </div>
  );
};

export default BranchShiftSelector;
