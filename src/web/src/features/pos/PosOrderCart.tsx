import { Badge, Button, Card, EmptyState, Input } from '../../components/ui';
import { useLanguage } from '../../i18n/LanguageProvider';
import { useRestaurantCurrency } from '../auth/useRestaurantCurrency';
import { canUseDraftLineForOrderType, getDraftLineQuantityError, type PosOrderLineErrors } from './posOrderFormValidation';
import { formatPosCurrency, roundMoney } from './posDisplay';
import type { PosOrderDraftLineForm } from './posOrderFormValidation';
import type { PosOrderType } from './posTypes';

export interface PosOrderCartProps {
  lines: PosOrderDraftLineForm[];
  orderType: PosOrderType;
  canEdit: boolean;
  lineErrors?: PosOrderLineErrors;
  onChangeQuantity: (draftLineId: string, value: string) => void;
  onChangeNotes: (draftLineId: string, value: string) => void;
  onRemoveLine: (draftLineId: string) => void;
}

export const PosOrderCart = ({
  lines,
  orderType,
  canEdit,
  lineErrors,
  onChangeQuantity,
  onChangeNotes,
  onRemoveLine,
}: PosOrderCartProps) => {
  const { currencyCode, locale } = useRestaurantCurrency();
  const { t } = useLanguage();

  if (lines.length === 0) {
    return (
      <>
        <Card
          title={t('pos.cartTitle')}
          description={t('pos.cartEmptyDescription')}
          tone="orders"
          className="pos-cart-empty-desktop"
        >
          <EmptyState title={t('pos.cartEmptyTitle')} description={t('pos.cartEmptyText')} tone="orders" />
        </Card>
        <p className="pos-cart--empty" aria-label={t('pos.cartEmptyTitle')}>
          {t('pos.cartEmptyMobileText')}
        </p>
      </>
    );
  }

  return (
    <Card title={t('pos.cartTitle')} description={t('pos.cartDescription')} tone="orders">
      <div className="pos-cart-lines">
        {lines.map(line => {
          const quantityError = lineErrors?.[line.draftLineId]?.quantity ?? getDraftLineQuantityError(line, t);
          const availabilityError = lineErrors?.[line.draftLineId]?.availability;
          const availabilityOk = canUseDraftLineForOrderType(line, orderType);
          const quantity = Number.parseFloat(line.quantity);
          const lineSubtotal = Number.isFinite(quantity) && quantity > 0 ? roundMoney(quantity * line.unitPrice) : 0;
          const lineTax = roundMoney((lineSubtotal * line.taxRate) / 100);
          const lineTotal = roundMoney(lineSubtotal + lineTax);

          return (
            <Card key={line.draftLineId} tone="default" className="pos-cart-line">
              <div className="pos-cart-line__header">
                <div className="pos-cart-line__title-block">
                  <strong>{line.menuItemNameSnapshot}</strong>
                  <div className="pos-cart-line__subtitle">{line.menuCategoryNameSnapshot}</div>
                </div>
                <div className="pos-cart-line__badges">
                  <Badge tone={availabilityOk ? 'success' : 'warning'} label={availabilityOk ? t('pos.available') : t('pos.unavailable')} />
                </div>
              </div>

              <div className="mini-status-row">
                <Badge tone="primary" label={formatPosCurrency(line.unitPrice, currencyCode, locale)} />
                <Badge tone="accent" label={t('pos.taxLabel', { rate: line.taxRate.toFixed(2) })} />
                <Badge tone="neutral" label={line.skuSnapshot ?? t('pos.noSku')} />
                <Badge tone="success" label={formatPosCurrency(lineTotal, currencyCode, locale)} />
              </div>

              <div className="admin-form-grid pos-cart-line__form-grid">
                <Input
                  label={t('pos.quantity')}
                  type="number"
                  inputMode="decimal"
                  step="0.001"
                  min="0.001"
                  value={line.quantity}
                  error={quantityError ?? undefined}
                  disabled={!canEdit}
                  onChange={event => onChangeQuantity(line.draftLineId, event.target.value)}
                />
                <Input
                  label={t('pos.lineNotes')}
                  value={line.notes}
                  disabled={!canEdit}
                  placeholder={t('pos.orderNotesPlaceholder')}
                  onChange={event => onChangeNotes(line.draftLineId, event.target.value)}
                />
              </div>

              {availabilityOk ? null : <div className="admin-form-note">{t('pos.lineUnavailableNote')}</div>}
              {availabilityError ? <div className="admin-form-error">{availabilityError}</div> : null}

              {canEdit ? (
                <div className="pos-cart-line__actions">
                  <Button variant="secondary" size="sm" onClick={() => onRemoveLine(line.draftLineId)}>
                    {t('pos.removeLine')}
                  </Button>
                </div>
              ) : null}
            </Card>
          );
        })}
      </div>
    </Card>
  );
};

export default PosOrderCart;
