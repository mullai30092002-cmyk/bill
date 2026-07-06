import { useCallback, useEffect, useMemo, useState } from 'react';

import { isApiError } from '../../api/apiErrors';
import { OrderManagementLayout } from '../../components/layout';
import type { ShellNavItem } from '../../components/layout/navigation';
import { Badge, Button, Card, EmptyState, Input } from '../../components/ui';
import { useAuth } from '../auth/useAuth';
import { useLanguage } from '../../i18n/LanguageProvider';
import type { TranslationFunction } from '../../i18n/LanguageProvider';
import { useRestaurantCurrency } from '../auth/useRestaurantCurrency';
import { listAdminBranches, listMenuCategories, listMenuItems } from '../admin/adminApi';
import type { AdminBranchListItem, MenuCategory, MenuItem } from '../admin/adminTypes';
import { sortBranches } from '../admin/branches/branchDisplay';
import {
  cancelPosOrder,
  confirmPosOrder,
  createPosOrder,
  getPosOrder,
  listPosOrders,
  updatePosOrder,
} from './posApi';
import PosBranchSelector, { type PosBranchSelectorOption } from './PosBranchSelector';
import PosMenuBrowser from './PosMenuBrowser';
import PosOrderActions from './PosOrderActions';
import PosOrderCart from './PosOrderCart';
import PosOrderListPanel from './PosOrderListPanel';
import PosOrderSummary from './PosOrderSummary';
import PosOrderTypeToggle from './PosOrderTypeToggle';
import {
  buildCancelPosOrderRequest,
  buildCreatePosOrderRequest,
  buildPosOrderDraftFromDetail,
  buildPosOrderDraftValidation,
  buildUpdatePosOrderRequest,
  createDraftLineFromMenuItem,
  emptyPosOrderDraftForm,
  getDraftLineQuantityError,
  type PosOrderDraftForm,
  type PosOrderDraftLineForm,
  type PosOrderLineErrors,
} from './posOrderFormValidation';
import {
  buildEstimatedTotals,
  canAddMenuItemToOrderType,
  formatPosCurrency,
  sortPosCategories,
  sortPosItems,
  sortPosLines,
} from './posDisplay';
import { getSafePosErrorMessage } from './posErrorDisplay';
import type { PosOrderDetail, PosOrderListItem, PosOrderStatus } from './posTypes';

export interface PosOrderCapturePageProps {
  navItems?: ShellNavItem[];
  restaurantName?: string;
  branchName?: string;
  operatorLabel?: string;
}

type NoticeTone = 'success' | 'info' | 'warning' | 'danger';

interface Notice {
  tone: NoticeTone;
  message: string;
}

const buildCategoryLookup = (categories: MenuCategory[]) =>
  new Map(categories.map(category => [category.menuCategoryId, category] as const));

const buildItemLookup = (items: MenuItem[]) =>
  new Map(items.map(item => [item.menuItemId, item] as const));

const buildBranchLookup = (branches: AdminBranchListItem[]) =>
  new Map(branches.map(branch => [branch.branchId, branch] as const));

const statusKey = (value: PosOrderStatus) => {
  if (value === 'Confirmed') {
    return 'pos.statusConfirmed';
  }

  if (value === 'Cancelled') {
    return 'pos.statusCancelled';
  }

  return 'pos.statusDraft';
};

const buildDraftLineErrors = (lines: PosOrderDraftLineForm[], t: TranslationFunction): PosOrderLineErrors => {
  const errors: PosOrderLineErrors = {};

  for (const line of lines) {
    const quantityError = getDraftLineQuantityError(line, t);
    if (quantityError) {
      errors[line.draftLineId] = {
        ...(errors[line.draftLineId] ?? {}),
        quantity: quantityError,
      };
    }
  }

  return errors;
};

