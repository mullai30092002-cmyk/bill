import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react';

import { isApiError } from '../../api/apiErrors';
import { AdminLayout } from '../../components/layout';
import type { ShellNavItem } from '../../components/layout/navigation';
import { Badge, Button, Card, EmptyState, SummaryCard } from '../../components/ui';
import { useAuth } from '../auth/useAuth';
import { useLanguage } from '../../i18n/LanguageProvider';
import {
  activateAdminUser,
  createAdminUser,
  deactivateAdminUser,
  getAdminUser,
  listAdminBranches,
  listAdminRoles,
  listAdminUsers,
  resetAdminUserPassword,
  updateAdminUser,
  updateAdminUserRoles,
} from './adminApi';
import type {
  AdminBranchListItem,
  AdminRoleListItem,
  AdminUserDetail,
  AdminUserListItem,
  AdminUserStatus,
  CreateAdminUserRequest,
  ResetAdminUserPasswordRequest,
  UpdateAdminUserRequest,
  UpdateAdminUserRolesRequest,
} from './adminTypes';
import {
  buildCreateBranchOptions,
  buildEditBranchOptions,
  buildUserDirectoryRows,
  resolveBranchLabel,
} from './users/adminUserDisplay';
import {
  buildCreateUserFormErrors,
  buildProfileFormErrors,
  buildResetPasswordFormErrors,
  emptyCreateUserForm,
  emptyProfileForm,
  emptyResetPasswordForm,
  normalizeOptionalText,
  type AdminCreateUserFormErrors,
  type AdminCreateUserFormState,
  type AdminProfileFormErrors,
  type AdminProfileFormState,
  type AdminResetPasswordFormErrors,
  type AdminResetPasswordFormState,
} from './users/adminUserFormValidation';
import { CreateUserForm } from './users/CreateUserForm';
import { EditUserProfileForm } from './users/EditUserProfileForm';
import { ResetUserPasswordForm } from './users/ResetUserPasswordForm';
import { UserDirectoryList } from './users/UserDirectoryList';
import { UserRoleAssignmentPanel } from './users/UserRoleAssignmentPanel';
import { UserStatusActions } from './users/UserStatusActions';

export interface AdminUsersPageProps {
  navItems?: ShellNavItem[];
  restaurantName?: string;
  branchName?: string;
  operatorLabel?: string;
}

type WorkspaceMode = 'create' | 'edit';

type NoticeTone = 'success' | 'info' | 'warning' | 'danger';

interface Notice {
  tone: NoticeTone;
  message: string;
}

const resolveSafeMessage = (error: unknown, fallback: string) => {
  const sanitizeMessage = (value: string) =>
    value
      .replace(/<script\b[^>]*>[\s\S]*?<\/script>/gi, ' ')
      .replace(/<style\b[^>]*>[\s\S]*?<\/style>/gi, ' ')
      .replace(/<[^>]*>/g, ' ')
      .replace(/\bSQL error:.*$/i, '')
      .replace(/\bStack trace:.*$/i, '')
      .replace(/\r?\n\s*at .*/g, ' ')
      .replace(/\s+/g, ' ')
      .trim();

  if (isApiError(error)) {
    if (error.status === 401) {
      return 'Your session expired. Please sign in again.';
    }

    if (error.status === 403) {
      return 'You are not authorized to make this change.';
    }

    return sanitizeMessage(error.message || fallback) || fallback;
  }

  if (error instanceof Error && error.message.trim()) {
    return sanitizeMessage(error.message) || fallback;
  }

  return fallback;
};

