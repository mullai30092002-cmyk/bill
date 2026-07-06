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
import type { TranslationFunction } from '../../i18n/LanguageProvider';
import type { TranslationKey } from '../../i18n/translations';
import { getPreparedStockReport } from './preparedStockReportApi';
import type { PreparedStockReportResponse, PreparedStockReportRow } from './preparedStockReportTypes';

export interface PreparedStockReportPageProps {
  navItems?: ShellNavItem[];
  restaurantName?: string;
  branchName?: string;
  operatorLabel?: string;
}

type ReportRow = PreparedStockReportRow & { id: string };

const isValidDateInput = (value: string | null) => Boolean(value && /^\d{4}-\d{2}-\d{2}$/.test(value));

const defaultDateInput = () => {
  const today = new Date();
  const year = today.getFullYear();
  const month = `${today.getMonth() + 1}`.padStart(2, '0');
  const day = `${today.getDate()}`.padStart(2, '0');
  return `${year}-${month}-${day}`;
};

const toRows = (report: PreparedStockReportResponse | null): ReportRow[] =>
  (report?.rows ?? []).map(item => ({
    ...item,
    id: `${item.menuItemId}-${item.preparedInventoryItemId ?? 'missing'}`,
  }));

const formatQuantity = (value: number, unitOfMeasure?: string | null) => {
  const quantity = value.toLocaleString(undefined, {
    maximumFractionDigits: 3,
  });

  return unitOfMeasure ? `${quantity} ${unitOfMeasure}` : quantity;
};

const translateWarningReason = (reason: string | null, t: TranslationFunction) => {
  if (!reason) {
    return null;
  }

  const warnings: Array<{ fragment: string; key: TranslationKey }> = [
    { fragment: 'Missing prepared stock mapping.', key: 'preparedStock.missingPreparedStockMapping' },
    { fragment: 'Negative remaining stock.', key: 'preparedStock.negativeRemainingStock' },
    { fragment: 'Prepared stock item could not be resolved.', key: 'preparedStock.preparedStockItemMissing' },
    { fragment: 'Menu item could not be resolved.', key: 'preparedStock.menuItemMissing' },
    { fragment: 'Prepared stock unit mismatch.', key: 'preparedStock.unitMismatch' },
    { fragment: 'Prepared stock mapping is inconsistent.', key: 'preparedStock.mappingInconsistent' },
    { fragment: 'Menu item is inconsistent.', key: 'preparedStock.menuItemInconsistent' },
    { fragment: 'Prepared stock item is inconsistent.', key: 'preparedStock.preparedStockItemInconsistent' },
    { fragment: 'Menu item is not configured for batch prepared deduction.', key: 'preparedStock.misconfiguredMenuItem' },
  ];

  return reason
    .split(';')
    .map(part => part.trim())
    .filter(Boolean)
    .map(part => warnings.find(entry => part === entry.fragment)?.key)
    .filter((key): key is TranslationKey => key !== undefined)
    .map(key => t(key))
    .join('; ') || reason;
};

