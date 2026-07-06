import { useEffect, useState } from 'react';

import { Badge, Button, Card, EmptyState, Input, StatusBadge } from '../../components/ui';
import { useLanguage } from '../../i18n/LanguageProvider';
import { useRestaurantCurrency } from '../auth/useRestaurantCurrency';
import { formatPosCurrency, formatPosTimestamp } from './posDisplay';
import { getSafePosErrorMessage } from './posErrorDisplay';
import type { PosOrderDetail, PosOrderStatus, PosOrderType } from './posTypes';

export interface PosOrderActionsProps {
  order: PosOrderDetail | null;
  loading?: boolean;
  error?: string | null;
  branchName?: string;
  canCreate: boolean;
  canCancel: boolean;
  cancelReason: string;
  cancelReasonError?: string;
  onRetry?: () => void;
  onLoadIntoDraft: () => void;
  onConfirm: () => void;
  onCancelReasonChange: (value: string) => void;
  onCancel: () => void;
}

const orderTypeKey = (value: PosOrderType) => (value === 'EatIn' ? 'pos.orderTypeEatIn' : 'pos.orderTypeParcel');

const statusKey = (value: PosOrderStatus) => {
  if (value === 'Confirmed') {
    return 'pos.statusConfirmed';
  }

  if (value === 'Cancelled') {
    return 'pos.statusCancelled';
  }

  return 'pos.statusDraft';
};

