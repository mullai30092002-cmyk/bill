import { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import { isApiError } from '../../api/apiErrors';
import { OrderManagementLayout } from '../../components/layout';
import type { ShellNavItem } from '../../components/layout/navigation';
import { Badge, Card, EmptyState, SummaryCard } from '../../components/ui';
import { useAuth } from '../auth/useAuth';
import { useLanguage } from '../../i18n/LanguageProvider';
import {
  cancelKitchenTicket,
  getKitchenTicket,
  getKitchenTicketDeductionPreview,
  listKitchenTickets,
  updateKitchenTicketStatus,
} from './kitchenTicketApi';
import KitchenTicketDetailPanel from './KitchenTicketDetailPanel';
import KitchenTicketDeductionPreviewPanel from './KitchenTicketDeductionPreviewPanel';
import KitchenTicketFilters from './KitchenTicketFilters';
import KitchenTicketListPanel from './KitchenTicketListPanel';
import KitchenTicketStatusActions from './KitchenTicketStatusActions';
import { getSafeKitchenTicketErrorMessage } from './kitchenTicketErrorDisplay';
import {
  filterKitchenTickets,
  formatKitchenTicketAge,
  formatKitchenTicketStatus,
  isActiveKitchenTicket,
  sortKitchenTickets,
} from './kitchenTicketDisplay';
import {
  buildKitchenTicketCancelRequest,
  buildKitchenTicketStatusRequest,
  getKitchenTicketCancelReasonError,
} from './kitchenTicketValidation';
import type {
  KitchenTicketDetail,
  KitchenTicketDeductionPreviewResponse,
  KitchenTicketListItem,
  KitchenTicketQueueFilter,
  KitchenTicketStatus,
} from './kitchenTicketTypes';

export interface KitchenTicketsPageProps {
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

const POLL_INTERVAL_MS = 30_000;
const activeQueueFilter: KitchenTicketQueueFilter = 'Active';

const queueSummaryLabel = (tickets: KitchenTicketListItem[], activeTicketLabel: string) => {
  const activeTickets = tickets.filter(ticket => isActiveKitchenTicket(ticket.status)).length;
  return activeTicketLabel.replace('{count}', String(activeTickets));
};

const formatLastRefreshed = (date: Date | null, notYetRefreshed: string, ageLabel: string) => {
  if (!date) {
    return notYetRefreshed;
  }
  return ageLabel;
};

export const KitchenTicketsPage = ({ navItems, restaurantName, branchName, operatorLabel }: KitchenTicketsPageProps) => {
  const auth = useAuth();
  const { t } = useLanguage();
  const canView = auth.hasPermission('KitchenTicket.View');
  const canUpdateStatus = auth.hasPermission('KitchenTicket.UpdateStatus') || auth.hasPermission('KitchenTicket.Manage');
  const canManage = auth.hasPermission('KitchenTicket.Manage');
  const canAccess = canView || canUpdateStatus || canManage;

  const kitchenMessages = useMemo(
    () => ({
      statusPending: t('kitchen.statusPending'),
      statusPreparing: t('kitchen.statusPreparing'),
      statusReady: t('kitchen.statusReady'),
      statusServed: t('kitchen.statusServed'),
      statusCancelled: t('kitchen.statusCancelled'),
      orderTypeEatIn: t('kitchen.orderTypeEatIn'),
      orderTypeParcel: t('kitchen.orderTypeParcel'),
      notAvailable: t('kitchen.notAvailable'),
      notRecorded: t('kitchen.notRecorded'),
      activeQueue: t('kitchen.activeQueue'),
      allTickets: t('kitchen.allTickets'),
      createdPrefix: t('kitchen.createdPrefix'),
      updatedPrefix: t('kitchen.updatedPrefix'),
      ageUnavailable: t('kitchen.ageUnavailable'),
      ageMinutesOld: t('kitchen.ageMinutesOld'),
      ageHoursOld: t('kitchen.ageHoursOld'),
      ageHoursMinutesOld: t('kitchen.ageHoursMinutesOld'),
      ageDaysOld: t('kitchen.ageDaysOld'),
      ageDaysHoursOld: t('kitchen.ageDaysHoursOld'),
      ageDaysHoursMinutesOld: t('kitchen.ageDaysHoursMinutesOld'),
      sufficient: t('kitchen.statusSufficient'),
      insufficient: t('kitchen.statusInsufficient'),
      noRecipe: t('kitchen.statusNoRecipe'),
    }),
    [t]
  );
  const errorMessages = useMemo(
    () => ({
      sessionExpired: t('kitchen.sessionExpired'),
      unauthorized: t('kitchen.unauthorized'),
    }),
    [t]
  );
  const validationMessages = useMemo(
    () => ({
      cancelReasonRequired: t('kitchen.cancelReasonRequired'),
    }),
    [t]
  );

  const [tickets, setTickets] = useState<KitchenTicketListItem[]>([]);
  const [ticketsLoading, setTicketsLoading] = useState(canAccess);
  const [ticketsError, setTicketsError] = useState<string | null>(null);
  const [lastRefreshedAt, setLastRefreshedAt] = useState<Date | null>(null);
  const [lastRefreshedLabel, setLastRefreshedLabel] = useState<string>(t('kitchen.lastRefreshedNotYet'));
  const [selectedFilter, setSelectedFilter] = useState<KitchenTicketQueueFilter>(activeQueueFilter);
  const [selectedTicketId, setSelectedTicketId] = useState<string | null>(null);
  const [selectedTicket, setSelectedTicket] = useState<KitchenTicketDetail | null>(null);
  const [selectedTicketLoading, setSelectedTicketLoading] = useState(false);
  const [selectedTicketError, setSelectedTicketError] = useState<string | null>(null);
  const [deductionPreview, setDeductionPreview] = useState<KitchenTicketDeductionPreviewResponse | null>(null);
  const [deductionPreviewLoading, setDeductionPreviewLoading] = useState(false);
  const [deductionPreviewError, setDeductionPreviewError] = useState<string | null>(null);
  const [notice, setNotice] = useState<Notice | null>(null);
  const [statusSubmitting, setStatusSubmitting] = useState(false);
  const [cancelReason, setCancelReason] = useState('');
  const [cancelConfirmPending, setCancelConfirmPending] = useState(false);

  // Track in-flight list refresh to prevent duplicate requests
  const refreshInFlightRef = useRef(false);

  const visibleTickets = useMemo(() => filterKitchenTickets(tickets, selectedFilter), [selectedFilter, tickets]);
  const queueSummary = useMemo(() => queueSummaryLabel(tickets, t('kitchen.activeTicketCount')), [t, tickets]);

  // Update relative "last refreshed" label every 30 seconds
  useEffect(() => {
    if (!lastRefreshedAt) {
      return;
    }
    setLastRefreshedLabel(formatLastRefreshed(lastRefreshedAt, t('kitchen.lastRefreshedNotYet'), formatKitchenTicketAge(lastRefreshedAt.toISOString(), kitchenMessages)));
    const timer = setInterval(() => {
      setLastRefreshedLabel(formatLastRefreshed(lastRefreshedAt, t('kitchen.lastRefreshedNotYet'), formatKitchenTicketAge(lastRefreshedAt.toISOString(), kitchenMessages)));
    }, 30_000);
    return () => clearInterval(timer);
  }, [kitchenMessages, lastRefreshedAt, t]);

  const refreshTickets = useCallback(async () => {
    if (!canAccess || refreshInFlightRef.current) {
      return;
    }

    refreshInFlightRef.current = true;
    setTicketsLoading(true);
    setTicketsError(null);

    try {
      const response = await listKitchenTickets();
      setTickets(sortKitchenTickets(response.items));
      setLastRefreshedAt(new Date());
    } catch (caughtError) {
      setTicketsError(
        getSafeKitchenTicketErrorMessage(caughtError, t('kitchen.errorLoadTickets'), errorMessages)
      );
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setTicketsLoading(false);
      refreshInFlightRef.current = false;
    }
  }, [auth, canAccess, errorMessages, t]);

  const loadDeductionPreview = useCallback(
    async (ticketId: string) => {
      setDeductionPreviewLoading(true);
      setDeductionPreviewError(null);

      try {
        const preview = await getKitchenTicketDeductionPreview(ticketId);
        setDeductionPreview(preview);
        return preview;
      } catch (caughtError) {
        setDeductionPreview(null);
        setDeductionPreviewError(
          getSafeKitchenTicketErrorMessage(caughtError, t('kitchen.errorLoadDeductionPreview'), errorMessages)
        );
        if (isApiError(caughtError) && caughtError.status === 401) {
          void auth.logout();
        }
        throw caughtError;
      } finally {
        setDeductionPreviewLoading(false);
      }
    },
    [auth, errorMessages, t]
  );

  const loadTicketDetail = useCallback(
    async (ticketId: string) => {
      setSelectedTicketId(ticketId);
      setSelectedTicket(null);
      setSelectedTicketError(null);
      setSelectedTicketLoading(true);
      setDeductionPreview(null);
      setDeductionPreviewError(null);
      setDeductionPreviewLoading(false);
      setNotice(null);
      setCancelConfirmPending(false);

      try {
        const detail = await getKitchenTicket(ticketId);
        setSelectedTicket(detail);
        setCancelReason('');
        void loadDeductionPreview(ticketId).catch(() => undefined);
      } catch (caughtError) {
        setSelectedTicketError(
        getSafeKitchenTicketErrorMessage(caughtError, t('kitchen.errorLoadTicketDetails'), errorMessages)
        );
        if (isApiError(caughtError) && caughtError.status === 401) {
          void auth.logout();
        }
      } finally {
        setSelectedTicketLoading(false);
      }
    },
    [auth, errorMessages, loadDeductionPreview, t]
  );

  const handleStatusChange = useCallback(
    async (status: KitchenTicketStatus) => {
      if (!selectedTicket || !canUpdateStatus) {
        return;
      }

      setStatusSubmitting(true);
      setNotice(null);

      try {
        const updated = await updateKitchenTicketStatus(
          selectedTicket.kitchenTicketId,
          buildKitchenTicketStatusRequest(status)
        );
        setSelectedTicket(updated);
        setSelectedTicketId(updated.kitchenTicketId);
        setCancelReason('');
        await refreshTickets();
        void loadDeductionPreview(updated.kitchenTicketId).catch(() => undefined);
        setNotice({
          tone: 'success',
          message: t('kitchen.ticketMovedToStatus', {
            ticketNumber: updated.ticketNumber,
            status: formatKitchenTicketStatus(updated.status, kitchenMessages),
          }),
        });
      } catch (caughtError) {
        setNotice({
          tone: 'danger',
          message: getSafeKitchenTicketErrorMessage(caughtError, t('kitchen.errorUpdateTicketStatus'), errorMessages),
        });
        if (isApiError(caughtError) && caughtError.status === 401) {
          void auth.logout();
        }
      } finally {
        setStatusSubmitting(false);
      }
    },
    [auth, canUpdateStatus, errorMessages, kitchenMessages, loadDeductionPreview, refreshTickets, selectedTicket, t]
  );

  const handleCancelRequest = useCallback(() => {
    if (!selectedTicket || !canManage) {
      return;
    }
    const reasonError = getKitchenTicketCancelReasonError(cancelReason, validationMessages);
    if (reasonError) {
      setNotice({ tone: 'warning', message: reasonError });
      return;
    }
    setCancelConfirmPending(true);
  }, [cancelReason, canManage, selectedTicket, t, validationMessages]);

  const handleCancelConfirm = useCallback(async () => {
    if (!selectedTicket || !canManage) {
      return;
    }

    setCancelConfirmPending(false);
    setStatusSubmitting(true);
    setNotice(null);

    try {
      const updated = await cancelKitchenTicket(
        selectedTicket.kitchenTicketId,
        buildKitchenTicketCancelRequest(cancelReason)
      );
      setSelectedTicket(updated);
      setSelectedTicketId(updated.kitchenTicketId);
      setCancelReason('');
      await refreshTickets();
      setNotice({
        tone: 'success',
        message: t('kitchen.ticketCancelledNotice', { ticketNumber: updated.ticketNumber }),
      });
    } catch (caughtError) {
      setNotice({
        tone: 'danger',
        message: getSafeKitchenTicketErrorMessage(caughtError, t('kitchen.errorCancelTicket'), errorMessages),
      });
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setStatusSubmitting(false);
    }
  }, [auth, canManage, cancelReason, errorMessages, refreshTickets, selectedTicket, t]);

  const handleCancelAbort = useCallback(() => {
    setCancelConfirmPending(false);
  }, []);

  // Initial load
  useEffect(() => {
    if (!canAccess) {
      return;
    }
    void refreshTickets();
  }, [canAccess, refreshTickets]);

  // Polling
  useEffect(() => {
    if (!canAccess) {
      return;
    }
    const timer = setInterval(() => {
      void refreshTickets();
    }, POLL_INTERVAL_MS);
    return () => clearInterval(timer);
  }, [canAccess, refreshTickets]);

  if (!canAccess) {
    return (
      <OrderManagementLayout
        title={t('kitchen.workspaceTitle')}
        description={t('kitchen.workspaceDescription')}
        breadcrumbs={[t('nav.dashboard'), t('nav.kitchenDisplay')]}
        operatorLabel={operatorLabel}
        restaurantName={restaurantName}
        branchName={branchName}
        navItems={navItems}
      >
        <EmptyState
          title={t('kitchen.notAuthorizedTitle')}
          description={t('kitchen.notAuthorizedDescription')}
          tone="orders"
        />
      </OrderManagementLayout>
    );
  }

  return (
    <OrderManagementLayout
      title={t('kitchen.workspaceTitle')}
      description={t('kitchen.workspaceDescription')}
      breadcrumbs={[t('nav.dashboard'), t('nav.kitchenDisplay')]}
      operatorLabel={operatorLabel}
      restaurantName={restaurantName}
      branchName={branchName}
      navItems={navItems}
      actions={
        <Badge
          tone="neutral"
          label={ticketsLoading ? t('kitchen.refreshing') : t('kitchen.loadedCount', { count: tickets.length })}
        />
      }
    >
      <div className="preview-sequence kitchen-tickets-workspace">
        <div className="summary-grid">
          <SummaryCard
            label={t('kitchen.ticketsLoadedLabel')}
            value={ticketsLoading && tickets.length === 0 ? t('kitchen.loading') : tickets.length.toString()}
            tone="orders"
            detail={t('kitchen.ticketsLoadedDetail')}
          />
          <SummaryCard
            label={t('kitchen.activeQueueLabel')}
            value={queueSummary}
            tone="accent"
            detail={t('kitchen.activeQueueDetail')}
          />
          <SummaryCard
            label={t('kitchen.selectedTicketLabel')}
            value={selectedTicket?.ticketNumber ?? t('kitchen.none')}
            tone="inventory"
            detail={
              selectedTicket
                ? `${formatKitchenTicketStatus(selectedTicket.status, kitchenMessages)} · ${selectedTicket.orderNumberSnapshot}`
                : t('kitchen.selectedTicketDetail')
            }
          />
          <SummaryCard
            label={t('kitchen.lastRefreshedLabel')}
            value={ticketsLoading ? t('kitchen.refreshing') : lastRefreshedLabel}
            tone="dashboard"
            detail={t('kitchen.lastRefreshedDetail')}
          />
        </div>

        <div className="kitchen-tickets-workspace__toolbar">
            <button
              type="button"
              className="ui-button ui-button--secondary ui-button--md"
              disabled={ticketsLoading}
            aria-label={t('kitchen.refreshQueueAriaLabel')}
              onClick={() => void refreshTickets()}
            >
            {ticketsLoading ? t('kitchen.refreshing') : t('kitchen.refreshQueue')}
            </button>
        </div>

        {notice ? (
          <div
            className={['admin-notice', `admin-notice--${notice.tone}`].join(' ')}
            role={notice.tone === 'danger' ? 'alert' : 'status'}
            aria-live={notice.tone === 'danger' ? 'assertive' : 'polite'}
          >
            {notice.message}
          </div>
        ) : null}

          <Card
          title={t('kitchen.ticketFiltersTitle')}
          description={t('kitchen.ticketFiltersDescription')}
          tone="orders"
        >
          <KitchenTicketFilters selectedFilter={selectedFilter} onChange={setSelectedFilter} />
        </Card>

        <div className="preview-split kitchen-tickets-workspace__split">
          <div className="preview-main kitchen-tickets-workspace__main">
            <Card
              title={t('kitchen.ticketQueueTitle')}
              description={t('kitchen.ticketQueueDescription')}
              tone="orders"
              actions={<Badge tone="neutral" label={ticketsLoading ? t('kitchen.refreshing') : t('kitchen.visibleCount', { count: visibleTickets.length })} />}
            >
              <KitchenTicketListPanel
                tickets={visibleTickets}
                selectedTicketId={selectedTicketId}
                loading={ticketsLoading}
                error={ticketsError}
                filter={selectedFilter}
                onRetry={() => void refreshTickets()}
                onSelectTicket={ticketId => void loadTicketDetail(ticketId)}
              />
            </Card>

            <Card
              title={t('kitchen.selectedTicketTitle')}
              description={t('kitchen.selectedTicketDescription')}
              tone="orders"
            >
              <KitchenTicketDetailPanel
                ticket={selectedTicket}
                loading={selectedTicketLoading}
                error={selectedTicketError}
                onRetry={selectedTicketId ? () => void loadTicketDetail(selectedTicketId) : undefined}
              />
            </Card>
          </div>

          {canUpdateStatus || canManage ? (
            <div className="preview-side-column kitchen-tickets-workspace__aside">
              <Card
                title={t('kitchen.ticketActionsTitle')}
                description={t('kitchen.ticketActionsDescription')}
                tone="orders"
              >
                <KitchenTicketStatusActions
                  ticket={selectedTicket}
                  canUpdateStatus={canUpdateStatus}
                  canManage={canManage}
                  submitting={statusSubmitting}
                  cancelReason={cancelReason}
                  cancelConfirmPending={cancelConfirmPending}
                  onCancelReasonChange={setCancelReason}
                  onStatusChange={status => void handleStatusChange(status)}
                  onCancelRequest={handleCancelRequest}
                  onCancelConfirm={() => void handleCancelConfirm()}
                  onCancelAbort={handleCancelAbort}
                />
              </Card>

              <Card
                title={t('kitchen.completionPreviewTitle')}
                description={t('kitchen.completionPreviewDescription')}
                tone="orders"
              >
                <KitchenTicketDeductionPreviewPanel
                  preview={deductionPreview}
                  loading={deductionPreviewLoading}
                  error={deductionPreviewError}
                  onRetry={selectedTicketId ? () => void loadDeductionPreview(selectedTicketId).catch(() => undefined) : undefined}
                />
              </Card>
            </div>
          ) : null}
        </div>
      </div>
    </OrderManagementLayout>
  );
};

export default KitchenTicketsPage;
