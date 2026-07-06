import { useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';

import { isApiError } from '../../api/apiErrors';
import { InventoryManagementLayout } from '../../components/layout';
import type { ShellNavItem } from '../../components/layout/navigation';
import { Badge, Button, Card, EmptyState, Input, ResponsiveDataList, SummaryCard, StatusBadge } from '../../components/ui';
import { useAuth } from '../auth/useAuth';
import { useLanguage } from '../../i18n/LanguageProvider';
import { getDailyCashSalesReport } from './dailyCashSalesReportApi';
import {
  buildDailyCashSalesScopeSummary,
  formatDailyCashSalesCurrency,
  formatDailyCashSalesDateInput,
  formatDailyCashSalesPaymentMode,
  formatDailyCashSalesTimestamp,
  getDailyCashSalesSeverityTone,
} from './dailyCashSalesReportDisplay';
import { getSafeDailyCashSalesReportErrorMessage } from './dailyCashSalesReportErrorDisplay';
import type {
  DailyCashSalesCashShiftSummary,
  DailyCashSalesExceptionItem,
  DailyCashSalesPaymentBreakdown,
  DailyCashSalesReportResponse,
} from './dailyCashSalesReportTypes';

export interface DailyCashSalesReportPageProps {
  navItems?: ShellNavItem[];
  restaurantName?: string;
  branchName?: string;
  operatorLabel?: string;
}

type ReportPaymentRow = DailyCashSalesPaymentBreakdown & { id: string };
type ReportShiftRow = DailyCashSalesCashShiftSummary & { id: string };

const isValidDateInput = (value: string | null) => Boolean(value && /^\d{4}-\d{2}-\d{2}$/.test(value));

const toPaymentRows = (report: DailyCashSalesReportResponse | null): ReportPaymentRow[] =>
  (report?.paymentBreakdown ?? []).map(item => ({
    ...item,
    id: item.paymentMode,
  }));

const toShiftRows = (report: DailyCashSalesReportResponse | null): ReportShiftRow[] =>
  (report?.cashShiftSummaries ?? []).map(item => ({
    ...item,
    id: item.cashierShiftId,
  }));

const toExceptionRows = (items: DailyCashSalesExceptionItem[]): DailyCashSalesExceptionItem[] => items.map(item => ({ ...item }));

const formatCount = (value: number) => value.toLocaleString();

const worstSeverity = (rows: DailyCashSalesExceptionItem[]) =>
  rows.reduce<'Low' | 'Medium' | 'High'>((worst, r) => {
    const order = { High: 2, Medium: 1, Low: 0 } as const;
    return order[r.severity] > order[worst] ? r.severity : worst;
  }, rows[0]?.severity ?? 'Low');

export const DailyCashSalesReportPage = ({
  navItems,
  restaurantName,
  branchName,
  operatorLabel,
}: DailyCashSalesReportPageProps) => {
  const auth = useAuth();
  const { t } = useLanguage();
  const [searchParams, setSearchParams] = useSearchParams();
  const canAccess = auth.hasPermission('Report.View');
  const reportMessages = useMemo(
    () => ({
      workspaceTitle: t('dailyCashSales.workspaceTitle'),
      workspaceDescription: t('dailyCashSales.workspaceDescription'),
      notAuthorizedTitle: t('dailyCashSales.notAuthorizedTitle'),
      notAuthorizedDescription: t('dailyCashSales.notAuthorizedDescription'),
      reportEyebrow: t('dailyCashSales.reportEyebrow'),
      businessDateLabel: t('dailyCashSales.businessDateLabel'),
      refreshReport: t('dailyCashSales.refreshReport'),
      refreshing: t('dailyCashSales.refreshing'),
      ready: t('dailyCashSales.ready'),
      loadingReportTitle: t('dailyCashSales.loadingReportTitle'),
      loadingReportDescription: t('dailyCashSales.loadingReportDescription'),
      noReportLoadedTitle: t('dailyCashSales.noReportLoadedTitle'),
      noReportLoadedDescription: t('dailyCashSales.noReportLoadedDescription'),
      refreshReportAction: t('dailyCashSales.refreshReportAction'),
      summaryLabel: t('dailyCashSales.summaryLabel'),
      grossBillTotalLabel: t('dailyCashSales.grossBillTotalLabel'),
      cancelledBillsTrackedSeparately: t('dailyCashSales.cancelledBillsTrackedSeparately'),
      paidTotalLabel: t('dailyCashSales.paidTotalLabel'),
      recordedPaymentsOnly: t('dailyCashSales.recordedPaymentsOnly'),
      unpaidBalanceLabel: t('dailyCashSales.unpaidBalanceLabel'),
      unpaidBillsDetailSuffix: t('dailyCashSales.unpaidBillsDetailSuffix'),
      closedShiftVarianceTotalLabel: t('dailyCashSales.closedShiftVarianceTotalLabel'),
      cashVarianceExplanation: t('dailyCashSales.cashVarianceExplanation'),
      cancelledBillsLabel: t('dailyCashSales.cancelledBillsLabel'),
      openShiftsLabel: t('dailyCashSales.openShiftsLabel'),
      closedShiftsLabel: t('dailyCashSales.closedShiftsLabel'),
      openingCashTotalLabel: t('dailyCashSales.openingCashTotalLabel'),
      declaredClosingCashLabel: t('dailyCashSales.declaredClosingCashLabel'),
      expectedCashTotalLabel: t('dailyCashSales.expectedCashTotalLabel'),
      openingCashPlusPayments: t('dailyCashSales.openingCashPlusPayments'),
      paymentBreakdownTitle: t('dailyCashSales.paymentBreakdownTitle'),
      paymentBreakdownDescription: t('dailyCashSales.paymentBreakdownDescription'),
      paymentModeLabel: t('dailyCashSales.paymentModeLabel'),
      recordedLabel: t('dailyCashSales.recordedLabel'),
      cancelledLabel: t('dailyCashSales.cancelledLabel'),
      netLabel: t('dailyCashSales.netLabel'),
      countLabel: t('dailyCashSales.countLabel'),
      cancelledCountLabel: t('dailyCashSales.cancelledCountLabel'),
      noPaymentRowsTitle: t('dailyCashSales.noPaymentRowsTitle'),
      noPaymentRowsDescription: t('dailyCashSales.noPaymentRowsDescription'),
      cashierShiftSummaryTitle: t('dailyCashSales.cashierShiftSummaryTitle'),
      cashierShiftSummaryDescription: t('dailyCashSales.cashierShiftSummaryDescription'),
      branchLabel: t('dailyCashSales.branchLabel'),
      statusLabel: t('dailyCashSales.statusLabel'),
      openedLabel: t('dailyCashSales.openedLabel'),
      closedLabel: t('dailyCashSales.closedLabel'),
      openingCashLabel: t('dailyCashSales.openingCashLabel'),
      expectedCashLabel: t('dailyCashSales.expectedCashLabel'),
      declaredClosingCashValueNotCounted: t('dailyCashSales.declaredClosingCashValueNotCounted'),
      closedShiftVarianceValueOpen: t('dailyCashSales.closedShiftVarianceValueOpen'),
      cashPaymentsLabel: t('dailyCashSales.cashPaymentsLabel'),
      noCashShiftsTitle: t('dailyCashSales.noCashShiftsTitle'),
      noCashShiftsDescription: t('dailyCashSales.noCashShiftsDescription'),
      controlExceptionsLabel: t('dailyCashSales.controlExceptionsLabel'),
      unpaidBillsTitle: t('dailyCashSales.unpaidBillsTitle'),
      cancelledBillsTitle: t('dailyCashSales.cancelledBillsTitle'),
      closedShiftVariancesTitle: t('dailyCashSales.closedShiftVariancesTitle'),
      openShiftsCardTitle: t('dailyCashSales.openShiftsCardTitle'),
      allControlsClearMessage: t('dailyCashSales.allControlsClearMessage'),
      noUnpaidBillsTitle: t('dailyCashSales.noUnpaidBillsTitle'),
      noUnpaidBillsDescription: t('dailyCashSales.noUnpaidBillsDescription'),
      noCancelledBillsTitle: t('dailyCashSales.noCancelledBillsTitle'),
      noCancelledBillsDescription: t('dailyCashSales.noCancelledBillsDescription'),
      noClosedShiftVariancesTitle: t('dailyCashSales.noClosedShiftVariancesTitle'),
      noClosedShiftVariancesDescription: t('dailyCashSales.noClosedShiftVariancesDescription'),
      noOpenShiftsTitle: t('dailyCashSales.noOpenShiftsTitle'),
      noOpenShiftsDescription: t('dailyCashSales.noOpenShiftsDescription'),
      scopePrefix: t('dailyCashSales.scopePrefix'),
      generated: t('dailyCashSales.generated'),
      branchScopeActive: t('dailyCashSales.branchScopeActive'),
      allBranches: t('dailyCashSales.allBranches'),
      generatedPrefix: t('dailyCashSales.generatedPrefix'),
      readOnlyNote: t('dailyCashSales.readOnlyNote'),
      sessionExpired: t('dailyCashSales.sessionExpired'),
      unauthorized: t('dailyCashSales.unauthorized'),
      errorLoadReport: t('dailyCashSales.errorLoadReport'),
      notAvailable: t('dailyCashSales.notAvailable'),
      notCounted: t('dailyCashSales.notCounted'),
      openValue: t('dailyCashSales.openValue'),
      notApplicable: t('dailyCashSales.notApplicable'),
    }),
    [t]
  );
  const [businessDate, setBusinessDate] = useState(() =>
    isValidDateInput(searchParams.get('date')) ? searchParams.get('date')! : formatDailyCashSalesDateInput()
  );
  const branchId = searchParams.get('branchId') || null;
  const [report, setReport] = useState<DailyCashSalesReportResponse | null>(null);
  const [loading, setLoading] = useState(canAccess);
  const [error, setError] = useState<string | null>(null);

  const loadReport = async (date: string, selectedBranchId: string | null) => {
    if (!canAccess) {
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const response = await getDailyCashSalesReport({
        date,
        branchId: selectedBranchId,
      });
      setReport(response);
    } catch (caughtError) {
      setError(
        getSafeDailyCashSalesReportErrorMessage(
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
    if (!canAccess) {
      return;
    }

    void loadReport(businessDate, branchId).catch(() => undefined);
  }, [branchId, businessDate, canAccess]);

  const paymentRows = useMemo(() => toPaymentRows(report), [report]);
  const shiftRows = useMemo(() => toShiftRows(report), [report]);
  const summary = report?.summary ?? null;
  const exceptionSections = report
    ? [
        {
          title: reportMessages.unpaidBillsTitle,
          emptyTitle: reportMessages.noUnpaidBillsTitle,
          emptyDescription: reportMessages.noUnpaidBillsDescription,
          rows: report.exceptions.unpaidBills,
        },
        {
          title: reportMessages.cancelledBillsTitle,
          emptyTitle: reportMessages.noCancelledBillsTitle,
          emptyDescription: reportMessages.noCancelledBillsDescription,
          rows: report.exceptions.cancelledBills,
        },
        {
          title: reportMessages.closedShiftVariancesTitle,
          emptyTitle: reportMessages.noClosedShiftVariancesTitle,
          emptyDescription: reportMessages.noClosedShiftVariancesDescription,
          rows: report.exceptions.cashVariances,
        },
        {
          title: reportMessages.openShiftsLabel,
          emptyTitle: reportMessages.noOpenShiftsTitle,
          emptyDescription: reportMessages.noOpenShiftsDescription,
          rows: report.exceptions.openShifts,
        },
      ]
    : [];

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
      <InventoryManagementLayout
        title={reportMessages.workspaceTitle}
        description={reportMessages.workspaceDescription}
        breadcrumbs={[t('nav.dashboard'), t('nav.dailyReport')]}
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

  const allClear = exceptionSections.length > 0 && exceptionSections.every(s => s.rows.length === 0);

  return (
    <InventoryManagementLayout
      title={reportMessages.workspaceTitle}
      description={reportMessages.workspaceDescription}
      breadcrumbs={[t('nav.dashboard'), t('nav.dailyReport')]}
      operatorLabel={operatorLabel}
      restaurantName={restaurantName}
      branchName={branchName}
      navItems={navItems}
    >
      <div className="preview-sequence daily-cash-sales-report">

        {/* ── Compact header toolbar ── */}
        <div className="dcsr-toolbar">
          <div className="dcsr-toolbar__left">
            <p className="dcsr-toolbar__eyebrow">{reportMessages.reportEyebrow}</p>
            {report ? (
              <p className="dcsr-toolbar__meta">
                <span>{`${reportMessages.scopePrefix}: ${branchId ? `${report.branchName ?? branchId} · ` : ''}${report.businessDate}`}</span>
                <span className="dcsr-toolbar__meta-sep"> · </span>
                <span>{`${reportMessages.generated} ${formatDailyCashSalesTimestamp(report.generatedAt, { notAvailable: reportMessages.notAvailable })}`}</span>
              </p>
            ) : (
              <p className="dcsr-toolbar__meta">
                {branchId ? `${reportMessages.branchScopeActive} · ` : `${reportMessages.allBranches} · `}{businessDate}
              </p>
            )}
          </div>
          <div className="dcsr-toolbar__right">
            <div className="dcsr-toolbar__controls">
              <Input
                label={reportMessages.businessDateLabel}
                type="date"
                value={businessDate}
                onChange={event => handleDateChange(event.target.value)}
              />
              <div className="dcsr-toolbar__actions">
                <Button variant="secondary" onClick={() => void loadReport(businessDate, branchId).catch(() => undefined)}>
                  {reportMessages.refreshReport}
                </Button>
                <Badge tone={loading ? 'warning' : 'neutral'} label={loading ? reportMessages.refreshing : reportMessages.ready} />
              </div>
            </div>
          </div>
        </div>

        {error ? (
          <div className="admin-notice admin-notice--danger" role="alert">
            {error}
          </div>
        ) : null}

        {!report ? (
          loading ? (
            <EmptyState title={reportMessages.loadingReportTitle} description={reportMessages.loadingReportDescription} tone="inventory" />
          ) : (
            <EmptyState
              title={reportMessages.noReportLoadedTitle}
              description={reportMessages.noReportLoadedDescription}
              tone="inventory"
              actionLabel={reportMessages.refreshReportAction}
              onAction={() => void loadReport(businessDate, branchId).catch(() => undefined)}
            />
          )
        ) : (
          <>
            {/* ── Primary financial summary ── */}
            <p className="dcsr-section-label">{reportMessages.summaryLabel}</p>
            <div className="dcsr-primary-grid">
              <SummaryCard
                label={reportMessages.grossBillTotalLabel}
                value={formatDailyCashSalesCurrency(summary?.grossBillTotal ?? 0, report.currencyCode)}
                detail={reportMessages.cancelledBillsTrackedSeparately}
                tone="inventory"
              />
              <SummaryCard
                label={reportMessages.paidTotalLabel}
                value={formatDailyCashSalesCurrency(summary?.totalAmountPaid ?? 0, report.currencyCode)}
                detail={reportMessages.recordedPaymentsOnly}
                tone="dashboard"
              />
              <SummaryCard
                label={reportMessages.unpaidBalanceLabel}
                value={formatDailyCashSalesCurrency(summary?.totalBalanceDue ?? 0, report.currencyCode)}
                detail={`${summary?.unpaidBills ?? 0} ${reportMessages.unpaidBillsDetailSuffix}`}
                tone="admin"
              />
              <SummaryCard
                label={reportMessages.closedShiftVarianceTotalLabel}
                value={formatDailyCashSalesCurrency(summary?.cashVarianceTotal ?? 0, report.currencyCode)}
                detail={reportMessages.cashVarianceExplanation}
                tone={summary && Math.abs(summary.cashVarianceTotal) > 0 ? 'admin' : 'dashboard'}
              />
            </div>

            {/* ── Secondary control status ── */}
            <div className="dcsr-secondary-grid">
              <SummaryCard
                label={reportMessages.cancelledBillsLabel}
                value={formatCount(summary?.cancelledBills ?? 0)}
                detail={formatDailyCashSalesCurrency(summary?.cancelledBillAmount ?? 0, report.currencyCode)}
                tone="admin"
              />
              <SummaryCard
                label={reportMessages.openShiftsLabel}
                value={formatCount(summary?.openShifts ?? 0)}
                detail={reportMessages.cashierShiftSummaryDescription}
                tone="inventory"
              />
              <SummaryCard
                label={reportMessages.closedShiftsLabel}
                value={formatCount(summary?.closedShifts ?? 0)}
                detail={reportMessages.closedShiftVarianceTotalLabel}
                tone="dashboard"
              />
              <SummaryCard
                label={reportMessages.openingCashTotalLabel}
                value={formatDailyCashSalesCurrency(summary?.openingCashTotal ?? 0, report.currencyCode)}
                tone="orders"
              />
              <SummaryCard
                label={reportMessages.declaredClosingCashLabel}
                value={formatDailyCashSalesCurrency(summary?.declaredClosingCashTotal ?? 0, report.currencyCode)}
                tone="orders"
              />
              <SummaryCard
                label={reportMessages.expectedCashTotalLabel}
                value={formatDailyCashSalesCurrency(summary?.expectedCashTotal ?? 0, report.currencyCode)}
                detail={reportMessages.openingCashPlusPayments}
                tone="accent"
              />
            </div>

            {/* ── Payment breakdown ── */}
            <p className="dcsr-section-label">{reportMessages.paymentBreakdownTitle}</p>
            <Card title={reportMessages.paymentBreakdownTitle} description={reportMessages.paymentBreakdownDescription} tone="inventory">
              <ResponsiveDataList
                rows={paymentRows}
                columns={[
                  { key: 'paymentMode', label: reportMessages.paymentModeLabel, render: row => formatDailyCashSalesPaymentMode(row.paymentMode) },
                  {
                    key: 'recordedAmount',
                    label: reportMessages.recordedLabel,
                    align: 'right',
                    render: row => formatDailyCashSalesCurrency(row.recordedAmount, report.currencyCode),
                  },
                  {
                    key: 'cancelledAmount',
                    label: reportMessages.cancelledLabel,
                    align: 'right',
                    render: row => formatDailyCashSalesCurrency(row.cancelledAmount, report.currencyCode),
                  },
                  {
                    key: 'netAmount',
                    label: reportMessages.netLabel,
                    align: 'right',
                    render: row => (
                      <strong className="dcsr-net-value">
                        {formatDailyCashSalesCurrency(row.netAmount, report.currencyCode)}
                      </strong>
                    ),
                  },
                  { key: 'paymentCount', label: reportMessages.countLabel, align: 'right', render: row => formatCount(row.paymentCount) },
                  {
                    key: 'cancelledCount',
                    label: reportMessages.cancelledCountLabel,
                    align: 'right',
                    render: row => formatCount(row.cancelledCount),
                  },
                ]}
                mobileTitle={row => formatDailyCashSalesPaymentMode(row.paymentMode)}
                mobileDescription={row => `${formatDailyCashSalesCurrency(row.netAmount, report.currencyCode)} ${t('dailyCashSales.netSuffix')}`}
                emptyTitle={reportMessages.noPaymentRowsTitle}
                emptyDescription={reportMessages.noPaymentRowsDescription}
              />
            </Card>

            <Card title={reportMessages.cashierShiftSummaryTitle} description={reportMessages.cashierShiftSummaryDescription} tone="inventory">
              <ResponsiveDataList
                rows={shiftRows}
                columns={[
                  { key: 'branchName', label: reportMessages.branchLabel },
                  {
                    key: 'status',
                    label: reportMessages.statusLabel,
                    render: row => <StatusBadge status={row.status} />,
                  },
                  {
                    key: 'openedAt',
                    label: reportMessages.openedLabel,
                    render: row => formatDailyCashSalesTimestamp(row.openedAt, { notAvailable: reportMessages.notAvailable }),
                  },
                  {
                    key: 'closedAt',
                    label: reportMessages.closedLabel,
                    render: row => formatDailyCashSalesTimestamp(row.closedAt, { notAvailable: reportMessages.notAvailable }),
                  },
                  {
                    key: 'openingCashAmount',
                    label: reportMessages.openingCashLabel,
                    align: 'right',
                    render: row => formatDailyCashSalesCurrency(row.openingCashAmount, report.currencyCode),
                  },
                  {
                    key: 'expectedCashAmount',
                    label: reportMessages.expectedCashLabel,
                    align: 'right',
                    render: row => formatDailyCashSalesCurrency(row.expectedCashAmount, report.currencyCode),
                  },
                  {
                    key: 'countedCashAmount',
                    label: reportMessages.declaredClosingCashLabel,
                    align: 'right',
                    render: row =>
                      row.countedCashAmount === null
                        ? reportMessages.declaredClosingCashValueNotCounted
                        : formatDailyCashSalesCurrency(row.countedCashAmount, report.currencyCode),
                  },
                  {
                    key: 'cashVarianceAmount',
                    label: reportMessages.closedShiftVarianceTotalLabel,
                    align: 'right',
                    render: row =>
                      row.cashVarianceAmount === null
                        ? reportMessages.openValue
                        : formatDailyCashSalesCurrency(row.cashVarianceAmount, report.currencyCode),
                  },
                  {
                    key: 'cashPaymentTotal',
                    label: reportMessages.cashPaymentsLabel,
                    align: 'right',
                    render: row => formatDailyCashSalesCurrency(row.cashPaymentTotal, report.currencyCode),
                  },
                ]}
                mobileTitle={row => row.branchName}
                mobileDescription={row => `${row.status} ${t('dailyCashSales.shiftSuffix')}`}
                emptyTitle={reportMessages.noCashShiftsTitle}
                emptyDescription={reportMessages.noCashShiftsDescription}
              />
            </Card>

            {/* ── Control exceptions ── */}
            <p className="dcsr-section-label">{reportMessages.controlExceptionsLabel}</p>
            <div className="dcsr-exceptions-card">
              {allClear ? (
                <p className="dcsr-exceptions-card__all-clear">
                  <span className="dcsr-exceptions-card__all-clear-icon" aria-hidden="true">✓</span>
                  {reportMessages.allControlsClearMessage}
                </p>
              ) : null}

              <div className="dcsr-exc-list">
                {exceptionSections.map(({ title, emptyTitle, emptyDescription, rows }) => {
                  const typedRows = toExceptionRows(rows);
                  const hasRows = typedRows.length > 0;
                  const tone = hasRows ? getDailyCashSalesSeverityTone(worstSeverity(typedRows)) : 'neutral';

                  return (
                    <div
                      key={title}
                      className={`dcsr-exc-section dcsr-exc-section--${tone}${hasRows ? ' dcsr-exc-section--active' : ''}`}
                    >
                      <div className="dcsr-exc-section__header">
                        <h3 className="dcsr-exc-section__title">{title}</h3>
                        {hasRows ? (
                          <Badge
                            tone={tone}
                            label={
                              typedRows.length === 1
                                ? t('dailyCashSales.itemCountSingular', { count: typedRows.length })
                                : t('dailyCashSales.itemCountPlural', { count: typedRows.length })
                            }
                          />
                        ) : null}
                      </div>

                      {/* Always rendered — provides "No X" heading for tests; visually collapsed when empty */}
                      <div className={`dcsr-exc-body${hasRows ? ' dcsr-exc-body--expanded' : ' dcsr-exc-body--empty'}`}>
                        <ResponsiveDataList
                          rows={typedRows}
                          columns={[
                            { key: 'referenceNumber', label: t('dailyCashSales.referenceColumnLabel') },
                            { key: 'branchName', label: reportMessages.branchLabel },
                            {
                              key: 'amount',
                              label: t('dailyCashSales.amountColumnLabel'),
                              align: 'right',
                              render: row =>
                                row.amount === null ? reportMessages.notApplicable : formatDailyCashSalesCurrency(row.amount, report.currencyCode),
                            },
                            { key: 'status', label: reportMessages.statusLabel },
                            {
                              key: 'occurredAt',
                              label: t('dailyCashSales.occurredColumnLabel'),
                              render: row => formatDailyCashSalesTimestamp(row.occurredAt, { notAvailable: reportMessages.notAvailable }),
                            },
                            {
                              key: 'severity',
                              label: t('dailyCashSales.severityColumnLabel'),
                              render: row => <Badge tone={getDailyCashSalesSeverityTone(row.severity)} label={row.severity} />,
                            },
                            {
                              key: 'reason',
                              label: t('dailyCashSales.reasonColumnLabel'),
                              render: row => row.reason ?? reportMessages.notAvailable,
                            },
                          ]}
                          mobileTitle={row => row.referenceNumber}
                          mobileDescription={row => row.reason ?? row.status}
                          emptyTitle={emptyTitle}
                          emptyDescription={emptyDescription}
                        />
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>

            {/* ── Scope footer ── */}
            <div className="admin-form-note">
              {reportMessages.scopePrefix}: {buildDailyCashSalesScopeSummary(report)}. {reportMessages.generatedPrefix} {formatDailyCashSalesTimestamp(report.generatedAt, { notAvailable: reportMessages.notAvailable })}.
            </div>
            <div className="admin-form-note">
              {reportMessages.readOnlyNote}
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

export default DailyCashSalesReportPage;
