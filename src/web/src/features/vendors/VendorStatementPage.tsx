import { useCallback, useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';

import { isApiError } from '../../api/apiErrors';
import { AdminLayout } from '../../components/layout';
import type { ShellNavItem } from '../../components/layout/navigation';
import { Badge, Button, Card, EmptyState, Input, ResponsiveDataList, Select, StatusBadge, SummaryCard } from '../../components/ui';
import { useLanguage } from '../../i18n/LanguageProvider';
import { useAuth } from '../auth/useAuth';
import { useRestaurantCurrency } from '../auth/useRestaurantCurrency';
import { listAdminBranches } from '../admin/adminApi';
import type { AdminBranchListItem } from '../admin/adminTypes';
import { sortBranches } from '../admin/branches/branchDisplay';
import { formatCurrency } from '../finance/currencyDisplay';
import { listVendors } from './vendorApi';
import { getSafeVendorErrorMessage } from './vendorErrorDisplay';
import { getVendorStatement } from './vendorStatementApi';
import type { VendorStatementResponse } from './vendorStatementTypes';
import type { VendorDetail } from './vendorTypes';

export interface VendorStatementPageProps {
  navItems?: ShellNavItem[];
  restaurantName?: string;
  branchName?: string;
  operatorLabel?: string;
}

const formatDateInput = (value = new Date()) => {
  const year = value.getFullYear();
  const month = `${value.getMonth() + 1}`.padStart(2, '0');
  const day = `${value.getDate()}`.padStart(2, '0');
  return `${year}-${month}-${day}`;
};

const isValidDateInput = (value: string | null) => Boolean(value && /^\d{4}-\d{2}-\d{2}$/.test(value));

const formatDate = (value: string | null | undefined) => (value ? value.slice(0, 10) : '-');
const formatBillNumber = (value: string | null | undefined) => value?.trim() || '-';

const formatTimestamp = (value: string | null | undefined) => {
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

const formatReference = (value: string | null | undefined) => value || 'Not available';

export const VendorStatementPage = ({
  navItems,
  restaurantName,
  branchName,
  operatorLabel,
}: VendorStatementPageProps) => {
  const { t } = useLanguage();
  const auth = useAuth();
  const { currencyCode, locale } = useRestaurantCurrency();
  const [searchParams, setSearchParams] = useSearchParams();
  const canAccess = auth.hasPermission('VendorBill.Upload') || auth.hasPermission('VendorBill.Confirm') || auth.hasPermission('VendorPayment.Create');
  const canSwitchBranch = auth.hasPermission('Branch.Manage') || auth.hasPermission('User.Manage');

  const defaultRange = useMemo(() => {
    const today = new Date();
    const firstOfMonth = new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), 1));
    return {
      fromDate: formatDateInput(firstOfMonth),
      toDate: formatDateInput(today),
    };
  }, []);

  const [branches, setBranches] = useState<AdminBranchListItem[]>([]);
  const [branchesLoading, setBranchesLoading] = useState(canSwitchBranch);
  const [branchesError, setBranchesError] = useState<string | null>(null);
  const [vendors, setVendors] = useState<VendorDetail[]>([]);
  const [vendorsLoading, setVendorsLoading] = useState(canAccess);
  const [vendorsError, setVendorsError] = useState<string | null>(null);
  const [selectedBranchId, setSelectedBranchId] = useState(() => searchParams.get('branchId') || auth.session?.branchId || '');
  const [selectedVendorId, setSelectedVendorId] = useState(() => searchParams.get('vendorId') || '');
  const [fromDate, setFromDate] = useState(() => (isValidDateInput(searchParams.get('fromDate')) ? searchParams.get('fromDate')! : defaultRange.fromDate));
  const [toDate, setToDate] = useState(() => (isValidDateInput(searchParams.get('toDate')) ? searchParams.get('toDate')! : defaultRange.toDate));
  const [statement, setStatement] = useState<VendorStatementResponse | null>(null);
  const [statementLoading, setStatementLoading] = useState(canAccess);
  const [statementError, setStatementError] = useState<string | null>(null);

  const selectedBranch = useMemo(
    () => branches.find(branch => branch.branchId === selectedBranchId) ?? null,
    [branches, selectedBranchId]
  );

  const selectedVendor = useMemo(
    () => vendors.find(vendor => vendor.vendorId === selectedVendorId) ?? null,
    [selectedVendorId, vendors]
  );

  const formatMoney = useCallback(
    (value: number, currency?: string | null) => formatCurrency(value, currency ?? currencyCode, locale),
    [currencyCode, locale]
  );

  const loadBranches = useCallback(async () => {
    if (!canSwitchBranch) {
      return;
    }

    setBranchesLoading(true);
    setBranchesError(null);

    try {
      const response = await listAdminBranches();
      const activeBranches = sortBranches(response.items.filter(branch => branch.status === 'Active'));
      setBranches(activeBranches);

      const activeBranchIds = new Set(activeBranches.map(branch => branch.branchId));
      setSelectedBranchId(current =>
        current && activeBranchIds.has(current)
          ? current
          : auth.session?.branchId && activeBranchIds.has(auth.session.branchId)
            ? auth.session.branchId
            : activeBranches.length === 1
              ? activeBranches[0].branchId
              : ''
      );
    } catch (caughtError) {
      setBranchesError(
        getSafeVendorErrorMessage(caughtError, 'Could not load branches right now.', {
          sessionExpired: t('vendor.sessionExpired'),
          unauthorized: t('vendor.notAuthorizedChange'),
        })
      );
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setBranchesLoading(false);
    }
  }, [auth, canSwitchBranch, t]);

  const loadVendors = useCallback(async () => {
    if (!canAccess) {
      return;
    }

    setVendorsLoading(true);
    setVendorsError(null);

    try {
      const response = await listVendors({ branchId: selectedBranchId || undefined });
      setVendors(response.items);
      setSelectedVendorId(current =>
        current && response.items.some(vendor => vendor.vendorId === current)
          ? current
          : response.items[0]?.vendorId || ''
      );
    } catch (caughtError) {
      setVendors([]);
      setVendorsError(
        getSafeVendorErrorMessage(caughtError, 'Could not load vendors right now.', {
          sessionExpired: t('vendor.sessionExpired'),
          unauthorized: t('vendor.notAuthorizedChange'),
        })
      );
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setVendorsLoading(false);
    }
  }, [auth, canAccess, selectedBranchId, t]);

  const loadStatement = useCallback(
    async (vendorId: string, branchId: string, nextFromDate: string, nextToDate: string) => {
      if (!vendorId) {
        setStatement(null);
        setStatementError(null);
        return;
      }

      setStatementLoading(true);
      setStatementError(null);

      try {
        const response = await getVendorStatement({
          vendorId,
          branchId: branchId || undefined,
          fromDate: nextFromDate,
          toDate: nextToDate,
        });
        setStatement(response);
      } catch (caughtError) {
        setStatement(null);
        setStatementError(
          getSafeVendorErrorMessage(caughtError, 'Could not load the vendor statement right now.', {
            sessionExpired: t('vendor.sessionExpired'),
            unauthorized: t('vendor.notAuthorizedChange'),
          })
        );
        if (isApiError(caughtError) && caughtError.status === 401) {
          void auth.logout();
        }
      } finally {
        setStatementLoading(false);
      }
    },
    [auth, t]
  );

  useEffect(() => {
    if (!canSwitchBranch) {
      return;
    }

    void loadBranches();
  }, [canSwitchBranch, loadBranches]);

  useEffect(() => {
    if (!canAccess) {
      return;
    }

    void loadVendors();
  }, [canAccess, loadVendors, selectedBranchId]);

  useEffect(() => {
    if (!canAccess) {
      return;
    }

    void loadStatement(selectedVendorId, selectedBranchId, fromDate, toDate);
  }, [canAccess, fromDate, loadStatement, selectedBranchId, selectedVendorId, toDate]);

  const updateFilters = (nextVendorId: string, nextBranchId: string, nextFromDate: string, nextToDate: string) => {
    const nextParams = new URLSearchParams(searchParams);

    if (nextVendorId) {
      nextParams.set('vendorId', nextVendorId);
    } else {
      nextParams.delete('vendorId');
    }

    if (nextBranchId) {
      nextParams.set('branchId', nextBranchId);
    } else {
      nextParams.delete('branchId');
    }

    if (nextFromDate) {
      nextParams.set('fromDate', nextFromDate);
    } else {
      nextParams.delete('fromDate');
    }

    if (nextToDate) {
      nextParams.set('toDate', nextToDate);
    } else {
      nextParams.delete('toDate');
    }

    setSearchParams(nextParams, { replace: true });
    setSelectedVendorId(nextVendorId);
    setSelectedBranchId(nextBranchId);
    setFromDate(nextFromDate);
    setToDate(nextToDate);
  };

  if (!canAccess) {
    return (
      <AdminLayout
        title={t('vendorStatement.pageTitle')}
        description={t('vendorStatement.pageDescriptionNoAccess')}
        breadcrumbs={['Dashboard', 'Vendors']}
        operatorLabel={operatorLabel}
        restaurantName={restaurantName}
        branchName={branchName}
        navItems={navItems}
      >
        <EmptyState title={t('vendor.notAuthorizedTitle')} description={t('vendorStatement.notAuthorizedDescription')} tone="admin" />
      </AdminLayout>
    );
  }

  const payableRows = (statement?.payableBills ?? []).map(item => ({
    id: item.vendorBillId,
    billNumber: formatBillNumber(item.billNumber),
    branchName: item.branchName ?? '-',
    billDate: formatDate(item.billDate),
    dueDate: formatDate(item.dueDate),
    totalAmount: formatMoney(item.totalAmount, statement?.currencyCode),
    paidAmount: formatMoney(item.paidAmount, statement?.currencyCode),
    outstandingAmount: formatMoney(item.outstandingAmount, statement?.currencyCode),
    status: item.status,
  }));

  const settlementRows = (statement?.settlements ?? []).map(item => ({
    id: item.vendorSettlementId,
    billNumber: formatBillNumber(item.billNumber),
    paidAtUtc: formatTimestamp(item.paidAtUtc),
    paymentMode: item.paymentMode,
    amount: formatMoney(item.amount, statement?.currencyCode),
    reference: formatReference(item.referenceNumberMasked),
    previousOutstandingAmount: formatMoney(item.previousOutstandingAmount, statement?.currencyCode),
    newOutstandingAmount: formatMoney(item.newOutstandingAmount, statement?.currencyCode),
    status: item.status,
  }));

  const timelineRows = (statement?.timeline ?? []).map(item => ({
    id: `${item.entryType}-${item.timestampUtc}-${item.billNumber ?? item.reference ?? 'row'}`,
    entryType: item.entryType,
    timestampUtc: formatTimestamp(item.timestampUtc),
    billNumber: formatBillNumber(item.billNumber),
    reference: formatReference(item.reference),
    description: item.description ?? '-',
    debitAmount: formatMoney(item.debitAmount, statement?.currencyCode),
    creditAmount: formatMoney(item.creditAmount, statement?.currencyCode),
    runningBalance: formatMoney(item.runningBalance, statement?.currencyCode),
    paymentMode: item.paymentMode ?? '-',
    status: item.status ?? '-',
  }));

  return (
    <AdminLayout
      title={t('vendorStatement.pageTitle')}
      description={t('vendorStatement.pageDescription')}
      breadcrumbs={['Dashboard', 'Vendors']}
      operatorLabel={operatorLabel}
      restaurantName={restaurantName}
      branchName={selectedBranch?.name ?? branchName}
      navItems={navItems}
      actions={
        <Button variant="secondary" onClick={() => void loadStatement(selectedVendorId, selectedBranchId, fromDate, toDate)}>
          {t('vendorStatement.refreshButton')}
        </Button>
      }
    >
      <div className="preview-sequence">
        <Card
          title={t('vendorStatement.filtersTitle')}
          description={t('vendorStatement.filtersDescription')}
          tone="admin"
          actions={<Badge tone={statementLoading ? 'warning' : 'neutral'} label={statementLoading ? t('vendorStatement.statusRefreshing') : t('vendorStatement.statusReady')} />}
        >
          {statementError ? (
            <div className="admin-notice admin-notice--warning" role="status">
              {statementError}
            </div>
          ) : null}

          {branchesError ? (
            <div className="admin-notice admin-notice--danger" role="alert">
              {branchesError}
            </div>
          ) : null}

          <div className="admin-form-grid">
            {canSwitchBranch ? (
              <Select
                label={t('vendorStatement.branchLabel')}
                value={selectedBranchId}
                onChange={event => updateFilters(selectedVendorId, event.target.value, fromDate, toDate)}
                error={branchesError ?? undefined}
              >
                <option value="">{t('vendorStatement.branchAllBranches')}</option>
                {branches.map(branch => (
                  <option key={branch.branchId} value={branch.branchId}>
                    {branch.name}
                  </option>
                ))}
              </Select>
            ) : null}
            <Select
              label={t('vendorStatement.vendorLabel')}
              value={selectedVendorId}
              onChange={event => updateFilters(event.target.value, selectedBranchId, fromDate, toDate)}
              error={vendorsError ?? undefined}
            >
              <option value="">{t('vendorStatement.vendorSelectPlaceholder')}</option>
              {vendors.map(vendor => (
                <option key={vendor.vendorId} value={vendor.vendorId}>
                  {vendor.name} ({vendor.vendorType})
                </option>
              ))}
            </Select>
            <Input
              label={t('vendorStatement.fromDateLabel')}
              type="date"
              value={fromDate}
              onChange={event => updateFilters(selectedVendorId, selectedBranchId, event.target.value, toDate)}
            />
            <Input
              label={t('vendorStatement.toDateLabel')}
              type="date"
              value={toDate}
              onChange={event => updateFilters(selectedVendorId, selectedBranchId, fromDate, event.target.value)}
            />
          </div>

          {vendorsError ? (
            <div className="admin-notice admin-notice--danger" role="alert">
              {vendorsError}
            </div>
          ) : null}
          {vendorsLoading ? <div className="admin-form-note">{t('vendorStatement.vendorsLoadingNote')}</div> : null}
        </Card>

        {statement ? (
          <>
            <div className="summary-grid">
              <SummaryCard
                label={t('vendorStatement.summaryOpeningOutstanding')}
                value={formatMoney(statement.openingOutstandingAmount, statement.currencyCode)}
                tone="inventory"
                detail={t('vendorStatement.summaryDateRange', { from: statement.fromDate, to: statement.toDate })}
              />
              <SummaryCard
                label={t('vendorStatement.summaryCurrentOutstanding')}
                value={formatMoney(statement.currentOutstandingAmount, statement.currencyCode)}
                tone="admin"
                detail={statement.branchName ?? t('vendorStatement.branchAllBranches')}
              />
              <SummaryCard
                label={t('vendorStatement.summaryPayableBills')}
                value={statement.summary.payableBillCount.toString()}
                tone="orders"
                detail={t('vendorStatement.summaryBillsDetail', { amount: statement.summary.totalBillAmount.toFixed(2) })}
              />
              <SummaryCard
                label={t('vendorStatement.summarySettlements')}
                value={statement.summary.settlementCount.toString()}
                tone="dashboard"
                detail={t('vendorStatement.summaryOverdueDetail', { count: statement.summary.overdueBillCount.toString() })}
              />
            </div>

            <Card title={t('vendorStatement.payableBillsTitle')} description={t('vendorStatement.payableBillsDescription')} tone="admin">
              <ResponsiveDataList
                rows={payableRows}
                columns={[
                  { key: 'billNumber', label: t('vendorStatement.colBillNumber') },
                  { key: 'branchName', label: t('vendorStatement.colBranch'), hideOnMobile: true },
                  { key: 'billDate', label: t('vendorStatement.colBillDate') },
                  { key: 'dueDate', label: t('vendorStatement.colDueDate'), hideOnMobile: true },
                  { key: 'totalAmount', label: t('vendorStatement.colTotal'), align: 'right' },
                  { key: 'paidAmount', label: t('vendorStatement.colPaid'), align: 'right' },
                  { key: 'outstandingAmount', label: t('vendorStatement.colOutstanding'), align: 'right' },
                  { key: 'status', label: t('vendorStatement.colStatus'), render: row => <StatusBadge status={row.status} label={row.status} /> },
                ]}
                mobileTitle={row => row.billNumber}
                mobileDescription={row => t('vendorStatement.mobileDescriptionOutstanding', { branch: row.branchName, amount: row.outstandingAmount })}
                emptyTitle={t('vendorStatement.payableBillsEmptyTitle')}
                emptyDescription={t('vendorStatement.payableBillsEmptyDescription')}
              />
            </Card>

            <Card title={t('vendorStatement.settlementHistoryTitle')} description={t('vendorStatement.settlementHistoryDescription')} tone="admin">
              <ResponsiveDataList
                rows={settlementRows}
                columns={[
                  { key: 'billNumber', label: t('vendorStatement.colBillNumber') },
                  { key: 'paidAtUtc', label: t('vendorStatement.colPaidAt') },
                  { key: 'paymentMode', label: t('vendorStatement.colMode') },
                  { key: 'amount', label: t('vendorStatement.colAmount'), align: 'right' },
                  { key: 'reference', label: t('vendorStatement.colReference'), hideOnMobile: true },
                  { key: 'previousOutstandingAmount', label: t('vendorStatement.colBefore'), align: 'right', hideOnMobile: true },
                  { key: 'newOutstandingAmount', label: t('vendorStatement.colAfter'), align: 'right', hideOnMobile: true },
                  { key: 'status', label: t('vendorStatement.colStatus'), render: row => <StatusBadge status={row.status} label={row.status} /> },
                ]}
                mobileTitle={row => row.billNumber}
                mobileDescription={row => t('vendorStatement.mobileDescriptionSettlement', { mode: row.paymentMode, amount: row.amount })}
                emptyTitle={t('vendorStatement.settlementEmptyTitle')}
                emptyDescription={t('vendorStatement.settlementEmptyDescription')}
              />
            </Card>

            <Card title={t('vendorStatement.timelineTitle')} description={t('vendorStatement.timelineDescription')} tone="admin">
              <ResponsiveDataList
                rows={timelineRows}
                columns={[
                  { key: 'timestampUtc', label: t('vendorStatement.colDate') },
                  { key: 'entryType', label: t('vendorStatement.colType') },
                  { key: 'billNumber', label: t('vendorStatement.colBillNumber') },
                  { key: 'reference', label: t('vendorStatement.colReference'), hideOnMobile: true },
                  { key: 'description', label: t('vendorStatement.colDescription'), hideOnMobile: true },
                  { key: 'debitAmount', label: t('vendorStatement.colDebit'), align: 'right' },
                  { key: 'creditAmount', label: t('vendorStatement.colCredit'), align: 'right' },
                  { key: 'runningBalance', label: t('vendorStatement.colRunningBalance'), align: 'right' },
                ]}
                mobileTitle={row => row.timestampUtc}
                mobileDescription={row => t('vendorStatement.mobileDescriptionTimeline', { type: row.entryType, bill: row.billNumber })}
                emptyTitle={t('vendorStatement.timelineEmptyTitle')}
                emptyDescription={t('vendorStatement.timelineEmptyDescription')}
              />
            </Card>
          </>
        ) : (
          <EmptyState
            title={statementLoading ? t('vendorStatement.loadingTitle') : t('vendorStatement.chooseVendorTitle')}
            description={
              statementLoading
                ? t('vendorStatement.loadingDescription')
                : t('vendorStatement.chooseVendorDescription')
            }
            tone="admin"
          />
        )}
      </div>
    </AdminLayout>
  );
};

export default VendorStatementPage;
