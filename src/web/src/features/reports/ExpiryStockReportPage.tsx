import { useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';

import { isApiError } from '../../api/apiErrors';
import { InventoryManagementLayout } from '../../components/layout';
import type { ShellNavItem } from '../../components/layout/navigation';
import { Badge, Button, Card, EmptyState, Input, ResponsiveDataList, Select, SummaryCard } from '../../components/ui';
import { listAdminBranches } from '../admin/adminApi';
import type { AdminBranchListItem } from '../admin/adminTypes';
import { sortBranches } from '../admin/branches/branchDisplay';
import { useAuth } from '../auth/useAuth';
import { useLanguage } from '../../i18n/LanguageProvider';
import { getExpiryStockReport } from './expiryStockReportApi';
import type { ExpiryStockReportResponse, ExpiryStockReportRow, ExpiryStatus } from './expiryStockReportTypes';

export interface ExpiryStockReportPageProps {
  navItems?: ShellNavItem[];
  restaurantName?: string;
  branchName?: string;
  operatorLabel?: string;
}

type ReportRow = ExpiryStockReportRow & { id: string };

const isValidDateInput = (value: string | null) => Boolean(value && /^\d{4}-\d{2}-\d{2}$/.test(value));

const defaultDateInput = () => {
  const today = new Date();
  const year = today.getFullYear();
  const month = `${today.getMonth() + 1}`.padStart(2, '0');
  const day = `${today.getDate()}`.padStart(2, '0');
  return `${year}-${month}-${day}`;
};

const toRows = (report: ExpiryStockReportResponse | null): ReportRow[] =>
  (report?.rows ?? []).map((row, index) => ({
    ...row,
    id: `${row.inventoryItemId}-${row.sourceType}-${index}`,
  }));

const formatDateTime = (value: string | null) => {
  if (!value) {
    return '—';
  }
  try {
    return new Date(value).toLocaleString(undefined, {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  } catch {
    return value;
  }
};

const expiryStatusTone = (status: ExpiryStatus): 'success' | 'warning' | 'danger' | 'neutral' => {
  if (status === 'Expired') return 'danger';
  if (status === 'NearExpiry') return 'warning';
  if (status === 'Fresh') return 'success';
  return 'neutral';
};

export const ExpiryStockReportPage = ({
  navItems,
  restaurantName,
  branchName,
  operatorLabel,
}: ExpiryStockReportPageProps) => {
  const auth = useAuth();
  const { t } = useLanguage();
  const [searchParams, setSearchParams] = useSearchParams();
  const canAccess = auth.hasPermission('Report.View');
  const canSwitchBranch = auth.hasPermission('Branch.Manage') || auth.hasPermission('User.Manage');

  const messages = useMemo(
    () => ({
      workspaceTitle: t('expiryStock.workspaceTitle'),
      workspaceDescription: t('expiryStock.workspaceDescription'),
      notAuthorizedTitle: t('expiryStock.notAuthorizedTitle'),
      notAuthorizedDescription: t('expiryStock.notAuthorizedDescription'),
      reportTitle: t('expiryStock.reportTitle'),
      branchLabel: t('expiryStock.branchLabel'),
      branchHelperLoading: t('expiryStock.branchHelperLoading'),
      branchHelperReady: t('expiryStock.branchHelperReady'),
      asOfDateLabel: t('expiryStock.asOfDateLabel'),
      refresh: t('expiryStock.refresh'),
      refreshing: t('expiryStock.refreshing'),
      ready: t('expiryStock.ready'),
      loadingTitle: t('expiryStock.loadingTitle'),
      loadingDescription: t('expiryStock.loadingDescription'),
      noBranchSelectedTitle: t('expiryStock.noBranchSelectedTitle'),
      noBranchSelectedDescription: t('expiryStock.noBranchSelectedDescription'),
      noActivityTitle: t('expiryStock.noActivityTitle'),
      noActivityDescription: t('expiryStock.noActivityDescription'),
      noReportLoadedTitle: t('expiryStock.noReportLoadedTitle'),
      noReportLoadedDescription: t('expiryStock.noReportLoadedDescription'),
      refreshReportAction: t('expiryStock.refreshReportAction'),
      fresh: t('expiryStock.fresh'),
      nearExpiry: t('expiryStock.nearExpiry'),
      expired: t('expiryStock.expired'),
      noExpiry: t('expiryStock.noExpiry'),
      freshCount: t('expiryStock.freshCount'),
      nearExpiryCount: t('expiryStock.nearExpiryCount'),
      expiredCount: t('expiryStock.expiredCount'),
      totalTrackedItems: t('expiryStock.totalTrackedItems'),
      itemLabel: t('expiryStock.itemLabel'),
      unitLabel: t('expiryStock.unitLabel'),
      sourceTypeLabel: t('expiryStock.sourceTypeLabel'),
      quantityLabel: t('expiryStock.lotRemainingLabel'),
      expiresAtLabel: t('expiryStock.expiresAtLabel'),
      batchReferenceLabel: t('expiryStock.batchReferenceLabel'),
      statusLabel: t('expiryStock.statusLabel'),
      sourceBatchProduction: t('expiryStock.sourceBatchProduction'),
      sourceStockIn: t('expiryStock.sourceStockIn'),
      sourceAdjustment: t('expiryStock.sourceAdjustment'),
      openingLot: t('expiryStock.openingLot'),
      currentLotRemaining: t('expiryStock.currentLotRemaining'),
      expiryStatusAsOfDate: t('expiryStock.expiryStatusAsOfDate'),
      readOnlyNote: t('expiryStock.readOnlyNote'),
      sessionExpired: t('expiryStock.sessionExpired'),
      unauthorized: t('expiryStock.unauthorized'),
      errorLoadReport: t('expiryStock.errorLoadReport'),
      errorLoadBranches: t('expiryStock.errorLoadBranches'),
    }),
    [t]
  );

  const [branches, setBranches] = useState<AdminBranchListItem[]>([]);
  const [branchesLoading, setBranchesLoading] = useState(canSwitchBranch);
  const [branchesError, setBranchesError] = useState<string | null>(null);
  const [asOfDate, setAsOfDate] = useState(() =>
    isValidDateInput(searchParams.get('asOfDate')) ? searchParams.get('asOfDate')! : defaultDateInput()
  );
  const [branchId, setBranchId] = useState(() => searchParams.get('branchId') || auth.session?.branchId || '');
  const [report, setReport] = useState<ExpiryStockReportResponse | null>(null);
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
    if (!canAccess || !selectedBranchId) {
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const response = await getExpiryStockReport({
        branchId: selectedBranchId,
        asOfDate: date,
      });
      setReport(response);
    } catch (caughtError) {
      setError(
        isApiError(caughtError) && caughtError.status === 401
          ? messages.sessionExpired
          : isApiError(caughtError) && caughtError.status === 403
            ? messages.unauthorized
            : messages.errorLoadReport
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
    if (!canAccess || !branchId) {
      return;
    }

    void loadReport(branchId, asOfDate);
  }, [branchId, asOfDate, canAccess]);

  const handleFilterChange = (nextBranchId: string, nextAsOfDate: string) => {
    const nextParams = new URLSearchParams(searchParams);

    if (nextBranchId) {
      nextParams.set('branchId', nextBranchId);
    } else {
      nextParams.delete('branchId');
    }

    if (nextAsOfDate) {
      nextParams.set('asOfDate', nextAsOfDate);
    } else {
      nextParams.delete('asOfDate');
    }

    setSearchParams(nextParams, { replace: true });
    setBranchId(nextBranchId);
    setAsOfDate(nextAsOfDate);
  };

  const resolveSourceLabel = (sourceType: string) => {
    if (sourceType === 'BatchProduction') return messages.sourceBatchProduction;
    if (sourceType === 'StockIn') return messages.sourceStockIn;
    if (sourceType === 'Adjustment') return messages.sourceAdjustment;
    if (sourceType === 'OpeningLot') return messages.openingLot;
    return sourceType;
  };

  const resolveStatusLabel = (status: ExpiryStatus) => {
    if (status === 'Fresh') return messages.fresh;
    if (status === 'NearExpiry') return messages.nearExpiry;
    if (status === 'Expired') return messages.expired;
    return messages.noExpiry;
  };

  if (!canAccess) {
    return (
      <InventoryManagementLayout
        title={messages.workspaceTitle}
        description={messages.workspaceDescription}
        breadcrumbs={[t('nav.dashboard'), t('nav.expiryStockReport')]}
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
      breadcrumbs={[t('nav.dashboard'), t('nav.expiryStockReport')]}
      operatorLabel={operatorLabel}
      restaurantName={restaurantName}
      branchName={selectedBranch?.name ?? branchName}
      navItems={navItems}
    >
      <div className="preview-sequence expiry-stock-report">
        {error ? (
          <div className="admin-notice admin-notice--danger" role="alert">
            {error}
          </div>
        ) : null}

        <Card
          title={messages.reportTitle}
          description={messages.workspaceDescription}
          tone="inventory"
          actions={<Badge tone={loading ? 'warning' : 'neutral'} label={loading ? messages.refreshing : messages.ready} />}
        >
          <div className="admin-form-grid">
            {canSwitchBranch ? (
              <Select
                label={messages.branchLabel}
                value={branchId}
                onChange={event => handleFilterChange(event.target.value, asOfDate)}
                helperText={branchesLoading ? messages.branchHelperLoading : messages.branchHelperReady}
                error={branchesError ?? undefined}
              >
                <option value="">{messages.noBranchSelectedTitle}</option>
                {branches.map(branch => (
                  <option key={branch.branchId} value={branch.branchId}>
                    {branch.name}
                  </option>
                ))}
              </Select>
            ) : null}
            <Input
              label={messages.asOfDateLabel}
              type="date"
              value={asOfDate}
              onChange={event => handleFilterChange(branchId, event.target.value)}
            />
            <div className="ui-field">
              <span className="ui-field__label">&nbsp;</span>
              <Button variant="secondary" onClick={() => void loadReport(branchId, asOfDate)} disabled={!branchId || loading}>
                {messages.refresh}
              </Button>
            </div>
          </div>
        </Card>

        {!branchId ? (
          <EmptyState
            title={messages.noBranchSelectedTitle}
            description={messages.noBranchSelectedDescription}
            tone="inventory"
          />
        ) : !report ? (
          loading ? (
            <EmptyState title={messages.loadingTitle} description={messages.loadingDescription} tone="inventory" />
          ) : (
            <EmptyState
              title={messages.noReportLoadedTitle}
              description={messages.noReportLoadedDescription}
              tone="inventory"
              actionLabel={messages.refreshReportAction}
              onAction={() => void loadReport(branchId, asOfDate)}
            />
          )
        ) : report.rows.length === 0 ? (
          <EmptyState
            title={messages.noActivityTitle}
            description={messages.noActivityDescription}
            tone="inventory"
          />
        ) : (
          <>
            <div className="summary-grid">
              <SummaryCard
                label={messages.freshCount}
                value={report.totals.freshCount.toLocaleString()}
                detail={messages.fresh}
                tone="dashboard"
              />
              <SummaryCard
                label={messages.nearExpiryCount}
                value={report.totals.nearExpiryCount.toLocaleString()}
                detail={messages.nearExpiry}
                tone={report.totals.nearExpiryCount > 0 ? 'admin' : 'inventory'}
              />
              <SummaryCard
                label={messages.expiredCount}
                value={report.totals.expiredCount.toLocaleString()}
                detail={messages.expired}
                tone={report.totals.expiredCount > 0 ? 'admin' : 'inventory'}
              />
              <SummaryCard
                label={messages.totalTrackedItems}
                value={report.totals.totalTrackedItems.toLocaleString()}
                detail={messages.itemLabel}
                tone="inventory"
              />
            </div>

            <Card title={messages.reportTitle} tone="inventory">
              <ResponsiveDataList
                rows={toRows(report)}
                columns={[
                  { key: 'inventoryItemName', label: messages.itemLabel },
                  { key: 'sourceType', label: messages.sourceTypeLabel, render: row => resolveSourceLabel(row.sourceType) },
                  {
                    key: 'quantity',
                    label: messages.quantityLabel,
                    align: 'right',
                    render: row => `${row.quantity.toLocaleString(undefined, { maximumFractionDigits: 3 })} ${row.unitOfMeasure}`,
                  },
                  {
                    key: 'expiresAtUtc',
                    label: messages.expiresAtLabel,
                    render: row => formatDateTime(row.expiresAtUtc),
                  },
                  {
                    key: 'batchReference',
                    label: messages.batchReferenceLabel,
                    render: row => row.batchReference ?? '—',
                  },
                  {
                    key: 'expiryStatus',
                    label: messages.statusLabel,
                    render: row => (
                      <Badge
                        tone={expiryStatusTone(row.expiryStatus)}
                        label={resolveStatusLabel(row.expiryStatus)}
                      />
                    ),
                  },
                ]}
                mobileTitle={row => row.inventoryItemName}
                mobileDescription={row =>
                  `${resolveStatusLabel(row.expiryStatus)} · ${formatDateTime(row.expiresAtUtc)}`
                }
                emptyTitle={messages.noActivityTitle}
                emptyDescription={messages.noActivityDescription}
              />
            </Card>

            <div className="admin-form-note">{messages.currentLotRemaining}</div>
            <div className="admin-form-note">{messages.expiryStatusAsOfDate}</div>
            <div className="admin-form-note">
              {messages.readOnlyNote}
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

export default ExpiryStockReportPage;
