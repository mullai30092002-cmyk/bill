import { useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';

import { isApiError } from '../../api/apiErrors';
import { InventoryManagementLayout } from '../../components/layout';
import type { ShellNavItem } from '../../components/layout/navigation';
import { Badge, Button, Card, EmptyState, Input, ResponsiveDataList, Select, SummaryCard } from '../../components/ui';
import { useAuth } from '../auth/useAuth';
import { useLanguage } from '../../i18n/LanguageProvider';
import { listAdminBranches } from '../admin/adminApi';
import type { AdminBranchListItem } from '../admin/adminTypes';
import { sortBranches } from '../admin/branches/branchDisplay';
import { getVendorPayablesReport } from './vendorPayablesReportApi';
import {
  formatVendorPayablesCurrency,
  formatVendorPayablesDateInput,
  formatVendorPayablesScopeSummary,
  formatVendorPayablesReference,
  formatVendorPayablesTimestamp,
} from './vendorPayablesReportDisplay';
import { getSafeVendorPayablesReportErrorMessage } from './vendorPayablesReportErrorDisplay';
import type {
  VendorPayablesInventoryPurchaseTotal,
  VendorPayablesOverdueBillItem,
  VendorPayablesReportResponse,
  VendorPayablesSettlementItem,
  VendorPayablesVendorBalance,
} from './vendorPayablesReportTypes';

export interface VendorPayablesReportPageProps {
  navItems?: ShellNavItem[];
  restaurantName?: string;
  branchName?: string;
  operatorLabel?: string;
}

const isValidDateInput = (value: string | null) => Boolean(value && /^\d{4}-\d{2}-\d{2}$/.test(value));

type VendorBalanceRow = VendorPayablesVendorBalance & { id: string };
type OverdueBillRow = VendorPayablesOverdueBillItem & { id: string };
type SettlementRow = VendorPayablesSettlementItem & { id: string };
type InventoryPurchaseRow = VendorPayablesInventoryPurchaseTotal & { id: string };

const defaultDateRange = () => {
  const today = new Date();
  const firstOfMonth = new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), 1));

  return {
    fromDate: formatVendorPayablesDateInput(firstOfMonth),
    toDate: formatVendorPayablesDateInput(today),
  };
};

const buildOverdueRows = (report: VendorPayablesReportResponse | null): OverdueBillRow[] =>
  (report?.overdueBills ?? []).map(item => ({
    ...item,
    id: `${item.billNumber ?? item.vendorName}-${item.dueDate ?? item.billDate}`,
  }));

const buildSettlementRows = (report: VendorPayablesReportResponse | null): SettlementRow[] =>
  (report?.recentSettlements ?? []).map(item => ({
    ...item,
    id: `${item.vendorName}-${item.billNumber ?? 'bill'}-${item.paidAtUtc}-${item.amount}-${item.paymentMode}`,
  }));

const buildInventoryRows = (report: VendorPayablesReportResponse | null): InventoryPurchaseRow[] =>
  (report?.inventoryPurchaseTotals ?? []).map(item => ({
    ...item,
    id: item.inventoryItemName,
  }));

const buildVendorRows = (report: VendorPayablesReportResponse | null): VendorBalanceRow[] =>
  (report?.vendorBalances ?? []).map(item => ({
    ...item,
    id: item.vendorId,
  }));

const formatCount = (value: number) => value.toLocaleString();