export const PreparedStockReportPage = ({
  navItems,
  restaurantName,
  branchName,
  operatorLabel,
}: PreparedStockReportPageProps) => {
  const auth = useAuth();
  const { t } = useLanguage();
  const [searchParams, setSearchParams] = useSearchParams();
  const canAccess = auth.hasPermission('Report.View');
  const canSwitchBranch = auth.hasPermission('Branch.Manage') || auth.hasPermission('User.Manage');
  const reportMessages = useMemo(
    () => ({
      workspaceTitle: t('preparedStock.workspaceTitle'),
      workspaceDescription: t('preparedStock.workspaceDescription'),
      notAuthorizedTitle: t('preparedStock.notAuthorizedTitle'),
      notAuthorizedDescription: t('preparedStock.notAuthorizedDescription'),
      reportTitle: t('preparedStock.reportTitle'),
      branchLabel: t('preparedStock.branchLabel'),
      branchHelperLoading: t('preparedStock.branchHelperLoading'),
      branchHelperReady: t('preparedStock.branchHelperReady'),
      businessDateLabel: t('preparedStock.businessDateLabel'),
      refresh: t('preparedStock.refresh'),
      refreshing: t('preparedStock.refreshing'),
      ready: t('preparedStock.ready'),
      loadingTitle: t('preparedStock.loadingTitle'),
      loadingDescription: t('preparedStock.loadingDescription'),
      noBranchSelectedTitle: t('preparedStock.noBranchSelectedTitle'),
      noBranchSelectedDescription: t('preparedStock.noBranchSelectedDescription'),
      noActivityTitle: t('preparedStock.noActivityTitle'),
      noActivityDescription: t('preparedStock.noActivityDescription'),
      produced: t('preparedStock.produced'),
      served: t('preparedStock.served'),
      wasted: t('preparedStock.wasted'),
      remaining: t('preparedStock.remaining'),
      preparedStockItem: t('preparedStock.preparedStockItem'),
      item: t('preparedStock.item'),
      status: t('preparedStock.status'),
      warning: t('preparedStock.warning'),
      healthy: t('preparedStock.healthy'),
      warningLabel: t('preparedStock.warningLabel'),
      noReportLoadedTitle: t('preparedStock.noReportLoadedTitle'),
      noReportLoadedDescription: t('preparedStock.noReportLoadedDescription'),
      refreshReportAction: t('preparedStock.refreshReportAction'),
      noPreparedStockActivity: t('preparedStock.noPreparedStockActivity'),
      missingPreparedStockMapping: t('preparedStock.missingPreparedStockMapping'),
      negativeRemainingStock: t('preparedStock.negativeRemainingStock'),
      notAvailable: t('preparedStock.notAvailable'),
      sessionExpired: t('preparedStock.sessionExpired'),
      unauthorized: t('preparedStock.unauthorized'),
      errorLoadReport: t('preparedStock.errorLoadReport'),
      errorLoadBranches: t('preparedStock.errorLoadBranches'),
    }),
    [t]
  );
  const [branches, setBranches] = useState<AdminBranchListItem[]>([]);
  const [branchesLoading, setBranchesLoading] = useState(canSwitchBranch);
  const [branchesError, setBranchesError] = useState<string | null>(null);
  const [businessDate, setBusinessDate] = useState(() =>
    isValidDateInput(searchParams.get('businessDate')) ? searchParams.get('businessDate')! : defaultDateInput()
  );
  const [branchId, setBranchId] = useState(() => searchParams.get('branchId') || auth.session?.branchId || '');
  const [report, setReport] = useState<PreparedStockReportResponse | null>(null);
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

  const loadReport = async (selectedBranchId: string, date: string) => {
    if (!canAccess || !selectedBranchId) {
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const response = await getPreparedStockReport({
        branchId: selectedBranchId,
        businessDate: date,
      });
      setReport(response);
    } catch (caughtError) {
      setError(
        isApiError(caughtError) && caughtError.status === 401
          ? reportMessages.sessionExpired
          : isApiError(caughtError) && caughtError.status === 403
            ? reportMessages.unauthorized
            : reportMessages.errorLoadReport
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

    void loadReport(branchId, businessDate);
  }, [branchId, businessDate, canAccess]);

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

  if (!canAccess) {
    return (
      <InventoryManagementLayout
        title={reportMessages.workspaceTitle}
        description={reportMessages.workspaceDescription}
        breadcrumbs={[t('nav.dashboard'), t('nav.preparedStockReport')]}
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

  return (
    <InventoryManagementLayout
      title={reportMessages.workspaceTitle}
      description={reportMessages.workspaceDescription}
      breadcrumbs={[t('nav.dashboard'), t('nav.preparedStockReport')]}
      operatorLabel={operatorLabel}
      restaurantName={restaurantName}
      branchName={selectedBranch?.name ?? branchName}
      navItems={navItems}
    >
      <div className="preview-sequence prepared-stock-report">
        {error ? (
          <div className="admin-notice admin-notice--danger" role="alert">
            {error}
          </div>
        ) : null}

        <Card
          title={reportMessages.reportTitle}
          description={reportMessages.workspaceDescription}
          tone="inventory"
          actions={<Badge tone={loading ? 'warning' : 'neutral'} label={loading ? reportMessages.refreshing : reportMessages.ready} />}
        >
          <div className="admin-form-grid">
            {canSwitchBranch ? (
              <Select
                label={reportMessages.branchLabel}
                value={branchId}
                onChange={event => handleFilterChange(event.target.value, businessDate)}
                helperText={branchesLoading ? reportMessages.branchHelperLoading : reportMessages.branchHelperReady}
                error={branchesError ?? undefined}
              >
                <option value="">{reportMessages.noBranchSelectedTitle}</option>
                {branches.map(branch => (
                  <option key={branch.branchId} value={branch.branchId}>
                    {branch.name}
                  </option>
                ))}
              </Select>
            ) : null}
            <Input
              label={reportMessages.businessDateLabel}
              type="date"
              value={businessDate}
              onChange={event => handleFilterChange(branchId, event.target.value)}
            />
            <div className="ui-field">
              <span className="ui-field__label">&nbsp;</span>
              <Button variant="secondary" onClick={() => void loadReport(branchId, businessDate)} disabled={!branchId || loading}>
                {reportMessages.refresh}
              </Button>
            </div>
          </div>
        </Card>

        {!branchId ? (
          <EmptyState
            title={reportMessages.noBranchSelectedTitle}
            description={reportMessages.noBranchSelectedDescription}
            tone="inventory"
          />
        ) : !report ? (
          loading ? (
            <EmptyState title={reportMessages.loadingTitle} description={reportMessages.loadingDescription} tone="inventory" />
          ) : (
            <EmptyState
              title={reportMessages.noReportLoadedTitle}
              description={reportMessages.noReportLoadedDescription}
              tone="inventory"
              actionLabel={reportMessages.refreshReportAction}
              onAction={() => void loadReport(branchId, businessDate)}
            />
          )
        ) : report.rows.length === 0 ? (
          <EmptyState
            title={reportMessages.noActivityTitle}
            description={reportMessages.noActivityDescription}
            tone="inventory"
          />
        ) : (
          <>
            <div className="summary-grid">
              <SummaryCard
                label={reportMessages.produced}
                value={formatQuantity(report.totals.producedQuantity)}
                detail={reportMessages.item}
                tone="inventory"
              />
              <SummaryCard
                label={reportMessages.served}
                value={formatQuantity(report.totals.servedQuantity)}
                detail={reportMessages.item}
                tone="dashboard"
              />
              <SummaryCard
                label={reportMessages.wasted}
                value={formatQuantity(report.totals.wastedQuantity)}
                detail={reportMessages.item}
                tone="admin"
              />
              <SummaryCard
                label={reportMessages.remaining}
                value={formatQuantity(report.totals.remainingQuantity)}
                detail={reportMessages.item}
                tone={report.totals.remainingQuantity < 0 ? 'admin' : 'inventory'}
              />
              <SummaryCard
                label={reportMessages.warningLabel}
                value={report.totals.warningCount.toLocaleString()}
                detail={reportMessages.healthy}
                tone={report.totals.warningCount > 0 ? 'admin' : 'dashboard'}
              />
            </div>

            <Card title={reportMessages.reportTitle} description={reportMessages.noPreparedStockActivity} tone="inventory">
              <ResponsiveDataList
                rows={toRows(report)}
                columns={[
                  { key: 'menuItemName', label: reportMessages.item },
                  { key: 'preparedInventoryItemName', label: reportMessages.preparedStockItem },
                  {
                    key: 'producedQuantity',
                    label: reportMessages.produced,
                    align: 'right',
                    render: row => formatQuantity(row.producedQuantity, row.unitOfMeasure),
                  },
                  {
                    key: 'servedQuantity',
                    label: reportMessages.served,
                    align: 'right',
                    render: row => formatQuantity(row.servedQuantity, row.unitOfMeasure),
                  },
                  {
                    key: 'wastedQuantity',
                    label: reportMessages.wasted,
                    align: 'right',
                    render: row => formatQuantity(row.wastedQuantity, row.unitOfMeasure),
                  },
                  {
                    key: 'remainingQuantity',
                    label: reportMessages.remaining,
                    align: 'right',
                    render: row =>
                      row.remainingQuantity < 0
                        ? <Badge tone="danger" label={formatQuantity(row.remainingQuantity, row.unitOfMeasure)} />
                        : formatQuantity(row.remainingQuantity, row.unitOfMeasure),
                  },
                  {
                    key: 'warningReason',
                    label: reportMessages.status,
                    render: row =>
                      row.hasWarning ? (
                        <div className="prepared-stock-report__warning">
                          <Badge tone="warning" label={reportMessages.warning} />
                          {translateWarningReason(row.warningReason, t) ? (
                            <div className="prepared-stock-report__warning-text">
                              {translateWarningReason(row.warningReason, t)}
                            </div>
                          ) : null}
                        </div>
                      ) : (
                        <Badge tone="success" label={reportMessages.healthy} />
                      ),
                  },
                ]}
                mobileTitle={row => row.menuItemName ?? row.menuItemId}
                mobileDescription={row =>
                  `${formatQuantity(row.producedQuantity, row.unitOfMeasure)} · ${formatQuantity(row.servedQuantity, row.unitOfMeasure)} · ${formatQuantity(row.wastedQuantity, row.unitOfMeasure)} · ${formatQuantity(row.remainingQuantity, row.unitOfMeasure)}`
                }
                emptyTitle={reportMessages.noPreparedStockActivity}
                emptyDescription={reportMessages.noPreparedStockActivity}
              />
            </Card>

            <div className="admin-form-note">
              {reportMessages.noPreparedStockActivity}
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

export default PreparedStockReportPage;
