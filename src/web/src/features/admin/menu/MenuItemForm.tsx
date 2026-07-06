import type { Dispatch, FormEvent, SetStateAction } from 'react';
import type { RefObject } from 'react';

import { Badge, Button, Checkbox, Input, Select } from '../../../components/ui';
import { useLanguage } from '../../../i18n/LanguageProvider';
import type { MenuItemFormErrors, MenuItemFormState } from './menuFormValidation';

export interface MenuItemCategoryOption {
  value: string;
  label: string;
  disabled?: boolean;
}

export interface MenuItemInventoryOption {
  value: string;
  label: string;
  disabled?: boolean;
}

export interface MenuItemFormProps {
  mode: 'create' | 'edit';
  form: MenuItemFormState;
  errors: MenuItemFormErrors;
  categoryOptions: MenuItemCategoryOption[];
  inventoryOptions: MenuItemInventoryOption[];
  submitting: boolean;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
  onSecondaryAction: () => void;
  secondaryActionLabel: string;
  categorySelectRef?: RefObject<HTMLSelectElement | null>;
  setForm: Dispatch<SetStateAction<MenuItemFormState>>;
}

export const MenuItemForm = ({
  mode,
  form,
  errors,
  categoryOptions,
  inventoryOptions,
  submitting,
  onSubmit,
  onSecondaryAction,
  secondaryActionLabel,
  categorySelectRef,
  setForm,
}: MenuItemFormProps) => {
  const { t } = useLanguage();

  return (
    <form className="admin-form" onSubmit={onSubmit}>
      <Select
        label={t('menu.categoryLabel')}
        ref={categorySelectRef}
        value={form.menuCategoryId}
        onChange={event => setForm(current => ({ ...current, menuCategoryId: event.target.value }))}
        error={errors.menuCategoryId}
        helperText={t('menu.categoryHelperText')}
      >
        {categoryOptions.map(option => (
          <option key={option.value || 'empty'} value={option.value} disabled={option.disabled}>
            {option.label}
          </option>
        ))}
      </Select>

      <div className="admin-form-grid">
        <Input
          label={t('menu.itemNameLabel')}
          value={form.name}
          onChange={event => setForm(current => ({ ...current, name: event.target.value }))}
          error={errors.name}
          placeholder="Masala Dosa"
          autoComplete="off"
        />
        <Input
          label={t('menu.skuLabel')}
          value={form.sku}
          onChange={event => setForm(current => ({ ...current, sku: event.target.value }))}
          placeholder="DOSA-01"
          autoComplete="off"
          helperText={t('menu.skuHelperText')}
        />
      </div>

      <Input
        label={t('menu.descriptionLabel')}
        value={form.description}
        onChange={event => setForm(current => ({ ...current, description: event.target.value }))}
        placeholder="Crisp rice crepe with potato filling"
        helperText={t('menu.descriptionHelperText')}
      />

      <div className="admin-form-grid">
        <Input
          label={t('menu.basePriceLabel')}
          type="number"
          min={0}
          step="0.01"
          inputMode="decimal"
          value={form.basePrice}
          onChange={event => setForm(current => ({ ...current, basePrice: event.target.value }))}
          error={errors.basePrice}
          placeholder="2.50"
          helperText={t('menu.basePriceHelperText')}
        />
        <Input
          label={t('menu.taxRateLabel')}
          type="number"
          min={0}
          max={100}
          step="0.01"
          inputMode="decimal"
          value={form.taxRate}
          onChange={event => setForm(current => ({ ...current, taxRate: event.target.value }))}
          error={errors.taxRate}
          placeholder="0"
          helperText={t('menu.taxRateHelperText')}
        />
      </div>

      <div className="admin-form-grid">
        <Select
          label={t('menu.inventoryDeductionModeLabel')}
          value={form.inventoryDeductionMode}
          onChange={event =>
            setForm(current => ({
              ...current,
              inventoryDeductionMode: event.target.value as MenuItemFormState['inventoryDeductionMode'],
              stockInventoryItemId:
                event.target.value === 'BatchPrepared' || event.target.value === 'DirectStockItem'
                  ? current.stockInventoryItemId
                  : '',
            }))
          }
          error={errors.inventoryDeductionMode}
          helperText={t('menu.inventoryDeductionModeHelper')}
        >
          <option value="RecipeOnServe">{t('menu.inventoryDeductionModeRecipeOnServe')}</option>
          <option value="BatchPrepared">{t('menu.inventoryDeductionModeBatchPrepared')}</option>
          <option value="DirectStockItem">{t('menu.inventoryDeductionModeDirectStockItem')}</option>
          <option value="NoDeduction">{t('menu.inventoryDeductionModeNoDeduction')}</option>
        </Select>

        {form.inventoryDeductionMode === 'BatchPrepared' || form.inventoryDeductionMode === 'DirectStockItem' ? (
          <Select
            label={t('menu.stockInventoryItemLabel')}
            value={form.stockInventoryItemId}
            onChange={event => setForm(current => ({ ...current, stockInventoryItemId: event.target.value }))}
            error={errors.stockInventoryItemId}
            helperText={t('menu.stockInventoryItemHelper')}
          >
            <option value="">{t('menu.selectStockInventoryItem')}</option>
            {inventoryOptions.map(option => (
              <option key={option.value || 'empty'} value={option.value} disabled={option.disabled}>
                {option.label}
              </option>
            ))}
          </Select>
        ) : (
          <div className="admin-form-note">
            {t('menu.stockInventoryItemNotRequired')}
          </div>
        )}
      </div>

      <div className="admin-form-section">
        <div className="admin-form-section__title-row">
          <div>
            <div className="admin-form-section__title">{t('menu.itemOptionsTitle')}</div>
            <div className="admin-form-section__description">
              {t('menu.itemOptionsDescription')}
            </div>
          </div>
          <Badge tone="neutral" label={t('menu.menuItemBadge')} />
        </div>

        <div className="menu-item-option-grid">
          <Checkbox
            label={t('menu.vegetarianCheckbox')}
            checked={form.isVegetarian}
            onChange={event => setForm(current => ({ ...current, isVegetarian: event.target.checked }))}
          />
          <Checkbox
            label={t('menu.availableForEatInCheckbox')}
            checked={form.isAvailableForEatIn}
            onChange={event => setForm(current => ({ ...current, isAvailableForEatIn: event.target.checked }))}
          />
          <Checkbox
            label={t('menu.availableForParcelCheckbox')}
            checked={form.isAvailableForParcel}
            onChange={event => setForm(current => ({ ...current, isAvailableForParcel: event.target.checked }))}
          />
        </div>
        {errors.availability ? <div className="admin-form-error">{errors.availability}</div> : null}
      </div>

      <div className="admin-form-note">
        {mode === 'create'
          ? t('menu.itemCreateNote')
          : t('menu.itemEditNote')}
      </div>

      <div className="admin-form-actions">
        <Button type="submit" size="lg" disabled={submitting}>
          {submitting
            ? t('menu.creatingButton')
            : mode === 'create' ? t('menu.createItemButton') : t('menu.saveChangesButton')}
        </Button>
        <Button type="button" variant="secondary" size="lg" onClick={onSecondaryAction} disabled={submitting}>
          {secondaryActionLabel}
        </Button>
      </div>
    </form>
  );
};

export default MenuItemForm;
