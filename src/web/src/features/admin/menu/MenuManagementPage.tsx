import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from 'react';

import { getSafeApiErrorMessage, isApiError } from '../../../api/apiErrors';
import { AdminLayout } from '../../../components/layout';
import type { ShellNavItem } from '../../../components/layout/navigation';
import { Badge, Button, Card, EmptyState, SummaryCard } from '../../../components/ui';
import { useAuth } from '../../auth/useAuth';
import { useRestaurantCurrency } from '../../auth/useRestaurantCurrency';
import { useLanguage } from '../../../i18n/LanguageProvider';
import {
  activateMenuCategory,
  activateMenuItem,
  createMenuCategory,
  createMenuItem,
  deactivateMenuCategory,
  deactivateMenuItem,
  getMenuCategory,
  getMenuItem,
  getMenuItemRecipe,
  getMenuItemPriceHistory,
  listMenuCategories,
  listMenuItems,
  updateMenuCategory,
  updateMenuItem,
  updateMenuItemRecipe,
} from '../adminApi';
import type {
  MenuCategory,
  MenuCategoryStatus,
  MenuItem,
  MenuItemPriceHistory,
  MenuItemRecipeResponse,
  MenuItemStatus,
  UpdateMenuItemRecipeRequest,
} from '../adminTypes';
import MenuCategoryForm from './MenuCategoryForm';
import MenuCategoryList from './MenuCategoryList';
import MenuCategoryStatusActions from './MenuCategoryStatusActions';
import MenuItemRecipeSection from './MenuItemRecipeSection';
import MenuItemForm, { type MenuItemCategoryOption, type MenuItemInventoryOption } from './MenuItemForm';
import MenuItemList from './MenuItemList';
import MenuItemPriceHistoryPanel from './MenuItemPriceHistoryPanel';
import MenuItemStatusActions from './MenuItemStatusActions';
import MenuImportPanel from './MenuImportPanel';
import {
  buildCreateMenuCategoryRequest,
  buildCreateMenuItemRequest,
  buildMenuCategoryFormErrors,
  buildMenuItemFormErrors,
  buildUpdateMenuCategoryRequest,
  buildUpdateMenuItemRequest,
  emptyMenuCategoryForm,
  emptyMenuItemForm,
  type MenuCategoryFormErrors,
  type MenuCategoryFormState,
  type MenuItemFormErrors,
  type MenuItemFormState,
} from './menuFormValidation';
import {
  buildCategoryItemCounts,
  formatMenuPrice,
  formatMenuTimestamp,
  sortMenuCategories,
  sortMenuItems,
} from './menuDisplay';
import { listInventoryItems } from '../../inventory/inventoryApi';
import type { InventoryItemListItem } from '../../inventory/inventoryTypes';

export interface MenuManagementPageProps {
  navItems?: ShellNavItem[];
  restaurantName?: string;
  branchName?: string;
  operatorLabel?: string;
}

type NoticeTone = 'success' | 'info' | 'warning' | 'danger';

interface Notice {
  tone: NoticeTone;
  message: string;
}

const resolveSafeMessage = (error: unknown, fallback: string) => getSafeApiErrorMessage(error, fallback);

const buildMenuItemCategoryOptions = (
  categories: MenuCategory[],
  selectedCategoryId: string | null,
  labels: { selectCategory: string; inactive: (name: string) => string; unavailable: string }
): MenuItemCategoryOption[] => {
  const activeOptions = sortMenuCategories(categories)
    .filter(category => category.status === 'Active')
    .map(category => ({
      value: category.menuCategoryId,
      label: category.name,
    }));

  const emptyOption: MenuItemCategoryOption = {
    value: '',
    label: labels.selectCategory,
  };

  if (!selectedCategoryId || activeOptions.some(option => option.value === selectedCategoryId)) {
    return [emptyOption, ...activeOptions];
  }

  const selectedCategory = categories.find(category => category.menuCategoryId === selectedCategoryId);
  if (selectedCategory) {
    return [
      emptyOption,
      ...activeOptions,
      {
        value: selectedCategory.menuCategoryId,
        label: labels.inactive(selectedCategory.name),
        disabled: true,
      },
    ];
  }

  return [
    emptyOption,
    ...activeOptions,
    {
      value: selectedCategoryId,
      label: labels.unavailable,
      disabled: true,
    },
  ];
};

const buildMenuItemInventoryOptions = (
  items: InventoryItemListItem[],
  selectedInventoryItemId: string | null,
  labels: { selectItem: string; inactive: (name: string) => string; unavailable: string }
): MenuItemInventoryOption[] => {
  const activeOptions = items
    .filter(item => item.isActive)
    .map(item => ({
      value: item.inventoryItemId,
      label: `${item.name} (${item.category})`,
    }));

  const emptyOption: MenuItemInventoryOption = {
    value: '',
    label: labels.selectItem,
  };

  if (!selectedInventoryItemId || activeOptions.some(option => option.value === selectedInventoryItemId)) {
    return [emptyOption, ...activeOptions];
  }

  const selectedInventoryItem = items.find(item => item.inventoryItemId === selectedInventoryItemId);
  if (selectedInventoryItem) {
    return [
      emptyOption,
      ...activeOptions,
      {
        value: selectedInventoryItem.inventoryItemId,
        label: labels.inactive(selectedInventoryItem.name),
        disabled: true,
      },
    ];
  }

  return [
    emptyOption,
    ...activeOptions,
    {
      value: selectedInventoryItemId,
      label: labels.unavailable,
      disabled: true,
    },
  ];
};

