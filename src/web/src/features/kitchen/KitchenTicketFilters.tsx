import { useLanguage } from '../../i18n/LanguageProvider';
import type { KitchenTicketQueueFilter } from './kitchenTicketTypes';

export interface KitchenTicketFiltersProps {
  selectedFilter: KitchenTicketQueueFilter;
  onChange: (filter: KitchenTicketQueueFilter) => void;
}

export const KitchenTicketFilters = ({ selectedFilter, onChange }: KitchenTicketFiltersProps) => {
  const { t } = useLanguage();

  const filterOptions: Array<{ value: KitchenTicketQueueFilter; label: string; description: string }> = [
    { value: 'Active', label: t('kitchen.filterActive'), description: t('kitchen.filterActiveDescription') },
    { value: 'Pending', label: t('kitchen.filterPending'), description: t('kitchen.filterPendingDescription') },
    { value: 'Preparing', label: t('kitchen.filterPreparing'), description: t('kitchen.filterPreparingDescription') },
    { value: 'Ready', label: t('kitchen.filterReady'), description: t('kitchen.filterReadyDescription') },
    { value: 'Served', label: t('kitchen.filterServed'), description: t('kitchen.filterServedDescription') },
    { value: 'Cancelled', label: t('kitchen.filterCancelled'), description: t('kitchen.filterCancelledDescription') },
    { value: 'All', label: t('kitchen.filterAll'), description: t('kitchen.filterAllDescription') },
  ];

  const filterLabel =
    selectedFilter === 'Pending'
      ? t('kitchen.filterPending')
      : selectedFilter === 'Preparing'
        ? t('kitchen.filterPreparing')
        : selectedFilter === 'Ready'
          ? t('kitchen.filterReady')
          : selectedFilter === 'Served'
            ? t('kitchen.filterServed')
            : selectedFilter === 'Cancelled'
              ? t('kitchen.filterCancelled')
              : t('kitchen.filterAll');

  const filterTitle =
    selectedFilter === 'Active'
      ? t('kitchen.activeQueue')
      : selectedFilter === 'All'
        ? t('kitchen.allTickets')
        : t('kitchen.filterTitleWithSuffix', { filter: filterLabel });

  return (
    <div className="kitchen-ticket-filters">
      <div className="kitchen-ticket-filters__title-row">
        <strong>{filterTitle}</strong>
        <span className="kitchen-ticket-filters__note">{t('kitchen.filterNote')}</span>
      </div>
      <div className="kitchen-ticket-filters__chips">
        {filterOptions.map(option => {
          const isSelected = option.value === selectedFilter;
          return (
            <button
              key={option.value}
              type="button"
              className={['segment-button', isSelected && 'segment-button--active', 'kitchen-ticket-filter-chip']
                .filter(Boolean)
                .join(' ')}
              aria-pressed={isSelected}
              aria-label={option.label}
              title={option.description}
              onClick={() => onChange(option.value)}
            >
              {option.label}
            </button>
          );
        })}
      </div>
    </div>
  );
};

export default KitchenTicketFilters;
