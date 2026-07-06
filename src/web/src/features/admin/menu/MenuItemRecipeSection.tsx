import { useEffect, useMemo, useState, type FormEvent } from 'react';

import { Badge, Button, EmptyState, Input, ResponsiveDataList, Select } from '../../../components/ui';
import { useLanguage } from '../../../i18n/LanguageProvider';
import { formatInventoryStock } from '../../inventory/inventoryDisplay';
import type { InventoryItemListItem } from '../../inventory/inventoryTypes';
import type {
  MenuItem,
  MenuItemRecipeIngredientRequest,
  MenuItemRecipeResponse,
  UpdateMenuItemRecipeRequest,
} from '../adminTypes';

interface RecipeDraftRow {
  id: string;
  inventoryItemId: string;
  quantityRequired: string;
}

interface RecipeRowError {
  inventoryItemId?: string;
  quantityRequired?: string;
}

export interface MenuItemRecipeSectionProps {
  item: MenuItem | null;
  branchId: string | null;
  inventoryItems: InventoryItemListItem[];
  inventoryLoading: boolean;
  inventoryError: string | null;
  recipe: MenuItemRecipeResponse | null;
  loading: boolean;
  error: string | null;
  canManageItems: boolean;
  submitting: boolean;
  onSave: (request: UpdateMenuItemRecipeRequest) => Promise<void>;
  onRetry?: () => void;
}

const createDraftRow = (inventoryItemId = '', quantityRequired = ''): RecipeDraftRow => ({
  id: `recipe-row-${Math.random().toString(36).slice(2)}`,
  inventoryItemId,
  quantityRequired,
});

const validateRows = (rows: RecipeDraftRow[]) => {
  const nextErrors: Record<string, RecipeRowError> = {};
  const seenInventoryItemIds = new Set<string>();

  rows.forEach(row => {
    const rowErrors: RecipeRowError = {};
    const inventoryItemId = row.inventoryItemId.trim();
    const quantityRequired = Number.parseFloat(row.quantityRequired.trim());

    if (!inventoryItemId) {
      rowErrors.inventoryItemId = 'Inventory item is required.';
    } else if (seenInventoryItemIds.has(inventoryItemId)) {
      rowErrors.inventoryItemId = 'Each ingredient can appear only once.';
    } else {
      seenInventoryItemIds.add(inventoryItemId);
    }

    if (row.quantityRequired.trim() === '' || Number.isNaN(quantityRequired) || quantityRequired <= 0) {
      rowErrors.quantityRequired = 'Quantity required must be greater than zero.';
    }

    if (Object.keys(rowErrors).length > 0) {
      nextErrors[row.id] = rowErrors;
    }
  });

  return nextErrors;
};

const asIngredientRequest = (rows: RecipeDraftRow[]): MenuItemRecipeIngredientRequest[] =>
  rows.map(row => ({
    inventoryItemId: row.inventoryItemId.trim(),
    quantityRequired: Number.parseFloat(row.quantityRequired.trim()),
  }));

const sortInventoryItems = (items: InventoryItemListItem[]) =>
  [...items].sort((left, right) => {
    const byCategory = left.category.localeCompare(right.category);
    if (byCategory !== 0) {
      return byCategory;
    }

    return left.name.localeCompare(right.name);
  });

