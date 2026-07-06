import type { AdminBranchListItem, AdminBranchStatus } from '../adminTypes';

export interface BranchDirectoryRow {
  id: string;
  branchId: string;
  name: string;
  address: string | null;
  phone: string | null;
  timezone: string;
  currency: string;
  status: AdminBranchStatus;
  updatedAt?: string | null;
}

const statusPriority: Record<AdminBranchStatus, number> = {
  Active: 0,
  Inactive: 1,
};

export const sortBranches = (branches: AdminBranchListItem[]) =>
  [...branches].sort((left, right) => {
    const statusDelta = statusPriority[left.status] - statusPriority[right.status];
    if (statusDelta !== 0) {
      return statusDelta;
    }

    return left.name.localeCompare(right.name, undefined, { sensitivity: 'base' });
  });

export const filterBranches = (
  branches: AdminBranchListItem[],
  search: string,
  statusFilter: 'All' | AdminBranchStatus
) => {
  const normalizedSearch = search.trim().toLowerCase();

  return branches.filter(branch => {
    if (statusFilter !== 'All' && branch.status !== statusFilter) {
      return false;
    }

    if (!normalizedSearch) {
      return true;
    }

    return [branch.name, branch.address ?? '', branch.phone ?? ''].some(value =>
      value.toLowerCase().includes(normalizedSearch)
    );
  });
};

export const buildBranchDirectoryRows = (branches: AdminBranchListItem[]): BranchDirectoryRow[] =>
  branches.map(branch => ({
    id: branch.branchId,
    branchId: branch.branchId,
    name: branch.name,
    address: branch.address,
    phone: branch.phone,
    timezone: branch.timezone,
    currency: branch.currency,
    status: branch.status,
    updatedAt: branch.updatedAt ?? null,
  }));

export const formatBranchTimestamp = (value?: string | null) => {
  if (!value) {
    return 'Not available';
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return parsed.toLocaleString(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  });
};
