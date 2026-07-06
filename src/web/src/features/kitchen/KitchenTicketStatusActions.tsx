import { useLanguage } from '../../i18n/LanguageProvider';
import { Button, EmptyState, Input } from '../../components/ui';
import type { KitchenTicketDetail, KitchenTicketStatus } from './kitchenTicketTypes';

export interface KitchenTicketStatusActionsProps {
  ticket: KitchenTicketDetail | null;
  canUpdateStatus: boolean;
  canManage: boolean;
  submitting: boolean;
  cancelReason: string;
  cancelConfirmPending: boolean;
  onCancelReasonChange: (value: string) => void;
  onStatusChange: (status: KitchenTicketStatus) => void;
  onCancelRequest: () => void;
  onCancelConfirm: () => void;
  onCancelAbort: () => void;
}

export const KitchenTicketStatusActions = ({
  ticket,
  canUpdateStatus,
  canManage,
  submitting,
  cancelReason,
  cancelConfirmPending,
  onCancelReasonChange,
  onStatusChange,
  onCancelRequest,
  onCancelConfirm,
  onCancelAbort,
}: KitchenTicketStatusActionsProps) => {
  const { t } = useLanguage();

  if (!ticket) {
    return (
      <EmptyState
        title={t('kitchen.noTicketSelectedForActionsTitle')}
        description={t('kitchen.noTicketSelectedForActionsDescription')}
        tone="orders"
      />
    );
  }

  const actions =
    ticket.status === 'Pending'
      ? [
          { label: t('kitchen.startPreparing'), status: 'Preparing' as const },
          { label: t('kitchen.markReady'), status: 'Ready' as const },
        ]
      : ticket.status === 'Preparing'
        ? [{ label: t('kitchen.markReady'), status: 'Ready' as const }]
        : ticket.status === 'Ready'
          ? [{ label: t('kitchen.markServed'), status: 'Served' as const }]
          : [];
  const canCancelTicket = canManage && ['Pending', 'Preparing', 'Ready'].includes(ticket.status);

  if (cancelConfirmPending) {
    return (
      <div className="kitchen-ticket-status-actions">
        <div className="kitchen-ticket-cancel-confirm" role="alertdialog" aria-labelledby="cancel-confirm-heading">
          <p id="cancel-confirm-heading" className="kitchen-ticket-cancel-confirm__message">
            {t('kitchen.cancelConfirmPromptPrefix')} <strong>{ticket.ticketNumber}</strong>? {t('kitchen.cancelConfirmPromptSuffix')}
          </p>
          <div className="kitchen-ticket-cancel-confirm__buttons">
            <Button
              variant="danger"
              fullWidth
              size="lg"
              disabled={submitting}
              onClick={onCancelConfirm}
            >
              {submitting ? t('kitchen.cancelling') : t('kitchen.confirmCancel')}
            </Button>
            <Button
              variant="secondary"
              fullWidth
              size="lg"
              disabled={submitting}
              onClick={onCancelAbort}
            >
              {t('kitchen.goBack')}
            </Button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="kitchen-ticket-status-actions">
      {canUpdateStatus && actions.length > 0 ? (
        <div className="kitchen-ticket-status-actions__buttons">
          {actions.map(action => (
            <Button
              key={action.label}
              fullWidth
              size="lg"
              variant={action.status === 'Served' ? 'primary' : 'secondary'}
              disabled={submitting}
              aria-busy={submitting}
              onClick={() => onStatusChange(action.status)}
            >
              {submitting ? `${action.label}…` : action.label}
            </Button>
          ))}
        </div>
      ) : null}

      {canCancelTicket ? (
        <div className="kitchen-ticket-status-actions__cancel">
          <Input
            label={t('kitchen.cancelReasonLabel')}
            placeholder={t('kitchen.cancelReasonPlaceholder')}
            value={cancelReason}
            disabled={submitting}
            onChange={event => onCancelReasonChange(event.target.value)}
            helperText={t('kitchen.cancelReasonHelper')}
          />
          <Button
            variant="danger"
            fullWidth
            size="lg"
            disabled={submitting}
            onClick={onCancelRequest}
          >
            {t('kitchen.cancelTicket')}
          </Button>
        </div>
      ) : null}

      {actions.length === 0 && !canCancelTicket ? (
        <p className="kitchen-ticket-status-actions__terminal">
          {ticket.status === 'Cancelled'
            ? t('kitchen.ticketCancelledNoFurtherActions')
            : t('kitchen.ticketServedNoFurtherActions')}
        </p>
      ) : null}
    </div>
  );
};

export default KitchenTicketStatusActions;
