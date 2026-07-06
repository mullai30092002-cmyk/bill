import type { FormEvent, Ref } from 'react';

import { Button, Card, Input, Select, SummaryCard } from '../../components/ui';
import { useLanguage } from '../../i18n/LanguageProvider';
import { formatInventoryStock } from './inventoryDisplay';
import type { InventoryItemListItem } from './inventoryTypes';

export interface InventoryAdjustmentFormState {
  movementType: string;
  quantity: string;
  reason: string;
  notes: string;
}

export interface InventoryAdjustmentFormErrors {
  movementType?: string;
  quantity?: string;
  reason?: string;
  notes?: string;
}

export interface InventoryAdjustmentDialogProps {
  item: InventoryItemListItem;
  form: InventoryAdjustmentFormState;
  errors: InventoryAdjustmentFormErrors;
  previewDelta: number | null;
  previewQuantity: number | null;
  confirmDisabled: boolean;
  submitting: boolean;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
  onClose: () => void;
  onMovementTypeChange: (value: string) => void;
  onQuantityChange: (value: string) => void;
  onReasonChange: (value: string) => void;
  onNoteChange: (value: string) => void;
  dialogRef?: Ref<HTMLDivElement>;
  quantityInputRef?: Ref<HTMLInputElement>;
}

const ADJUSTMENT_REASON_OPTIONS = [
  'Opening stock correction',
  'Damaged/wastage',
  'Physical count correction',
  'Manual purchase entry',
  'Other',
];

const getAdjustmentPreviewTone = (previewQuantity: number | null) => {
  if (previewQuantity === null) {
    return 'inventory' as const;
  }

  if (previewQuantity <= 0) {
    return 'admin' as const;
  }

  return 'accent' as const;
};

export const InventoryAdjustmentDialog = ({
  item,
  form,
  errors,
  previewDelta,
  previewQuantity,
  confirmDisabled,
  submitting,
  onSubmit,
  onClose,
  onMovementTypeChange,
  onQuantityChange,
  onReasonChange,
  onNoteChange,
  dialogRef,
  quantityInputRef,
}: InventoryAdjustmentDialogProps) => {
  const { t } = useLanguage();
  const previewTone = getAdjustmentPreviewTone(previewQuantity);
  const deltaLabel =
    previewDelta === null
      ? t('inventoryAdjustment.enterQuantity')
      : `${previewDelta > 0 ? '+' : '-'}${formatInventoryStock(Math.abs(previewDelta))}`;

  return (
    <div
      ref={dialogRef}
      className="inventory-adjustment-dialog scroll-target"
      role="dialog"
      aria-modal="true"
      aria-label="Adjust stock dialog"
    >
      <Card
        title={t('inventoryAdjustment.dialogTitle')}
        description={t('inventoryAdjustment.dialogDescription')}
        tone="inventory"
      >
        <form className="admin-form" onSubmit={onSubmit} noValidate>
          <div className="inventory-adjustment-dialog__summary">
            <div className="inventory-adjustment-dialog__item">
              <strong>{item.name}</strong>
              <span>
                {item.category} · {item.unitOfMeasure}
              </span>
              <span>{t('inventoryAdjustment.statusLabel')}: {item.status}</span>
            </div>
          </div>

          <div className="summary-grid">
            <SummaryCard
              label={t('inventoryAdjustment.currentQuantityLabel')}
              value={formatInventoryStock(item.currentStock)}
              tone="inventory"
              detail={t('inventoryAdjustment.currentQuantityDetail')}
            />
            <SummaryCard
              label={t('inventoryAdjustment.adjustmentDeltaLabel')}
              value={deltaLabel}
              tone={previewDelta === null ? 'inventory' : previewDelta > 0 ? 'accent' : 'admin'}
              detail={t('inventoryAdjustment.adjustmentDeltaDetail')}
            />
            <SummaryCard
              label={t('inventoryAdjustment.previewNewQuantityLabel')}
              value={previewQuantity === null ? t('inventoryAdjustment.enterQuantity') : formatInventoryStock(previewQuantity)}
              tone={previewTone}
              detail={t('inventoryAdjustment.previewNewQuantityDetail')}
            />
          </div>

          <div className="admin-form-grid">
            <Select
              label={t('inventoryAdjustment.adjustmentTypeLabel')}
              value={form.movementType}
              onChange={event => onMovementTypeChange(event.target.value)}
              error={errors.movementType}
              helperText={t('inventoryAdjustment.adjustmentTypeHelper')}
            >
              <option value="Increase">{t('inventoryAdjustment.increaseOption')}</option>
              <option value="Decrease">{t('inventoryAdjustment.decreaseOption')}</option>
            </Select>
            <Input
              label={t('inventoryAdjustment.quantityLabel')}
              type="number"
              min="0.01"
              step="0.01"
              value={form.quantity}
              onChange={event => onQuantityChange(event.target.value)}
              error={errors.quantity}
              helperText={t('inventoryAdjustment.quantityHelper')}
              ref={quantityInputRef}
              className="scroll-target"
            />
            <Select
              label={t('inventoryAdjustment.reasonLabel')}
              value={form.reason}
              onChange={event => onReasonChange(event.target.value)}
              error={errors.reason}
              helperText={t('inventoryAdjustment.reasonHelper')}
            >
              <option value="">{t('inventoryAdjustment.reasonSelectPlaceholder')}</option>
              {ADJUSTMENT_REASON_OPTIONS.map(reason => (
                <option key={reason} value={reason}>
                  {reason}
                </option>
              ))}
            </Select>
            <Input
              label={t('inventoryAdjustment.noteLabel')}
              value={form.notes}
              onChange={event => onNoteChange(event.target.value)}
              error={errors.notes}
              helperText={t('inventoryAdjustment.noteHelper')}
            />
          </div>

          <div className="admin-form-note">
            {t('inventoryAdjustment.decreaseNote')}
          </div>

          <div className="admin-form-actions">
            <Button type="button" variant="secondary" onClick={onClose} disabled={submitting}>
              {t('inventoryAdjustment.cancelButton')}
            </Button>
            <Button type="submit" size="lg" disabled={submitting || confirmDisabled}>
              {submitting ? t('inventoryAdjustment.savingButton') : t('inventoryAdjustment.confirmButton')}
            </Button>
          </div>
        </form>
      </Card>
    </div>
  );
};

export default InventoryAdjustmentDialog;
