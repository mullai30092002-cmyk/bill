import { Badge, EmptyState, StatusBadge } from '../../components/ui';
import { useLanguage } from '../../i18n/LanguageProvider';
import { useRestaurantCurrency } from '../auth/useRestaurantCurrency';
import { formatPosCurrency, formatPosTimestamp } from './posDisplay';
import { getSafePosErrorMessage } from './posErrorDisplay';
import type { PosOrderListItem, PosOrderStatus, PosOrderType } from './posTypes';

export interface PosOrderListPanelProps {
  orders: PosOrderListItem[];
  loading: boolean;
  error?: string | null;
  selectedOrderId: string | null;
  onRetry: () => void;
  onSelectOrder: (orderId: string) => void;
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

export const PosOrderListPanel = ({
  orders,
  loading,
  error,
  selectedOrderId,
  onRetry,
  onSelectOrder,
}: PosOrderListPanelProps) => {
  const { currencyCode, locale } = useRestaurantCurrency();
  const { t } = useLanguage();

  if (loading && orders.length === 0) {
    return (
      <EmptyState
        title={t('pos.loadingRecentOrdersTitle')}
        description={t('pos.loadingRecentOrdersDescription')}
        tone="orders"
      />
    );
  }

  if (error) {
    return (
      <EmptyState
        title={t('pos.couldNotLoadRecentOrdersTitle')}
        description={getSafePosErrorMessage(error, t('pos.errorLoadRecentOrders'))}
        tone="orders"
        actionLabel={t('pos.tryAgain')}
        onAction={onRetry}
      />
    );
  }

  if (orders.length === 0) {
    return (
      <EmptyState
        title={t('pos.noRecentOrdersTitle')}
        description={t('pos.noRecentOrdersDescription')}
        tone="orders"
      />
    );
  }

  return (
    <div className="pos-order-list">
      <div className="pos-order-list__desktop" aria-label={t('pos.recentOrdersAria')}>
        <table className="pos-order-list__table">
          <thead>
            <tr>
              <th scope="col">{t('pos.order')}</th>
              <th scope="col">{t('pos.type')}</th>
              <th scope="col">{t('pos.status')}</th>
              <th scope="col" className="is-right">{t('pos.total')}</th>
              <th scope="col" className="is-right">{t('pos.lines')}</th>
              <th scope="col">{t('pos.created')}</th>
            </tr>
          </thead>
          <tbody>
            {orders.map(order => {
              const isSelected = selectedOrderId === order.posOrderId;
              return (
                <tr
                  key={order.posOrderId}
                  className={isSelected ? 'pos-order-list__row pos-order-list__row--selected' : 'pos-order-list__row'}
                >
                  <td>
                    <button
                      type="button"
                      className="pos-order-list__order-btn"
                      aria-pressed={isSelected}
                      onClick={() => onSelectOrder(order.posOrderId)}
                    >
                      {order.orderNumber}
                    </button>
                  </td>
                  <td><Badge tone="primary" label={t(orderTypeKey(order.orderType))} /></td>
                  <td><StatusBadge status={order.status} label={t(statusKey(order.status))} /></td>
                  <td className="is-right">{formatPosCurrency(order.grandTotal, currencyCode, locale)}</td>
                  <td className="is-right">{order.lineCount}</td>
                  <td>{formatPosTimestamp(order.createdAt)}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      <div className="pos-order-list__mobile" aria-label={t('pos.recentOrdersAria')}>
        {orders.map(order => {
          const isSelected = selectedOrderId === order.posOrderId;
          return (
            <button
              key={order.posOrderId}
              type="button"
              className={isSelected ? 'pos-order-card pos-order-card--selected' : 'pos-order-card'}
              aria-pressed={isSelected}
              onClick={() => onSelectOrder(order.posOrderId)}
            >
              <div className="pos-order-card__header">
                <span className="pos-order-card__number">{order.orderNumber}</span>
                <StatusBadge status={order.status} label={t(statusKey(order.status))} />
              </div>
              <div className="pos-order-card__meta">
                <Badge tone="primary" label={t(orderTypeKey(order.orderType))} />
                <span className="pos-order-card__total">{formatPosCurrency(order.grandTotal, currencyCode, locale)}</span>
                <span className="pos-order-card__lines">
                  {t('pos.lineCount', { count: order.lineCount, suffix: order.lineCount !== 1 ? 's' : '' })}
                </span>
              </div>
              <div className="pos-order-card__time">{formatPosTimestamp(order.createdAt)}</div>
            </button>
          );
        })}
      </div>
    </div>
  );
};

export default PosOrderListPanel;
