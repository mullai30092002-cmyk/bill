import { useCallback, useEffect, useMemo, useState, type ChangeEvent } from 'react';
import { Link, useSearchParams } from 'react-router-dom';

import { isApiError } from '../../api/apiErrors';
import { AdminLayout } from '../../components/layout';
import type { ShellNavItem } from '../../components/layout/navigation';
import { Badge, Button, Card, EmptyState, Select, StatusBadge, SummaryCard } from '../../components/ui';
import { useAuth } from '../auth/useAuth';
import { useLanguage } from '../../i18n/LanguageProvider';
import { getSetupChecklist, updateSetupBusinessType } from './setupChecklistApi';
import {
  buildSetupChecklistViewItem,
  getSetupChecklistCardTone,
  getSetupChecklistPriorityTone,
} from './setupChecklistDisplay';
import { getSafeSetupChecklistErrorMessage } from './setupChecklistErrorDisplay';
import {
  getSetupBusinessTypeGuidanceKey,
  getSetupBusinessTypeLabelKey,
  setupBusinessTypeOptions,
} from './setupBusinessTypeDisplay';
import type { SetupBusinessType, SetupChecklistResponse } from './setupChecklistTypes';

export interface SetupChecklistPageProps {
  navItems?: ShellNavItem[];
  restaurantName?: string;
  branchName?: string;
  operatorLabel?: string;
}

