import { useLanguage } from '../../i18n/LanguageProvider';
import { Badge, EmptyState, StatusBadge } from '../../components/ui';
import {
  formatKitchenTicketCreatedLabel,
  formatKitchenTicketLifecycleLabel,
  formatKitchenTicketReference,
  formatKitchenTicketOrderType,
  formatKitchenTicketStatus,
  formatKitchenTicketUpdatedLabel,
} from './kitchenTicketDisplay';
import type { KitchenTicketDetail } from './kitchenTicketTypes';

const resolveDeductionStatusTone = (status: KitchenTicketDetail['inventoryDeductionStatus']) => {
  switch (status) {
    case 'Deducted':
      return 'success' as const;
    case 'DeductionWarning':
      return 'warning' as const;
    default:
      return 'neutral' as const;
  }
};

export interface KitchenTicketDetailPanelProps {
  ticket: KitchenTicketDetail | null;
  loading: boolean;
  error: string | null;
  onRetry?: () => void;
}

export const KitchenTicketDetailPanel = ({ ticket, loading, error, onRetry }: KitchenTicketDetailPanelProps) => {
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
        title={t('kitchen.couldNotLoadTicketDetailsTitle')}
        description={error}
        actionLabel={onRetry ? t('kitchen.tryAgain') : undefined}
        onAction={onRetry}
        tone="orders"
      />
    );
  }

  if (loading && !ticket) {
    return <EmptyState title={t('kitchen.loadingTicketDetailsTitle')} description={t('kitchen.loadingTicketDetailsDescription')} tone="orders" />;
  }

  if (!ticket) {
    return (
      <EmptyState
        title={t('kitchen.noTicketSelectedTitle')}
        description={t('kitchen.noTicketSelectedDescription')}
        tone="orders"
      />
    );
  }

  const isCancelled = ticket.status === 'Cancelled';
  const reference = formatKitchenTicketReference(ticket);
  const statusLabel = formatKitchenTicketStatus(ticket.status, messages);
  const deductionStatusLabel =
    ticket.inventoryDeductionStatus === 'Deducted'
      ? t('kitchen.inventoryDeductionStatusDeducted')
      : ticket.inventoryDeductionStatus === 'DeductionWarning'
        ? t('kitchen.inventoryDeductionStatusWarning')
        : t('kitchen.inventoryDeductionStatusNotDeducted');

  return (
    <div className="kitchen-ticket-detail">
      {isCancelled ? (
        <div className="kitchen-ticket-detail__cancel-alert" role="alert">
          <strong>{t('kitchen.cancelledDoNotPrepare')}</strong>
          {ticket.cancelReason ? <span> {t('kitchen.cancelledReasonPrefix')} {ticket.cancelReason}</span> : null}
        </div>
      ) : null}

      <div className="kitchen-ticket-detail__header">
        <div className="kitchen-ticket-detail__heading-block">
          <div className="kitchen-ticket-detail__ticket-number">{ticket.ticketNumber}</div>
          <div className="kitchen-ticket-detail__subhead">
            <span>{ticket.orderNumberSnapshot}</span>
            <span>·</span>
            <span>
              <Badge
                tone={ticket.orderTypeSnapshot === 'EatIn' ? 'primary' : 'accent'}
                label={formatKitchenTicketOrderType(ticket.orderTypeSnapshot === 'EatIn' ? 'EatIn' : 'Parcel', messages)}
              />
            </span>
            {reference ? (
              <>
                <span>·</span>
                <span className="kitchen-ticket-detail__reference">{reference}</span>
              </>
            ) : null}
          </div>
        </div>
        <StatusBadge status={ticket.status} label={statusLabel} />
      </div>

      {ticket.orderNotesSnapshot ? (
        <div className="kitchen-ticket-detail__order-notes">
          <span className="kitchen-ticket-detail__order-notes-label">{t('kitchen.orderNotesLabel')}:</span>{' '}
          {ticket.orderNotesSnapshot}
        </div>
      ) : null}

      <div className="kitchen-ticket-detail__meta-grid">
        <div className="kitchen-ticket-detail__meta-item">
          <span className="kitchen-ticket-detail__meta-label">{t('kitchen.createdLabel')}</span>
          <span className="kitchen-ticket-detail__meta-value">{formatKitchenTicketCreatedLabel(ticket, messages)}</span>
        </div>
        <div className="kitchen-ticket-detail__meta-item">
          <span className="kitchen-ticket-detail__meta-label">{t('kitchen.updatedLabel')}</span>
          <span className="kitchen-ticket-detail__meta-value">{formatKitchenTicketUpdatedLabel(ticket, messages)}</span>
        </div>
        {ticket.preparingAt ? (
          <div className="kitchen-ticket-detail__meta-item">
            <span className="kitchen-ticket-detail__meta-label">{t('kitchen.preparingLabel')}</span>
            <span className="kitchen-ticket-detail__meta-value">{formatKitchenTicketLifecycleLabel(t('kitchen.preparingAtLabel'), ticket.preparingAt, messages)}</span>
          </div>
        ) : null}
        {ticket.readyAt ? (
          <div className="kitchen-ticket-detail__meta-item">
            <span className="kitchen-ticket-detail__meta-label">{t('kitchen.readyLabel')}</span>
            <span className="kitchen-ticket-detail__meta-value">{formatKitchenTicketLifecycleLabel(t('kitchen.readyAtLabel'), ticket.readyAt, messages)}</span>
          </div>
        ) : null}
        {ticket.servedAt ? (
          <div className="kitchen-ticket-detail__meta-item">
            <span className="kitchen-ticket-detail__meta-label">{t('kitchen.servedLabel')}</span>
            <span className="kitchen-ticket-detail__meta-value">{formatKitchenTicketLifecycleLabel(t('kitchen.servedAtLabel'), ticket.servedAt, messages)}</span>
          </div>
        ) : null}
        {ticket.cancelledAt ? (
          <div className="kitchen-ticket-detail__meta-item kitchen-ticket-detail__meta-item--cancelled">
            <span className="kitchen-ticket-detail__meta-label">{t('kitchen.cancelledLabel')}</span>
            <span className="kitchen-ticket-detail__meta-value">{formatKitchenTicketLifecycleLabel(t('kitchen.cancelledAtLabel'), ticket.cancelledAt, messages)}</span>
          </div>
        ) : null}
        <div className="kitchen-ticket-detail__meta-item">
          <span className="kitchen-ticket-detail__meta-label">{t('kitchen.inventoryDeductionLabel')}</span>
          <span className="kitchen-ticket-detail__meta-value">
            <Badge tone={resolveDeductionStatusTone(ticket.inventoryDeductionStatus)} label={deductionStatusLabel} />
          </span>
        </div>
      </div>

      <div className="kitchen-ticket-line-list">
        {ticket.lines.map(line => (
          <article key={line.kitchenTicketLineId} className="kitchen-ticket-line">
            <div className="kitchen-ticket-line__header">
              <div className="kitchen-ticket-line__title-block">
                <strong>{line.menuItemNameSnapshot}</strong>
                <span>{line.menuCategoryNameSnapshot}</span>
              </div>
              <span className="kitchen-ticket-line__quantity">×{line.quantity}</span>
            </div>
            {line.notes ? (
              <div className="kitchen-ticket-line__notes">{line.notes}</div>
            ) : null}
            <dl className="kitchen-ticket-line__fields">
              <div className="kitchen-ticket-line__field">
                <dt>{t('kitchen.skuLabel')}</dt>
                <dd>{line.skuSnapshot || t('kitchen.none')}</dd>
              </div>
              <div className="kitchen-ticket-line__field">
                <dt>{t('kitchen.displayOrderLabel')}</dt>
                <dd>{line.displayOrder}</dd>
              </div>
            </dl>
          </article>
        ))}
      </div>
    </div>
  );
};

export default KitchenTicketDetailPanel;
