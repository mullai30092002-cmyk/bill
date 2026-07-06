import { useLanguage } from '../../i18n/LanguageProvider';
import { Badge, EmptyState, ResponsiveDataList } from '../../components/ui';
import { formatInventoryStock } from '../inventory/inventoryDisplay';
import { formatKitchenTicketPreviewStatus } from './kitchenTicketDisplay';
import type { KitchenTicketDeductionPreviewResponse } from './kitchenTicketTypes';

export interface KitchenTicketDeductionPreviewPanelProps {
  preview: KitchenTicketDeductionPreviewResponse | null;
  loading: boolean;
  error: string | null;
  onRetry?: () => void;
}

const statusTone = (status?: string | null) => {
  const normalized = (status ?? '').trim().toLowerCase();
  if (normalized === 'insufficient') {
    return 'danger' as const;
  }

  if (normalized === 'norecipe') {
    return 'warning' as const;
  }

  return 'success' as const;
};

export const KitchenTicketDeductionPreviewPanel = ({ preview, loading, error, onRetry }: KitchenTicketDeductionPreviewPanelProps) => {
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
        title={t('kitchen.couldNotLoadDeductionPreviewTitle')}
        description={error}
        actionLabel={onRetry ? t('kitchen.tryAgain') : undefined}
        onAction={onRetry}
        tone="orders"
      />
    );
  }

  if (loading && !preview) {
    return <EmptyState title={t('kitchen.loadingDeductionPreviewTitle')} description={t('kitchen.loadingDeductionPreviewDescription')} tone="orders" />;
  }

  if (!preview) {
    return (
      <EmptyState
        title={t('kitchen.noTicketSelectedForPreviewTitle')}
        description={t('kitchen.noTicketSelectedForPreviewDescription')}
        tone="orders"
      />
    );
  }

  if (preview.lines.length === 0) {
    return (
      <div className="kitchen-ticket-deduction-preview">
        <div className="kitchen-ticket-deduction-preview__summary">
          <strong>{t('kitchen.canCompleteLabel')}:</strong>{' '}
          <Badge tone={preview.canComplete ? 'success' : 'danger'} label={preview.canComplete ? t('kitchen.yes') : t('kitchen.no')} />
        </div>
        <EmptyState
          title={t('kitchen.noRecipeBasedStockUsageTitle')}
          description={t('kitchen.noRecipeBasedStockUsageDescription')}
          tone="orders"
        />
      </div>
    );
  }

  return (
    <div className="kitchen-ticket-deduction-preview">
      <div className="kitchen-ticket-deduction-preview__summary">
        <strong>{t('kitchen.canCompleteLabel')}:</strong>{' '}
        <Badge tone={preview.canComplete ? 'success' : 'danger'} label={preview.canComplete ? t('kitchen.yes') : t('kitchen.no')} />
      </div>

      <ResponsiveDataList
        rows={preview.lines.map((line, index) => ({
          id: `${preview.ticketId}-${index}`,
          menuItemName: line.menuItemName,
          inventoryItemName: line.inventoryItemName,
          requiredQuantity: formatInventoryStock(line.requiredQuantity),
          availableQuantity: formatInventoryStock(line.availableQuantity),
          resultingQuantity: formatInventoryStock(line.resultingQuantity),
          status: line.status,
        }))}
        columns={[
          { key: 'menuItemName', label: t('kitchen.menuItemLabel') },
          { key: 'inventoryItemName', label: t('kitchen.ingredientLabel') },
          { key: 'requiredQuantity', label: t('kitchen.requiredLabel'), align: 'right' },
          { key: 'availableQuantity', label: t('kitchen.availableLabel'), align: 'right' },
          { key: 'resultingQuantity', label: t('kitchen.resultingLabel'), align: 'right' },
          {
            key: 'status',
            label: t('kitchen.statusLabel'),
            render: row => (
              <Badge
                tone={statusTone(row.status)}
                label={formatKitchenTicketPreviewStatus(row.status, messages)}
              />
            ),
          },
        ]}
        mobileTitle={row => row.menuItemName}
        mobileDescription={row => row.inventoryItemName}
        emptyTitle={t('kitchen.noDeductionPreviewTitle')}
        emptyDescription={t('kitchen.noDeductionPreviewDescription')}
      />
    </div>
  );
};

export default KitchenTicketDeductionPreviewPanel;
