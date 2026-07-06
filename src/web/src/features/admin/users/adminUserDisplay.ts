import type { AdminBranchListItem, AdminUserListItem } from '../adminTypes';

export interface BranchSelectOption {
  value: string;
  label: string;
  disabled?: boolean;
}

export interface UserDirectoryRow {
  id: string;
  userId: string;
  fullName: string;
  mobileNumber: string;
  branchName: string;
  roleNames: string[];
  status: string;
}

export const resolveBranchLabel = (branchId: string | null, branches: AdminBranchListItem[]) => {
  if (!branchId) {
    return 'No branch';
  }

  const branch = branches.find(candidate => candidate.branchId === branchId);
  return branch?.name ?? 'Unknown branch';
};

export const buildCreateBranchOptions = (branches: AdminBranchListItem[]): BranchSelectOption[] => [
  { value: '', label: 'No branch assignment' },
  ...branches
    .filter(branch => branch.status === 'Active')
    .map(branch => ({
      value: branch.branchId,
      label: branch.name,
    })),
];

export const buildEditBranchOptions = (
  branches: AdminBranchListItem[],
  selectedBranchId: string | null
): BranchSelectOption[] => {
  const options = buildCreateBranchOptions(branches);

  if (!selectedBranchId || options.some(option => option.value === selectedBranchId)) {
    return options;
  }

  const selectedBranch = branches.find(branch => branch.branchId === selectedBranchId);
  if (selectedBranch) {
    return [
      ...options,
      {
        value: selectedBranch.branchId,
        label: `${selectedBranch.name} (Inactive)`,
        disabled: true,
      },
    ];
  }

  return [
    ...options,
    {
      value: selectedBranchId,
      label: 'Current branch unavailable',
      disabled: true,
    },
  ];
};

export const buildUserDirectoryRows = (
  users: AdminUserListItem[],
  branches: AdminBranchListItem[]
): UserDirectoryRow[] =>
  users.map(user => ({
    id: user.userId,
    userId: user.userId,
    fullName: user.fullName,
    mobileNumber: user.mobileNumber,
    branchName: resolveBranchLabel(user.branchId, branches),
    roleNames: user.roleNames,
    status: user.status,
  }));
