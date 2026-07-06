import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';

import { isApiError } from '../../api/apiErrors';
import { ModuleLayout } from '../../components/layout';
import type { ShellNavItem } from '../../components/layout/navigation';
import { ActionTile, Badge, Button, Card, EmptyState, Input, ResponsiveDataList, StatusBadge, SummaryCard } from '../../components/ui';
import { useAuth } from '../auth/useAuth';
import { useLanguage } from '../../i18n/LanguageProvider';
import { getOwnerDashboard } from './ownerDashboardApi';
import {
  buildOwnerDashboardScopeSummary,
  formatOwnerDashboardCurrency,
  formatOwnerDashboardDateInput,
  formatOwnerDashboardQuantity,
  formatOwnerDashboardTimestamp,
  getOwnerDashboardCardTone,
  getOwnerDashboardSeverityTone,
  sortOwnerDashboardAlerts,
} from './ownerDashboardDisplay';
import { getSafeOwnerDashboardErrorMessage } from './ownerDashboardErrorDisplay';
import type { OwnerDashboardResponse } from './ownerDashboardTypes';
import { getSetupChecklist } from '../setup/setupChecklistApi';
import type { SetupChecklistResponse } from '../setup/setupChecklistTypes';

export interface OwnerDashboardPageProps {
  navItems?: ShellNavItem[];
  restaurantName?: string;
  branchName?: string;
  operatorLabel?: string;
}

const isValidDateInput = (value: string | null) => Boolean(value && /^\d{4}-\d{2}-\d{2}$/.test(value));