export const AdminUsersPage = ({
  navItems,
  restaurantName,
  branchName,
  operatorLabel,
}: AdminUsersPageProps) => {
  const auth = useAuth();
  const { t } = useLanguage();
  const canManageUsers = auth.hasPermission('User.Manage');
  const [workspaceMode, setWorkspaceMode] = useState<WorkspaceMode>('create');
  const [users, setUsers] = useState<AdminUserListItem[]>([]);
  const [roles, setRoles] = useState<AdminRoleListItem[]>([]);
  const [branches, setBranches] = useState<AdminBranchListItem[]>([]);
  const [usersLoading, setUsersLoading] = useState(canManageUsers);
  const [rolesLoading, setRolesLoading] = useState(canManageUsers);
  const [branchesLoading, setBranchesLoading] = useState(canManageUsers);
  const [usersError, setUsersError] = useState<string | null>(null);
  const [rolesError, setRolesError] = useState<string | null>(null);
  const [branchesError, setBranchesError] = useState<string | null>(null);
  const [selectedUserId, setSelectedUserId] = useState<string | null>(null);
  const [selectedUser, setSelectedUser] = useState<AdminUserDetail | null>(null);
  const [selectedUserLoading, setSelectedUserLoading] = useState(false);
  const [selectedUserError, setSelectedUserError] = useState<string | null>(null);
  const [notice, setNotice] = useState<Notice | null>(null);
  const [createForm, setCreateForm] = useState<AdminCreateUserFormState>(() => emptyCreateUserForm());
  const [createErrors, setCreateErrors] = useState<AdminCreateUserFormErrors>({});
  const [createSubmitting, setCreateSubmitting] = useState(false);
  const [profileForm, setProfileForm] = useState<AdminProfileFormState>(() => emptyProfileForm());
  const [profileErrors, setProfileErrors] = useState<AdminProfileFormErrors>({});
  const [profileSubmitting, setProfileSubmitting] = useState(false);
  const [selectedRoleNames, setSelectedRoleNames] = useState<string[]>([]);
  const [roleSubmitting, setRoleSubmitting] = useState(false);
  const [statusSubmitting, setStatusSubmitting] = useState(false);
  const [confirmDeactivate, setConfirmDeactivate] = useState(false);
  const [showResetPasswordForm, setShowResetPasswordForm] = useState(false);
  const [resetPasswordForm, setResetPasswordForm] = useState<AdminResetPasswordFormState>(() =>
    emptyResetPasswordForm()
  );
  const [resetPasswordErrors, setResetPasswordErrors] = useState<AdminResetPasswordFormErrors>({});
  const [resetPasswordSubmitting, setResetPasswordSubmitting] = useState(false);

  const refreshUsers = useCallback(async () => {
    if (!canManageUsers) {
      return;
    }

    setUsersLoading(true);
    setUsersError(null);

    try {
      const response = await listAdminUsers();
      setUsers(response.items);
    } catch (caughtError) {
      const message = resolveSafeMessage(caughtError, 'Could not load admin users right now.');
      setUsersError(message);
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
      throw caughtError;
    } finally {
      setUsersLoading(false);
    }
  }, [auth, canManageUsers]);

  const refreshRoles = useCallback(async () => {
    if (!canManageUsers) {
      return;
    }

    setRolesLoading(true);
    setRolesError(null);

    try {
      const response = await listAdminRoles();
      setRoles(response.items);
    } catch (caughtError) {
      const message = resolveSafeMessage(caughtError, 'Could not load the role catalog right now.');
      setRolesError(message);
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
      throw caughtError;
    } finally {
      setRolesLoading(false);
    }
  }, [auth, canManageUsers]);

  const refreshBranches = useCallback(async () => {
    if (!canManageUsers) {
      return;
    }

    setBranchesLoading(true);
    setBranchesError(null);

    try {
      const response = await listAdminBranches();
      setBranches(response.items);
    } catch (caughtError) {
      const message = resolveSafeMessage(caughtError, 'Could not load the branch catalog right now.');
      setBranchesError(message);
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
      throw caughtError;
    } finally {
      setBranchesLoading(false);
    }
  }, [auth, canManageUsers]);

  useEffect(() => {
    if (!canManageUsers) {
      return;
    }

    void refreshUsers().catch(() => undefined);
    void refreshRoles().catch(() => undefined);
    void refreshBranches().catch(() => undefined);
  }, [canManageUsers, refreshBranches, refreshRoles, refreshUsers]);

  useEffect(() => {
    if (!canManageUsers || workspaceMode === 'create' || !selectedUserId) {
      setSelectedUser(null);
      setSelectedUserError(null);
      setSelectedUserLoading(false);
      setShowResetPasswordForm(false);
      setResetPasswordForm(emptyResetPasswordForm());
      setResetPasswordErrors({});
      return;
    }

    const controller = new AbortController();

    const loadUser = async () => {
      setSelectedUserLoading(true);
      setSelectedUserError(null);
      setConfirmDeactivate(false);

      try {
        const detail = await getAdminUser(selectedUserId);
        if (!controller.signal.aborted) {
          setSelectedUser(detail);
        }
      } catch (caughtError) {
        if (controller.signal.aborted) {
          return;
        }

        const message = resolveSafeMessage(caughtError, 'Could not load the selected user.');
        setSelectedUserError(message);
        setSelectedUser(null);
        if (isApiError(caughtError) && caughtError.status === 401) {
          void auth.logout();
        }
      } finally {
        if (!controller.signal.aborted) {
          setSelectedUserLoading(false);
        }
      }
    };

    void loadUser();

    return () => {
      controller.abort();
    };
  }, [auth, canManageUsers, selectedUserId, workspaceMode]);

  useEffect(() => {
    if (!selectedUser) {
      return;
    }

    setProfileForm({
      fullName: selectedUser.fullName,
      mobileNumber: selectedUser.mobileNumber,
      email: selectedUser.email ?? '',
      status: selectedUser.status as AdminUserStatus,
      branchId: selectedUser.branchId,
    });
    setSelectedRoleNames([...selectedUser.roleNames]);
    setProfileErrors({});
    setConfirmDeactivate(false);
    setShowResetPasswordForm(false);
    setResetPasswordForm(emptyResetPasswordForm());
    setResetPasswordErrors({});
  }, [selectedUser]);

  const activeCount = useMemo(() => users.filter(user => user.status === 'Active').length, [users]);
  const inactiveCount = useMemo(
    () => users.filter(user => user.status !== 'Active').length,
    [users]
  );
  const listRows = useMemo(() => buildUserDirectoryRows(users, branches), [branches, users]);
  const branchLookup = useCallback(
    (branchId: string | null) => resolveBranchLabel(branchId, branches),
    [branches]
  );
  const selectedUserIsCurrentSession = Boolean(selectedUser?.userId && auth.session?.userId === selectedUser.userId);
  const canResetSelectedUser = Boolean(selectedUser && !selectedUserIsCurrentSession);
  const hasRolesError = Boolean(rolesError);
  const selectedUserTitle = selectedUser ? selectedUser.fullName : 'Choose a user to edit';
  const createBranchOptions = useMemo(() => buildCreateBranchOptions(branches), [branches]);
  const editBranchOptions = useMemo(
    () => buildEditBranchOptions(branches, selectedUser?.branchId ?? null),
    [branches, selectedUser?.branchId]
  );
  const createBranchSelectorDisabled = branchesLoading || Boolean(branchesError);
  const editBranchSelectorDisabled = branchesLoading || Boolean(branchesError);
  const createBranchHelperText = branchesError
    ? t('adminUsers.branchesUnavailable')
    : branchesLoading
      ? t('adminUsers.loadingBranches')
      : createBranchOptions.length === 1
        ? t('adminUsers.noActiveBranches')
        : t('adminUsers.chooseBranchIfNeeded');
  const editBranchHelperText = branchesError
    ? t('adminUsers.branchesUnavailableEdit')
    : branchesLoading
      ? t('adminUsers.loadingBranches')
      : t('adminUsers.chooseBranchEdit');

  const openCreateWorkspace = () => {
    setWorkspaceMode('create');
    setSelectedUserId(null);
    setSelectedUser(null);
    setSelectedUserError(null);
    setConfirmDeactivate(false);
    setShowResetPasswordForm(false);
    setResetPasswordForm(emptyResetPasswordForm());
    setResetPasswordErrors({});
    setNotice(null);
  };

  const openEditWorkspace = (userId: string) => {
    setWorkspaceMode('edit');
    setSelectedUserId(userId);
    setSelectedUserError(null);
    setNotice(null);
  };

  const toggleSelectedRole = (roleName: string) => {
    setSelectedRoleNames(current =>
      current.includes(roleName) ? current.filter(name => name !== roleName) : [...current, roleName]
    );
  };

  const toggleCreateRole = (roleName: string) => {
    setCreateForm(current => ({
      ...current,
      roleNames: current.roleNames.includes(roleName)
        ? current.roleNames.filter(name => name !== roleName)
        : [...current.roleNames, roleName],
    }));
  };

  const validationMessages = useMemo(() => ({
    fullNameRequired: t('adminUsers.validationFullNameRequired'),
    mobileRequired: t('adminUsers.validationMobileRequired'),
    statusRequired: t('adminUsers.validationStatusRequired'),
    selectAtLeastOneRole: t('adminUsers.validationSelectAtLeastOneRole'),
    initialPasswordMinLength: (n: number) =>
      t('adminUsers.validationInitialPasswordMinLength').replace('{n}', String(n)),
    newPasswordMinLength: (n: number) =>
      t('adminUsers.validationNewPasswordMinLength').replace('{n}', String(n)),
    confirmPassword: t('adminUsers.validationConfirmPassword'),
    passwordsMismatch: t('adminUsers.validationPasswordsMismatch'),
  }), [t]);

  const handleCreateSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const nextErrors = buildCreateUserFormErrors(createForm, validationMessages);
    setCreateErrors(nextErrors);

    if (Object.keys(nextErrors).length > 0) {
      setNotice({
        tone: 'warning',
        message: t('adminUsers.noticeFixFormBeforeCreate'),
      });
      return;
    }

    setCreateSubmitting(true);
    setNotice(null);

    try {
      const request: CreateAdminUserRequest = {
        branchId: createForm.branchId,
        fullName: createForm.fullName.trim(),
        mobileNumber: createForm.mobileNumber.trim(),
        email: normalizeOptionalText(createForm.email),
        initialPassword: createForm.initialPassword,
        roleNames: createForm.roleNames,
      };

      const created = await createAdminUser(request);
      await refreshUsers();
      setCreateForm(emptyCreateUserForm());
      setCreateErrors({});
      setNotice({
        tone: 'success',
        message: t('adminUsers.createdUser').replace('{name}', created.fullName),
      });
    } catch (caughtError) {
      const message = resolveSafeMessage(caughtError, 'Could not create the user right now.');
      setNotice({ tone: 'danger', message });
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setCreateSubmitting(false);
    }
  };

  const handleProfileSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    if (!selectedUser) {
      return;
    }

    const nextErrors = buildProfileFormErrors(profileForm, validationMessages);
    setProfileErrors(nextErrors);

    if (Object.keys(nextErrors).length > 0) {
      setNotice({
        tone: 'warning',
        message: t('adminUsers.noticeFixProfileBeforeSaving'),
      });
      return;
    }

    setProfileSubmitting(true);
    setNotice(null);

    try {
      const request: UpdateAdminUserRequest = {
        branchId: profileForm.branchId,
        fullName: profileForm.fullName.trim(),
        mobileNumber: profileForm.mobileNumber.trim(),
        email: normalizeOptionalText(profileForm.email),
        status: profileForm.status,
      };
      const updated = await updateAdminUser(selectedUser.userId, request);
      setSelectedUser(updated);
      await refreshUsers();
      setNotice({
        tone: 'success',
        message: t('adminUsers.savedProfileChanges').replace('{name}', updated.fullName),
      });
    } catch (caughtError) {
      const message = resolveSafeMessage(caughtError, 'Could not save the profile right now.');
      setNotice({ tone: 'danger', message });
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setProfileSubmitting(false);
    }
  };

  const handleRoleSubmit = async () => {
    if (!selectedUser) {
      return;
    }

    setRoleSubmitting(true);
    setNotice(null);

    try {
      const request: UpdateAdminUserRolesRequest = {
        roleNames: selectedRoleNames,
      };
      const updated = await updateAdminUserRoles(selectedUser.userId, request);
      setSelectedUser(updated);
      setSelectedRoleNames([...updated.roleNames]);
      await refreshUsers();
      setNotice({
        tone: 'success',
        message: t('adminUsers.updatedRolesFor').replace('{name}', updated.fullName),
      });
    } catch (caughtError) {
      const message = resolveSafeMessage(caughtError, 'Could not update the roles right now.');
      setNotice({ tone: 'danger', message });
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setRoleSubmitting(false);
    }
  };

  const handleActivate = async () => {
    if (!selectedUser) {
      return;
    }

    setStatusSubmitting(true);
    setNotice(null);

    try {
      const updated = await activateAdminUser(selectedUser.userId);
      setSelectedUser(updated);
      await refreshUsers();
      setNotice({
        tone: 'success',
        message: t('adminUsers.isNowActive').replace('{name}', updated.fullName),
      });
    } catch (caughtError) {
      const message = resolveSafeMessage(caughtError, 'Could not activate the user right now.');
      setNotice({ tone: 'danger', message });
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setStatusSubmitting(false);
    }
  };

  const handleDeactivate = async () => {
    if (!selectedUser) {
      return;
    }

    setStatusSubmitting(true);
    setNotice(null);

    try {
      const updated = await deactivateAdminUser(selectedUser.userId);
      setSelectedUser(updated);
      await refreshUsers();
      setConfirmDeactivate(false);
      setNotice({
        tone: 'success',
        message: t('adminUsers.isNowInactive').replace('{name}', updated.fullName),
      });
    } catch (caughtError) {
      const message = resolveSafeMessage(caughtError, 'Could not deactivate the user right now.');
      setNotice({ tone: 'danger', message });
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setStatusSubmitting(false);
    }
  };

  const openResetPasswordForm = () => {
    setShowResetPasswordForm(true);
    setResetPasswordErrors({});
    setNotice(null);
  };

  const closeResetPasswordForm = () => {
    setShowResetPasswordForm(false);
    setResetPasswordForm(emptyResetPasswordForm());
    setResetPasswordErrors({});
  };

  const handleResetPasswordSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    if (!selectedUser) {
      return;
    }

    const nextErrors = buildResetPasswordFormErrors(resetPasswordForm, selectedUser.roleNames, validationMessages);
    setResetPasswordErrors(nextErrors);

    if (Object.keys(nextErrors).length > 0) {
      setNotice({
        tone: 'warning',
        message: t('adminUsers.noticeFixResetPasswordBeforeSubmitting'),
      });
      return;
    }

    setResetPasswordSubmitting(true);
    setNotice(null);

    try {
      const request: ResetAdminUserPasswordRequest = {
        newPassword: resetPasswordForm.newPassword,
        confirmPassword: resetPasswordForm.confirmPassword,
      };

      await resetAdminUserPassword(selectedUser.userId, request);
      closeResetPasswordForm();
      setNotice({
        tone: 'success',
        message: t('adminUsers.passwordResetSuccessfully'),
      });
    } catch (caughtError) {
      const message = resolveSafeMessage(caughtError, 'Could not reset the password right now.');
      setNotice({ tone: 'danger', message });
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setResetPasswordSubmitting(false);
    }
  };

  if (!canManageUsers) {
    return (
      <AdminLayout
        title={t('adminUsers.pageTitle')}
        description={t('adminUsers.pageDescription')}
        breadcrumbs={[t('adminUsers.breadcrumbDashboard'), t('adminUsers.breadcrumbAdmin'), t('adminUsers.breadcrumbUsers')]}
        operatorLabel={operatorLabel}
        restaurantName={restaurantName}
        branchName={branchName}
        navItems={navItems}
      >
        <EmptyState
          title={t('adminUsers.notAuthorizedTitle')}
          description={t('adminUsers.notAuthorizedDescription')}
          tone="admin"
        />
      </AdminLayout>
    );
  }

  return (
    <AdminLayout
      title={t('adminUsers.pageTitle')}
      description={t('adminUsers.pageDescription')}
      breadcrumbs={[t('adminUsers.breadcrumbDashboard'), t('adminUsers.breadcrumbAdmin'), t('adminUsers.breadcrumbUsers')]}
      operatorLabel={operatorLabel}
      restaurantName={restaurantName}
      branchName={branchName}
      navItems={navItems}
      actions={
        <>
          <Button variant="secondary" onClick={() => void refreshUsers().catch(() => undefined)}>
            {t('adminUsers.refreshList')}
          </Button>
          <Button onClick={openCreateWorkspace}>{t('adminUsers.createUser')}</Button>
        </>
      }
    >
      <div className="preview-sequence">
        <div className="summary-grid">
          <SummaryCard
            label={t('adminUsers.summaryUsers')}
            value={users.length.toString()}
            tone="admin"
            detail={t('adminUsers.summaryUsersDetail')}
          />
          <SummaryCard
            label={t('adminUsers.summaryActive')}
            value={activeCount.toString()}
            tone="accent"
            detail={t('adminUsers.summaryActiveDetail')}
          />
          <SummaryCard
            label={t('adminUsers.summaryInactiveLocked')}
            value={inactiveCount.toString()}
            tone="orders"
            detail={t('adminUsers.summaryInactiveLockedDetail')}
          />
          <SummaryCard
            label={t('adminUsers.summaryRoles')}
            value={roles.length.toString()}
            tone="inventory"
            detail={t('adminUsers.summaryRolesDetail')}
          />
        </div>

        {notice ? (
          <div
            className={['admin-notice', `admin-notice--${notice.tone}`].join(' ')}
            role={notice.tone === 'danger' ? 'alert' : 'status'}
          >
            {notice.message}
          </div>
        ) : null}

        <div className="preview-split preview-split--admin">
          <Card
            title={t('adminUsers.directoryCardTitle')}
            description={t('adminUsers.directoryCardDescription')}
            tone="admin"
            actions={<Badge tone="neutral" label={usersLoading ? t('adminUsers.refreshingBadge') : t('adminUsers.loadedBadge').replace('{n}', String(users.length))} />}
          >
            <UserDirectoryList
              rows={listRows}
              loading={usersLoading}
              error={usersError}
              onRetry={() => void refreshUsers().catch(() => undefined)}
              onEditUser={openEditWorkspace}
            />
          </Card>

          <div className="admin-workspace-stack">
            {workspaceMode === 'create' ? (
              <Card
                title={t('adminUsers.createCardTitle')}
                description={t('adminUsers.createCardDescription')}
                tone="admin"
              >
                {rolesLoading ? (
                  <EmptyState
                    title={t('adminUsers.loadingRoleCatalogTitle')}
                    description={t('adminUsers.loadingRoleCatalogDescription')}
                    tone="admin"
                  />
                ) : hasRolesError ? (
                  <EmptyState
                    title={t('adminUsers.couldNotLoadRolesTitle')}
                    description={rolesError ?? t('adminUsers.couldNotLoadRolesDescription')}
                    tone="admin"
                    actionLabel={t('adminUsers.tryAgain')}
                    onAction={() => void refreshRoles().catch(() => undefined)}
                  />
                ) : (
                  <CreateUserForm
                    form={createForm}
                    errors={createErrors}
                    roles={roles}
                    branchOptions={createBranchOptions}
                    branchHelperText={createBranchHelperText}
                    branchDisabled={createBranchSelectorDisabled}
                    submitting={createSubmitting}
                    onSubmit={handleCreateSubmit}
                    onClear={() => setCreateForm(emptyCreateUserForm())}
                    setForm={setCreateForm}
                    onToggleRole={toggleCreateRole}
                  />
                )}
              </Card>
            ) : selectedUserLoading ? (
              <Card
                title={t('adminUsers.loadingUserCardTitle')}
                description={t('adminUsers.loadingUserCardDescription')}
                tone="admin"
              >
                <EmptyState
                  title={t('adminUsers.loadingUserDetailsTitle')}
                  description={t('adminUsers.loadingUserDetailsDescription')}
                  tone="admin"
                />
              </Card>
            ) : selectedUserError ? (
              <EmptyState
                title={t('adminUsers.couldNotLoadSelectedUserTitle')}
                description={selectedUserError}
                tone="admin"
                actionLabel={t('adminUsers.chooseAnotherUser')}
                onAction={openCreateWorkspace}
              />
            ) : selectedUser ? (
              <div className="admin-workspace-stack">
                <Card
                  title={selectedUserTitle}
                  description={t('adminUsers.profileCardDescription')}
                  tone="admin"
                  actions={
                    <>
                      <Badge tone="neutral" label={selectedUser.status} />
                      {selectedUserIsCurrentSession ? <Badge tone="info" label={t('adminUsers.currentSession')} /> : null}
                      {canResetSelectedUser ? (
                        <Button type="button" variant="secondary" size="sm" onClick={openResetPasswordForm}>
                          {t('adminUsers.resetPassword')}
                        </Button>
                      ) : null}
                    </>
                  }
                >
                  <div className="admin-selected-user">
                    <div className="admin-selected-user__meta">
                      <div className="admin-selected-user__row">
                        <span className="admin-selected-user__label">{t('adminUsers.metaMobile')}</span>
                        <strong>{selectedUser.mobileNumber}</strong>
                      </div>
                      <div className="admin-selected-user__row">
                        <span className="admin-selected-user__label">{t('adminUsers.metaEmail')}</span>
                        <strong>{selectedUser.email || t('adminUsers.notProvided')}</strong>
                      </div>
                      <div className="admin-selected-user__row">
                        <span className="admin-selected-user__label">{t('adminUsers.metaBranch')}</span>
                        <strong>{branchLookup(selectedUser.branchId)}</strong>
                      </div>
                      <div className="admin-selected-user__row">
                        <span className="admin-selected-user__label">{t('adminUsers.metaCreated')}</span>
                        <strong>{selectedUser.createdAt}</strong>
                      </div>
                    </div>
                    <div className="admin-selected-user__roles">
                      {selectedUser.roleNames.map(roleName => (
                        <Badge key={roleName} tone="accent" label={roleName} />
                      ))}
                    </div>
                  </div>
                </Card>

                <Card
                  title={t('adminUsers.editProfileCardTitle')}
                  description={t('adminUsers.editProfileCardDescription')}
                  tone="admin"
                >
                  <EditUserProfileForm
                    form={profileForm}
                    errors={profileErrors}
                    branchOptions={editBranchOptions}
                    branchHelperText={editBranchHelperText}
                    branchDisabled={editBranchSelectorDisabled}
                    submitting={profileSubmitting}
                    onSubmit={handleProfileSubmit}
                    setForm={setProfileForm}
                  />
                </Card>

                <Card
                  title={t('adminUsers.replaceRolesCardTitle')}
                  description={t('adminUsers.replaceRolesCardDescription')}
                  tone="admin"
                >
                  <UserRoleAssignmentPanel
                    roles={roles}
                    selectedRoleNames={selectedRoleNames}
                    onToggleRole={toggleSelectedRole}
                    onSave={() => void handleRoleSubmit()}
                    submitting={roleSubmitting}
                  />
                </Card>

                <Card
                  title={t('adminUsers.activateDeactivateCardTitle')}
                  description={t('adminUsers.activateDeactivateCardDescription')}
                  tone="admin"
                >
                  <UserStatusActions
                    status={selectedUser.status as AdminUserStatus}
                    isCurrentSession={selectedUserIsCurrentSession}
                    confirmDeactivate={confirmDeactivate}
                    submitting={statusSubmitting}
                    onActivate={() => void handleActivate()}
                    onRequestDeactivate={() => setConfirmDeactivate(true)}
                    onConfirmDeactivate={() => void handleDeactivate()}
                    onCancelDeactivate={() => setConfirmDeactivate(false)}
                  />
                </Card>

                {showResetPasswordForm && selectedUser && canResetSelectedUser ? (
                  <ResetUserPasswordForm
                    form={resetPasswordForm}
                    errors={resetPasswordErrors}
                    targetUserName={selectedUser.fullName}
                    targetUserMobile={selectedUser.mobileNumber}
                    roleNames={selectedUser.roleNames}
                    submitting={resetPasswordSubmitting}
                    onSubmit={handleResetPasswordSubmit}
                    onCancel={closeResetPasswordForm}
                    setForm={setResetPasswordForm}
                  />
                ) : null}
              </div>
            ) : (
              <EmptyState
                title={t('adminUsers.chooseUserTitle')}
                description={t('adminUsers.chooseUserDescription')}
                tone="admin"
                actionLabel={t('adminUsers.createUser')}
                onAction={openCreateWorkspace}
              />
            )}
          </div>
        </div>
      </div>
    </AdminLayout>
  );
};

export default AdminUsersPage;