export const PosOrderCapturePage = ({ navItems, restaurantName, branchName, operatorLabel }: PosOrderCapturePageProps) => {
  const auth = useAuth();
  const { t } = useLanguage();
  const { currencyCode, locale } = useRestaurantCurrency();
  const canCreate = auth.hasPermission('Order.Create');
  const canView = auth.hasPermission('Order.View');
  const canCancel = canCreate || auth.hasPermission('Order.Cancel');
  const canAccess = canCreate || canView;

  const [branches, setBranches] = useState<AdminBranchListItem[]>([]);
  const [branchesLoading, setBranchesLoading] = useState(canAccess);
  const [branchesError, setBranchesError] = useState<string | null>(null);
  const [categories, setCategories] = useState<MenuCategory[]>([]);
  const [categoriesLoading, setCategoriesLoading] = useState(canAccess);
  const [categoriesError, setCategoriesError] = useState<string | null>(null);
  const [items, setItems] = useState<MenuItem[]>([]);
  const [itemsLoading, setItemsLoading] = useState(canAccess);
  const [itemsError, setItemsError] = useState<string | null>(null);
  const [recentOrders, setRecentOrders] = useState<PosOrderListItem[]>([]);
  const [recentOrdersLoading, setRecentOrdersLoading] = useState(canAccess);
  const [recentOrdersError, setRecentOrdersError] = useState<string | null>(null);
  const [selectedOrderId, setSelectedOrderId] = useState<string | null>(null);
  const [selectedOrder, setSelectedOrder] = useState<PosOrderDetail | null>(null);
  const [selectedOrderLoading, setSelectedOrderLoading] = useState(false);
  const [selectedOrderError, setSelectedOrderError] = useState<string | null>(null);
  const [draftOrderDetail, setDraftOrderDetail] = useState<PosOrderDetail | null>(null);
  const [draftForm, setDraftForm] = useState<PosOrderDraftForm>(() => emptyPosOrderDraftForm());
  const [selectedCategoryId, setSelectedCategoryId] = useState('');
  const [notice, setNotice] = useState<Notice | null>(null);
  const [draftSubmitting, setDraftSubmitting] = useState(false);
  const [selectedActionSubmitting, setSelectedActionSubmitting] = useState(false);
  const [cancelReason, setCancelReason] = useState('');
  const [cancelAttempted, setCancelAttempted] = useState(false);
  const [draftLineErrors, setDraftLineErrors] = useState<PosOrderLineErrors>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [clearDraftPending, setClearDraftPending] = useState(false);

  const activeBranches = useMemo(() => sortBranches(branches.filter(branch => branch.status === 'Active')), [branches]);
  const activeCategories = useMemo(() => sortPosCategories(categories).filter(category => category.status === 'Active'), [categories]);
  const activeItems = useMemo(() => sortPosItems(items).filter(item => item.status === 'Active'), [items]);
  const branchLookup = useMemo(() => buildBranchLookup(branches), [branches]);
  const categoryLookup = useMemo(() => buildCategoryLookup(categories), [categories]);
  const itemLookup = useMemo(() => buildItemLookup(items), [items]);
  const visibleItems = useMemo(
    () => activeItems.filter(item => !selectedCategoryId || item.menuCategoryId === selectedCategoryId),
    [activeItems, selectedCategoryId]
  );
  const estimatedTotals = useMemo(() => buildEstimatedTotals(draftForm.lines), [draftForm.lines]);
  const selectedOrderLines = useMemo(() => sortPosLines(selectedOrder?.lines ?? []), [selectedOrder]);
  const canEditDraft = canCreate && (!draftOrderDetail || draftOrderDetail.status === 'Draft');
  const canEditBranch = canCreate && !draftOrderDetail;
  const estimatedDraftTotals = estimatedTotals;
  const selectedOrderBranchName = selectedOrder ? branchLookup.get(selectedOrder.branchId)?.name ?? t('pos.unknownBranch') : undefined;

  const branchOptions = useMemo<PosBranchSelectorOption[]>(() => {
    const options: PosBranchSelectorOption[] = activeBranches.map(branch => ({
      value: branch.branchId,
      label: branch.name,
    }));

    if (draftForm.branchId && !activeBranches.some(branch => branch.branchId === draftForm.branchId)) {
      const selectedBranch = branchLookup.get(draftForm.branchId);
      if (selectedBranch) {
        options.push({
          value: selectedBranch.branchId,
          label: `${selectedBranch.name} (${t('pos.inactiveSuffix')})`,
          disabled: true,
        });
      }
    }

    return options;
  }, [activeBranches, branchLookup, draftForm.branchId, t]);

  const refreshBranches = useCallback(async () => {
    if (!canAccess) {
      return;
    }

    setBranchesLoading(true);
    setBranchesError(null);

    try {
      const response = await listAdminBranches();
      setBranches(sortBranches(response.items));
    } catch (caughtError) {
      setBranchesError(getSafePosErrorMessage(caughtError, t('pos.errorLoadBranches')));
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setBranchesLoading(false);
    }
  }, [auth, canAccess, t]);

  const refreshCategories = useCallback(async () => {
    if (!canAccess) {
      return;
    }

    setCategoriesLoading(true);
    setCategoriesError(null);

    try {
      const response = await listMenuCategories();
      setCategories(response.items);
    } catch (caughtError) {
      setCategoriesError(getSafePosErrorMessage(caughtError, t('pos.errorLoadMenu')));
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setCategoriesLoading(false);
    }
  }, [auth, canAccess, t]);

  const refreshItems = useCallback(async () => {
    if (!canAccess) {
      return;
    }

    setItemsLoading(true);
    setItemsError(null);

    try {
      const response = await listMenuItems();
      setItems(response.items);
    } catch (caughtError) {
      setItemsError(getSafePosErrorMessage(caughtError, t('pos.errorLoadMenu')));
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setItemsLoading(false);
    }
  }, [auth, canAccess, t]);

  const refreshRecentOrders = useCallback(async () => {
    if (!canAccess) {
      return;
    }

    setRecentOrdersLoading(true);
    setRecentOrdersError(null);

    try {
      const response = await listPosOrders();
      setRecentOrders([...response.items].sort((left, right) => {
        const createdDelta = Date.parse(right.createdAt) - Date.parse(left.createdAt);
        if (createdDelta !== 0) {
          return createdDelta;
        }

        return right.orderNumber.localeCompare(left.orderNumber, undefined, { sensitivity: 'base' });
      }));
    } catch (caughtError) {
      setRecentOrdersError(getSafePosErrorMessage(caughtError, t('pos.errorLoadRecentOrders')));
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setRecentOrdersLoading(false);
    }
  }, [auth, canAccess, t]);

  const loadOrderDetail = useCallback(
    async (orderId: string) => {
      setSelectedOrderId(orderId);
      setSelectedOrder(null);
      setSelectedOrderError(null);
      setSelectedOrderLoading(true);
      setCancelReason('');
      setCancelAttempted(false);
      setNotice(null);

      try {
        const detail = await getPosOrder(orderId);
        setSelectedOrder(detail);
      } catch (caughtError) {
        setSelectedOrderError(getSafePosErrorMessage(caughtError, t('pos.errorLoadSelectedOrder')));
        if (isApiError(caughtError) && caughtError.status === 401) {
          void auth.logout();
        }
      } finally {
        setSelectedOrderLoading(false);
      }
    },
    [auth, t]
  );

  useEffect(() => {
    if (!canAccess) {
      return;
    }

    void refreshBranches();
    void refreshCategories();
    void refreshItems();
    void refreshRecentOrders();
  }, [canAccess, refreshBranches, refreshCategories, refreshItems, refreshRecentOrders]);

  useEffect(() => {
    if (canEditBranch && !draftForm.branchId && activeBranches.length === 1) {
      setDraftForm(previous => ({ ...previous, branchId: activeBranches[0].branchId }));
    }
  }, [activeBranches, canEditBranch, draftForm.branchId]);

  useEffect(() => {
    if (selectedCategoryId && !activeCategories.some(category => category.menuCategoryId === selectedCategoryId)) {
      setSelectedCategoryId('');
    }
  }, [activeCategories, selectedCategoryId]);

  const resetDraft = useCallback(() => {
    setDraftOrderDetail(null);
    setDraftForm(emptyPosOrderDraftForm(draftForm.orderType));
    setDraftLineErrors({});
    setFormError(null);
    setCancelReason('');
    setCancelAttempted(false);
    setClearDraftPending(false);
    setNotice({
      tone: 'info',
      message: t('pos.noticeDraftCleared'),
    });
  }, [draftForm.orderType, t]);

  const handleClearDraft = useCallback(() => {
    if (draftForm.lines.length === 0) {
      resetDraft();
    } else {
      setClearDraftPending(true);
    }
  }, [draftForm.lines.length, resetDraft]);

  const loadSelectedOrderIntoDraft = useCallback(() => {
    if (!selectedOrder || selectedOrder.status !== 'Draft' || !canCreate) {
      return;
    }

    setDraftOrderDetail(selectedOrder);
    setDraftForm(buildPosOrderDraftFromDetail(selectedOrder, items, categories));
    setDraftLineErrors({});
    setFormError(null);
    setCancelReason('');
    setCancelAttempted(false);
    setNotice({
      tone: 'info',
      message: t('pos.noticeLoadedIntoDraft', { orderNumber: selectedOrder.orderNumber }),
    });
  }, [canCreate, categories, items, selectedOrder, t]);

  const addMenuItemToDraft = useCallback(
    (menuItemId: string) => {
      if (!canEditDraft) {
        return;
      }

      const menuItem = itemLookup.get(menuItemId);
      if (!menuItem || !canAddMenuItemToOrderType(menuItem, draftForm.orderType)) {
        setNotice({
          tone: 'warning',
          message: t('pos.noticeItemCannotBeAdded'),
        });
        return;
      }

      const category = categoryLookup.get(menuItem.menuCategoryId);
      if (!category) {
        setNotice({
          tone: 'warning',
          message: t('pos.noticeNoActiveCategory'),
        });
        return;
      }

      setDraftForm(previous => ({
        ...previous,
        lines: [...previous.lines, createDraftLineFromMenuItem(menuItem, category.name)],
      }));
      setNotice({
        tone: 'success',
        message: t('pos.noticeItemAdded', { itemName: menuItem.name }),
      });
    },
    [canEditDraft, categoryLookup, draftForm.orderType, itemLookup, t]
  );

  const saveDraft = useCallback(async () => {
    if (!canCreate) {
      return;
    }

    const validation = buildPosOrderDraftValidation(draftForm, draftOrderDetail?.status ?? null, canCreate, t);
    const allLineErrors = buildDraftLineErrors(draftForm.lines, t);
    const mergedLineErrors: PosOrderLineErrors = {
      ...validation.lineErrors,
      ...allLineErrors,
    };
    setDraftLineErrors(mergedLineErrors);
    setFormError(validation.formErrors.state ?? validation.formErrors.branchId ?? validation.formErrors.lines ?? null);

    if (validation.formErrors.state || validation.formErrors.branchId || validation.formErrors.lines || Object.keys(mergedLineErrors).length > 0) {
      setNotice({
        tone: 'warning',
        message: validation.formErrors.state || validation.formErrors.branchId || validation.formErrors.lines || t('pos.noticeReviewDraft'),
      });
      return;
    }

    setDraftSubmitting(true);
    setNotice(null);

    try {
      const saved =
        draftOrderDetail?.status === 'Draft' && draftOrderDetail.posOrderId
          ? await updatePosOrder(draftOrderDetail.posOrderId, buildUpdatePosOrderRequest(draftForm))
          : await createPosOrder(buildCreatePosOrderRequest(draftForm));

      setDraftOrderDetail(saved);
      setSelectedOrderId(saved.posOrderId);
      setSelectedOrder(saved);
      setDraftForm(buildPosOrderDraftFromDetail(saved, items, categories));
      setDraftLineErrors({});
      setFormError(null);
      setCancelReason('');
      await refreshRecentOrders();
      setNotice({
        tone: 'success',
        message: t('pos.noticeSavedOrder', {
          action: draftOrderDetail?.status === 'Draft' ? t('pos.updatedAction') : t('pos.createdAction'),
          orderNumber: saved.orderNumber,
        }),
      });
    } catch (caughtError) {
      setNotice({
        tone: 'danger',
        message: getSafePosErrorMessage(caughtError, t('pos.errorSaveOrder')),
      });
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setDraftSubmitting(false);
    }
  }, [auth, canCreate, categories, draftForm, draftOrderDetail, items, refreshRecentOrders, t]);

  const confirmSelectedOrder = useCallback(async () => {
    if (!selectedOrder || !canCreate || selectedOrder.status !== 'Draft') {
      return;
    }

    setSelectedActionSubmitting(true);
    setNotice(null);

    try {
      const confirmed = await confirmPosOrder(selectedOrder.posOrderId);
      setSelectedOrder(confirmed);
      if (draftOrderDetail?.posOrderId === confirmed.posOrderId) {
        setDraftOrderDetail(confirmed);
        setDraftForm(buildPosOrderDraftFromDetail(confirmed, items, categories));
      }
      await refreshRecentOrders();
      setNotice({
        tone: 'success',
        message: t('pos.noticeOrderConfirmed', {
          ticketLabel: confirmed.kitchenTicketNumber
            ? t('pos.kitchenTicketAs', { ticketNumber: confirmed.kitchenTicketNumber })
            : '',
        }),
      });
    } catch (caughtError) {
      setNotice({
        tone: 'danger',
        message: getSafePosErrorMessage(caughtError, t('pos.errorConfirmOrder')),
      });
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setSelectedActionSubmitting(false);
    }
  }, [auth, canCreate, categories, draftOrderDetail?.posOrderId, items, refreshRecentOrders, selectedOrder, t]);

  const cancelSelectedOrder = useCallback(async () => {
    if (!selectedOrder || !canCancel || (selectedOrder.status !== 'Draft' && selectedOrder.status !== 'Confirmed')) {
      return;
    }

    setCancelAttempted(true);
    const reason = cancelReason.trim();
    if (!reason) {
      setNotice({
        tone: 'warning',
        message: t('pos.noticeEnterCancelReason'),
      });
      return;
    }

    setSelectedActionSubmitting(true);
    setNotice(null);

    try {
      const cancelled = await cancelPosOrder(selectedOrder.posOrderId, buildCancelPosOrderRequest(reason));
      setSelectedOrder(cancelled);
      if (draftOrderDetail?.posOrderId === cancelled.posOrderId) {
        setDraftOrderDetail(cancelled);
        setDraftForm(buildPosOrderDraftFromDetail(cancelled, items, categories));
      }
      setCancelReason('');
      await refreshRecentOrders();
      setNotice({
        tone: 'success',
        message: t('pos.noticeOrderCancelled', { orderNumber: cancelled.orderNumber }),
      });
    } catch (caughtError) {
      setNotice({
        tone: 'danger',
        message: getSafePosErrorMessage(caughtError, t('pos.errorCancelOrder')),
      });
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setSelectedActionSubmitting(false);
    }
  }, [auth, cancelReason, canCancel, categories, draftOrderDetail?.posOrderId, items, refreshRecentOrders, selectedOrder, t]);

  if (!canAccess) {
    return (
      <OrderManagementLayout
        title={t('pos.pageTitle')}
        description={t('pos.pageDescriptionReadOnly')}
        breadcrumbs={[t('nav.dashboard'), t('nav.orders')]}
        operatorLabel={operatorLabel}
        restaurantName={restaurantName}
        branchName={branchName}
        navItems={navItems}
      >
        <EmptyState
          title={t('pos.notAuthorizedTitle')}
          description={t('pos.notAuthorizedDescription')}
          tone="orders"
        />
      </OrderManagementLayout>
    );
  }

  const currentDraftTitle = draftOrderDetail?.status === 'Draft' ? t('pos.updateDraft') : t('pos.createDraft');

  return (
    <OrderManagementLayout
      title={t('pos.pageTitle')}
      description={t('pos.pageDescription')}
      breadcrumbs={[t('nav.dashboard'), t('nav.orders')]}
      operatorLabel={operatorLabel}
      restaurantName={restaurantName}
      branchName={branchName}
      navItems={navItems}
    >
      <div className="pos-workspace">

        {/* ── Context bar ── */}
        <div className="pos-context-bar">
          {canCreate ? (
            <>
              <div className="pos-context-bar__branch">
                <PosBranchSelector
                  options={branchOptions}
                  value={draftForm.branchId}
                  canEdit={canEditBranch}
                  error={draftOrderDetail?.status ? undefined : !draftForm.branchId ? t('pos.branchRequired') : undefined}
                  helperText={
                    canEditBranch
                      ? activeBranches.length === 1
                        ? t('pos.branchSingleAuto')
                        : t('pos.branchNewDraftHelp')
                      : t('pos.branchLockedHelp')
                  }
                  onChange={value => setDraftForm(previous => ({ ...previous, branchId: value }))}
                />
              </div>

              <div className="pos-context-bar__type">
                <span className="pos-context-bar__type-label">{t('pos.orderType')}</span>
                <PosOrderTypeToggle
                  value={draftForm.orderType}
                  disabled={!canEditDraft}
                  onChange={value => setDraftForm(previous => ({ ...previous, orderType: value }))}
                />
              </div>

              {draftForm.orderType === 'EatIn' ? (
                <div className="pos-context-bar__table">
                  <Input
                    label={t('pos.tableName')}
                    placeholder={t('pos.tablePlaceholder')}
                    value={draftForm.tableName}
                    disabled={!canEditDraft}
                    onChange={event => setDraftForm(previous => ({ ...previous, tableName: event.target.value }))}
                  />
                  {canEditDraft && !draftForm.tableName.trim() ? (
                    <p className="pos-context-bar__table-warn" role="status" aria-live="polite">
                      {t('pos.confirmOrderNoTableWarning')}
                    </p>
                  ) : null}
                </div>
              ) : null}

              <div className="pos-context-bar__actions">
                <Button variant="secondary" size="sm" onClick={resetDraft} disabled={draftSubmitting}>
                  {t('pos.newDraft')}
                </Button>
                <Button variant="secondary" size="sm" onClick={() => void refreshRecentOrders()}>
                  {t('pos.refreshOrders')}
                </Button>
              </div>
            </>
          ) : (
            <div className="pos-context-bar__actions">
              <Button variant="secondary" size="sm" onClick={() => void refreshRecentOrders()}>
                {t('pos.refreshOrders')}
              </Button>
            </div>
          )}
        </div>

        {/* ── Notice banner ── */}
        {notice ? (
          <div
            className={['pos-notice', `pos-notice--${notice.tone}`].join(' ')}
            role={notice.tone === 'danger' ? 'alert' : 'status'}
          >
            {notice.message}
          </div>
        ) : null}

        {/* ── Workspace body ── */}
        <div className="pos-workspace__body">

          {/* LEFT: menu browser + draft details */}
          <div className="pos-workspace__menu">
            {canCreate ? (
              <>
                <PosMenuBrowser
                  categories={activeCategories}
                  items={visibleItems}
                  selectedCategoryId={selectedCategoryId}
                  selectedOrderType={draftForm.orderType}
                  canCreate={canEditDraft}
                  onCategorySelect={setSelectedCategoryId}
                  onAddItem={addMenuItemToDraft}
                />

                {/* Draft details — collapsed by default to not interrupt item picking.
                    For EatIn, table name lives in the context bar so it is hidden here. */}
                <details className="pos-draft-details">
                  <summary className="pos-draft-details__summary">
                    {draftForm.orderType === 'EatIn' ? t('pos.detailsSummaryEatIn') : t('pos.detailsSummaryParcel')}
                    {(
                      (draftForm.orderType !== 'EatIn' && draftForm.tableName) ||
                      draftForm.customerName ||
                      draftForm.customerMobile ||
                      draftForm.notes
                    ) ? (
                      <span className="pos-draft-details__filled" aria-label={t('pos.detailsFilled')}>●</span>
                    ) : null}
                  </summary>
                  <div className="pos-draft-details__body">
                    <p className="pos-draft-details__hint">
                      {t('pos.detailsHint')}
                    </p>
                    <div className="admin-form-grid">
                      {draftForm.orderType !== 'EatIn' ? (
                        <Input
                          label={t('pos.tableName')}
                          placeholder={t('pos.tablePlaceholder')}
                          value={draftForm.tableName}
                          disabled={!canEditDraft}
                          onChange={event => setDraftForm(previous => ({ ...previous, tableName: event.target.value }))}
                        />
                      ) : null}
                      <Input
                        label={t('pos.customerName')}
                        placeholder={t('pos.customerNamePlaceholder')}
                        value={draftForm.customerName}
                        disabled={!canEditDraft}
                        onChange={event => setDraftForm(previous => ({ ...previous, customerName: event.target.value }))}
                      />
                      <Input
                        label={t('pos.customerMobile')}
                        placeholder={t('pos.customerMobilePlaceholder')}
                        value={draftForm.customerMobile}
                        disabled={!canEditDraft}
                        onChange={event => setDraftForm(previous => ({ ...previous, customerMobile: event.target.value }))}
                      />
                      <Input
                        label={t('pos.orderNotes')}
                        placeholder={t('pos.orderNotesPlaceholder')}
                        value={draftForm.notes}
                        disabled={!canEditDraft}
                        onChange={event => setDraftForm(previous => ({ ...previous, notes: event.target.value }))}
                      />
                    </div>
                  </div>
                </details>
              </>
            ) : (
              <EmptyState
                title={t('pos.readOnlyModeTitle')}
                description={t('pos.readOnlyModeDescription')}
                tone="orders"
              />
            )}
          </div>

          {/* RIGHT: sticky active order workflow ONLY — cart, compact totals, draft actions */}
          <div className="pos-workspace__cart">

            {canCreate ? (
              <>
                {/* Cart — always visible while selecting items */}
                <PosOrderCart
                  lines={draftForm.lines}
                  orderType={draftForm.orderType}
                  canEdit={canEditDraft}
                  lineErrors={draftLineErrors}
                  onChangeQuantity={(draftLineId, value) =>
                    setDraftForm(previous => ({
                      ...previous,
                      lines: previous.lines.map(line => (line.draftLineId === draftLineId ? { ...line, quantity: value } : line)),
                    }))
                  }
                  onChangeNotes={(draftLineId, value) =>
                    setDraftForm(previous => ({
                      ...previous,
                      lines: previous.lines.map(line => (line.draftLineId === draftLineId ? { ...line, notes: value } : line)),
                    }))
                  }
                  onRemoveLine={draftLineId =>
                    setDraftForm(previous => ({
                      ...previous,
                      lines: previous.lines.filter(line => line.draftLineId !== draftLineId),
                    }))
                  }
                />

                {/* Compact totals — one bar, not four large cards */}
                <PosOrderSummary
                  lineCount={draftForm.lines.length}
                  estimatedSubtotal={estimatedDraftTotals.subtotal}
                  estimatedTaxTotal={estimatedDraftTotals.taxTotal}
                  estimatedGrandTotal={estimatedDraftTotals.grandTotal}
                  savedOrder={draftOrderDetail}
                />

                {/* Primary draft action — always close to cart */}
                <div className="pos-draft-actions">
                  {formError ? <div className="pos-draft-actions__error">{formError}</div> : null}
                  <Button
                    fullWidth
                    onClick={() => void saveDraft()}
                    disabled={!canEditDraft || draftSubmitting || draftForm.lines.length === 0}
                  >
                    {currentDraftTitle}
                  </Button>
                  {draftOrderDetail?.status === 'Draft' && canCreate ? (
                    <Button
                      fullWidth
                      onClick={() => void confirmSelectedOrder()}
                      disabled={draftSubmitting || selectedActionSubmitting}
                    >
                      {t('pos.confirmAndSendToKitchen')}
                    </Button>
                  ) : null}
                  {clearDraftPending ? (
                    <div className="pos-clear-draft-confirm">
                      <p className="pos-clear-draft-confirm__message">
                        {t('pos.clearDraftConfirmPrompt', {
                          lineCount: draftForm.lines.length,
                          suffix: draftForm.lines.length !== 1 ? 's' : '',
                        })}
                      </p>
                      <div className="pos-clear-draft-confirm__actions">
                        <Button variant="danger" size="sm" onClick={resetDraft} disabled={draftSubmitting}>
                          {t('pos.clearDraftConfirmYes')}
                        </Button>
                        <Button variant="secondary" size="sm" onClick={() => setClearDraftPending(false)} disabled={draftSubmitting}>
                          {t('pos.clearDraftConfirmNo')}
                        </Button>
                      </div>
                    </div>
                  ) : (
                    <Button
                      variant="secondary"
                      fullWidth
                      onClick={handleClearDraft}
                      disabled={draftSubmitting}
                    >
                      {t('pos.clearDraft')}
                    </Button>
                  )}
                  {!canEditDraft && draftOrderDetail ? (
                    <p className="pos-draft-actions__note">
                      {t('pos.lockedOrderNote', { status: t(statusKey(draftOrderDetail.status)) })}
                    </p>
                  ) : null}
                  <p className="pos-draft-actions__note">
                    {t('pos.backendTotalsTruth')}
                  </p>
                </div>
              </>
            ) : null}

          </div>
        </div>

        {/* ── Lower section: recent orders (left) + selected order (right) ── */}
        <div className="pos-lower-section">

          {/* Recent orders — full-width left column on desktop */}
          <div className="pos-lower-section__orders">
            <Card
              title={t('pos.recentOrdersTitle')}
              description={t('pos.recentOrdersDescription')}
              tone="orders"
              actions={<Badge tone="neutral" label={recentOrdersLoading ? t('pos.refreshing') : t('pos.loadedCount', { count: recentOrders.length })} />}
            >
              <PosOrderListPanel
                orders={recentOrders}
                loading={recentOrdersLoading}
                error={recentOrdersError}
                selectedOrderId={selectedOrderId}
                onRetry={() => void refreshRecentOrders()}
                onSelectOrder={orderId => void loadOrderDetail(orderId)}
              />
            </Card>
          </div>

          {/* Selected order — review panel to the right of recent orders */}
          <div className="pos-lower-section__detail" data-testid="pos-selected-order-panel">
            <PosOrderActions
              order={selectedOrder}
              loading={selectedOrderLoading || selectedActionSubmitting}
              error={selectedOrderError}
              branchName={selectedOrderBranchName}
              canCreate={canCreate}
              canCancel={canCancel}
              cancelReason={cancelReason}
              cancelReasonError={cancelAttempted && canCancel && selectedOrder && !cancelReason.trim() ? t('pos.cancelReasonRequired') : undefined}
              onRetry={selectedOrderId ? () => void loadOrderDetail(selectedOrderId) : undefined}
              onLoadIntoDraft={loadSelectedOrderIntoDraft}
              onConfirm={() => void confirmSelectedOrder()}
              onCancelReasonChange={setCancelReason}
              onCancel={() => void cancelSelectedOrder()}
            />

            {selectedOrder?.lines?.length ? (
              <Card
                title={t('pos.selectedLinesTitle', { orderNumber: selectedOrder.orderNumber })}
                description={t('pos.lineSnapshotsDescription')}
                tone="orders"
              >
                <div className="order-line-list">
                  {selectedOrderLines.map(line => (
                    <div key={line.posOrderLineId} className="order-line">
                      <div className="order-line__main">
                        <strong>{line.menuItemNameSnapshot}</strong>
                        <span>
                          {line.quantity} x {line.menuCategoryNameSnapshot}
                        </span>
                        {line.skuSnapshot ? <span>{t('pos.skuLabel')}: {line.skuSnapshot}</span> : null}
                        {line.notes ? <span>{t('pos.notesLabel')}: {line.notes}</span> : null}
                      </div>
                      <strong>{formatPosCurrency(line.lineTotal, currencyCode, locale)}</strong>
                    </div>
                  ))}
                </div>
                <div className="admin-form-note">
                  {t('pos.snapshotTotalsLocked')}
                </div>
              </Card>
            ) : null}
          </div>

        </div>
      </div>
    </OrderManagementLayout>
  );
};

export default PosOrderCapturePage;