export const OwnerDashboardPage = ({
  navItems,
  restaurantName,
  branchName,
  operatorLabel,
}: OwnerDashboardPageProps) => {
  const auth = useAuth();
  const { t } = useLanguage();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const canAccess = auth.hasPermission('Report.View');
  const canAccessSetup =
    auth.hasPermission('Report.View') ||
    auth.hasPermission('Branch.Manage') ||
    auth.hasPermission('User.Manage');
  const ownerMessages = useMemo(
    () => ({
      workspaceTitle: t('ownerDashboard.workspaceTitle'),
      workspaceDescription: t('ownerDashboard.workspaceDescription'),
      notAuthorizedTitle: t('ownerDashboard.notAuthorizedTitle'),
      notAuthorizedDescription: t('ownerDashboard.notAuthorizedDescription'),
      refreshDashboard: t('ownerDashboard.refreshDashboard'),
      businessDateCardTitle: t('ownerDashboard.businessDateCardTitle'),
      businessDateCardDescription: t('ownerDashboard.businessDateCardDescription'),
      businessDateLabel: t('ownerDashboard.businessDateLabel'),
      businessDateHelper: t('ownerDashboard.businessDateHelper'),
      loadingDashboardTitle: t('ownerDashboard.loadingDashboardTitle'),
      loadingDashboardDescription: t('ownerDashboard.loadingDashboardDescription'),
      noDashboardLoadedTitle: t('ownerDashboard.noDashboardLoadedTitle'),
      noDashboardLoadedDescription: t('ownerDashboard.noDashboardLoadedDescription'),
      refreshDashboardAction: t('ownerDashboard.refreshDashboardAction'),
      refreshing: t('ownerDashboard.refreshing'),
      ready: t('ownerDashboard.ready'),
      netSalesLabel: t('ownerDashboard.netSalesLabel'),
      grossSalesDetailPrefix: t('ownerDashboard.grossSalesDetailPrefix'),
      cashPaymentsLabel: t('ownerDashboard.cashPaymentsLabel'),
      nonCashDetailPrefix: t('ownerDashboard.nonCashDetailPrefix'),
      unpaidBalanceLabel: t('ownerDashboard.unpaidBalanceLabel'),
      unpaidBillsDetailSuffix: t('ownerDashboard.unpaidBillsDetailSuffix'),
      cancelledActivityLabel: t('ownerDashboard.cancelledActivityLabel'),
      receiptReprintsLabel: t('ownerDashboard.receiptReprintsLabel'),
      receiptPrintAuditSignal: t('ownerDashboard.receiptPrintAuditSignal'),
      closedShiftVarianceLabel: t('ownerDashboard.closedShiftVarianceLabel'),
      cashVarianceExplanation: t('ownerDashboard.cashVarianceExplanation'),
      openShiftsLabel: t('ownerDashboard.openShiftsLabel'),
      cashierShiftsStillOpen: t('ownerDashboard.cashierShiftsStillOpen'),
      billsLabel: t('ownerDashboard.billsLabel'),
      paymentsLabel: t('ownerDashboard.paymentsLabel'),
      vendorDuesTitle: t('ownerDashboard.vendorDuesTitle'),
      vendorDuesDescription: t('ownerDashboard.vendorDuesDescription'),
      openStatement: t('ownerDashboard.openStatement'),
      totalOutstandingLabel: t('ownerDashboard.totalOutstandingLabel'),
      acrossSelectedScope: t('ownerDashboard.acrossSelectedScope'),
      vendorsWithDuesLabel: t('ownerDashboard.vendorsWithDuesLabel'),
      overdueVendorsSuffix: t('ownerDashboard.overdueVendorsSuffix'),
      noVendorDuesTitle: t('ownerDashboard.noVendorDuesTitle'),
      noVendorDuesDescription: t('ownerDashboard.noVendorDuesDescription'),
      inventoryAlertsTitle: t('ownerDashboard.inventoryAlertsTitle'),
      inventoryAlertsDescription: t('ownerDashboard.inventoryAlertsDescription'),
      viewInventory: t('ownerDashboard.viewInventory'),
      totalInventoryAlertsLabel: t('ownerDashboard.totalInventoryAlertsLabel'),
      lowStockPlusOutOfStock: t('ownerDashboard.lowStockPlusOutOfStock'),
      outOfStockLabel: t('ownerDashboard.outOfStockLabel'),
      lowStockLabel: t('ownerDashboard.lowStockLabel'),
      highestPriorityItems: t('ownerDashboard.highestPriorityItems'),
      stillAvailableBelowMinimum: t('ownerDashboard.stillAvailableBelowMinimum'),
      noInventoryAlertsTitle: t('ownerDashboard.noInventoryAlertsTitle'),
      noInventoryAlertsDescription: t('ownerDashboard.noInventoryAlertsDescription'),
      alertsTitle: t('ownerDashboard.alertsTitle'),
      alertsDescription: t('ownerDashboard.alertsDescription'),
      noActiveAlertsTitle: t('ownerDashboard.noActiveAlertsTitle'),
      noActiveAlertsDescription: t('ownerDashboard.noActiveAlertsDescription'),
      quickLinksTitle: t('ownerDashboard.quickLinksTitle'),
      quickLinksDescription: t('ownerDashboard.quickLinksDescription'),
      scopePrefix: t('ownerDashboard.scopePrefix'),
      generatedPrefix: t('ownerDashboard.generatedPrefix'),
      readOnlyNote: t('ownerDashboard.readOnlyNote'),
      countLabel: t('ownerDashboard.countLabel'),
      targetLabel: t('ownerDashboard.targetLabel'),
      noAmountAttached: t('ownerDashboard.noAmountAttached'),
      openDetails: t('ownerDashboard.openDetails'),
      vendorColumnLabel: t('ownerDashboard.vendorColumnLabel'),
      typeColumnLabel: t('ownerDashboard.typeColumnLabel'),
      branchColumnLabel: t('ownerDashboard.branchColumnLabel'),
      outstandingColumnLabel: t('ownerDashboard.outstandingColumnLabel'),
      oldestDueColumnLabel: t('ownerDashboard.oldestDueColumnLabel'),
      openBillsColumnLabel: t('ownerDashboard.openBillsColumnLabel'),
      pilotSetupTitle: t('ownerDashboard.pilotSetupTitle'),
      setupProgressLabel: t('ownerDashboard.setupProgressLabel'),
      completedStepsLabel: t('ownerDashboard.completedStepsLabel'),
      setupReadyForPilot: t('ownerDashboard.setupReadyForPilot'),
      setupNeedsAttention: t('ownerDashboard.setupNeedsAttention'),
      viewSetupChecklist: t('ownerDashboard.viewSetupChecklist'),
      setupStatusUnavailable: t('ownerDashboard.setupStatusUnavailable'),
      completeSetupStepsBeforePilotUsage: t('ownerDashboard.completeSetupStepsBeforePilotUsage'),
      itemColumnLabel: t('ownerDashboard.itemColumnLabel'),
      categoryColumnLabel: t('ownerDashboard.categoryColumnLabel'),
      unitColumnLabel: t('ownerDashboard.unitColumnLabel'),
      currentQuantityColumnLabel: t('ownerDashboard.currentQuantityColumnLabel'),
      minimumQuantityColumnLabel: t('ownerDashboard.minimumQuantityColumnLabel'),
      statusColumnLabel: t('ownerDashboard.statusColumnLabel'),
      lastUpdatedColumnLabel: t('ownerDashboard.lastUpdatedColumnLabel'),
      notAvailable: t('ownerDashboard.notAvailable'),
      sessionExpired: t('ownerDashboard.sessionExpired'),
      unauthorized: t('ownerDashboard.unauthorized'),
      errorLoadDashboard: t('ownerDashboard.errorLoadDashboard'),
    }),
    [t]
  );
  const [businessDate, setBusinessDate] = useState(() =>
    isValidDateInput(searchParams.get('date')) ? searchParams.get('date')! : formatOwnerDashboardDateInput()
  );
  const [dashboard, setDashboard] = useState<OwnerDashboardResponse | null>(null);
  const [setupChecklist, setSetupChecklist] = useState<SetupChecklistResponse | null>(null);
  const [setupUnavailable, setSetupUnavailable] = useState(false);
  const [loading, setLoading] = useState(canAccess);
  const [error, setError] = useState<string | null>(null);

  const branchId = searchParams.get('branchId') || null;
  const setupBranchId = branchId || auth.session?.branchId || null;
  const setupChecklistHref = useMemo(() => {
    if (!setupBranchId) {
      return '/setup';
    }

    const query = new URLSearchParams();
    query.set('branchId', setupBranchId);
    return `/setup?${query.toString()}`;
  }, [setupBranchId]);

  const loadDashboard = useCallback(
    async (date: string, selectedBranchId: string | null) => {
      if (!canAccess) {
        return;
      }

      setLoading(true);
      setError(null);

      try {
        const response = await getOwnerDashboard({
          date,
          branchId: selectedBranchId,
        });
        setDashboard(response);
      } catch (caughtError) {
        setError(
          getSafeOwnerDashboardErrorMessage(
            caughtError,
            ownerMessages.errorLoadDashboard,
            {
              sessionExpired: ownerMessages.sessionExpired,
              unauthorized: ownerMessages.unauthorized,
            }
          )
        );
        setDashboard(null);
        if (isApiError(caughtError) && caughtError.status === 401) {
          void auth.logout();
        }
      } finally {
        setLoading(false);
      }
    },
    [auth, canAccess, ownerMessages]
  );

  const loadSetupChecklist = useCallback(
    async (selectedBranchId: string | null) => {
      if (!canAccessSetup) {
        return;
      }

      try {
        const response = await getSetupChecklist(selectedBranchId);
        setSetupChecklist(response);
        setSetupUnavailable(false);
      } catch {
        setSetupChecklist(null);
        setSetupUnavailable(true);
      }
    },
    [canAccessSetup]
  );

  useEffect(() => {
    if (!canAccess) {
      return;
    }

    void loadDashboard(businessDate, branchId).catch(() => undefined);
  }, [branchId, businessDate, canAccess, loadDashboard]);

  useEffect(() => {
    if (!canAccessSetup) {
      return;
    }

    setSetupChecklist(null);
    setSetupUnavailable(false);
    void loadSetupChecklist(setupBranchId).catch(() => undefined);
  }, [canAccessSetup, loadSetupChecklist, setupBranchId]);

  const sortedAlerts = useMemo(() => sortOwnerDashboardAlerts(dashboard?.alerts ?? []), [dashboard]);
  const inventoryAlerts = dashboard?.inventoryAlerts ?? null;
  const vendorDues = dashboard?.vendorDues ?? null;
  const inventoryAlertRows = useMemo(
    () =>
      (inventoryAlerts?.criticalItems ?? []).map(item => ({
        id: item.inventoryItemId,
        item: item.name,
        category: item.category,
        unit: item.unit,
        currentQuantity: item.currentQuantity,
        minimumQuantity: item.minimumQuantity,
        status: item.status,
        lastUpdatedAt: item.lastUpdatedAt,
      })),
    [inventoryAlerts]
  );
  const vendorDueRows = useMemo(
    () =>
      (vendorDues?.criticalVendors ?? []).map(item => ({
        id: item.vendorId,
        vendorName: item.vendorName,
        vendorType: item.vendorType,
        branchName: item.branchName ?? ownerMessages.notAvailable,
        outstandingAmount: item.outstandingAmount,
        oldestDueDate: item.oldestDueDate,
        openBillCount: item.openBillCount,
      })),
    [ownerMessages.notAvailable, vendorDues]
  );
  const setupStatusLabel =
    setupChecklist?.completionPercent === 100 ? ownerMessages.setupReadyForPilot : ownerMessages.setupNeedsAttention;
  const setupStatusTone = setupChecklist?.completionPercent === 100 ? 'success' : 'warning';
  const setupCardTone = setupChecklist?.completionPercent === 100 ? 'dashboard' : 'admin';
  const inventoryViewTarget = useMemo(() => {
    if (!inventoryAlerts || inventoryAlerts.totalAlertCount <= 0) {
      return '/inventory';
    }

    if (inventoryAlerts.outOfStockCount > 0) {
      return '/inventory?status=Out%20of%20stock';
    }

    if (inventoryAlerts.lowStockCount > 0) {
      return '/inventory?status=Low%20stock';
    }

    return '/inventory';
  }, [inventoryAlerts]);

  const handleDateChange = (nextDate: string) => {
    setBusinessDate(nextDate);
    const nextParams = new URLSearchParams(searchParams);
    nextParams.set('date', nextDate);
    if (branchId) {
      nextParams.set('branchId', branchId);
    } else {
      nextParams.delete('branchId');
    }

    setSearchParams(nextParams, { replace: true });
  };

  if (!canAccess) {
    return (
      <ModuleLayout
        tone="dashboard"
        title={ownerMessages.workspaceTitle}
        description={ownerMessages.workspaceDescription}
        breadcrumbs={[t('nav.dashboard'), ownerMessages.workspaceTitle]}
        operatorLabel={operatorLabel}
        restaurantName={restaurantName}
        branchName={branchName}
        navItems={navItems}
      >
        <EmptyState
          title={ownerMessages.notAuthorizedTitle}
          description={ownerMessages.notAuthorizedDescription}
          tone="accent"
        />
      </ModuleLayout>
    );
  }

  const metrics = dashboard?.metrics ?? null;

  return (
    <ModuleLayout
      tone="dashboard"
      title={ownerMessages.workspaceTitle}
      description={ownerMessages.workspaceDescription}
      breadcrumbs={[t('nav.dashboard'), ownerMessages.workspaceTitle]}
      operatorLabel={operatorLabel}
      restaurantName={restaurantName}
      branchName={dashboard?.branchName ?? branchName}
      navItems={navItems}
      maxWidth="xl"
      actions={
        <Button variant="secondary" onClick={() => void loadDashboard(businessDate, branchId).catch(() => undefined)}>
          {ownerMessages.refreshDashboard}
        </Button>
      }
    >
      <div className="preview-sequence owner-dashboard-page">
        {error ? (
          <div className="admin-notice admin-notice--danger" role="alert">
            {error}
          </div>
        ) : null}

        <Card
          title={ownerMessages.businessDateCardTitle}
          description={ownerMessages.businessDateCardDescription}
          tone="dashboard"
          actions={<Badge tone={loading ? 'warning' : 'neutral'} label={loading ? ownerMessages.refreshing : ownerMessages.ready} />}
        >
          <div className="admin-controls">
            <Input
              label={ownerMessages.businessDateLabel}
              type="date"
              value={businessDate}
              onChange={event => handleDateChange(event.target.value)}
              helperText={ownerMessages.businessDateHelper}
            />
          </div>
        </Card>

        {!dashboard ? (
          loading ? (
            <EmptyState
              title={ownerMessages.loadingDashboardTitle}
              description={ownerMessages.loadingDashboardDescription}
              tone="accent"
            />
          ) : (
            <EmptyState
              title={ownerMessages.noDashboardLoadedTitle}
              description={ownerMessages.noDashboardLoadedDescription}
              tone="accent"
              actionLabel={ownerMessages.refreshDashboardAction}
              onAction={() => void loadDashboard(businessDate, branchId).catch(() => undefined)}
            />
          )
        ) : (
          <>
            <div className="summary-grid">
              <SummaryCard
                label={ownerMessages.netSalesLabel}
                value={formatOwnerDashboardCurrency(metrics?.netSales ?? 0, dashboard.currencyCode)}
                detail={`${ownerMessages.grossSalesDetailPrefix} ${formatOwnerDashboardCurrency(metrics?.grossSales ?? 0, dashboard.currencyCode)}`}
                tone="dashboard"
              />
              <SummaryCard
                label={ownerMessages.cashPaymentsLabel}
                value={formatOwnerDashboardCurrency(metrics?.cashPayments ?? 0, dashboard.currencyCode)}
                detail={`${ownerMessages.nonCashDetailPrefix} ${formatOwnerDashboardCurrency(metrics?.nonCashPayments ?? 0, dashboard.currencyCode)}`}
                tone="orders"
              />
              <SummaryCard
                label={ownerMessages.unpaidBalanceLabel}
                value={formatOwnerDashboardCurrency(metrics?.totalBalanceDue ?? 0, dashboard.currencyCode)}
                detail={`${metrics?.unpaidBills ?? 0} ${ownerMessages.unpaidBillsDetailSuffix}`}
                tone="admin"
              />
              <SummaryCard
                label={ownerMessages.cancelledActivityLabel}
                value={`${(metrics?.cancelledBills ?? 0) + (metrics?.cancelledPayments ?? 0)}`}
                detail={`${ownerMessages.billsLabel} ${metrics?.cancelledBills ?? 0} · ${ownerMessages.paymentsLabel} ${metrics?.cancelledPayments ?? 0}`}
                tone="admin"
              />
              <SummaryCard
                label={ownerMessages.receiptReprintsLabel}
                value={`${metrics?.receiptReprints ?? 0}`}
                detail={ownerMessages.receiptPrintAuditSignal}
                tone="inventory"
              />
              <SummaryCard
                label={ownerMessages.closedShiftVarianceLabel}
                value={formatOwnerDashboardCurrency(metrics?.cashVarianceTotal ?? 0, dashboard.currencyCode)}
                detail={ownerMessages.cashVarianceExplanation}
                tone={metrics && Math.abs(metrics.cashVarianceTotal) > 0 ? 'admin' : 'dashboard'}
              />
              <SummaryCard
                label={ownerMessages.openShiftsLabel}
                value={`${metrics?.openShifts ?? 0}`}
                detail={ownerMessages.cashierShiftsStillOpen}
                tone="inventory"
              />
            </div>

            {canAccessSetup && (setupChecklist || setupUnavailable) ? (
              <Card
                title={ownerMessages.pilotSetupTitle}
                description={ownerMessages.completeSetupStepsBeforePilotUsage}
                tone={setupChecklist?.completionPercent === 100 ? setupCardTone : 'admin'}
                actions={
                  <>
                    {setupChecklist ? <Badge tone={setupStatusTone} label={setupStatusLabel} /> : null}
                    <Link className="ui-button ui-button--secondary ui-button--sm" to={setupChecklistHref}>
                      {ownerMessages.viewSetupChecklist}
                    </Link>
                  </>
                }
              >
                {setupChecklist ? (
                  <div className="summary-grid">
                    <SummaryCard
                      label={ownerMessages.setupProgressLabel}
                      value={`${setupChecklist.completionPercent}%`}
                      detail={`${setupChecklist.completedCount} / ${setupChecklist.totalCount}`}
                      tone={setupChecklist.completionPercent === 100 ? 'dashboard' : 'admin'}
                    />
                    <SummaryCard
                      label={ownerMessages.completedStepsLabel}
                      value={`${setupChecklist.completedCount} / ${setupChecklist.totalCount}`}
                      detail={`${setupChecklist.totalCount}`}
                      tone="inventory"
                    />
                  </div>
                ) : (
                  <div className="admin-form-note">{ownerMessages.setupStatusUnavailable}</div>
                )}
              </Card>
            ) : null}

            <Card
              title={ownerMessages.vendorDuesTitle}
              description={ownerMessages.vendorDuesDescription}
              tone="admin"
              actions={
                <Button variant="secondary" onClick={() => navigate('/vendors/statement')}>
                  {ownerMessages.openStatement}
                </Button>
              }
            >
              <div className="summary-grid">
                <SummaryCard
                  label={ownerMessages.totalOutstandingLabel}
                  value={formatOwnerDashboardCurrency(vendorDues?.totalVendorOutstanding ?? 0, dashboard.currencyCode)}
                  detail={ownerMessages.acrossSelectedScope}
                  tone="inventory"
                />
                <SummaryCard
                  label={ownerMessages.vendorsWithDuesLabel}
                  value={`${vendorDues?.vendorsWithOutstandingCount ?? 0}`}
                  detail={`${ownerMessages.overdueVendorsSuffix} ${vendorDues?.overdueVendorCount ?? 0}`}
                  tone="admin"
                />
              </div>

              {vendorDueRows.length > 0 ? (
                <ResponsiveDataList
                  rows={vendorDueRows}
                  columns={[
                    { key: 'vendorName', label: t('ownerDashboard.vendorColumnLabel') },
                    { key: 'vendorType', label: t('ownerDashboard.typeColumnLabel'), hideOnMobile: true },
                    { key: 'branchName', label: t('ownerDashboard.branchColumnLabel'), hideOnMobile: true },
                    {
                      key: 'outstandingAmount',
                      label: t('ownerDashboard.outstandingColumnLabel'),
                      align: 'right',
                      render: row => formatOwnerDashboardCurrency(row.outstandingAmount, dashboard.currencyCode),
                    },
                    {
                      key: 'oldestDueDate',
                      label: t('ownerDashboard.oldestDueColumnLabel'),
                      render: row => (row.oldestDueDate ? row.oldestDueDate.slice(0, 10) : ownerMessages.notAvailable),
                      hideOnMobile: true,
                    },
                    {
                      key: 'openBillCount',
                      label: t('ownerDashboard.openBillsColumnLabel'),
                      align: 'right',
                      render: row => row.openBillCount.toString(),
                    },
                  ]}
                  mobileTitle={row => row.vendorName}
                  mobileDescription={row => `${row.vendorType} · ${formatOwnerDashboardCurrency(row.outstandingAmount, dashboard.currencyCode)} outstanding`}
                  emptyTitle={ownerMessages.noVendorDuesTitle}
                  emptyDescription={ownerMessages.noVendorDuesDescription}
                />
              ) : (
                <EmptyState
                  title={ownerMessages.noVendorDuesTitle}
                  description={ownerMessages.noVendorDuesDescription}
                  tone="admin"
                />
              )}
            </Card>

            <Card
              title={ownerMessages.inventoryAlertsTitle}
              description={ownerMessages.inventoryAlertsDescription}
              tone="inventory"
              actions={
                <Button variant="secondary" onClick={() => navigate(inventoryViewTarget)}>
                  {ownerMessages.viewInventory}
                </Button>
              }
            >
              <div className="summary-grid">
                <SummaryCard
                  label={ownerMessages.totalInventoryAlertsLabel}
                  value={`${inventoryAlerts?.totalAlertCount ?? 0}`}
                  detail={ownerMessages.lowStockPlusOutOfStock}
                  tone="inventory"
                />
                <SummaryCard
                  label={ownerMessages.outOfStockLabel}
                  value={`${inventoryAlerts?.outOfStockCount ?? 0}`}
                  detail={ownerMessages.highestPriorityItems}
                  tone="admin"
                />
                <SummaryCard
                  label={ownerMessages.lowStockLabel}
                  value={`${inventoryAlerts?.lowStockCount ?? 0}`}
                  detail={ownerMessages.stillAvailableBelowMinimum}
                  tone="dashboard"
                />
              </div>

              {inventoryAlertRows.length > 0 ? (
                <ResponsiveDataList
                  className="preview-sequence"
                  rows={inventoryAlertRows}
                  columns={[
                    { key: 'item', label: t('ownerDashboard.itemColumnLabel') },
                    { key: 'category', label: t('ownerDashboard.categoryColumnLabel'), hideOnMobile: true },
                    { key: 'unit', label: t('ownerDashboard.unitColumnLabel'), hideOnMobile: true },
                    {
                      key: 'currentQuantity',
                      label: t('ownerDashboard.currentQuantityColumnLabel'),
                      align: 'right',
                      render: row => `${formatOwnerDashboardQuantity(row.currentQuantity)} ${row.unit}`,
                    },
                    {
                      key: 'minimumQuantity',
                      label: t('ownerDashboard.minimumQuantityColumnLabel'),
                      align: 'right',
                      render: row => formatOwnerDashboardQuantity(row.minimumQuantity),
                    },
                    {
                      key: 'status',
                      label: t('ownerDashboard.statusColumnLabel'),
                      render: row => <StatusBadge status={row.status} label={row.status} />,
                    },
                    {
                      key: 'lastUpdatedAt',
                      label: t('ownerDashboard.lastUpdatedColumnLabel'),
                      render: row => formatOwnerDashboardTimestamp(row.lastUpdatedAt, ownerMessages),
                      hideOnMobile: true,
                    },
                  ]}
                  mobileTitle={row => row.item}
                  mobileDescription={row => `${row.category} · ${row.unit}`}
                  emptyTitle={ownerMessages.noInventoryAlertsTitle}
                  emptyDescription={ownerMessages.noInventoryAlertsDescription}
                />
              ) : (
                <EmptyState
                  title={ownerMessages.noInventoryAlertsTitle}
                  description={ownerMessages.noInventoryAlertsDescription}
                  tone="inventory"
                />
              )}
            </Card>

            <div className="preview-sequence">
              <Card
                title={ownerMessages.alertsTitle}
                description={ownerMessages.alertsDescription}
                tone="dashboard"
              >
                {sortedAlerts.length > 0 ? (
                  <div className="preview-sequence">
                    {sortedAlerts.map(alert => (
                      <Card
                        key={alert.type}
                        title={alert.title}
                        description={alert.message}
                        tone={getOwnerDashboardCardTone(alert.severity)}
                        actions={<Badge tone={getOwnerDashboardSeverityTone(alert.severity)} label={alert.severity} />}
                      >
                        <div className="preview-sequence">
                          <div className="summary-grid">
                            <SummaryCard label={ownerMessages.countLabel} value={alert.count.toLocaleString()} tone="dashboard" />
                            <SummaryCard
                              label={ownerMessages.targetLabel}
                              value={alert.targetPath}
                              detail={alert.amount === null ? ownerMessages.noAmountAttached : formatOwnerDashboardCurrency(alert.amount, dashboard.currencyCode)}
                              tone="inventory"
                            />
                          </div>
                          <div className="preview-checks">
                            <Button variant="secondary" onClick={() => navigate(alert.targetPath)}>
                              {ownerMessages.openDetails}
                            </Button>
                          </div>
                        </div>
                      </Card>
                    ))}
                  </div>
                ) : (
                  <EmptyState
                    title={ownerMessages.noActiveAlertsTitle}
                    description={ownerMessages.noActiveAlertsDescription}
                    tone="accent"
                  />
                )}
              </Card>
            </div>

            <Card
              title={ownerMessages.quickLinksTitle}
              description={ownerMessages.quickLinksDescription}
              tone="dashboard"
            >
              <div className="preview-tile-grid">
                {dashboard.quickLinks.map(link => (
                  <ActionTile
                    key={link.path}
                    title={link.label}
                    description={link.description}
                    tone="dashboard"
                    onClick={() => navigate(link.path)}
                  />
                ))}
              </div>
            </Card>

            <div className="admin-form-note">
              {ownerMessages.scopePrefix}: {buildOwnerDashboardScopeSummary(dashboard)}. {ownerMessages.generatedPrefix} {formatOwnerDashboardTimestamp(dashboard.generatedAt, ownerMessages)}.
            </div>
            <div className="admin-form-note">
              {ownerMessages.readOnlyNote}
            </div>
          </>
        )}
      </div>
    </ModuleLayout>
  );
};

export default OwnerDashboardPage;