export const MenuItemRecipeSection = ({
  item,
  branchId,
  inventoryItems,
  inventoryLoading,
  inventoryError,
  recipe,
  loading,
  error,
  canManageItems,
  submitting,
  onSave,
  onRetry,
}: MenuItemRecipeSectionProps) => {
  const { t } = useLanguage();
  const [rows, setRows] = useState<RecipeDraftRow[]>([]);
  const [rowErrors, setRowErrors] = useState<Record<string, RecipeRowError>>({});
  const [saveError, setSaveError] = useState<string | null>(null);

  useEffect(() => {
    if (!item) {
      setRows([]);
      setRowErrors({});
      setSaveError(null);
      return;
    }

    const nextRows =
      recipe?.ingredients?.length && recipe.ingredients.length > 0
        ? recipe.ingredients.map(ingredient =>
            createDraftRow(ingredient.inventoryItemId, ingredient.quantityRequired.toString())
          )
        : canManageItems && branchId
          ? [createDraftRow()]
          : [];

    setRows(nextRows);
    setRowErrors({});
    setSaveError(null);
  }, [branchId, canManageItems, item?.menuItemId, recipe]);

  const inventoryOptions = useMemo(() => sortInventoryItems(inventoryItems), [inventoryItems]);
  const existingIngredients = recipe?.ingredients ?? [];
  const canEdit = Boolean(item && branchId && canManageItems);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    if (!item || !canEdit) {
      return;
    }

    const nextErrors = validateRows(rows);
    setRowErrors(nextErrors);

    if (Object.keys(nextErrors).length > 0) {
      setSaveError(t('menu.recipeFixBeforeSaving'));
      return;
    }

    setSaveError(null);

    try {
      await onSave({
        ingredients: rows.length > 0 ? asIngredientRequest(rows) : [],
      });
    } catch (caughtError) {
      if (caughtError instanceof Error && caughtError.message.trim()) {
        setSaveError(caughtError.message);
        return;
      }

      setSaveError(t('menu.recipeCouldNotSave'));
    }
  };

  if (!item) {
    return (
      <EmptyState
        title={t('menu.recipeSelectItemTitle')}
        description={t('menu.recipeSelectItemDescription')}
        tone="admin"
      />
    );
  }

  if (error) {
    return (
      <EmptyState
        title={t('menu.recipeUnableToLoadTitle')}
        description={error}
        actionLabel={onRetry ? t('menu.recipeRetryAction') : undefined}
        onAction={onRetry}
        tone="admin"
      />
    );
  }

  if (loading && existingIngredients.length === 0) {
    return <EmptyState title={t('menu.recipeLoadingTitle')} description={t('menu.recipeLoadingDescription')} tone="admin" />;
  }

  return (
    <div className="menu-item-recipe-section">
      <div className="menu-item-recipe-section__summary">
        <div className="menu-item-recipe-section__summary-title-row">
          <strong>{t('menu.recipeIngredientsTitle')}</strong>
          <Badge tone={canEdit ? 'success' : 'neutral'} label={canEdit ? t('menu.recipeEditableBadge') : t('menu.recipeReadOnlyBadge')} />
        </div>
        <div className="menu-item-recipe-section__summary-note">
          {branchId
            ? t('menu.recipeBranchNote').replace('{branchId}', branchId)
            : t('menu.recipeNoBranchNote')}
        </div>
      </div>

      {inventoryError ? (
        <div className="admin-notice admin-notice--danger" role="alert">
          {inventoryError}
        </div>
      ) : null}

      {existingIngredients.length > 0 ? (
        <ResponsiveDataList
          rows={existingIngredients.map(ingredient => ({
            id: ingredient.menuItemRecipeIngredientId,
            ingredient: ingredient.inventoryItemName,
            quantity: formatInventoryStock(ingredient.quantityRequired),
            updatedAt: ingredient.updatedAtUtc ? new Date(ingredient.updatedAtUtc).toLocaleString() : t('menu.notRecorded'), // uses newly added key
          }))}
          columns={[
            { key: 'ingredient', label: t('menu.recipeColumnIngredient') },
            { key: 'quantity', label: t('menu.recipeColumnQuantityRequired'), align: 'right' },
            { key: 'updatedAt', label: t('menu.recipeColumnUpdated') },
          ]}
          mobileTitle={row => row.ingredient}
          mobileDescription={row => row.quantity}
          emptyTitle={t('menu.recipeNoIngredientsYetTitle')}
          emptyDescription={t('menu.recipeNoIngredientsYetDescription')}
        />
      ) : (
        <EmptyState
          title={t('menu.recipeNoIngredientsYetTitle')}
          description={t('menu.noRecipeIngredientsAddDescription')}
          tone="admin"
        />
      )}

      {canEdit ? (
        <form className="admin-form" onSubmit={handleSubmit} noValidate>
          <div className="admin-form-section">
            <div className="admin-form-section__title-row">
              <div>
                <div className="admin-form-section__title">{t('menu.recipeEditIngredientsTitle')}</div>
                <div className="admin-form-section__description">
                  {t('menu.recipeEditIngredientsDescription')}
                </div>
              </div>
              <Button
                type="button"
                variant="secondary"
                onClick={() => setRows(current => [...current, createDraftRow()])}
                disabled={submitting || inventoryLoading}
              >
                {t('menu.recipeAddIngredientButton')}
              </Button>
            </div>

            {rows.length === 0 ? (
              <EmptyState
                title={t('menu.recipeNoEditableIngredientsTitle')}
                description={t('menu.recipeNoEditableIngredientsDescription')}
                tone="admin"
              />
            ) : null}

            <div className="admin-workspace-stack">
              {rows.map((row, index) => {
                const currentErrors = rowErrors[row.id] ?? {};
                return (
                  <div key={row.id} className="menu-item-recipe-section__row">
                    <div className="menu-item-recipe-section__row-header">
                      <strong>{t('menu.recipeIngredientHeader').replace('{n}', String(index + 1))}</strong>
                      <Button
                        type="button"
                        variant="secondary"
                        size="sm"
                        className="menu-item-recipe-section__remove-button"
                        onClick={() => setRows(current => current.filter(candidate => candidate.id !== row.id))}
                        disabled={submitting}
                      >
                        {t('menu.recipeRemoveButton')}
                      </Button>
                    </div>

                    <Select
                      label={t('menu.recipeInventoryItemLabel')}
                      value={row.inventoryItemId}
                      onChange={event =>
                        setRows(current =>
                          current.map(candidate =>
                            candidate.id === row.id
                              ? { ...candidate, inventoryItemId: event.target.value }
                              : candidate
                          )
                        )
                      }
                      error={currentErrors.inventoryItemId}
                      helperText={t('menu.recipeInventoryItemHelper')}
                      disabled={submitting || inventoryLoading || !branchId}
                    >
                      <option value="">{t('menu.recipeSelectInventoryItem')}</option>
                      {inventoryOptions.map(inventoryItem => (
                        <option key={inventoryItem.inventoryItemId} value={inventoryItem.inventoryItemId}>
                          {inventoryItem.name} ({inventoryItem.category})
                        </option>
                      ))}
                    </Select>

                    <Input
                      label={t('menu.recipeQuantityRequiredLabel')}
                      type="number"
                      min="0.01"
                      step="0.01"
                      value={row.quantityRequired}
                      onChange={event =>
                        setRows(current =>
                          current.map(candidate =>
                            candidate.id === row.id
                              ? { ...candidate, quantityRequired: event.target.value }
                              : candidate
                          )
                        )
                      }
                      error={currentErrors.quantityRequired}
                      helperText={t('menu.recipeQuantityRequiredHelper')}
                      disabled={submitting}
                    />
                  </div>
                );
              })}
            </div>
          </div>

          {saveError ? (
            <div className="admin-notice admin-notice--danger" role="alert">
              {saveError}
            </div>
          ) : null}

          <div className="admin-form-actions">
            <Button type="submit" size="lg" disabled={submitting || inventoryLoading || !branchId}>
              {submitting ? t('menu.recipeSavingButton') : t('menu.recipeSaveButton')}
            </Button>
          </div>
        </form>
      ) : (
        <div className="admin-form-note">
          {t('menu.recipeReadOnlyNote')}
        </div>
      )}

      {inventoryLoading ? (
        <div className="admin-form-note">{t('menu.recipeLoadingInventoryNote')}</div>
      ) : null}
    </div>
  );
};

export default MenuItemRecipeSection;