export const SetupChecklistPage = ({
  navItems,
  restaurantName,
  branchName,
  operatorLabel,
}: SetupChecklistPageProps) => {
  const auth = useAuth();
  const { t } = useLanguage();
  const [searchParams] = useSearchParams();
  const canAccess =
    auth.hasPermission('Report.View') ||
    auth.hasPermission('Branch.Manage') ||
    auth.hasPermission('User.Manage');
  const canUpdateBusinessType = auth.hasPermission('Branch.Manage') || auth.hasPermission('User.Manage');
  const branchId = searchParams.get('branchId') || auth.session?.branchId || null;
  const messages = useMemo(
    () => ({
      title: t('setupChecklist.pageTitle'),
      description: t('setupChecklist.pageDescription'),
      progressCardTitle: t('setupChecklist.progressCardTitle'),
      progressLabel: t('setupChecklist.progressLabel'),
      completedSteps: t('setupChecklist.completedSteps'),
      businessTypeLabel: t('setupChecklist.businessTypeLabel'),
      businessTypeHelper: t('setupChecklist.businessTypeHelper'),
      businessTypeSaving: t('setupChecklist.businessTypeSaving'),
      businessTypeSaved: t('setupChecklist.businessTypeSaved'),
      businessTypeSaveFailed: t('setupChecklist.businessTypeSaveFailed'),
      businessTypeReadOnlyLabel: t('setupChecklist.businessTypeReadOnlyLabel'),
      businessTypeReadOnlyHelper: t('setupChecklist.businessTypeReadOnlyHelper'),
      profilePrefix: t('setupChecklist.profilePrefix'),
      setupReady: t('setupChecklist.setupReady'),
      setupNeedsAttention: t('setupChecklist.setupNeedsAttention'),
      refresh: t('setupChecklist.refresh'),
      loadingTitle: t('setupChecklist.loadingTitle'),
      loadingDescription: t('setupChecklist.loadingDescription'),
      errorTitle: t('setupChecklist.couldNotLoadTitle'),
      errorDescription: t('setupChecklist.couldNotLoadDescription'),
      tryAgain: t('setupChecklist.tryAgain'),
      notAuthorizedTitle: t('setupChecklist.notAuthorizedTitle'),
      notAuthorizedDescription: t('setupChecklist.notAuthorizedDescription'),
      sessionExpired: t('setupChecklist.sessionExpired'),
      unauthorized: t('setupChecklist.unauthorized'),
    }),
    [t]
  );
  const [checklist, setChecklist] = useState<SetupChecklistResponse | null>(null);
  const [businessType, setBusinessType] = useState<SetupBusinessType>('Restaurant');
  const [savingBusinessType, setSavingBusinessType] = useState(false);
  const [businessTypeNotice, setBusinessTypeNotice] = useState<{ tone: 'success' | 'warning' | 'danger'; message: string } | null>(null);
  const [loading, setLoading] = useState(canAccess);
  const [error, setError] = useState<string | null>(null);

  const loadChecklist = useCallback(
    async (selectedBranchId: string | null) => {
      if (!canAccess) {
        return;
      }

      setLoading(true);
      setError(null);

      try {
        const response = await getSetupChecklist(selectedBranchId);
        setChecklist(response);
        setBusinessType(response.businessType);
      } catch (caughtError) {
        setChecklist(null);
        setError(
          getSafeSetupChecklistErrorMessage(caughtError, messages.errorDescription, {
            sessionExpired: messages.sessionExpired,
            unauthorized: messages.unauthorized,
          })
        );

        if (isApiError(caughtError) && caughtError.status === 401) {
          void auth.logout();
        }
      } finally {
        setLoading(false);
      }
    },
    [auth, canAccess, messages]
  );

  const handleBusinessTypeChange = useCallback(
    async (event: ChangeEvent<HTMLSelectElement>) => {
      if (!canUpdateBusinessType) {
        return;
      }

      const nextBusinessType = event.target.value as SetupBusinessType;
      const previousBusinessType = businessType;

      setBusinessType(nextBusinessType);
      setSavingBusinessType(true);
      setBusinessTypeNotice(null);

      try {
        await updateSetupBusinessType(nextBusinessType);
        setBusinessTypeNotice({ tone: 'success', message: messages.businessTypeSaved });
        void loadChecklist(branchId).catch(() => undefined);
      } catch {
        setBusinessType(previousBusinessType);
        setBusinessTypeNotice({ tone: 'danger', message: messages.businessTypeSaveFailed });
      } finally {
        setSavingBusinessType(false);
      }
    },
    [businessType, branchId, canUpdateBusinessType, loadChecklist, messages.businessTypeSaveFailed, messages.businessTypeSaved]
  );

  useEffect(() => {
    if (!canAccess) {
      return;
    }

    void loadChecklist(branchId).catch(() => undefined);
  }, [branchId, canAccess, loadChecklist]);

  const checklistItems = useMemo(
    () => (checklist?.items ?? []).map(item => buildSetupChecklistViewItem(item, t)),
    [checklist, t]
  );
  const completionTone = checklist?.completionPercent === 100 ? 'success' : 'warning';
  const completionLabel = checklist?.completionPercent === 100 ? messages.setupReady : messages.setupNeedsAttention;
  const branchLabel = checklist?.branchName ?? branchName;
  const selectedBusinessTypeLabel = t(getSetupBusinessTypeLabelKey(businessType));
  const businessTypeGuidance = t(getSetupBusinessTypeGuidanceKey(businessType));
  const businessTypeStatusTone = businessTypeNotice?.tone ?? (savingBusinessType ? 'warning' : 'neutral');
  const businessTypeStatusLabel =
    businessTypeNotice?.message ??
    (savingBusinessType ? messages.businessTypeSaving : !canUpdateBusinessType ? messages.businessTypeReadOnlyLabel : null);

  if (!canAccess) {
    return (
      <AdminLayout
        title={messages.title}
        description={messages.description}
        breadcrumbs={[t('nav.dashboard'), messages.title]}
        operatorLabel={operatorLabel}
        restaurantName={restaurantName}
        branchName={branchName}
        navItems={navItems}
      >
        <EmptyState
          title={messages.notAuthorizedTitle}
          description={messages.notAuthorizedDescription}
          tone="admin"
        />
      </AdminLayout>
    );
  }

  return (
    <AdminLayout
      title={messages.title}
      description={messages.description}
      breadcrumbs={[t('nav.dashboard'), messages.title]}
      operatorLabel={operatorLabel}
      restaurantName={checklist?.restaurantName ?? restaurantName}
      branchName={branchLabel || undefined}
      navItems={navItems}
      actions={
        <Button variant="secondary" onClick={() => void loadChecklist(branchId).catch(() => undefined)}>
          {messages.refresh}
        </Button>
      }
      >
      <div className="preview-sequence setup-checklist-page">
        {error ? (
          <div className="admin-notice admin-notice--danger" role="alert">
            {error}
          </div>
        ) : null}

        <Card
          title={messages.businessTypeLabel}
          description={messages.businessTypeHelper}
          tone="admin"
          actions={businessTypeStatusLabel ? <Badge tone={businessTypeStatusTone} label={businessTypeStatusLabel} /> : null}
        >
          {canUpdateBusinessType ? (
            <div className="admin-controls">
              <Select
                label={messages.businessTypeLabel}
                value={businessType}
                onChange={handleBusinessTypeChange}
                disabled={loading || savingBusinessType}
                helperText={messages.businessTypeHelper}
              >
                {setupBusinessTypeOptions.map(option => (
                  <option key={option.value} value={option.value}>
                    {t(option.labelKey)}
                  </option>
                ))}
              </Select>
            </div>
          ) : (
            <div className="admin-form-note">
              {messages.businessTypeReadOnlyHelper}
            </div>
          )}
          <div className="admin-form-note">
            {messages.profilePrefix}: {selectedBusinessTypeLabel}. {businessTypeGuidance}
          </div>
        </Card>

        <Card
          title={messages.progressCardTitle}
          description={checklist?.branchName ? checklist.branchName : messages.description}
          tone="admin"
          actions={checklist ? <Badge tone={completionTone} label={completionLabel} /> : null}
        >
          {checklist ? (
            <div className="summary-grid">
              <SummaryCard
                label={messages.progressLabel}
                value={`${checklist.completionPercent}%`}
                detail={`${checklist.completedCount} / ${checklist.totalCount}`}
                tone={checklist.completionPercent === 100 ? 'dashboard' : 'admin'}
              />
              <SummaryCard
                label={messages.completedSteps}
                value={`${checklist.completedCount}`}
                detail={`${checklist.totalCount}`}
                tone="inventory"
              />
            </div>
          ) : loading ? (
            <EmptyState title={messages.loadingTitle} description={messages.loadingDescription} tone="admin" />
          ) : (
            <EmptyState
              title={messages.errorTitle}
              description={messages.errorDescription}
              tone="admin"
              actionLabel={messages.tryAgain}
              onAction={() => void loadChecklist(branchId).catch(() => undefined)}
            />
          )}
        </Card>

        {checklistItems.length > 0 ? (
          <div className="preview-sequence">
            {checklistItems.map(item => (
              <Card
                key={item.key}
                title={item.title}
                description={item.description}
                tone={getSetupChecklistCardTone(item.status)}
                actions={<StatusBadge status={item.status} label={item.statusLabel} />}
              >
                <div className="preview-sequence">
                  <div className="setup-checklist-page__meta">
                    <Badge tone={getSetupChecklistPriorityTone(item.priority)} label={item.priorityLabel} />
                    {item.countLabel ? <Badge tone="neutral" label={item.countLabel} /> : null}
                    {item.warningCountLabel ? <Badge tone="warning" label={item.warningCountLabel} /> : null}
                  </div>
                  <div className="preview-checks">
                    <Link className="ui-button ui-button--secondary ui-button--sm" to={item.actionHref}>
                      {item.actionLabel}
                    </Link>
                  </div>
                </div>
              </Card>
            ))}
          </div>
        ) : null}
      </div>
    </AdminLayout>
  );
};

export default SetupChecklistPage;