export const PosOrderActions = ({
  order,
  loading = false,
  error,
  branchName,
  canCreate,
  canCancel,
  cancelReason,
  cancelReasonError,
  onRetry,
  onLoadIntoDraft,
  onConfirm,
  onCancelReasonChange,
  onCancel,
}: PosOrderActionsProps) => {
  const { currencyCode, locale } = useRestaurantCurrency();
  const { t } = useLanguage();
  const selectedOrderDescription = t('pos.selectedOrderDescription');
  const [confirmPending, setConfirmPending] = useState(false);

  useEffect(() => {
    setConfirmPending(false);
  }, [order?.posOrderId]);

  if (loading && !order) {
    return (
      <Card title={t('pos.selectedOrderTitle')} description={selectedOrderDescription} tone="orders">
        <EmptyState title={t('pos.loadingOrderTitle')} description={t('pos.loadingOrderDescription')} tone="orders" />
      </Card>
    );
  }

  if (error && !order) {
    return (
      <Card title={t('pos.selectedOrderTitle')} description={selectedOrderDescription} tone="orders">
        <EmptyState
          title={t('pos.couldNotLoadOrderTitle')}
          description={getSafePosErrorMessage(error, t('pos.errorLoadSelectedOrder'))}
          tone="orders"
          actionLabel={onRetry ? t('pos.tryAgain') : undefined}
          onAction={onRetry}
        />
      </Card>
    );
  }

  if (!order) {
    return (
      <Card title={t('pos.selectedOrderTitle')} description={selectedOrderDescription} tone="orders">
        <EmptyState title={t('pos.noOrderSelectedTitle')} description={t('pos.noOrderSelectedDescription')} tone="orders" />
      </Card>
    );
  }

  const canLoadIntoDraft = canCreate && order.status === 'Draft';
  const canConfirm = canCreate && order.status === 'Draft';
  const canCancelOrder = canCancel && (order.status === 'Draft' || order.status === 'Confirmed');
  const orderTypeLabel = t(orderTypeKey(order.orderType));
  const orderStatusLabel = t(statusKey(order.status));

  return (
    <Card
      title={t('pos.selectedOrderTitle')}
      description={t('pos.selectedOrderReviewDescription')}
      tone="orders"
      actions={
        <>
          <Badge tone="neutral" label={order.orderNumber} />
          <StatusBadge status={order.status} label={orderStatusLabel} />
        </>
      }
    >
      <div className="admin-selected-user">
        <div className="admin-selected-user__meta">
          <div className="admin-selected-user__row">
            <span className="admin-selected-user__label">{t('pos.branch')}</span>
            <strong>{branchName ?? t('pos.unknownBranch')}</strong>
          </div>
          <div className="admin-selected-user__row">
            <span className="admin-selected-user__label">{t('pos.type')}</span>
            <strong>{orderTypeLabel}</strong>
          </div>
          <div className="admin-selected-user__row">
            <span className="admin-selected-user__label">{t('pos.grandTotal')}</span>
            <strong>{formatPosCurrency(order.grandTotal, currencyCode, locale)}</strong>
          </div>
          <div className="admin-selected-user__row">
            <span className="admin-selected-user__label">{t('pos.lines')}</span>
            <strong>{order.lines?.length ?? 0}</strong>
          </div>
          <div className="admin-selected-user__row">
            <span className="admin-selected-user__label">{t('pos.created')}</span>
            <strong>{formatPosTimestamp(order.createdAt)}</strong>
          </div>
          <div className="admin-selected-user__row">
            <span className="admin-selected-user__label">{t('pos.updated')}</span>
            <strong>{formatPosTimestamp(order.updatedAt)}</strong>
          </div>
        </div>

        {order.tableName || order.customerName || order.customerMobile || order.notes ? (
          <div className="admin-form-note">
            {order.tableName ? <div>{t('pos.table')}: {order.tableName}</div> : null}
            {order.customerName ? <div>{t('pos.customer')}: {order.customerName}</div> : null}
            {order.customerMobile ? <div>{t('pos.mobile')}: {order.customerMobile}</div> : null}
            {order.notes ? <div>{t('pos.notesLabel')}: {order.notes}</div> : null}
          </div>
        ) : null}

        {order.kitchenTicketNumber ? (
          <div className="admin-form-note">
            <div>{t('pos.kitchenTicket')}: {order.kitchenTicketNumber}</div>
            {order.kitchenTicketStatus ? <div>{t('pos.status')}: {order.kitchenTicketStatus}</div> : null}
          </div>
        ) : null}
      </div>

      {canConfirm && confirmPending ? (
        <div className="pos-confirm-step" role="region" aria-label={t('pos.confirmOrder')}>
          <p className="pos-confirm-step__summary">
            {t('pos.confirmOrderInlineSummary', {
              orderNumber: order.orderNumber,
              lineCount: t('pos.lineCount', {
                count: order.lines?.length ?? 0,
                suffix: (order.lines?.length ?? 0) !== 1 ? 's' : '',
              }),
              total: formatPosCurrency(order.grandTotal, currencyCode, locale),
            })}
          </p>
          {order.orderType === 'EatIn' ? (
            order.tableName ? (
              <p className="pos-confirm-step__table">
                {t('pos.table')}: <strong>{order.tableName}</strong>
              </p>
            ) : (
              <p className="pos-confirm-step__table pos-confirm-step__table--warn" role="alert">
                {t('pos.confirmOrderNoTableWarning')}
              </p>
            )
          ) : null}
          <div className="pos-confirm-step__actions">
            <Button
              onClick={() => { setConfirmPending(false); onConfirm(); }}
              disabled={loading}
            >
              {t('pos.confirmOrderProceed')}
            </Button>
            <Button variant="secondary" onClick={() => setConfirmPending(false)} disabled={loading}>
              {t('pos.confirmOrderBack')}
            </Button>
          </div>
        </div>
      ) : (
        <div className="admin-form-actions">
          {canLoadIntoDraft ? (
            <Button variant="secondary" onClick={onLoadIntoDraft} disabled={loading}>
              {t('pos.loadIntoDraft')}
            </Button>
          ) : null}
          {canConfirm ? (
            <Button onClick={() => setConfirmPending(true)} disabled={loading}>
              {t('pos.confirmOrder')}
            </Button>
          ) : null}
        </div>
      )}

      {canCancelOrder ? (
        <div className="admin-form-section">
          {order.status === 'Confirmed' ? (
            <p className="pos-cancel-confirmed-warn" role="alert">
              {t('pos.confirmedOrderCancelWarning')}
            </p>
          ) : null}
          <Input
            label={t('pos.cancelReason')}
            value={cancelReason}
            error={cancelReasonError}
            placeholder={t('pos.cancelReasonPlaceholder')}
            helperText={t('pos.cancelReasonHelp')}
            onChange={event => onCancelReasonChange(event.target.value)}
          />
          <div className="admin-form-actions">
            <Button variant="danger" onClick={onCancel} disabled={!cancelReason.trim() || loading}>
              {t('pos.cancelOrder')}
            </Button>
          </div>
        </div>
      ) : null}

      {!canCreate && !canCancelOrder ? <div className="admin-form-note">{t('pos.viewOnlyOrderNote')}</div> : null}
    </Card>
  );
};

export default PosOrderActions;
