import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from 'react';

import { getSafeApiErrorMessage, isApiError } from '../../../api/apiErrors';
import { AdminLayout } from '../../../components/layout';
import type { ShellNavItem } from '../../../components/layout/navigation';
import { Badge, Button, Card, EmptyState, SummaryCard } from '../../../components/ui';
import { useAuth } from '../../auth/useAuth';
import { useLanguage } from '../../../i18n/LanguageProvider';
import {
  activateAdminBranch,
  createAdminBranch,
  deactivateAdminBranch,
  getAdminBranch,
  listAdminBranches,
  updateAdminBranch,
} from '../adminApi';
import type { AdminBranchDetail, AdminBranchListItem, AdminBranchStatus } from '../adminTypes';
import BranchDirectoryList from './BranchDirectoryList';
import BranchForm from './BranchForm';
import BranchStatusActions from './BranchStatusActions';
import {
  buildBranchFormErrors,
  buildCreateBranchRequest,
  buildUpdateBranchRequest,
  emptyBranchForm,
  type BranchFormErrors,
  type BranchFormState,
} from './branchFormValidation';
import { filterBranches, sortBranches, formatBranchTimestamp } from './branchDisplay';

export interface BranchManagementPageProps {
  navItems?: ShellNavItem[];
  restaurantName?: string;
  branchName?: string;
  operatorLabel?: string;
}

type BranchManagementMode = 'create' | 'edit';
type BranchStatusFilter = 'All' | AdminBranchStatus;
type NoticeTone = 'success' | 'info' | 'warning' | 'danger';

interface Notice {
  tone: NoticeTone;
  message: string;
}

const resolveSafeMessage = (error: unknown, fallback: string) => getSafeApiErrorMessage(error, fallback);

