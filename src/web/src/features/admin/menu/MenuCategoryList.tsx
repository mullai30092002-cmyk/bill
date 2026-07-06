import { Button, EmptyState, ResponsiveDataList, StatusBadge } from '../../../components/ui';
import { useLanguage } from '../../../i18n/LanguageProvider';
import type { MenuCategory, MenuCategoryStatus } from '../adminTypes';
import { formatMenuTimestamp } from './menuDisplay';

interface MenuCategoryRow {
  id: string;
  menuCategoryId: string;
  name: string;
  displayOrder: number;
  itemCount: number | null;
  status: MenuCategoryStatus;
  updatedAt: string | null;
}

interface MenuCategoryListProps {
  categories: MenuCategory[];
  itemCounts: Record<string, number> | null;
  loading?: boolean;
  error?: string | null;
  canManageCategories: boolean;
  onRetry: () => void;
  onSelectCategory: (categoryId: string) => void;
}

export const MenuCategoryList = ({
  categories,
  itemCounts,
  loading,
  error,
  canManageCategories,
  onRetry,
  onSelectCategory,
}: MenuCategoryListProps) => {
  const { t } = useLanguage();

  if (loading && categories.length === 0) {
    return (
      <EmptyState
        title={t('menu.loadingCategoriesTitle')}
        description={t('menu.loadingCategoriesDescription')}
        tone="admin"
      />
    );
  }

  if (error) {
    return (
      <EmptyState
        title={t('menu.couldNotLoadCategoriesTitle')}
        description={error}
        tone="admin"
        actionLabel={t('menu.tryAgain')}
        onAction={onRetry}
      />
    );
  }

  const rows: MenuCategoryRow[] = categories.map(category => ({
    id: category.menuCategoryId,
    menuCategoryId: category.menuCategoryId,
    name: category.name,
    displayOrder: category.displayOrder,
    itemCount: itemCounts ? itemCounts[category.menuCategoryId] ?? null : null,
    status: category.status,
    updatedAt: category.updatedAt,
  }));

  return (
    <ResponsiveDataList
      rows={rows}
      columns={[
        { key: 'name', label: t('menu.columnCategory') },
        {
          key: 'displayOrder',
          label: t('menu.columnOrder'),
          align: 'right',
          render: row => row.displayOrder.toString(),
        },
        {
          key: 'itemCount',
          label: t('menu.columnItems'),
          align: 'right',
          render: row => (row.itemCount === null ? '—' : row.itemCount.toString()),
        },
        {
          key: 'status',
          label: t('menu.columnStatus'),
          render: row => <StatusBadge status={row.status} />,
        },
        {
          key: 'updatedAt',
          label: t('menu.columnUpdated'),
          render: row => formatMenuTimestamp(row.updatedAt),
        },
        ...(canManageCategories
          ? [
              {
                key: 'menuCategoryId' as const,
                label: t('menu.columnActions'),
                render: (row: MenuCategoryRow) => (
                  <Button
                    type="button"
                    variant="secondary"
                    size="md"
                    fullWidth
                    onClick={() => onSelectCategory(row.menuCategoryId)}
                  >
                    {t('menu.editButton')}
                  </Button>
                ),
              },
            ]
          : []),
      ]}
      mobileTitle={row => row.name}
      mobileDescription={row =>
        row.itemCount === null
          ? `Order ${row.displayOrder}`
          : `Order ${row.displayOrder} · ${row.itemCount} items`
      }
      emptyTitle={t('menu.noCategoriesFoundTitle')}
      emptyDescription={t('menu.noCategoriesFoundDescription')}
    />
  );
};

export default MenuCategoryList;
