import { Badge } from '../../components/ui';
import { useLanguage } from '../../i18n/LanguageProvider';
import { useRestaurantCurrency } from '../auth/useRestaurantCurrency';
import { formatPosCurrency } from './posDisplay';
import type { PosOrderDetail, PosOrderStatus, PosOrderType } from './posTypes';

export interface PosOrderSummaryProps {
  lineCount: number;
  estimatedSubtotal: number;
  estimatedTaxTotal: number;
  estimatedGrandTotal: number;
  savedOrder?: PosOrderDetail | null;
}

const formatOrderTypeKey = (value: PosOrderType) => (value === 'EatIn' ? 'pos.orderTypeEatIn' : 'pos.orderTypeParcel');

const formatStatusKey = (value: PosOrderStatus) => {
  if (value === 'Confirmed') {
    return 'pos.statusConfirmed';
  }

  if (value === 'Cancelled') {
    return 'pos.statusCancelled';
  }

  return 'pos.statusDraft';
};

export const PosOrderSummary = ({
  lineCount,
  estimatedSubtotal,
  estimatedTaxTotal,
  estimatedGrandTotal,
  savedOrder,
}: PosOrderSummaryProps) => {
  const { currencyCode, locale } = useRestaurantCurrency();
  const { t } = useLanguage();

  return (
    <div className="pos-totals-bar">
      <div className="pos-totals-bar__header">
        <span className="pos-totals-bar__label">{t('pos.summaryTitle')}</span>
        {savedOrder ? (
          <span className="pos-totals-bar__badges">
            <Badge tone="neutral" label={savedOrder.orderNumber} />
            <Badge
              tone={savedOrder.status === 'Draft' ? 'warning' : savedOrder.status === 'Confirmed' ? 'success' : 'danger'}
              label={t(formatStatusKey(savedOrder.status))}
            />
          </span>
        ) : null}
      </div>

      <div className="pos-totals-bar__rows">
        <div className="pos-totals-bar__row">
          <span className="pos-totals-bar__row-label">{t('pos.lines')}</span>
          <span className="pos-totals-bar__row-value">{lineCount}</span>
        </div>
        <div className="pos-totals-bar__row">
          <span className="pos-totals-bar__row-label">{t('pos.subtotal')}</span>
          <span className="pos-totals-bar__row-value">{formatPosCurrency(estimatedSubtotal, currencyCode, locale)}</span>
        </div>
        {estimatedTaxTotal > 0 ? (
          <div className="pos-totals-bar__row">
            <span className="pos-totals-bar__row-label">{t('pos.tax')}</span>
            <span className="pos-totals-bar__row-value">{formatPosCurrency(estimatedTaxTotal, currencyCode, locale)}</span>
          </div>
        ) : null}
        <div className="pos-totals-bar__row pos-totals-bar__row--total">
          <span className="pos-totals-bar__row-label">{t('pos.total')}</span>
          <span className="pos-totals-bar__row-value">{formatPosCurrency(estimatedGrandTotal, currencyCode, locale)}</span>
        </div>
      </div>

      <p className="pos-totals-bar__note">
        {savedOrder
          ? t('pos.savedAsSummary', {
              orderNumber: savedOrder.orderNumber,
              orderType: t(formatOrderTypeKey(savedOrder.orderType)),
            })
          : t('pos.estimateOnly')}
      </p>
    </div>
  );
};

export default PosOrderSummary;