export const VendorPayablesReportPage = ({
  navItems,
  restaurantName,
  branchName,
  operatorLabel,
}: VendorPayablesReportPageProps) => {
  const auth = useAuth();
  const { t } = useLanguage();
  const [searchParams, setSearchParams] = useSearchParams();
  const canAccess = auth.hasPermission('Report.View');
  const canSwitchBranch = auth.hasPermission('Branch.Manage') || auth.hasPermission('User.Manage');
  const reportMessages = useMemo(
    () => ({
      workspaceTitle: t('vendorPayables.workspaceTitle'),
      workspaceDescription: t('vendorPayables.workspaceDescription'),
      notAuthorizedTitle: t('vendorPayables.notAuthorizedTitle'),
      notAuthorizedDescription: t('vendorPayables.notAuthorizedDescription'),
      refreshReport: t('vendorPayables.refreshReport'),
      refreshing: t('vendorPayables.refreshing'),
      ready: t('vendorPayables.ready'),
      reportFiltersTitle: t('vendorPayables.reportFiltersTitle'),
      reportFiltersDescription: t('vendorPayables.reportFiltersDescription'),
      branchLabel: t('vendorPayables.branchLabel'),
      branchHelperLoading: t('vendorPayables.branchHelperLoading'),
      branchHelperReady: t('vendorPayables.branchHelperReady'),
      allBranchesOption: t('vendorPayables.allBranchesOption'),
      fromDateLabel: t('vendorPayables.fromDateLabel'),
      toDateLabel: t('vendorPayables.toDateLabel'),
      loadingReportTitle: t('vendorPayables.loadingReportTitle'),
      loadingReportDescription: t('vendorPayables.loadingReportDescription'),
      noReportLoadedTitle: t('vendorPayables.noReportLoadedTitle'),
      noReportLoadedDescription: t('vendorPayables.noReportLoadedDescription'),
      refreshReportAction: t('vendorPayables.refreshReportAction'),
      purchaseTotalLabel: t('vendorPayables.purchaseTotalLabel'),
      paidTotalLabel: t('vendorPayables.paidTotalLabel'),
      outstandingTotalLabel: t('vendorPayables.outstandingTotalLabel'),
      overdueBillsLabel: t('vendorPayables.overdueBillsLabel'),
      unpaidBillsLabel: t('vendorPayables.unpaidBillsLabel'),
      partiallyPaidBillsLabel: t('vendorPayables.partiallyPaidBillsLabel'),
      paidBillsLabel: t('vendorPayables.paidBillsLabel'),
      cancelledBillsLabel: t('vendorPayables.cancelledBillsLabel'),
      vendorBalancesTitle: t('vendorPayables.vendorBalancesTitle'),
      vendorBalancesDescription: t('vendorPayables.vendorBalancesDescription'),
      overdueVendorBillsTitle: t('vendorPayables.overdueVendorBillsTitle'),
      overdueVendorBillsDescription: t('vendorPayables.overdueVendorBillsDescription'),
      settlementHistoryTitle: t('vendorPayables.settlementHistoryTitle'),
      settlementHistoryDescription: t('vendorPayables.settlementHistoryDescription'),
      inventoryPurchaseTotalsTitle: t('vendorPayables.inventoryPurchaseTotalsTitle'),
      inventoryPurchaseTotalsDescription: t('vendorPayables.inventoryPurchaseTotalsDescription'),
      noVendorBalancesTitle: t('vendorPayables.noVendorBalancesTitle'),
      noVendorBalancesDescription: t('vendorPayables.noVendorBalancesDescription'),
      noOverdueBillsTitle: t('vendorPayables.noOverdueBillsTitle'),
      noOverdueBillsDescription: t('vendorPayables.noOverdueBillsDescription'),
      noSettlementsTitle: t('vendorPayables.noSettlementsTitle'),
      noSettlementsDescription: t('vendorPayables.noSettlementsDescription'),
      noInventoryLinkedPurchasesTitle: t('vendorPayables.noInventoryLinkedPurchasesTitle'),
      noInventoryLinkedPurchasesDescription: t('vendorPayables.noInventoryLinkedPurchasesDescription'),
      scopePrefix: t('vendorPayables.scopePrefix'),
      generatedPrefix: t('vendorPayables.generatedPrefix'),
      readOnlyNote: t('vendorPayables.readOnlyNote'),
      sessionExpired: t('vendorPayables.sessionExpired'),
      unauthorized: t('vendorPayables.unauthorized'),
      errorLoadReport: t('vendorPayables.errorLoadReport'),
      errorLoadBranches: t('vendorPayables.errorLoadBranches'),
      notAvailable: t('vendorPayables.notAvailable'),
    }),
    [t]
  );
  const defaults = useMemo(() => defaultDateRange(), []);
  const [branches, setBranches] = useState<AdminBranchListItem[]>([]);
  const [branchesLoading, setBranchesLoading] = useState(canSwitchBranch);
  const [branchesError, setBranchesError] = useState<string | null>(null);
  const [branchId, setBranchId] = useState(() => searchParams.get('branchId') || auth.session?.branchId || '');
  const [fromDate, setFromDate] = useState(() =>
    isValidDateInput(searchParams.get('fromDate')) ? searchParams.get('fromDate')! : defaults.fromDate
  );
  const [toDate, setToDate] = useState(() =>
    isValidDateInput(searchParams.get('toDate')) ? searchParams.get('toDate')! : defaults.toDate
  );
  const [report, setReport] = useState<VendorPayablesReportResponse | null>(null);
  const [loading, setLoading] = useState(canAccess);
  const [error, setError] = useState<string | null>(null);

  const selectedBranch = useMemo(
    () => branches.find(branch => branch.branchId === branchId) ?? null,
    [branches, branchId]
  );

  const loadBranches = async () => {
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
      setBranchId(current =>
        current && activeBranchIds.has(current)
          ? current
          : auth.session?.branchId && activeBranchIds.has(auth.session.branchId)
            ? auth.session.branchId
            : activeBranches.length === 1
              ? activeBranches[0].branchId
              : ''
      );
    } catch (caughtError) {
      setBranchesError(reportMessages.errorLoadBranches);
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setBranchesLoading(false);
    }
  };

  const loadReport = async (nextBranchId: string, nextFromDate: string, nextToDate: string) => {
    if (!canAccess) {
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const response = await getVendorPayablesReport({
        branchId: nextBranchId || undefined,
        fromDate: nextFromDate,
        toDate: nextToDate,
      });
      setReport(response);
    } catch (caughtError) {
      setError(
        getSafeVendorPayablesReportErrorMessage(
          caughtError,
          reportMessages.errorLoadReport,
          {
            sessionExpired: reportMessages.sessionExpired,
            unauthorized: reportMessages.unauthorized,
          }
        )
      );
      setReport(null);
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (!canSwitchBranch) {
      return;
    }

    void loadBranches();
  }, [canSwitchBranch]);

  useEffect(() => {
    if (!canAccess) {
      return;
    }

    void loadReport(branchId, fromDate, toDate);
  }, [branchId, canAccess, fromDate, toDate]);

  const handleFilterChange = (nextBranchId: string, nextFromDate: string, nextToDate: string) => {
    const nextParams = new URLSearchParams(searchParams);
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
    setBranchId(nextBranchId);
    setFromDate(nextFromDate);
    setToDate(nextToDate);
  };

  if (!canAccess) {
    return (
      <InventoryManagementLayout
        title={reportMessages.workspaceTitle}
        description={reportMessages.workspaceDescription}
        breadcrumbs={[t('nav.dashboard'), t('nav.vendorPayables')]}
        operatorLabel={operatorLabel}
        restaurantName={restaurantName}
        branchName={branchName}
        navItems={navItems}
      >
        <EmptyState
          title={reportMessages.notAuthorizedTitle}
          description={reportMessages.notAuthorizedDescription}
          tone="inventory"
        />
      </InventoryManagementLayout>
    );
  }

  const summary = report?.summary ?? null;
  const vendorRows = buildVendorRows(report);
  const overdueRows = buildOverdueRows(report);
  const settlementRows = buildSettlementRows(report);
  const inventoryRows = buildInventoryRows(report);

  return (
    <InventoryManagementLayout
      title={reportMessages.workspaceTitle}
      description={reportMessages.workspaceDescription}
      breadcrumbs={[t('nav.dashboard'), t('nav.vendorPayables')]}
      operatorLabel={operatorLabel}
      restaurantName={restaurantName}
      branchName={selectedBranch?.name ?? branchName}
      navItems={navItems}
      actions={
        <Button variant="secondary" onClick={() => void loadReport(branchId, fromDate, toDate)} disabled={loading}>
          {reportMessages.refreshReport}
        </Button>
      }
    >
      <div className="preview-sequence vendor-payables-report">
        {error ? (
          <div className="admin-notice admin-notice--danger" role="alert">
            {error}
          </div>
        ) : null}

        <Card
          title={reportMessages.reportFiltersTitle}
          description={reportMessages.reportFiltersDescription}
          tone="inventory"
          actions={<Badge tone={loading ? 'warning' : 'neutral'} label={loading ? reportMessages.refreshing : reportMessages.ready} />}
        >
          <div className="admin-form-grid">
            {canSwitchBranch ? (
              <Select
                label={reportMessages.branchLabel}
                value={branchId}
                onChange={event => handleFilterChange(event.target.value, fromDate, toDate)}
                helperText={branchesLoading ? reportMessages.branchHelperLoading : reportMessages.branchHelperReady}
                error={branchesError ?? undefined}
              >
                <option value="">{reportMessages.allBranchesOption}</option>
                {branches.map(branch => (
                  <option key={branch.branchId} value={branch.branchId}>
                    {branch.name}
                  </option>
                ))}
              </Select>
            ) : null}
            <Input
              label={reportMessages.fromDateLabel}
              type="date"
              value={fromDate}
              onChange={event => handleFilterChange(branchId, event.target.value, toDate)}
            />
            <Input
              label={reportMessages.toDateLabel}
              type="date"
              value={toDate}
              onChange={event => handleFilterChange(branchId, fromDate, event.target.value)}
            />
          </div>
        </Card>

        {!report ? (
          loading ? (
            <EmptyState title={reportMessages.loadingReportTitle} description={reportMessages.loadingReportDescription} tone="inventory" />
          ) : (
            <EmptyState
              title={reportMessages.noReportLoadedTitle}
              description={reportMessages.noReportLoadedDescription}
              tone="inventory"
              actionLabel={reportMessages.refreshReportAction}
              onAction={() => void loadReport(branchId, fromDate, toDate)}
            />
          )
        ) : (
          <>
            <div className="summary-grid">
              <SummaryCard
                label={reportMessages.purchaseTotalLabel}
                value={formatVendorPayablesCurrency(summary?.totalPurchaseAmount ?? 0, report.currencyCode)}
                detail={reportMessages.purchaseTotalLabel}
                tone="inventory"
              />
              <SummaryCard
                label={reportMessages.paidTotalLabel}
                value={formatVendorPayablesCurrency(summary?.totalPaidAmount ?? 0, report.currencyCode)}
                detail={reportMessages.paidTotalLabel}
                tone="dashboard"
              />
              <SummaryCard
                label={reportMessages.outstandingTotalLabel}
                value={formatVendorPayablesCurrency(summary?.totalOutstandingAmount ?? 0, report.currencyCode)}
                detail={`${summary?.totalVendorBills ?? 0} ${reportMessages.overdueBillsLabel}`}
                tone="admin"
              />
              <SummaryCard
                label={reportMessages.overdueBillsLabel}
                value={formatCount(summary?.overdueBillCount ?? 0)}
                detail={reportMessages.overdueBillsLabel}
                tone="admin"
              />
              <SummaryCard
                label={reportMessages.unpaidBillsLabel}
                value={formatCount(summary?.unpaidBillCount ?? 0)}
                tone="inventory"
              />
              <SummaryCard
                label={reportMessages.partiallyPaidBillsLabel}
                value={formatCount(summary?.partiallyPaidBillCount ?? 0)}
                tone="orders"
              />
              <SummaryCard
                label={reportMessages.paidBillsLabel}
                value={formatCount(summary?.paidBillCount ?? 0)}
                tone="dashboard"
              />
              <SummaryCard
                label={reportMessages.cancelledBillsLabel}
                value={formatCount(summary?.cancelledBillCount ?? 0)}
                tone="inventory"
              />
            </div>

            <div className="preview-sequence">
              <Card title={reportMessages.vendorBalancesTitle} description={reportMessages.vendorBalancesDescription} tone="inventory">
                <ResponsiveDataList
                  rows={vendorRows}
                  columns={[
                    { key: 'vendorName', label: t('vendorPayables.vendorColumnLabel') },
                    { key: 'vendorType', label: t('vendorPayables.typeColumnLabel') },
                    { key: 'totalBills', label: t('vendorPayables.billsColumnLabel'), align: 'right', render: row => formatCount(row.totalBills) },
                    {
                      key: 'purchaseAmount',
                      label: t('vendorPayables.purchaseColumnLabel'),
                      align: 'right',
                      render: row => formatVendorPayablesCurrency(row.purchaseAmount, report.currencyCode),
                    },
                    {
                      key: 'paidAmount',
                      label: t('vendorPayables.paidColumnLabel'),
                      align: 'right',
                      render: row => formatVendorPayablesCurrency(row.paidAmount, report.currencyCode),
                    },
                    {
                      key: 'outstandingAmount',
                      label: t('vendorPayables.outstandingColumnLabel'),
                      align: 'right',
                      render: row => formatVendorPayablesCurrency(row.outstandingAmount, report.currencyCode),
                    },
                    { key: 'unpaidCount', label: t('vendorPayables.unpaidColumnLabel'), align: 'right', render: row => formatCount(row.unpaidCount) },
                    { key: 'partiallyPaidCount', label: t('vendorPayables.partialColumnLabel'), align: 'right', render: row => formatCount(row.partiallyPaidCount) },
                    { key: 'overdueCount', label: t('vendorPayables.overdueColumnLabel'), align: 'right', render: row => formatCount(row.overdueCount) },
                  ]}
                  mobileTitle={row => row.vendorName}
                  mobileDescription={row => `${formatVendorPayablesCurrency(row.outstandingAmount, report.currencyCode)} ${t('vendorPayables.outstandingSuffix')}`}
                  emptyTitle={reportMessages.noVendorBalancesTitle}
                  emptyDescription={reportMessages.noVendorBalancesDescription}
                />
              </Card>

              <Card title={reportMessages.overdueVendorBillsTitle} description={reportMessages.overdueVendorBillsDescription} tone="inventory">
                <ResponsiveDataList
                  rows={overdueRows}
                  columns={[
                    { key: 'vendorName', label: t('vendorPayables.vendorColumnLabel') },
                    { key: 'billNumber', label: t('vendorPayables.billNumberColumnLabel'), render: row => row.billNumber ?? reportMessages.notAvailable },
                    { key: 'branchName', label: t('vendorPayables.branchColumnLabel'), render: row => row.branchName ?? reportMessages.allBranchesOption },
                    { key: 'billDate', label: t('vendorPayables.billDateColumnLabel'), render: row => row.billDate.slice(0, 10) },
                    { key: 'dueDate', label: t('vendorPayables.dueDateColumnLabel'), render: row => row.dueDate ? row.dueDate.slice(0, 10) : reportMessages.notAvailable },
                    {
                      key: 'totalAmount',
                      label: t('vendorPayables.totalColumnLabel'),
                      align: 'right',
                      render: row => formatVendorPayablesCurrency(row.totalAmount, report.currencyCode),
                    },
                    {
                      key: 'paidAmount',
                      label: t('vendorPayables.paidColumnLabel'),
                      align: 'right',
                      render: row => formatVendorPayablesCurrency(row.paidAmount, report.currencyCode),
                    },
                    {
                      key: 'outstandingAmount',
                      label: t('vendorPayables.outstandingColumnLabel'),
                      align: 'right',
                      render: row => formatVendorPayablesCurrency(row.outstandingAmount, report.currencyCode),
                    },
                    { key: 'status', label: t('vendorPayables.statusColumnLabel') },
                  ]}
                  mobileTitle={row => row.billNumber ?? row.vendorName}
                  mobileDescription={row => `${row.vendorName} · ${formatVendorPayablesCurrency(row.outstandingAmount, report.currencyCode)} ${t('vendorPayables.outstandingSuffix')}`}
                  emptyTitle={reportMessages.noOverdueBillsTitle}
                  emptyDescription={reportMessages.noOverdueBillsDescription}
                />
              </Card>
            </div>

            <div className="preview-sequence">
              <Card title={reportMessages.settlementHistoryTitle} description={reportMessages.settlementHistoryDescription} tone="inventory">
                <ResponsiveDataList
                  rows={settlementRows}
                  columns={[
                    { key: 'paidAtUtc', label: t('vendorPayables.paidAtColumnLabel'), render: row => formatVendorPayablesTimestamp(row.paidAtUtc, reportMessages) },
                    { key: 'vendorName', label: t('vendorPayables.vendorColumnLabel') },
                    { key: 'billNumber', label: t('vendorPayables.billNumberColumnLabel'), render: row => row.billNumber ?? reportMessages.notAvailable },
                    { key: 'branchName', label: t('vendorPayables.branchColumnLabel'), render: row => row.branchName ?? reportMessages.allBranchesOption },
                    { key: 'paymentMode', label: t('vendorPayables.modeColumnLabel') },
                    {
                      key: 'amount',
                      label: t('vendorPayables.amountColumnLabel'),
                      align: 'right',
                      render: row => formatVendorPayablesCurrency(row.amount, report.currencyCode),
                    },
                    {
                      key: 'referenceNumberMasked',
                      label: t('vendorPayables.referenceColumnLabel'),
                      render: row => formatVendorPayablesReference(row.referenceNumberMasked, reportMessages),
                    },
                  ]}
                  mobileTitle={row => row.vendorName}
                  mobileDescription={row => `${formatVendorPayablesCurrency(row.amount, report.currencyCode)} · ${row.paymentMode}`}
                  emptyTitle={reportMessages.noSettlementsTitle}
                  emptyDescription={reportMessages.noSettlementsDescription}
                />
              </Card>

              <Card title={reportMessages.inventoryPurchaseTotalsTitle} description={reportMessages.inventoryPurchaseTotalsDescription} tone="inventory">
                <ResponsiveDataList
                  rows={inventoryRows}
                  columns={[
                    { key: 'inventoryItemName', label: t('vendorPayables.inventoryItemColumnLabel') },
                    { key: 'quantity', label: t('vendorPayables.quantityColumnLabel'), align: 'right', render: row => row.quantity.toLocaleString() },
                    {
                      key: 'amount',
                      label: t('vendorPayables.amountColumnLabel'),
                      align: 'right',
                      render: row => formatVendorPayablesCurrency(row.amount, report.currencyCode),
                    },
                  ]}
                  mobileTitle={row => row.inventoryItemName}
                  mobileDescription={row => formatVendorPayablesCurrency(row.amount, report.currencyCode)}
                  emptyTitle={reportMessages.noInventoryLinkedPurchasesTitle}
                  emptyDescription={reportMessages.noInventoryLinkedPurchasesDescription}
                />
              </Card>
            </div>

            <div className="admin-form-note">
              {reportMessages.scopePrefix}: {formatVendorPayablesScopeSummary(report)}. {reportMessages.generatedPrefix} {formatVendorPayablesTimestamp(report.generatedAt, reportMessages)}.
            </div>
            <div className="admin-form-note report-copyright-notice">
              {t('software.copyrightNotice')}
            </div>
          </>
        )}
      </div>
    </InventoryManagementLayout>
  );
};

export default VendorPayablesReportPage;