export const BranchManagementPage = ({
  navItems,
  restaurantName,
  branchName,
  operatorLabel,
}: BranchManagementPageProps) => {
  const auth = useAuth();
  const { t } = useLanguage();
  const canManageBranches = auth.hasPermission('Branch.Manage');
  const [branches, setBranches] = useState<AdminBranchListItem[]>([]);
  const [branchesLoading, setBranchesLoading] = useState(canManageBranches);
  const [branchesError, setBranchesError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<BranchStatusFilter>('All');
  const [mode, setMode] = useState<BranchManagementMode>('create');
  const [selectedBranchId, setSelectedBranchId] = useState<string | null>(null);
  const [selectedBranch, setSelectedBranch] = useState<AdminBranchDetail | null>(null);
  const [selectedBranchLoading, setSelectedBranchLoading] = useState(false);
  const [selectedBranchError, setSelectedBranchError] = useState<string | null>(null);
  const [notice, setNotice] = useState<Notice | null>(null);
  const [formScrollToken, setFormScrollToken] = useState(0);
  const [form, setForm] = useState<BranchFormState>(() => emptyBranchForm());
  const [formErrors, setFormErrors] = useState<BranchFormErrors>({});
  const [saveSubmitting, setSaveSubmitting] = useState(false);
  const [statusSubmitting, setStatusSubmitting] = useState(false);
  const [confirmDeactivate, setConfirmDeactivate] = useState(false);
  const branchFormSectionRef = useRef<HTMLDivElement | null>(null);
  const branchNameInputRef = useRef<HTMLInputElement | null>(null);

  const requestBranchFormScroll = useCallback(() => {
    setFormScrollToken(token => token + 1);
  }, []);

  const refreshBranches = useCallback(async () => {
    if (!canManageBranches) {
      return;
    }

    setBranchesLoading(true);
    setBranchesError(null);

    try {
      const response = await listAdminBranches();
      setBranches(sortBranches(response.items));
    } catch (caughtError) {
      const message = resolveSafeMessage(caughtError, 'Could not load branches right now.');
      setBranchesError(message);
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
      throw caughtError;
    } finally {
      setBranchesLoading(false);
    }
  }, [auth, canManageBranches]);

  const refreshSelectedBranch = useCallback(
    async (branchId: string) => {
      setSelectedBranchLoading(true);
      setSelectedBranchError(null);

      try {
        const detail = await getAdminBranch(branchId);
        setSelectedBranch(detail);
        return detail;
      } catch (caughtError) {
        const message = resolveSafeMessage(caughtError, 'Could not load the selected branch.');
        setSelectedBranchError(message);
        setSelectedBranch(null);
        if (isApiError(caughtError) && caughtError.status === 401) {
          void auth.logout();
        }
        throw caughtError;
      } finally {
        setSelectedBranchLoading(false);
      }
    },
    [auth]
  );

  useEffect(() => {
    if (!canManageBranches) {
      return;
    }

    void refreshBranches().catch(() => undefined);
  }, [canManageBranches, refreshBranches]);

  useEffect(() => {
    if (!canManageBranches || mode !== 'edit' || !selectedBranchId) {
      setSelectedBranch(null);
      setSelectedBranchError(null);
      setSelectedBranchLoading(false);
      setConfirmDeactivate(false);
      return;
    }

    const controller = new AbortController();

    void refreshSelectedBranch(selectedBranchId).catch(() => undefined);

    return () => {
      controller.abort();
    };
  }, [canManageBranches, mode, refreshSelectedBranch, selectedBranchId]);

  useEffect(() => {
    if (!selectedBranch) {
      return;
    }

    setForm({
      name: selectedBranch.name,
      address: selectedBranch.address ?? '',
      phone: selectedBranch.phone ?? '',
      timezone: selectedBranch.timezone,
      currency: selectedBranch.currency,
    });
    setFormErrors({});
    setConfirmDeactivate(false);
  }, [selectedBranch]);

  const visibleBranches = useMemo(
    () => sortBranches(filterBranches(branches, search, statusFilter)),
    [branches, search, statusFilter]
  );

  const activeCount = useMemo(() => branches.filter(branch => branch.status === 'Active').length, [branches]);
  const inactiveCount = useMemo(
    () => branches.filter(branch => branch.status === 'Inactive').length,
    [branches]
  );

  const startCreateMode = useCallback(() => {
    setMode('create');
    setSelectedBranchId(null);
    setSelectedBranch(null);
    setSelectedBranchError(null);
    setConfirmDeactivate(false);
    setForm(emptyBranchForm());
    setFormErrors({});
    setNotice(null);
    requestBranchFormScroll();
  }, [requestBranchFormScroll]);

  const startEditMode = useCallback((branchId: string) => {
    setMode('edit');
    setSelectedBranchId(branchId);
    setSelectedBranchError(null);
    setNotice(null);
    requestBranchFormScroll();
  }, [requestBranchFormScroll]);

  useEffect(() => {
    if (formScrollToken === 0) {
      return;
    }

    const target = branchFormSectionRef.current;
    const input = branchNameInputRef.current;

    if (!target || !input) {
      return;
    }

    if (typeof target.scrollIntoView === 'function') {
      target.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
    input.focus();
  }, [formScrollToken, mode, selectedBranch]);

  const handleSaveSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const nextErrors = buildBranchFormErrors(form);
    setFormErrors(nextErrors);

    if (Object.keys(nextErrors).length > 0) {
      setNotice({
        tone: 'warning',
        message: t('branches.noticeFixFormBeforeSaving'),
      });
      return;
    }

    setSaveSubmitting(true);
    setNotice(null);

    try {
      if (mode === 'create') {
        const created = await createAdminBranch(buildCreateBranchRequest(form));
        await refreshBranches();
        setForm(emptyBranchForm());
        setFormErrors({});
        setNotice({
          tone: 'success',
          message: t('branches.createdBranch').replace('{name}', created.name),
        });
        return;
      }

      if (!selectedBranchId) {
        return;
      }

      const updated = await updateAdminBranch(selectedBranchId, buildUpdateBranchRequest(form));
      setSelectedBranch(updated);
      setForm({
        name: updated.name,
        address: updated.address ?? '',
        phone: updated.phone ?? '',
        timezone: updated.timezone,
        currency: updated.currency,
      });
      await refreshBranches();
      setNotice({
        tone: 'success',
        message: t('branches.savedChangesFor').replace('{name}', updated.name),
      });
    } catch (caughtError) {
      const fallback =
        mode === 'create' ? 'Could not create the branch right now.' : 'Could not save the branch right now.';
      const message = resolveSafeMessage(caughtError, fallback);
      setNotice({ tone: 'danger', message });
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setSaveSubmitting(false);
    }
  };

  const handleActivate = async () => {
    if (!selectedBranchId) {
      return;
    }

    setStatusSubmitting(true);
    setNotice(null);

    try {
      const updated = await activateAdminBranch(selectedBranchId);
      setSelectedBranch(updated);
      await refreshBranches();
      setNotice({
        tone: 'success',
        message: t('branches.isNowActive').replace('{name}', updated.name),
      });
    } catch (caughtError) {
      const message = resolveSafeMessage(caughtError, 'Could not activate the branch right now.');
      setNotice({ tone: 'danger', message });
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setStatusSubmitting(false);
    }
  };

  const handleDeactivate = async () => {
    if (!selectedBranchId) {
      return;
    }

    setStatusSubmitting(true);
    setNotice(null);

    try {
      const updated = await deactivateAdminBranch(selectedBranchId);
      setSelectedBranch(updated);
      await refreshBranches();
      setConfirmDeactivate(false);
      setNotice({
        tone: 'success',
        message: t('branches.isNowInactive').replace('{name}', updated.name),
      });
    } catch (caughtError) {
      const message = resolveSafeMessage(
        caughtError,
        t('branches.cannotDeactivateWithActiveUsers')
      );
      setNotice({ tone: 'danger', message });
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setStatusSubmitting(false);
    }
  };

  if (!canManageBranches) {
    return (
      <AdminLayout
        title={t('branches.pageTitle')}
        description={t('branches.pageDescription')}
        breadcrumbs={[t('branches.breadcrumbDashboard'), t('branches.breadcrumbAdmin'), t('branches.breadcrumbBranches')]}
        operatorLabel={operatorLabel}
        restaurantName={restaurantName}
        branchName={branchName}
        navItems={navItems}
      >
        <EmptyState
          title={t('branches.notAuthorizedTitle')}
          description={t('branches.notAuthorizedDescription')}
          tone="admin"
        />
      </AdminLayout>
    );
  }

  return (
    <AdminLayout
      title={t('branches.pageTitle')}
      description={t('branches.pageDescription')}
      breadcrumbs={[t('branches.breadcrumbDashboard'), t('branches.breadcrumbAdmin'), t('branches.breadcrumbBranches')]}
      operatorLabel={operatorLabel}
      restaurantName={restaurantName}
      branchName={branchName}
      navItems={navItems}
      actions={
        <>
          <Button variant="secondary" onClick={() => void refreshBranches().catch(() => undefined)}>
            {t('branches.refreshList')}
          </Button>
          <Button onClick={startCreateMode}>{t('branches.newBranch')}</Button>
        </>
      }
    >
      <div className="preview-sequence">
        <div className="summary-grid">
          <SummaryCard
            label={t('branches.summaryBranches')}
            value={branches.length.toString()}
            tone="admin"
            detail={t('branches.summaryBranchesDetail')}
          />
          <SummaryCard
            label={t('branches.summaryActive')}
            value={activeCount.toString()}
            tone="accent"
            detail={t('branches.summaryActiveDetail')}
          />
          <SummaryCard
            label={t('branches.summaryInactive')}
            value={inactiveCount.toString()}
            tone="inventory"
            detail={t('branches.summaryInactiveDetail')}
          />
        </div>

        <div className="preview-split preview-split--admin branch-management__split">
          <Card
            title={t('branches.directoryCardTitle')}
            description={t('branches.directoryCardDescription')}
            tone="admin"
            actions={<Badge tone="neutral" label={branchesLoading ? t('branches.refreshingBadge') : t('branches.shownBadge').replace('{n}', String(visibleBranches.length))} />}
          >
            <BranchDirectoryList
              branches={visibleBranches}
              loading={branchesLoading}
              error={branchesError}
              search={search}
              statusFilter={statusFilter}
              onSearchChange={setSearch}
              onStatusFilterChange={setStatusFilter}
              onRetry={() => void refreshBranches().catch(() => undefined)}
              onSelectBranch={startEditMode}
            />
          </Card>

          <div className="admin-workspace-stack">
            {mode === 'edit' && selectedBranchLoading ? (
              <Card
                title={t('branches.loadingBranchCardTitle')}
                description={t('branches.loadingBranchCardDescription')}
                tone="admin"
              >
                <EmptyState
                  title={t('branches.loadingBranchDetailsTitle')}
                  description={t('branches.loadingBranchDetailsDescription')}
                  tone="admin"
                />
              </Card>
            ) : mode === 'edit' && selectedBranchError ? (
              <EmptyState
                title={t('branches.couldNotLoadSelectedBranchTitle')}
                description={selectedBranchError}
                tone="admin"
                actionLabel={t('branches.createNewBranch')}
                onAction={startCreateMode}
              />
            ) : mode === 'edit' && selectedBranch ? (
              <>
                <Card
                  title={selectedBranch.name}
                  description={t('branches.branchProfileDescription')}
                  tone="admin"
                  actions={<Badge tone="neutral" label={selectedBranch.status} />}
                >
                  <div className="admin-selected-branch">
                    <div className="admin-selected-branch__meta">
                      <div className="admin-selected-branch__row">
                        <span className="admin-selected-branch__label">{t('branches.metaAddress')}</span>
                        <strong>{selectedBranch.address || t('branches.notProvided')}</strong>
                      </div>
                      <div className="admin-selected-branch__row">
                        <span className="admin-selected-branch__label">{t('branches.metaPhone')}</span>
                        <strong>{selectedBranch.phone || t('branches.notProvided')}</strong>
                      </div>
                      <div className="admin-selected-branch__row">
                        <span className="admin-selected-branch__label">{t('branches.metaTimezone')}</span>
                        <strong>{selectedBranch.timezone}</strong>
                      </div>
                      <div className="admin-selected-branch__row">
                        <span className="admin-selected-branch__label">{t('branches.metaCurrency')}</span>
                        <strong>{selectedBranch.currency}</strong>
                      </div>
                      <div className="admin-selected-branch__row">
                        <span className="admin-selected-branch__label">{t('branches.metaCreated')}</span>
                        <strong>{formatBranchTimestamp(selectedBranch.createdAt)}</strong>
                      </div>
                      <div className="admin-selected-branch__row">
                        <span className="admin-selected-branch__label">{t('branches.metaUpdated')}</span>
                        <strong>{formatBranchTimestamp(selectedBranch.updatedAt)}</strong>
                      </div>
                    </div>
                  </div>
                </Card>

                <div ref={branchFormSectionRef} className="scroll-target">
                  <Card
                    title={t('branches.editBranchCardTitle')}
                    description={t('branches.editBranchCardDescription')}
                    tone="admin"
                  >
                    <BranchForm
                      mode="edit"
                      form={form}
                      errors={formErrors}
                      submitting={saveSubmitting}
                      onSubmit={handleSaveSubmit}
                      onSecondaryAction={startCreateMode}
                      secondaryActionLabel="New branch"
                      setForm={setForm}
                      nameInputRef={branchNameInputRef}
                    />
                  </Card>
                </div>

                <Card
                  title={t('branches.activateDeactivateCardTitle')}
                  description={t('branches.activateDeactivateCardDescription')}
                  tone="admin"
                >
                  <BranchStatusActions
                    status={selectedBranch.status}
                    confirmDeactivate={confirmDeactivate}
                    submitting={statusSubmitting}
                    onActivate={() => void handleActivate()}
                    onRequestDeactivate={() => setConfirmDeactivate(true)}
                    onConfirmDeactivate={() => void handleDeactivate()}
                    onCancelDeactivate={() => setConfirmDeactivate(false)}
                  />
                </Card>
              </>
            ) : (
              <div ref={branchFormSectionRef} className="scroll-target">
                <Card
                  title={t('branches.createBranchCardTitle')}
                  description={t('branches.createBranchCardDescription')}
                  tone="admin"
                >
                  <BranchForm
                    mode="create"
                    form={form}
                    errors={formErrors}
                    submitting={saveSubmitting}
                    onSubmit={handleSaveSubmit}
                    onSecondaryAction={() => setForm(emptyBranchForm())}
                    secondaryActionLabel="Clear"
                    setForm={setForm}
                    nameInputRef={branchNameInputRef}
                  />
                </Card>
              </div>
            )}

            {notice ? (
              <div
                className={['admin-notice', `admin-notice--${notice.tone}`].join(' ')}
                role={notice.tone === 'danger' ? 'alert' : 'status'}
              >
                {notice.message}
              </div>
            ) : null}
          </div>
        </div>
      </div>
    </AdminLayout>
  );
};

export default BranchManagementPage;