export const MenuManagementPage = ({
  navItems,
  restaurantName,
  branchName,
  operatorLabel,
}: MenuManagementPageProps) => {
  const auth = useAuth();
  const { currencyCode, locale } = useRestaurantCurrency();
  const { t } = useLanguage();
  const branchId = auth.session?.branchId ?? null;
  const canManageCategories = auth.hasPermission('MenuCategory.Manage');
  const canManageItems = auth.hasPermission('MenuItem.Manage');
  const canViewItems = canManageItems || auth.hasPermission('MenuItem.View');
  const canAccessMenu = canManageCategories || canManageItems || canViewItems;

  const [categories, setCategories] = useState<MenuCategory[]>([]);
  const [categoriesLoading, setCategoriesLoading] = useState(canAccessMenu);
  const [categoriesError, setCategoriesError] = useState<string | null>(null);
  const [items, setItems] = useState<MenuItem[]>([]);
  const [itemsLoading, setItemsLoading] = useState(canViewItems);
  const [itemsError, setItemsError] = useState<string | null>(null);
  const [inventoryItems, setInventoryItems] = useState<InventoryItemListItem[]>([]);
  const [inventoryLoading, setInventoryLoading] = useState(canManageItems && Boolean(branchId));
  const [inventoryError, setInventoryError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [categoryFilter, setCategoryFilter] = useState('');
  const [statusFilter, setStatusFilter] = useState<'All' | MenuItemStatus>('All');
  const [availabilityFilter, setAvailabilityFilter] = useState<'All' | 'EatIn' | 'Parcel'>('All');
  const [notice, setNotice] = useState<Notice | null>(null);
  const [categoryScrollToken, setCategoryScrollToken] = useState(0);
  const [itemScrollToken, setItemScrollToken] = useState(0);
  const [importScrollToken, setImportScrollToken] = useState(0);

  const [categoryMode, setCategoryMode] = useState<'create' | 'edit'>('create');
  const [selectedCategoryId, setSelectedCategoryId] = useState<string | null>(null);
  const [selectedCategory, setSelectedCategory] = useState<MenuCategory | null>(null);
  const [selectedCategoryLoading, setSelectedCategoryLoading] = useState(false);
  const [selectedCategoryError, setSelectedCategoryError] = useState<string | null>(null);
  const [categoryForm, setCategoryForm] = useState<MenuCategoryFormState>(() => emptyMenuCategoryForm());
  const [categoryErrors, setCategoryErrors] = useState<MenuCategoryFormErrors>({});
  const [categorySubmitting, setCategorySubmitting] = useState(false);
  const [categoryConfirmDeactivate, setCategoryConfirmDeactivate] = useState(false);

  const [itemMode, setItemMode] = useState<'create' | 'edit' | 'view'>('create');
  const [selectedItemId, setSelectedItemId] = useState<string | null>(null);
  const [selectedItem, setSelectedItem] = useState<MenuItem | null>(null);
  const [selectedItemLoading, setSelectedItemLoading] = useState(false);
  const [selectedItemError, setSelectedItemError] = useState<string | null>(null);
  const [itemForm, setItemForm] = useState<MenuItemFormState>(() => emptyMenuItemForm());
  const [itemErrors, setItemErrors] = useState<MenuItemFormErrors>({});
  const [itemSubmitting, setItemSubmitting] = useState(false);
  const [itemConfirmDeactivate, setItemConfirmDeactivate] = useState(false);
  const [priceHistory, setPriceHistory] = useState<MenuItemPriceHistory[]>([]);
  const [priceHistoryLoading, setPriceHistoryLoading] = useState(false);
  const [priceHistoryError, setPriceHistoryError] = useState<string | null>(null);
  const [recipe, setRecipe] = useState<MenuItemRecipeResponse | null>(null);
  const [recipeLoading, setRecipeLoading] = useState(false);
  const [recipeError, setRecipeError] = useState<string | null>(null);
  const [recipeSubmitting, setRecipeSubmitting] = useState(false);
  const [showImportPanel, setShowImportPanel] = useState(false);
  const categoryFormSectionRef = useRef<HTMLDivElement | null>(null);
  const categoryNameInputRef = useRef<HTMLInputElement | null>(null);
  const itemFormSectionRef = useRef<HTMLDivElement | null>(null);
  const itemCategorySelectRef = useRef<HTMLSelectElement | null>(null);
  const importPanelSectionRef = useRef<HTMLDivElement | null>(null);
  const importTextareaRef = useRef<HTMLTextAreaElement | null>(null);

  const requestCategoryScroll = useCallback(() => {
    setCategoryScrollToken(token => token + 1);
  }, []);

  const requestItemScroll = useCallback(() => {
    setItemScrollToken(token => token + 1);
  }, []);

  const requestImportScroll = useCallback(() => {
    setImportScrollToken(token => token + 1);
  }, []);

  const refreshCategories = useCallback(async () => {
    if (!canAccessMenu) {
      return;
    }

    setCategoriesLoading(true);
    setCategoriesError(null);

    try {
      const response = await listMenuCategories();
      setCategories(sortMenuCategories(response.items));
    } catch (caughtError) {
      const message = resolveSafeMessage(caughtError, 'Could not load menu categories right now.');
      setCategoriesError(message);
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
      throw caughtError;
    } finally {
      setCategoriesLoading(false);
    }
  }, [auth, canAccessMenu]);

  const refreshItems = useCallback(async () => {
    if (!canViewItems) {
      return;
    }

    setItemsLoading(true);
    setItemsError(null);

    try {
      const response = await listMenuItems();
      setItems(sortMenuItems(response.items));
    } catch (caughtError) {
      const message = resolveSafeMessage(caughtError, 'Could not load menu items right now.');
      setItemsError(message);
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
      throw caughtError;
    } finally {
      setItemsLoading(false);
    }
  }, [auth, canViewItems]);

  const refreshSelectedCategory = useCallback(
    async (categoryId: string) => {
      setSelectedCategoryLoading(true);
      setSelectedCategoryError(null);

      try {
        const detail = await getMenuCategory(categoryId);
        setSelectedCategory(detail);
        return detail;
      } catch (caughtError) {
        const message = resolveSafeMessage(caughtError, 'Could not load the selected category.');
        setSelectedCategoryError(message);
        setSelectedCategory(null);
        if (isApiError(caughtError) && caughtError.status === 401) {
          void auth.logout();
        }
        throw caughtError;
      } finally {
        setSelectedCategoryLoading(false);
      }
    },
    [auth]
  );

  const refreshSelectedItem = useCallback(
    async (itemId: string) => {
      setSelectedItemLoading(true);
      setSelectedItemError(null);

      try {
        const detail = await getMenuItem(itemId);
        setSelectedItem(detail);
        return detail;
      } catch (caughtError) {
        const message = resolveSafeMessage(caughtError, 'Could not load the selected item.');
        setSelectedItemError(message);
        setSelectedItem(null);
        if (isApiError(caughtError) && caughtError.status === 401) {
          void auth.logout();
        }
        throw caughtError;
      } finally {
        setSelectedItemLoading(false);
      }
    },
    [auth]
  );

  const refreshPriceHistory = useCallback(
    async (itemId: string) => {
      setPriceHistoryLoading(true);
      setPriceHistoryError(null);

      try {
        const response = await getMenuItemPriceHistory(itemId);
        setPriceHistory(response.items);
        return response.items;
      } catch (caughtError) {
        const message = resolveSafeMessage(caughtError, 'Could not load price history right now.');
        setPriceHistoryError(message);
        setPriceHistory([]);
        if (isApiError(caughtError) && caughtError.status === 401) {
          void auth.logout();
        }
        throw caughtError;
      } finally {
        setPriceHistoryLoading(false);
      }
    },
    [auth]
  );

  const refreshInventoryItems = useCallback(async () => {
    if (!canManageItems || !branchId) {
      setInventoryItems([]);
      setInventoryError(null);
      setInventoryLoading(false);
      return;
    }

    setInventoryLoading(true);
    setInventoryError(null);

    try {
      const response = await listInventoryItems({ branchId });
      setInventoryItems(response.items);
    } catch (caughtError) {
      const message = resolveSafeMessage(caughtError, 'Could not load branch inventory items right now.');
      setInventoryError(message);
      setInventoryItems([]);
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
      throw caughtError;
    } finally {
      setInventoryLoading(false);
    }
  }, [auth, branchId, canManageItems]);

  const refreshSelectedRecipe = useCallback(
    async (itemId: string) => {
      setRecipeLoading(true);
      setRecipeError(null);

      try {
        const response = await getMenuItemRecipe(itemId);
        setRecipe(response);
        return response;
      } catch (caughtError) {
        const message = resolveSafeMessage(caughtError, 'Could not load the recipe right now.');
        setRecipeError(message);
        setRecipe(null);
        if (isApiError(caughtError) && caughtError.status === 401) {
          void auth.logout();
        }
        throw caughtError;
      } finally {
        setRecipeLoading(false);
      }
    },
    [auth]
  );

  useEffect(() => {
    if (!canAccessMenu) {
      return;
    }

    void refreshCategories().catch(() => undefined);
  }, [canAccessMenu, refreshCategories]);

  useEffect(() => {
    if (!canViewItems) {
      return;
    }

    void refreshItems().catch(() => undefined);
  }, [canViewItems, refreshItems]);

  useEffect(() => {
    if (!canManageCategories || categoryMode !== 'edit' || !selectedCategoryId) {
      setSelectedCategory(null);
      setSelectedCategoryError(null);
      setSelectedCategoryLoading(false);
      setCategoryConfirmDeactivate(false);
      return;
    }

    void refreshSelectedCategory(selectedCategoryId).catch(() => undefined);
  }, [canManageCategories, categoryMode, refreshSelectedCategory, selectedCategoryId]);

  useEffect(() => {
    if (!selectedCategory) {
      return;
    }

    setCategoryForm({
      name: selectedCategory.name,
      displayOrder: selectedCategory.displayOrder.toString(),
    });
    setCategoryErrors({});
    setCategoryConfirmDeactivate(false);
  }, [selectedCategory]);

  useEffect(() => {
    if (!canViewItems || itemMode === 'create' || !selectedItemId) {
      setSelectedItem(null);
      setSelectedItemError(null);
      setSelectedItemLoading(false);
      setPriceHistory([]);
      setPriceHistoryError(null);
      setPriceHistoryLoading(false);
      setItemConfirmDeactivate(false);
      return;
    }

    void refreshSelectedItem(selectedItemId).catch(() => undefined);
  }, [canViewItems, itemMode, refreshSelectedItem, selectedItemId]);

  useEffect(() => {
    if (!selectedItem) {
      return;
    }

    setItemForm({
      menuCategoryId: selectedItem.menuCategoryId,
      name: selectedItem.name,
      description: selectedItem.description ?? '',
      sku: selectedItem.sku ?? '',
      basePrice: selectedItem.basePrice.toString(),
      taxRate: selectedItem.taxRate.toString(),
      isVegetarian: selectedItem.isVegetarian,
      isAvailableForEatIn: selectedItem.isAvailableForEatIn,
      isAvailableForParcel: selectedItem.isAvailableForParcel,
      inventoryDeductionMode: selectedItem.inventoryDeductionMode,
      stockInventoryItemId: selectedItem.stockInventoryItemId ?? '',
    });
    setItemErrors({});
    setItemConfirmDeactivate(false);
  }, [selectedItem]);

  useEffect(() => {
    if (!canViewItems || itemMode === 'create' || !selectedItemId) {
      return;
    }

    void refreshPriceHistory(selectedItemId).catch(() => undefined);
  }, [canViewItems, itemMode, refreshPriceHistory, selectedItemId]);

  useEffect(() => {
    if (!canManageItems || !branchId) {
      setInventoryItems([]);
      setInventoryError(null);
      setInventoryLoading(false);
      return;
    }

    void refreshInventoryItems().catch(() => undefined);
  }, [branchId, canManageItems, refreshInventoryItems]);

  useEffect(() => {
    if (!canViewItems || itemMode === 'create' || !selectedItemId || !branchId) {
      setRecipe(null);
      setRecipeError(null);
      setRecipeLoading(false);
      return;
    }

    void refreshSelectedRecipe(selectedItemId).catch(() => undefined);
  }, [canViewItems, itemMode, refreshSelectedRecipe, selectedItemId]);

  const categoryItemCounts = useMemo(() => buildCategoryItemCounts(items), [items]);
  const activeCategoryCount = useMemo(
    () => categories.filter(category => category.status === 'Active').length,
    [categories]
  );
  const inactiveCategoryCount = useMemo(
    () => categories.filter(category => category.status === 'Inactive').length,
    [categories]
  );
  const activeItemCount = useMemo(() => items.filter(item => item.status === 'Active').length, [items]);
  const vegetarianItemCount = useMemo(() => items.filter(item => item.isVegetarian).length, [items]);
  const filteredItems = useMemo(() => {
    const normalizedSearch = search.trim().toLowerCase();

    return sortMenuItems(
      items.filter(item => {
        if (statusFilter !== 'All' && item.status !== statusFilter) {
          return false;
        }

        if (categoryFilter && item.menuCategoryId !== categoryFilter) {
          return false;
        }

        if (availabilityFilter === 'EatIn' && !item.isAvailableForEatIn) {
          return false;
        }

        if (availabilityFilter === 'Parcel' && !item.isAvailableForParcel) {
          return false;
        }

        if (!normalizedSearch) {
          return true;
        }

        return [item.name, item.description ?? '', item.sku ?? '', item.categoryName].some(value =>
          value.toLowerCase().includes(normalizedSearch)
        );
      })
    );
  }, [availabilityFilter, categoryFilter, items, search, statusFilter]);

  const categoryOptionLabels = useMemo((): Parameters<typeof buildMenuItemCategoryOptions>[2] => ({
    selectCategory: t('menu.selectCategory'),
    inactive: (name: string) => `${name} ${t('menu.inactiveSuffix')}`,
    unavailable: t('menu.currentCategoryUnavailable'),
  }), [t]);

  const categoryOptions = useMemo(
    () => buildMenuItemCategoryOptions(categories, selectedItem?.menuCategoryId ?? null, categoryOptionLabels),
    [categories, selectedItem?.menuCategoryId, categoryOptionLabels]
  );

  const inventoryOptionLabels = useMemo(
    (): Parameters<typeof buildMenuItemInventoryOptions>[2] => ({
      selectItem: t('menu.selectStockInventoryItem'),
      inactive: (name: string) => `${name} ${t('menu.inactiveSuffix')}`,
      unavailable: t('menu.currentInventoryItemUnavailable'),
    }),
    [t]
  );

  const inventoryOptions = useMemo(
    () => buildMenuItemInventoryOptions(inventoryItems, itemForm.stockInventoryItemId || selectedItem?.stockInventoryItemId || null, inventoryOptionLabels),
    [inventoryItems, itemForm.stockInventoryItemId, inventoryOptionLabels, selectedItem?.stockInventoryItemId]
  );

  const openCreateCategory = useCallback(() => {
    setCategoryMode('create');
    setSelectedCategoryId(null);
    setSelectedCategory(null);
    setSelectedCategoryError(null);
    setCategoryConfirmDeactivate(false);
    setCategoryForm(emptyMenuCategoryForm());
    setCategoryErrors({});
    setNotice(null);
    requestCategoryScroll();
  }, [requestCategoryScroll]);

  const openEditCategory = useCallback((categoryId: string) => {
    setCategoryMode('edit');
    setSelectedCategoryId(categoryId);
    setSelectedCategoryError(null);
    setNotice(null);
    requestCategoryScroll();
  }, [requestCategoryScroll]);

  const openCreateItem = useCallback(() => {
    setItemMode('create');
    setSelectedItemId(null);
    setSelectedItem(null);
    setSelectedItemError(null);
    setRecipe(null);
    setRecipeError(null);
    setRecipeLoading(false);
    setItemConfirmDeactivate(false);
    setPriceHistory([]);
    setPriceHistoryError(null);
    setItemForm(emptyMenuItemForm());
    setItemErrors({});
    setNotice(null);
    requestItemScroll();
  }, [requestItemScroll]);

  const openImportPanel = useCallback(() => {
    setShowImportPanel(true);
    setNotice(null);
    requestImportScroll();
  }, [requestImportScroll]);

  const closeImportPanel = useCallback(() => {
    setShowImportPanel(false);
  }, []);

  const handleImportCompleted = useCallback(async () => {
    try {
      await refreshCategories();
      if (canViewItems) {
        await refreshItems();
      }
      setNotice({
        tone: 'success',
        message: t('menu.importedMenuRows'),
      });
    } catch {
      setNotice({
        tone: 'warning',
        message: t('menu.importRefreshFailed'),
      });
    }
  }, [canViewItems, refreshCategories, refreshItems]);

  const openSelectItem = useCallback(
    (itemId: string) => {
      setItemMode(canManageItems ? 'edit' : 'view');
      setSelectedItemId(itemId);
      setSelectedItemError(null);
      setRecipe(null);
      setRecipeError(null);
      setRecipeLoading(false);
      setNotice(null);
      requestItemScroll();
    },
    [canManageItems, requestItemScroll]
  );

  useEffect(() => {
    if (categoryScrollToken === 0) {
      return;
    }

    const target = categoryFormSectionRef.current;
    const input = categoryNameInputRef.current;

    if (!target || !input) {
      return;
    }

    if (typeof target.scrollIntoView === 'function') {
      target.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
    input.focus();
  }, [categoryScrollToken, categoryMode, selectedCategory]);

  useEffect(() => {
    if (itemScrollToken === 0) {
      return;
    }

    const target = itemFormSectionRef.current;
    const select = itemCategorySelectRef.current;

    if (!target || !select) {
      return;
    }

    if (typeof target.scrollIntoView === 'function') {
      target.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
    select.focus();
  }, [itemScrollToken, itemMode, selectedItem]);

  useEffect(() => {
    if (importScrollToken === 0 || !showImportPanel) {
      return;
    }

    const target = importPanelSectionRef.current;
    const textarea = importTextareaRef.current;

    if (!target || !textarea) {
      return;
    }

    if (typeof target.scrollIntoView === 'function') {
      target.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
    textarea.focus();
  }, [importScrollToken, showImportPanel]);

  const handleCategorySubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const nextErrors = buildMenuCategoryFormErrors(categoryForm);
    setCategoryErrors(nextErrors);

    if (Object.keys(nextErrors).length > 0) {
      setNotice({
        tone: 'warning',
        message: t('menu.noticeFixCategoryBeforeSaving'),
      });
      return;
    }

    setCategorySubmitting(true);
    setNotice(null);

    try {
      if (categoryMode === 'create') {
        const created = await createMenuCategory(buildCreateMenuCategoryRequest(categoryForm));
        await refreshCategories();
        if (canViewItems) {
          await refreshItems();
        }
        setSelectedCategoryId(null);
        setSelectedCategory(null);
        setCategoryMode('create');
        setCategoryForm(emptyMenuCategoryForm());
        setCategoryErrors({});
        setNotice({
          tone: 'success',
          message: t('menu.createdCategory').replace('{name}', created.name),
        });
        return;
      }

      if (!selectedCategoryId) {
        return;
      }

      const updated = await updateMenuCategory(selectedCategoryId, buildUpdateMenuCategoryRequest(categoryForm));
      setSelectedCategory(updated);
      await refreshCategories();
      if (canViewItems) {
        await refreshItems();
        if (selectedItemId) {
          await refreshSelectedItem(selectedItemId);
        }
      }
      setNotice({
        tone: 'success',
        message: t('menu.savedCategoryChanges').replace('{name}', updated.name),
      });
    } catch (caughtError) {
      const fallback =
        categoryMode === 'create' ? 'Could not create the category right now.' : 'Could not save the category right now.';
      const message = resolveSafeMessage(caughtError, fallback);
      setNotice({ tone: 'danger', message });
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setCategorySubmitting(false);
    }
  };

  const handleActivateCategory = async () => {
    if (!selectedCategoryId) {
      return;
    }

    setCategorySubmitting(true);
    setNotice(null);

    try {
      const updated = await activateMenuCategory(selectedCategoryId);
      setSelectedCategory(updated);
      await refreshCategories();
      if (canViewItems) {
        await refreshItems();
      }
      setNotice({
        tone: 'success',
        message: t('menu.categoryIsNowActive').replace('{name}', updated.name),
      });
    } catch (caughtError) {
      const message = resolveSafeMessage(caughtError, 'Could not activate the category right now.');
      setNotice({ tone: 'danger', message });
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setCategorySubmitting(false);
    }
  };

  const handleDeactivateCategory = async () => {
    if (!selectedCategoryId) {
      return;
    }

    setCategorySubmitting(true);
    setNotice(null);

    try {
      const updated = await deactivateMenuCategory(selectedCategoryId);
      setSelectedCategory(updated);
      setCategoryConfirmDeactivate(false);
      await refreshCategories();
      if (canViewItems) {
        await refreshItems();
      }
      setNotice({
        tone: 'success',
        message: t('menu.categoryIsNowInactive').replace('{name}', updated.name),
      });
    } catch (caughtError) {
      const message = resolveSafeMessage(
        caughtError,
        t('menu.cannotDeactivateCategoryWithItems')
      );
      setNotice({ tone: 'danger', message });
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setCategorySubmitting(false);
    }
  };

  const handleItemSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const nextErrors = buildMenuItemFormErrors(itemForm, branchId, {
      inventoryDeductionModeRequired: t('menu.inventoryDeductionModeRequired'),
      stockInventoryItemRequired: t('menu.stockInventoryItemRequired'),
      stockInventoryItemBranchRequired: t('menu.stockInventoryItemBranchRequired'),
    });
    setItemErrors(nextErrors);

    if (Object.keys(nextErrors).length > 0) {
      setNotice({
        tone: 'warning',
        message: t('menu.noticeFixItemBeforeSaving'),
      });
      return;
    }

    setItemSubmitting(true);
    setNotice(null);

    try {
      if (itemMode === 'create') {
        const created = await createMenuItem(buildCreateMenuItemRequest(itemForm));
        await refreshItems();
        await refreshCategories();
        setSelectedItemId(null);
        setSelectedItem(null);
        setItemMode('create');
        setItemForm(emptyMenuItemForm());
        setItemErrors({});
        setPriceHistory([]);
        setPriceHistoryError(null);
        setNotice({
          tone: 'success',
          message: t('menu.createdItem').replace('{name}', created.name),
        });
        return;
      }

      if (!selectedItemId) {
        return;
      }

      const updated = await updateMenuItem(selectedItemId, buildUpdateMenuItemRequest(itemForm));
      setSelectedItem(updated);
      await Promise.all([
        refreshItems(),
        refreshCategories(),
        refreshPriceHistory(updated.menuItemId),
      ]);
      setNotice({
        tone: 'success',
        message: t('menu.savedItemChanges').replace('{name}', updated.name),
      });
    } catch (caughtError) {
      const fallback = itemMode === 'create' ? 'Could not create the item right now.' : 'Could not save the item right now.';
      const message = resolveSafeMessage(caughtError, fallback);
      setNotice({ tone: 'danger', message });
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setItemSubmitting(false);
    }
  };

  const handleActivateItem = async () => {
    if (!selectedItemId) {
      return;
    }

    setItemSubmitting(true);
    setNotice(null);

    try {
      const updated = await activateMenuItem(selectedItemId);
      setSelectedItem(updated);
      setItemConfirmDeactivate(false);
      await refreshItems();
      await refreshCategories();
      await refreshPriceHistory(updated.menuItemId);
      setNotice({
        tone: 'success',
        message: t('menu.itemIsNowActive').replace('{name}', updated.name),
      });
    } catch (caughtError) {
      const message = resolveSafeMessage(caughtError, 'Could not activate the item right now.');
      setNotice({ tone: 'danger', message });
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setItemSubmitting(false);
    }
  };

  const handleDeactivateItem = async () => {
    if (!selectedItemId) {
      return;
    }

    setItemSubmitting(true);
    setNotice(null);

    try {
      const updated = await deactivateMenuItem(selectedItemId);
      setSelectedItem(updated);
      setItemConfirmDeactivate(false);
      await refreshItems();
      await refreshCategories();
      await refreshPriceHistory(updated.menuItemId);
      setNotice({
        tone: 'success',
        message: t('menu.itemIsNowInactive').replace('{name}', updated.name),
      });
    } catch (caughtError) {
      const message = resolveSafeMessage(caughtError, 'Could not deactivate the item right now.');
      setNotice({ tone: 'danger', message });
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setItemSubmitting(false);
    }
  };

  const handleRecipeSave = async (request: UpdateMenuItemRecipeRequest) => {
    if (!selectedItemId) {
      return;
    }

    setRecipeSubmitting(true);
    setNotice(null);

    try {
      const updated = await updateMenuItemRecipe(selectedItemId, request);
      setRecipe(updated);
      setNotice({
        tone: 'success',
        message: t('menu.savedRecipeIngredients').replace('{name}', updated.menuItemName),
      });
    } catch (caughtError) {
      const message = resolveSafeMessage(caughtError, 'Could not save the recipe ingredients.');
      setNotice({ tone: 'danger', message });
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setRecipeSubmitting(false);
    }
  };

  if (!canAccessMenu) {
    return (
      <AdminLayout
        title={t('menu.pageTitle')}
        description={t('menu.pageDescriptionCreate')}
        breadcrumbs={[t('menu.breadcrumbDashboard'), t('menu.breadcrumbAdmin'), t('menu.breadcrumbMenu')]}
        operatorLabel={operatorLabel}
        restaurantName={restaurantName}
        branchName={branchName}
        navItems={navItems}
      >
        <EmptyState
          title={t('menu.notAuthorizedTitle')}
          description={t('menu.notAuthorizedDescription')}
          tone="admin"
        />
      </AdminLayout>
    );
  }

  const categoryPanelTitle = categoryMode === 'create' ? t('menu.createCategoryPanelTitle') : selectedCategory?.name ?? t('menu.editCategoryPanelTitle');
  const itemPanelTitle =
    itemMode === 'create' ? t('menu.createItemPanelTitle') : selectedItem?.name ?? (itemMode === 'view' ? t('menu.itemDetailsPanelTitle') : t('menu.editItemPanelTitle'));
  const itemPanelDescription =
    itemMode === 'create'
      ? t('menu.createItemPanelDescription')
      : itemMode === 'view'
        ? t('menu.viewItemPanelDescription')
        : t('menu.editItemPanelDescription');
  const filteredItemCount = filteredItems.length;
  const selectedItemTitle = selectedItem ? selectedItem.name : t('menu.chooseItemTitle');

  return (
    <AdminLayout
      title={t('menu.pageTitle')}
      description={t('menu.pageDescriptionFull')}
      breadcrumbs={[t('menu.breadcrumbDashboard'), t('menu.breadcrumbAdmin'), t('menu.breadcrumbMenu')]}
      operatorLabel={operatorLabel}
      restaurantName={restaurantName}
      branchName={branchName}
      navItems={navItems}
        actions={
          <>
            <Button variant="secondary" onClick={() => void refreshCategories().catch(() => undefined)}>
              {t('menu.refreshList')}
            </Button>
            {canManageItems ? <Button variant="secondary" onClick={openImportPanel}>{t('menu.importMenu')}</Button> : null}
            {canManageCategories ? <Button onClick={openCreateCategory}>{t('menu.newCategory')}</Button> : null}
            {canManageItems ? <Button variant="secondary" onClick={openCreateItem}>{t('menu.newItem')}</Button> : null}
          </>
        }
      >
      <div className="preview-sequence">
        <div className="summary-grid">
          <SummaryCard
            label={t('menu.summaryCategories')}
            value={categoriesLoading && categories.length === 0 ? t('menu.loadingValue') : categories.length.toString()}
            tone="admin"
            detail={t('menu.summaryCategoriesDetail')}
          />
          <SummaryCard
            label={t('menu.summaryActiveCategories')}
            value={activeCategoryCount.toString()}
            tone="accent"
            detail={t('menu.summaryActiveCategoriesDetail')}
          />
          {canViewItems ? (
            <SummaryCard
              label={t('menu.summaryMenuItems')}
              value={itemsLoading && items.length === 0 ? t('menu.loadingValue') : items.length.toString()}
              tone="inventory"
              detail={t('menu.summaryMenuItemsDetail')}
            />
          ) : (
            <SummaryCard
              label={t('menu.summaryInactiveCategories')}
              value={inactiveCategoryCount.toString()}
              tone="inventory"
              detail={t('menu.summaryInactiveCategoriesDetail')}
            />
          )}
          {canViewItems ? (
            <SummaryCard
              label={t('menu.summaryActiveItems')}
              value={activeItemCount.toString()}
              tone="orders"
              detail={t('menu.summaryActiveItemsDetail')}
            />
          ) : (
            <SummaryCard
              label={t('menu.summaryVegetarianItems')}
              value={vegetarianItemCount.toString()}
              tone="orders"
              detail={t('menu.summaryVegetarianItemsDetail')}
            />
          )}
        </div>

            {showImportPanel && canManageItems ? (
          <div ref={importPanelSectionRef} className="scroll-target">
            <MenuImportPanel
              onClose={closeImportPanel}
              onImported={handleImportCompleted}
              textareaRef={importTextareaRef}
            />
          </div>
        ) : null}

        <div className="preview-split preview-split--admin menu-management__split">
          <div className="menu-management__main">
            <Card
              title={t('menu.categoryCatalogTitle')}
              description={t('menu.categoryCatalogDescription')}
              tone="admin"
              actions={<Badge tone="neutral" label={categoriesLoading ? t('menu.refreshingBadge') : t('menu.loadedBadge').replace('{n}', String(categories.length))} />}
            >
              <MenuCategoryList
                categories={categories}
                itemCounts={canViewItems ? categoryItemCounts : null}
                loading={categoriesLoading}
                error={categoriesError}
                canManageCategories={canManageCategories}
                onRetry={() => void refreshCategories().catch(() => undefined)}
                onSelectCategory={openEditCategory}
              />
            </Card>

            {canViewItems ? (
              <Card
                title={t('menu.itemCatalogTitle')}
                description={t('menu.itemCatalogDescription')}
                tone="admin"
                actions={<Badge tone="neutral" label={itemsLoading ? t('menu.refreshingBadge') : t('menu.shownBadge').replace('{n}', String(filteredItemCount))} />}
              >
                <MenuItemList
                  categories={categories}
                  items={filteredItems}
                  loading={itemsLoading}
                  error={itemsError}
                  search={search}
                  statusFilter={statusFilter}
                  categoryFilter={categoryFilter}
                  availabilityFilter={availabilityFilter}
                  canManageItems={canManageItems}
                  onSearchChange={setSearch}
                  onStatusFilterChange={setStatusFilter}
                  onCategoryFilterChange={setCategoryFilter}
                  onAvailabilityFilterChange={setAvailabilityFilter}
                  onRetry={() => void refreshItems().catch(() => undefined)}
                  onSelectItem={openSelectItem}
                />
              </Card>
            ) : null}
          </div>

          <div className="admin-workspace-stack menu-management__detail">
            {canManageCategories ? (
            <div ref={categoryFormSectionRef} className="scroll-target">
              <Card
                title={categoryPanelTitle}
                description={t('menu.categoryPanelDescription')}
                tone="admin"
                actions={selectedCategory ? <Badge tone="neutral" label={selectedCategory.status} /> : undefined}
              >
                {selectedCategoryLoading ? (
                  <EmptyState
                    title={t('menu.loadingCategoryTitle')}
                    description={t('menu.loadingCategoryDescription')}
                    tone="admin"
                  />
                ) : selectedCategoryError ? (
                  <EmptyState
                    title={t('menu.couldNotLoadSelectedCategoryTitle')}
                    description={selectedCategoryError}
                    tone="admin"
                    actionLabel={t('menu.createNewCategoryAction')}
                    onAction={openCreateCategory}
                  />
                ) : (
                  <>
                    <MenuCategoryForm
                      mode={categoryMode}
                      form={categoryForm}
                      errors={categoryErrors}
                      submitting={categorySubmitting}
                      onSubmit={handleCategorySubmit}
                      onSecondaryAction={openCreateCategory}
                      secondaryActionLabel={categoryMode === 'create' ? t('menu.clearButton') : t('menu.newCategory')}
                      setForm={setCategoryForm}
                      nameInputRef={categoryNameInputRef}
                    />

                    {selectedCategory ? (
                      <div className="menu-item-detail-summary">
                        <div className="menu-item-detail-summary__row">
                          <span className="menu-item-detail-summary__label">{t('menu.metaDisplayOrder')}</span>
                          <strong>{selectedCategory.displayOrder}</strong>
                        </div>
                        <div className="menu-item-detail-summary__row">
                          <span className="menu-item-detail-summary__label">{t('menu.metaCreated')}</span>
                          <strong>{formatMenuTimestamp(selectedCategory.createdAt)}</strong>
                        </div>
                        <div className="menu-item-detail-summary__row">
                          <span className="menu-item-detail-summary__label">{t('menu.metaUpdated')}</span>
                          <strong>{formatMenuTimestamp(selectedCategory.updatedAt)}</strong>
                        </div>
                        <div className="menu-item-detail-summary__row">
                          <span className="menu-item-detail-summary__label">{t('menu.metaItems')}</span>
                          <strong>{categoryItemCounts[selectedCategory.menuCategoryId] ?? 0}</strong>
                        </div>
                      </div>
                    ) : null}

                    {selectedCategory ? (
                      <MenuCategoryStatusActions
                        status={selectedCategory.status}
                        confirmDeactivate={categoryConfirmDeactivate}
                        submitting={categorySubmitting}
                        onActivate={() => void handleActivateCategory()}
                        onRequestDeactivate={() => setCategoryConfirmDeactivate(true)}
                        onConfirmDeactivate={() => void handleDeactivateCategory()}
                        onCancelDeactivate={() => setCategoryConfirmDeactivate(false)}
                      />
                    ) : (
                      <div className="admin-form-note">
                        {t('menu.categoryCreateNote')}
                      </div>
                    )}
                  </>
                )}
              </Card>
            </div>
            ) : null}

            {canViewItems ? (
              <div ref={itemFormSectionRef} className="scroll-target">
                <Card
                  title={itemPanelTitle}
                  description={itemPanelDescription}
                  tone="admin"
                  actions={selectedItem ? <Badge tone="neutral" label={selectedItem.status} /> : undefined}
                >
                  {selectedItemLoading ? (
                    <EmptyState
                      title={t('menu.loadingItemTitle')}
                      description={t('menu.loadingItemDescription')}
                      tone="admin"
                    />
                  ) : selectedItemError ? (
                    <EmptyState
                      title={t('menu.couldNotLoadSelectedItemTitle')}
                      description={selectedItemError}
                      tone="admin"
                      actionLabel={canManageItems ? t('menu.createNewItemAction') : undefined}
                      onAction={canManageItems ? openCreateItem : undefined}
                    />
                  ) : canManageItems ? (
                    <>
                      <MenuItemForm
                        mode={itemMode === 'create' ? 'create' : 'edit'}
                        form={itemForm}
                        errors={itemErrors}
                        categoryOptions={categoryOptions}
                        inventoryOptions={inventoryOptions}
                        submitting={itemSubmitting}
                        onSubmit={handleItemSubmit}
                        onSecondaryAction={openCreateItem}
                        secondaryActionLabel={itemMode === 'create' ? t('menu.clearButton') : t('menu.newItem')}
                        setForm={setItemForm}
                        categorySelectRef={itemCategorySelectRef}
                      />

                      <div className="admin-workspace-stack">
                        {selectedItem ? (
                          <div className="menu-item-detail-summary">
                            <div className="menu-item-detail-summary__row">
                              <span className="menu-item-detail-summary__label">{t('menu.metaCategory')}</span>
                              <strong>{selectedItem.categoryName}</strong>
                            </div>
                            <div className="menu-item-detail-summary__row">
                              <span className="menu-item-detail-summary__label">{t('menu.metaPrice')}</span>
                              <strong>{formatMenuPrice(selectedItem.basePrice)}</strong>
                            </div>
                            <div className="menu-item-detail-summary__row">
                              <span className="menu-item-detail-summary__label">{t('menu.metaTax')}</span>
                              <strong>{selectedItem.taxRate.toFixed(2)}%</strong>
                            </div>
                            <div className="menu-item-detail-summary__row">
                              <span className="menu-item-detail-summary__label">{t('menu.metaInventoryDeductionMode')}</span>
                              <strong>{t(`menu.inventoryDeductionMode${selectedItem.inventoryDeductionMode}`)}</strong>
                            </div>
                            {selectedItem.stockInventoryItemName ? (
                              <div className="menu-item-detail-summary__row">
                                <span className="menu-item-detail-summary__label">{t('menu.metaStockInventoryItem')}</span>
                                <strong>{selectedItem.stockInventoryItemName}</strong>
                              </div>
                            ) : null}
                            <div className="menu-item-detail-summary__row">
                              <span className="menu-item-detail-summary__label">{t('menu.metaCreated')}</span>
                              <strong>{formatMenuTimestamp(selectedItem.createdAt)}</strong>
                            </div>
                            <div className="menu-item-detail-summary__row">
                              <span className="menu-item-detail-summary__label">{t('menu.metaUpdated')}</span>
                              <strong>{formatMenuTimestamp(selectedItem.updatedAt)}</strong>
                            </div>
                          </div>
                        ) : null}

                        {selectedItem ? (
                          <MenuItemStatusActions
                            status={selectedItem.status}
                            confirmDeactivate={itemConfirmDeactivate}
                            submitting={itemSubmitting}
                            onActivate={() => void handleActivateItem()}
                            onRequestDeactivate={() => setItemConfirmDeactivate(true)}
                            onConfirmDeactivate={() => void handleDeactivateItem()}
                            onCancelDeactivate={() => setItemConfirmDeactivate(false)}
                          />
                        ) : (
                          <div className="admin-form-note">
                            {t('menu.itemCreateNote')}
                          </div>
                        )}

                        {selectedItem ? (
                          <MenuItemRecipeSection
                            item={selectedItem}
                            branchId={branchId}
                            inventoryItems={inventoryItems}
                            inventoryLoading={inventoryLoading}
                            inventoryError={inventoryError}
                            recipe={recipe}
                            loading={recipeLoading}
                            error={recipeError}
                            canManageItems={canManageItems}
                            submitting={recipeSubmitting}
                            onSave={handleRecipeSave}
                            onRetry={() => {
                              if (selectedItemId) {
                                void refreshSelectedRecipe(selectedItemId).catch(() => undefined);
                              }
                            }}
                          />
                        ) : null}
                      </div>
                    </>
                  ) : (
                    selectedItem ? (
                      <div className="admin-workspace-stack">
                        <div className="menu-item-detail-summary menu-item-detail-summary--stacked">
                          <div className="menu-item-detail-summary__row">
                            <span className="menu-item-detail-summary__label">{t('menu.metaCategory')}</span>
                            <strong>{selectedItem.categoryName}</strong>
                          </div>
                          <div className="menu-item-detail-summary__row">
                            <span className="menu-item-detail-summary__label">{t('menu.metaPrice')}</span>
                            <strong>{formatMenuPrice(selectedItem.basePrice, currencyCode, locale)}</strong>
                          </div>
                          <div className="menu-item-detail-summary__row">
                            <span className="menu-item-detail-summary__label">{t('menu.metaTax')}</span>
                            <strong>{selectedItem.taxRate.toFixed(2)}%</strong>
                          </div>
                          <div className="menu-item-detail-summary__row">
                            <span className="menu-item-detail-summary__label">{t('menu.metaInventoryDeductionMode')}</span>
                            <strong>{t(`menu.inventoryDeductionMode${selectedItem.inventoryDeductionMode}`)}</strong>
                          </div>
                          {selectedItem.stockInventoryItemName ? (
                            <div className="menu-item-detail-summary__row">
                              <span className="menu-item-detail-summary__label">{t('menu.metaStockInventoryItem')}</span>
                              <strong>{selectedItem.stockInventoryItemName}</strong>
                            </div>
                          ) : null}
                          <div className="menu-item-detail-summary__row">
                            <span className="menu-item-detail-summary__label">{t('menu.metaDescription')}</span>
                            <strong>{selectedItem.description ?? t('menu.notProvided')}</strong>
                          </div>
                          <div className="menu-item-detail-summary__row">
                            <span className="menu-item-detail-summary__label">{t('menu.metaAvailability')}</span>
                            <strong>
                              {selectedItem.isAvailableForEatIn && selectedItem.isAvailableForParcel
                                ? t('menu.availabilityEatInAndParcel')
                                : selectedItem.isAvailableForEatIn
                                  ? t('menu.availabilityEatInOnly')
                                  : selectedItem.isAvailableForParcel
                                    ? t('menu.availabilityParcelOnly')
                                    : t('menu.availabilityUnavailable')}
                            </strong>
                          </div>
                          <div className="menu-item-detail-summary__row">
                            <span className="menu-item-detail-summary__label">{t('menu.metaCreated')}</span>
                            <strong>{formatMenuTimestamp(selectedItem.createdAt)}</strong>
                          </div>
                          <div className="menu-item-detail-summary__row">
                            <span className="menu-item-detail-summary__label">{t('menu.metaUpdated')}</span>
                            <strong>{formatMenuTimestamp(selectedItem.updatedAt)}</strong>
                          </div>
                        </div>

                        <MenuItemRecipeSection
                          item={selectedItem}
                          branchId={branchId}
                          inventoryItems={inventoryItems}
                          inventoryLoading={inventoryLoading}
                          inventoryError={inventoryError}
                          recipe={recipe}
                          loading={recipeLoading}
                          error={recipeError}
                          canManageItems={canManageItems}
                          submitting={recipeSubmitting}
                          onSave={handleRecipeSave}
                          onRetry={() => {
                            if (selectedItemId) {
                              void refreshSelectedRecipe(selectedItemId).catch(() => undefined);
                            }
                          }}
                        />
                      </div>
                    ) : (
                      <EmptyState
                        title={t('menu.chooseItemTitle')}
                        description={t('menu.chooseItemDescription')}
                        tone="admin"
                      />
                    )
                  )}
                </Card>
              </div>
            ) : null}

            {canViewItems ? (
              <Card
                title={t('menu.priceHistoryTitle').replace('{name}', selectedItemTitle)}
                description={t('menu.priceHistoryCardDescription')}
                tone="admin"
              >
                <MenuItemPriceHistoryPanel
                  item={selectedItem}
                  history={priceHistory}
                  loading={priceHistoryLoading}
                  error={priceHistoryError}
                  onRetry={() => {
                    if (selectedItemId) {
                      void refreshPriceHistory(selectedItemId).catch(() => undefined);
                    }
                  }}
                />
              </Card>
            ) : null}

            {notice ? (
              <div
                className={['admin-notice', `admin-notice--${notice.tone}`].join(' ')}
                role={notice.tone === 'danger' ? 'alert' : 'status'}
              >
                {notice.message}
              </div>
            ) : null}
          </div>
        </div>
      </div>
    </AdminLayout>
  );
};

export default MenuManagementPage;
