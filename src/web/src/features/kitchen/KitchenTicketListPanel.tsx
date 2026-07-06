import { useLanguage } from '../../i18n/LanguageProvider';
import { Badge, EmptyState, StatusBadge } from '../../components/ui';
import {
  formatKitchenTicketAge,
  formatKitchenTicketReference,
  formatKitchenTicketOrderType,
  formatKitchenTicketStatus,
  getKitchenTicketUrgency,
} from './kitchenTicketDisplay';
import type { KitchenTicketListItem, KitchenTicketQueueFilter } from './kitchenTicketTypes';

export interface KitchenTicketListPanelProps {
  tickets: KitchenTicketListItem[];
  selectedTicketId: string | null;
  loading: boolean;
  error: string | null;
  filter: KitchenTicketQueueFilter;
  onRetry?: () => void;
  onSelectTicket: (ticketId: string) => void;
}

const urgencyClass: Record<string, string> = {
  warning: 'kitchen-ticket-card--urgent',
  critical: 'kitchen-ticket-card--critical',
};

export const KitchenTicketListPanel = ({
  tickets,
  selectedTicketId,
  loading,
  error,
  filter,
  onRetry,
  onSelectTicket,
}: KitchenTicketListPanelProps) => {
  const { t } = useLanguage();
  const messages = {
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
  };

  if (error) {
    return (
      <EmptyState
        title={t('kitchen.couldNotLoadTicketsTitle')}
        description={error}
        actionLabel={onRetry ? t('kitchen.tryAgain') : undefined}
        onAction={onRetry}
        tone="orders"
      />
    );
  }

  if (loading && tickets.length === 0) {
    return <EmptyState title={t('kitchen.loadingTicketsTitle')} description={t('kitchen.loadingTicketsDescription')} tone="orders" />;
  }

  if (tickets.length === 0) {
    return (
      <EmptyState
        title={filter === 'Active' ? t('kitchen.noActiveTicketsTitle') : t('kitchen.noTicketsFoundTitle')}
        description={
          filter === 'Active'
            ? t('kitchen.noActiveTicketsDescription')
            : t('kitchen.noTicketsMatchFilterDescription')
        }
        tone="orders"
      />
    );
  }

  // Tickets arrive pre-sorted from the parent (sortKitchenTickets already applied).
  // We do not re-sort here to avoid double sorting.
  return (
    <div className="kitchen-ticket-list" aria-live="polite" aria-label={t('kitchen.ticketQueueAriaLabel')}>
      {tickets.map(ticket => {
        const isCancelled = ticket.status === 'Cancelled';
        const isSelected = selectedTicketId === ticket.kitchenTicketId;
        const urgency = getKitchenTicketUrgency(ticket.createdAt, ticket.status);
        const reference = formatKitchenTicketReference(ticket);
        const statusLabel = formatKitchenTicketStatus(ticket.status, messages);

        return (
          <button
            key={ticket.kitchenTicketId}
            type="button"
            className={[
              'kitchen-ticket-card',
              isCancelled && 'kitchen-ticket-card--cancelled',
              !isCancelled && urgencyClass[urgency],
              isSelected && 'kitchen-ticket-card--selected',
            ]
              .filter(Boolean)
              .join(' ')}
            aria-pressed={isSelected}
            onClick={() => onSelectTicket(ticket.kitchenTicketId)}
          >
            <div className="kitchen-ticket-card__header">
              <span className="kitchen-ticket-card__number">{ticket.ticketNumber}</span>
              <StatusBadge status={ticket.status} label={statusLabel} />
            </div>

            {isCancelled ? (
              <div className="kitchen-ticket-card__cancelled-banner" role="alert">
                {t('kitchen.cancelledDoNotPrepare')}
                {ticket.cancelReason ? `: ${ticket.cancelReason}` : ''}
              </div>
            ) : null}

            <div className="kitchen-ticket-card__meta">
              <span className="kitchen-ticket-card__order">{ticket.orderNumberSnapshot}</span>
              <span className="kitchen-ticket-card__type-badge">
                <Badge
                  tone={ticket.orderTypeSnapshot === 'EatIn' ? 'primary' : 'accent'}
                  label={formatKitchenTicketOrderType(ticket.orderTypeSnapshot === 'EatIn' ? 'EatIn' : 'Parcel', messages)}
                />
              </span>
              {reference ? (
                <span className="kitchen-ticket-card__reference">{reference}</span>
              ) : null}
            </div>

            <div className="kitchen-ticket-card__footer">
              <Badge
                tone="neutral"
                label={t('kitchen.lineCount', {
                  count: ticket.lineCount,
                  suffix: ticket.lineCount !== 1 ? t('kitchen.lineCountSuffix') : '',
                })}
              />
              <Badge
                tone={urgency === 'critical' ? 'danger' : urgency === 'warning' ? 'warning' : 'neutral'}
                label={formatKitchenTicketAge(ticket.createdAt, messages)}
              />
              {ticket.orderNotesSnapshot ? (
                <Badge tone="info" label={t('kitchen.notesLabel')} />
              ) : null}
            </div>
          </button>
        );
      })}
    </div>
  );
};

export default KitchenTicketListPanel;
