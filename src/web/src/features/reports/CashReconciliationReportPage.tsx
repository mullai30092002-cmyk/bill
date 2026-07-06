import { useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';

import { isApiError } from '../../api/apiErrors';
import { InventoryManagementLayout } from '../../components/layout';
import type { ShellNavItem } from '../../components/layout/navigation';
import { Badge, Button, Card, EmptyState, Input, ResponsiveDataList, Select, StatusBadge, SummaryCard } from '../../components/ui';
import { listAdminBranches } from '../admin/adminApi';
import type { AdminBranchListItem } from '../admin/adminTypes';
import { sortBranches } from '../admin/branches/branchDisplay';
import { useAuth } from '../auth/useAuth';
import { useLanguage } from '../../i18n/LanguageProvider';
import { getCashReconciliationReport } from './cashReconciliationReportApi';
import {
  buildCashReconciliationScopeSummary,
  formatCashReconciliationCurrency,
  formatCashReconciliationDateInput,
  formatCashReconciliationTimestamp,
  getCashReconciliationVarianceTone,
} from './cashReconciliationReportDisplay';
import { getSafeCashReconciliationReportErrorMessage } from './cashReconciliationReportErrorDisplay';
import type {
  CashReconciliationReportResponse,
  CashReconciliationShiftRow,
  CashReconciliationVarianceStatus,
} from './cashReconciliationReportTypes';

export interface CashReconciliationReportPageProps {
  navItems?: ShellNavItem[];
  restaurantName?: string;
  branchName?: string;
  operatorLabel?: string;
}

type ReportRow = CashReconciliationShiftRow & { id: string };

const isValidDateInput = (value: string | null) => Boolean(value && /^\d{4}-\d{2}-\d{2}$/.test(value));

const toRows = (report: CashReconciliationReportResponse | null): ReportRow[] =>
  (report?.shifts ?? []).map(row => ({
    ...row,
    id: row.cashierShiftId,
  }));

const varianceTone = (status: CashReconciliationVarianceStatus) => getCashReconciliationVarianceTone(status);

export const CashReconciliationReportPage = ({
  navItems,
  restaurantName,
  branchName,
  operatorLabel,
}: CashReconciliationReportPageProps) => {
  const auth = useAuth();
  const { t } = useLanguage();
  const [searchParams, setSearchParams] = useSearchParams();
  const canAccess = auth.hasPermission('Report.View');
  const canSwitchBranch = auth.hasPermission('Branch.Manage') || auth.hasPermission('User.Manage');
  const messages = useMemo(
    () => ({
      workspaceTitle: t('cashReconciliation.workspaceTitle'),
      workspaceDescription: t('cashReconciliation.workspaceDescription'),
      notAuthorizedTitle: t('cashReconciliation.notAuthorizedTitle'),
      notAuthorizedDescription: t('cashReconciliation.notAuthorizedDescription'),
      branchLabel: t('cashReconciliation.branchLabel'),
      allBranchesOption: t('cashReconciliation.allBranchesOption'),
      branchHelperLoading: t('cashReconciliation.branchHelperLoading'),
      branchHelperReady: t('cashReconciliation.branchHelperReady'),
      businessDateLabel: t('cashReconciliation.businessDateLabel'),
      refresh: t('cashReconciliation.refresh'),
      refreshing: t('cashReconciliation.refreshing'),
      ready: t('cashReconciliation.ready'),
      loadingTitle: t('cashReconciliation.loadingTitle'),
      loadingDescription: t('cashReconciliation.loadingDescription'),
      noReportLoadedTitle: t('cashReconciliation.noReportLoadedTitle'),
      noReportLoadedDescription: t('cashReconciliation.noReportLoadedDescription'),
      viewCashReconciliation: t('cashReconciliation.viewCashReconciliation'),
      openingCash: t('cashReconciliation.openingCash'),
      cashPayments: t('cashReconciliation.cashPayments'),
      cashIn: t('cashReconciliation.cashIn'),
      cashOut: t('cashReconciliation.cashOut'),
      adjustments: t('cashReconciliation.adjustments'),
      expectedCash: t('cashReconciliation.expectedCash'),
      declaredCash: t('cashReconciliation.declaredCash'),
      variance: t('cashReconciliation.variance'),
      varianceStatus: t('cashReconciliation.varianceStatus'),
      balanced: t('cashReconciliation.balanced'),
      minorVariance: t('cashReconciliation.minorVariance'),
      majorVariance: t('cashReconciliation.majorVariance'),
      openShift: t('cashReconciliation.openShift'),
      cashier: t('cashReconciliation.cashier'),
      status: t('cashReconciliation.status'),
      opened: t('cashReconciliation.opened'),
      closed: t('cashReconciliation.closed'),
      movements: t('cashReconciliation.movements'),
      noCashierShiftsTitle: t('cashReconciliation.noCashierShiftsTitle'),
      noCashierShiftsDescription: t('cashReconciliation.noCashierShiftsDescription'),
      openShifts: t('cashReconciliation.openShifts'),
      majorCashVariance: t('cashReconciliation.majorCashVariance'),
      sessionExpired: t('cashReconciliation.sessionExpired'),
      unauthorized: t('cashReconciliation.unauthorized'),
      errorLoadReport: t('cashReconciliation.errorLoadReport'),
      errorLoadBranches: t('cashReconciliation.errorLoadBranches'),
    }),
    [t]
  );

  const [branches, setBranches] = useState<AdminBranchListItem[]>([]);
  const [branchesLoading, setBranchesLoading] = useState(canSwitchBranch);
  const [branchesError, setBranchesError] = useState<string | null>(null);
  const [branchId, setBranchId] = useState(() => searchParams.get('branchId') || auth.session?.branchId || '');
  const [businessDate, setBusinessDate] = useState(() =>
    isValidDateInput(searchParams.get('businessDate')) ? searchParams.get('businessDate')! : formatCashReconciliationDateInput()
  );
  const [report, setReport] = useState<CashReconciliationReportResponse | null>(null);
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
      setBranchesError(messages.errorLoadBranches);
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setBranchesLoading(false);
    }
  };

  const loadReport = async (selectedBranchId: string, date: string) => {
    if (!canAccess) {
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const response = await getCashReconciliationReport({
        branchId: selectedBranchId || undefined,
        businessDate: date,
      });
      setReport(response);
    } catch (caughtError) {
      setError(
        getSafeCashReconciliationReportErrorMessage(
          caughtError,
          messages.errorLoadReport,
          {
            sessionExpired: messages.sessionExpired,
            unauthorized: messages.unauthorized,
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

    if (canSwitchBranch && branchesLoading && !branchId) {
      return;
    }

    void loadReport(branchId, businessDate);
  }, [branchId, businessDate, canAccess, canSwitchBranch, branchesLoading]);

  const handleFilterChange = (nextBranchId: string, nextBusinessDate: string) => {
    const nextParams = new URLSearchParams(searchParams);

    if (nextBranchId) {
      nextParams.set('branchId', nextBranchId);
    } else {
      nextParams.delete('branchId');
    }

    if (nextBusinessDate) {
      nextParams.set('businessDate', nextBusinessDate);
    } else {
      nextParams.delete('businessDate');
    }

    setSearchParams(nextParams, { replace: true });
    setBranchId(nextBranchId);
    setBusinessDate(nextBusinessDate);
  };

  const rows = useMemo(() => toRows(report), [report]);
  const summary = report?.totals ?? null;
  const formatAmount = (value: number) => formatCashReconciliationCurrency(value, report?.currencyCode);
  const formatNullableAmount = (value: number | null) => (value === null ? '—' : formatAmount(value));

  if (!canAccess) {
    return (
      <InventoryManagementLayout
        title={messages.workspaceTitle}
        description={messages.workspaceDescription}
        breadcrumbs={[t('nav.dashboard'), t('nav.cashReconciliation')]}
        operatorLabel={operatorLabel}
        restaurantName={restaurantName}
        branchName={branchName}
        navItems={navItems}
      >
        <EmptyState
          title={messages.notAuthorizedTitle}
          description={messages.notAuthorizedDescription}
          tone="inventory"
        />
      </InventoryManagementLayout>
    );
  }

  return (
    <InventoryManagementLayout
      title={messages.workspaceTitle}
      description={messages.workspaceDescription}
      breadcrumbs={[t('nav.dashboard'), t('nav.cashReconciliation')]}
      operatorLabel={operatorLabel}
      restaurantName={restaurantName}
      branchName={selectedBranch?.name ?? (branchId ? branchName : undefined)}
      navItems={navItems}
    >
      <div className="preview-sequence cash-reconciliation-report">
        {error ? (
          <div className="admin-notice admin-notice--danger" role="alert">
            {error}
          </div>
        ) : null}

        <Card
          title={messages.workspaceTitle}
          description={messages.workspaceDescription}
          tone="inventory"
          actions={<Badge tone={loading ? 'warning' : 'neutral'} label={loading ? messages.refreshing : messages.ready} />}
        >
          <div className="admin-form-grid">
            {canSwitchBranch ? (
              <Select
                label={messages.branchLabel}
                value={branchId}
                onChange={event => handleFilterChange(event.target.value, businessDate)}
                helperText={branchesLoading ? messages.branchHelperLoading : messages.branchHelperReady}
                error={branchesError ?? undefined}
              >
                <option value="">{messages.allBranchesOption}</option>
                {branches.map(branch => (
                  <option key={branch.branchId} value={branch.branchId}>
                    {branch.name}
                  </option>
                ))}
              </Select>
            ) : null}
            <Input
              label={messages.businessDateLabel}
              type="date"
              value={businessDate}
              onChange={event => handleFilterChange(branchId, event.target.value)}
            />
            <div className="ui-field">
              <span className="ui-field__label">&nbsp;</span>
              <Button variant="secondary" onClick={() => void loadReport(branchId, businessDate)} disabled={loading}>
                {messages.refresh}
              </Button>
            </div>
          </div>
        </Card>

        {!report ? (
          loading ? (
            <EmptyState title={messages.loadingTitle} description={messages.loadingDescription} tone="inventory" />
          ) : (
            <EmptyState
              title={messages.noReportLoadedTitle}
              description={messages.noReportLoadedDescription}
              tone="inventory"
              actionLabel={messages.viewCashReconciliation}
              onAction={() => void loadReport(branchId, businessDate)}
            />
          )
        ) : (
          <>
            <div className="summary-grid">
              <SummaryCard label={messages.openingCash} value={formatAmount(summary?.openingCashTotal ?? 0)} tone="inventory" />
              <SummaryCard label={messages.cashPayments} value={formatAmount(summary?.cashPaymentTotal ?? 0)} tone="dashboard" />
              <SummaryCard label={messages.expectedCash} value={formatAmount(summary?.expectedCashTotal ?? 0)} tone="accent" />
              <SummaryCard label={messages.declaredCash} value={formatAmount(summary?.declaredCashTotal ?? 0)} tone="orders" />
              <SummaryCard
                label={messages.variance}
                value={formatAmount(summary?.varianceTotal ?? 0)}
                tone={summary && Math.abs(summary.varianceTotal) > 0 ? 'admin' : 'dashboard'}
              />
              <SummaryCard
                label={messages.openShifts}
                value={(summary?.openShiftCount ?? 0).toLocaleString()}
                tone={summary && summary.openShiftCount > 0 ? 'admin' : 'inventory'}
              />
              <SummaryCard
                label={messages.majorCashVariance}
                value={(summary?.majorVarianceCount ?? 0).toLocaleString()}
                tone={summary && summary.majorVarianceCount > 0 ? 'admin' : 'inventory'}
              />
            </div>

            <Card title={messages.workspaceTitle} description={messages.workspaceDescription} tone="inventory">
              <ResponsiveDataList
                rows={rows}
                columns={[
                  { key: 'cashierName', label: messages.cashier },
                  {
                    key: 'status',
                    label: messages.status,
                    render: row => <StatusBadge status={row.status} />,
                  },
                  {
                    key: 'openedAt',
                    label: messages.opened,
                    render: row => formatCashReconciliationTimestamp(row.openedAt),
                  },
                  {
                    key: 'closedAt',
                    label: messages.closed,
                    render: row => (row.closedAt ? formatCashReconciliationTimestamp(row.closedAt) : '—'),
                  },
                  {
                    key: 'openingCashAmount',
                    label: messages.openingCash,
                    align: 'right',
                    render: row => formatAmount(row.openingCashAmount),
                  },
                  {
                    key: 'cashPaymentTotal',
                    label: messages.cashPayments,
                    align: 'right',
                    render: row => formatAmount(row.cashPaymentTotal),
                  },
                  {
                    key: 'movementCount',
                    label: messages.movements,
                    render: row => (
                      <div className="admin-workspace-stack">
                        <strong>{row.movementCount.toLocaleString()}</strong>
                        <span>
                          {messages.cashIn}: {formatAmount(row.cashInTotal)} · {messages.cashOut}: {formatAmount(row.cashOutTotal)} · {messages.adjustments}:{' '}
                          {formatAmount(row.adjustmentTotal)}
                        </span>
                        {row.closingNote ? <span>{row.closingNote}</span> : null}
                      </div>
                    ),
                  },
                  {
                    key: 'expectedCashAmount',
                    label: messages.expectedCash,
                    align: 'right',
                    render: row => formatAmount(row.expectedCashAmount),
                  },
                  {
                    key: 'declaredClosingCashAmount',
                    label: messages.declaredCash,
                    align: 'right',
                    render: row => formatNullableAmount(row.declaredClosingCashAmount),
                  },
                  {
                    key: 'varianceAmount',
                    label: messages.variance,
                    align: 'right',
                    render: row => formatNullableAmount(row.varianceAmount),
                  },
                  {
                    key: 'varianceStatus',
                    label: messages.varianceStatus,
                    render: row => (
                      <Badge
                        tone={varianceTone(row.varianceStatus)}
                        label={
                          row.varianceStatus === 'Balanced'
                            ? messages.balanced
                            : row.varianceStatus === 'MinorVariance'
                              ? messages.minorVariance
                              : row.varianceStatus === 'MajorVariance'
                                ? messages.majorVariance
                                : messages.openShift
                        }
                      />
                    ),
                  },
                ]}
                mobileTitle={row => row.cashierName}
                mobileDescription={row =>
                  [
                    row.status,
                    formatCashReconciliationTimestamp(row.openedAt),
                    row.closedAt ? formatCashReconciliationTimestamp(row.closedAt) : messages.openShift,
                    row.closingNote ?? null,
                  ]
                    .filter(Boolean)
                    .join(' · ')
                }
                emptyTitle={messages.noCashierShiftsTitle}
                emptyDescription={messages.noCashierShiftsDescription}
              />
            </Card>

            <div className="admin-form-note">{buildCashReconciliationScopeSummary(report)}</div>
            <div className="admin-form-note report-copyright-notice">{t('software.copyrightNotice')}</div>
          </>
        )}
      </div>
    </InventoryManagementLayout>
  );
};

export default CashReconciliationReportPage;
