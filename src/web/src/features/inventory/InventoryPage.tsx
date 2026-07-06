import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from 'react';
import { useSearchParams } from 'react-router-dom';

import { isApiError } from '../../api/apiErrors';
import { InventoryManagementLayout } from '../../components/layout';
import type { ShellNavItem } from '../../components/layout/navigation';
import { Badge, Button, Card, Checkbox, EmptyState, Input, ResponsiveDataList, Select, StatusBadge, SummaryCard } from '../../components/ui';
import { useLanguage } from '../../i18n/LanguageProvider';
import { useAuth } from '../auth/useAuth';
import { listMenuItems } from '../admin/adminApi';
import { listAdminBranches } from '../admin/adminApi';
import type { AdminBranchListItem } from '../admin/adminTypes';
import type { MenuItem } from '../admin/adminTypes';
import { sortBranches } from '../admin/branches/branchDisplay';
import InventoryAdjustmentDialog, {
  type InventoryAdjustmentFormErrors,
  type InventoryAdjustmentFormState,
} from './InventoryAdjustmentDialog';
import { getSafeInventoryErrorMessage } from './inventoryErrorDisplay';
import {
  createBatchProduction,
  createInventoryItem,
  getInventorySummary,
  listBatchProductions,
  listInventoryItems,
  listInventoryMovements,
  recordInventoryMovement,
  recordPreparedStockWastage,
  updateInventoryItem,
} from './inventoryApi';
import { formatInventoryDateTime, formatInventoryMovementType, formatInventoryStock } from './inventoryDisplay';
import type {
  BatchProductionListItem,
  InventoryAlertItem,
  InventoryItemListItem,
  InventoryMovementItem,
  InventorySummaryResponse,
} from './inventoryTypes';

export interface InventoryPageProps {
  navItems?: ShellNavItem[];
  restaurantName?: string;
  branchName?: string;
  operatorLabel?: string;
}

type NoticeTone = 'success' | 'info' | 'warning' | 'danger';
type ScrollTarget = 'item-form' | 'adjustment-dialog' | null;

interface Notice {
  tone: NoticeTone;
  message: string;
}

interface InventoryItemFormState {
  name: string;
  category: string;
  unitOfMeasure: string;
  lowStockThreshold: string;
  isActive: boolean;
}

interface BatchProductionFormState {
  menuItemId: string;
  quantityProduced: string;
  businessDate: string;
  producedAtLocal: string;
  notes: string;
}

interface PreparedStockWastageFormState {
  menuItemId: string;
  quantity: string;
  wastedAtLocal: string;
  reason: string;
  notes: string;
}

interface FormErrors {
  branchId?: string;
  name?: string;
  category?: string;
  unitOfMeasure?: string;
  lowStockThreshold?: string;
  movementType?: string;
  quantity?: string;
  reason?: string;
  menuItemId?: string;
  businessDate?: string;
  producedAtLocal?: string;
  wastedAtLocal?: string;
  notes?: string;
}

const emptyItemForm = (): InventoryItemFormState => ({
  name: '',
  category: '',
  unitOfMeasure: '',
  lowStockThreshold: '',
  isActive: true,
});

const emptyAdjustmentForm = (): InventoryAdjustmentFormState => ({
  movementType: 'Increase',
  quantity: '',
  reason: '',
  notes: '',
});

const getLocalDateTimeInputValue = (date = new Date()) => {
  const offsetMs = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - offsetMs).toISOString().slice(0, 16);
};

const getDateInputValue = (date = new Date()) => {
  const offsetMs = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - offsetMs).toISOString().slice(0, 10);
};

const emptyBatchProductionForm = (): BatchProductionFormState => ({
  menuItemId: '',
  quantityProduced: '',
  businessDate: getDateInputValue(),
  producedAtLocal: getLocalDateTimeInputValue(),
  notes: '',
});

const emptyPreparedStockWastageForm = (): PreparedStockWastageFormState => ({
  menuItemId: '',
  quantity: '',
  wastedAtLocal: getLocalDateTimeInputValue(),
  reason: '',
  notes: '',
});

const isValidStatusFilter = (value: string | null) =>
  value === 'In stock' || value === 'Low stock' || value === 'Out of stock' || value === 'Inactive';

const validateItemForm = (form: InventoryItemFormState, branchRequired: boolean, branchId: string, t: (key: string) => string) => {
  const errors: FormErrors = {};

  if (branchRequired && !branchId) {
    errors.branchId = t('inventory.validationBranchRequired');
  }

  if (!form.name.trim()) {
    errors.name = t('inventory.validationItemNameRequired');
  }

  if (!form.category.trim()) {
    errors.category = t('inventory.validationCategoryRequired');
  }

  if (!form.unitOfMeasure.trim()) {
    errors.unitOfMeasure = t('inventory.validationUnitRequired');
  }

  if (form.lowStockThreshold.trim() === '') {
    errors.lowStockThreshold = t('inventory.validationLowStockRequired');
  } else if (Number.isNaN(Number(form.lowStockThreshold)) || Number(form.lowStockThreshold) < 0) {
    errors.lowStockThreshold = t('inventory.validationLowStockNonNegative');
  }

  return errors;
};

const validateAdjustmentForm = (form: InventoryAdjustmentFormState, currentStock: number | null, t?: (key: string) => string) => {
  const errors: FormErrors = {};

  if (form.movementType !== 'Increase' && form.movementType !== 'Decrease') {
    errors.movementType = t ? t('inventoryAdjustment.validationMovementTypeRequired') : 'Adjustment type is required.';
  }

  if (form.quantity.trim() === '' || Number.isNaN(Number(form.quantity)) || Number(form.quantity) <= 0) {
    errors.quantity = t ? t('inventoryAdjustment.validationQuantityRequired') : 'Quantity must be greater than zero.';
  }

  if (!form.reason.trim()) {
    errors.reason = t ? t('inventoryAdjustment.validationReasonRequired') : 'Reason is required.';
  }

  if (currentStock !== null && !Number.isNaN(Number(form.quantity))) {
    const delta = form.movementType === 'Decrease' ? -Number(form.quantity) : Number(form.quantity);
    if (currentStock + delta < 0) {
      errors.quantity = t ? t('inventoryAdjustment.validationStockNegative') : 'Decrease would make stock negative.';
    }
  }

  return errors;
};

const validateBatchProductionForm = (form: BatchProductionFormState, t: (key: string) => string) => {
  const errors: FormErrors = {};

  if (!form.menuItemId.trim()) {
    errors.menuItemId = t('inventory.batchProductionValidationMenuItemRequired');
  }

  if (form.quantityProduced.trim() === '' || Number.isNaN(Number(form.quantityProduced)) || Number(form.quantityProduced) <= 0) {
    errors.quantity = t('inventory.batchProductionValidationQuantityRequired');
  }

  if (!form.businessDate.trim()) {
    errors.businessDate = t('inventory.batchProductionValidationBusinessDateRequired');
  }

  if (!form.producedAtLocal.trim() || Number.isNaN(Date.parse(form.producedAtLocal))) {
    errors.producedAtLocal = t('inventory.batchProductionValidationProducedAtRequired');
  }

  return errors;
};

const validatePreparedStockWastageForm = (form: PreparedStockWastageFormState, t: (key: string) => string) => {
  const errors: FormErrors = {};

  if (!form.menuItemId.trim()) {
    errors.menuItemId = t('inventory.wastageValidationMenuItemRequired');
  }

  if (form.quantity.trim() === '' || Number.isNaN(Number(form.quantity)) || Number(form.quantity) <= 0) {
    errors.quantity = t('inventory.wastageValidationQuantityRequired');
  }

  if (!form.reason.trim()) {
    errors.reason = t('inventory.wastageValidationReasonRequired');
  }

  if (!form.wastedAtLocal.trim() || Number.isNaN(Date.parse(form.wastedAtLocal))) {
    errors.wastedAtLocal = t('inventory.wastageValidationWastedAtRequired');
  }

  return errors;
};

const buildAlertRows = (items: InventoryAlertItem[]) =>
  items.map(item => ({
    id: item.inventoryItemId,
    name: item.name,
    category: item.category,
    unit: item.unitOfMeasure,
    threshold: formatInventoryStock(item.lowStockThreshold),
    currentStock: formatInventoryStock(item.currentStock),
    status: item.status,
  }));

const buildBatchProductionRows = (items: BatchProductionListItem[]) =>
  items.map(item => ({
    id: item.batchProductionId,
    menuItemName: item.menuItemName,
    preparedStockName: item.preparedInventoryItemName,
    quantityProduced: formatInventoryStock(item.quantityProduced),
    businessDate: item.businessDate,
    producedAt: formatInventoryDateTime(item.producedAtUtc),
    producedBy: item.producedByUserName,
    notes: item.notes ?? '-',
    totalRawQuantityConsumed: formatInventoryStock(item.totalRawQuantityConsumed),
  }));

export const InventoryPage = ({ navItems, restaurantName, branchName, operatorLabel }: InventoryPageProps) => {
  const { t } = useLanguage();
  const auth = useAuth();
  const [searchParams, setSearchParams] = useSearchParams();
  const canViewInventory = auth.hasPermission('Inventory.View') || auth.hasPermission('Inventory.Adjust');
  const canAdjustInventory = auth.hasPermission('Inventory.Adjust');
  const canViewMenuItems = auth.hasPermission('MenuItem.View') || auth.hasPermission('MenuItem.Manage');
  const canSwitchBranch = auth.hasPermission('Branch.Manage') || auth.hasPermission('User.Manage');

  const [branches, setBranches] = useState<AdminBranchListItem[]>([]);
  const [branchesLoading, setBranchesLoading] = useState(canSwitchBranch);
  const [branchesError, setBranchesError] = useState<string | null>(null);
  const [selectedBranchId, setSelectedBranchId] = useState(auth.session?.branchId ?? '');
  const [items, setItems] = useState<InventoryItemListItem[]>([]);
  const [summary, setSummary] = useState<InventorySummaryResponse | null>(null);
  const [itemsLoading, setItemsLoading] = useState(canViewInventory);
  const [itemsError, setItemsError] = useState<string | null>(null);
  const [menuItems, setMenuItems] = useState<MenuItem[]>([]);
  const [menuItemsLoading, setMenuItemsLoading] = useState(canViewMenuItems);
  const [menuItemsError, setMenuItemsError] = useState<string | null>(null);
  const [batchProductions, setBatchProductions] = useState<BatchProductionListItem[]>([]);
  const [batchProductionsLoading, setBatchProductionsLoading] = useState(false);
  const [batchProductionsError, setBatchProductionsError] = useState<string | null>(null);
  const [selectedItemId, setSelectedItemId] = useState<string | null>(null);
  const [movements, setMovements] = useState<InventoryMovementItem[]>([]);
  const [movementsLoading, setMovementsLoading] = useState(false);
  const [movementsError, setMovementsError] = useState<string | null>(null);
  const [notice, setNotice] = useState<Notice | null>(null);
  const [pendingScrollTarget, setPendingScrollTarget] = useState<ScrollTarget>(null);
  const [mode, setMode] = useState<'create' | 'edit' | 'view'>(canAdjustInventory ? 'create' : 'view');
  const [itemForm, setItemForm] = useState<InventoryItemFormState>(() => emptyItemForm());
  const [itemErrors, setItemErrors] = useState<FormErrors>({});
  const [itemSubmitting, setItemSubmitting] = useState(false);
  const [searchTerm, setSearchTerm] = useState('');
  const [categoryFilter, setCategoryFilter] = useState('');
  const [statusFilter, setStatusFilter] = useState(() => {
    const status = searchParams.get('status');
    return isValidStatusFilter(status) ? status : '';
  });
  const [adjustmentTargetItemId, setAdjustmentTargetItemId] = useState<string | null>(null);
  const [adjustmentForm, setAdjustmentForm] = useState<InventoryAdjustmentFormState>(() => emptyAdjustmentForm());
  const [adjustmentErrors, setAdjustmentErrors] = useState<FormErrors>({});
  const [adjustmentSubmitting, setAdjustmentSubmitting] = useState(false);
  const [batchProductionForm, setBatchProductionForm] = useState<BatchProductionFormState>(() => emptyBatchProductionForm());
  const [batchProductionErrors, setBatchProductionErrors] = useState<FormErrors>({});
  const [batchProductionSubmitting, setBatchProductionSubmitting] = useState(false);
  const [wastageForm, setWastageForm] = useState<PreparedStockWastageFormState>(() => emptyPreparedStockWastageForm());
  const [wastageErrors, setWastageErrors] = useState<FormErrors>({});
  const [wastageSubmitting, setWastageSubmitting] = useState(false);

  const itemFormSectionRef = useRef<HTMLDivElement>(null);
  const itemNameInputRef = useRef<HTMLInputElement>(null);
  const adjustmentDialogRef = useRef<HTMLDivElement>(null);
  const adjustmentQuantityInputRef = useRef<HTMLInputElement>(null);

  const selectedBranch = useMemo(
    () => branches.find(branch => branch.branchId === selectedBranchId) ?? null,
    [branches, selectedBranchId]
  );
  const selectedItem = useMemo(
    () => items.find(item => item.inventoryItemId === selectedItemId) ?? null,
    [items, selectedItemId]
  );
  const selectedAdjustmentItem = useMemo(
    () => items.find(item => item.inventoryItemId === adjustmentTargetItemId) ?? null,
    [adjustmentTargetItemId, items]
  );
  const availableCategories = useMemo(
    () => [...new Set(items.map(item => item.category))].sort((left, right) => left.localeCompare(right)),
    [items]
  );
  const filteredItems = useMemo(() => {
    const query = searchTerm.trim().toLowerCase();
    return items
      .filter(item => {
        if (query) {
          const haystack = `${item.name} ${item.category} ${item.unitOfMeasure} ${item.status}`.toLowerCase();
          if (!haystack.includes(query)) {
            return false;
          }
        }

        if (categoryFilter && item.category !== categoryFilter) {
          return false;
        }

        if (statusFilter && item.status !== statusFilter) {
          return false;
        }

        return true;
      })
      .sort((left, right) => left.category.localeCompare(right.category) || left.name.localeCompare(right.name));
  }, [categoryFilter, items, searchTerm, statusFilter]);

  const loadBranches = useCallback(async () => {
    if (!canSwitchBranch) {
      return;
    }

    setBranchesLoading(true);
    setBranchesError(null);

    try {
      const response = await listAdminBranches();
      const nextBranches = sortBranches(response.items.filter(branch => branch.status === 'Active'));
      setBranches(nextBranches);

      const activeBranchIds = new Set(nextBranches.map(branch => branch.branchId));
      const fallbackBranchId = auth.session?.branchId && activeBranchIds.has(auth.session.branchId)
        ? auth.session.branchId
        : nextBranches.length === 1
          ? nextBranches[0].branchId
          : '';
      const nextSelectedBranchId = selectedBranchId && activeBranchIds.has(selectedBranchId) ? selectedBranchId : fallbackBranchId;

      if (nextSelectedBranchId !== selectedBranchId) {
        setSelectedBranchId(nextSelectedBranchId);
      }
    } catch (caughtError) {
      setBranchesError(getSafeInventoryErrorMessage(caughtError, t('inventory.couldNotLoadBranches')));
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setBranchesLoading(false);
    }
  }, [auth, canSwitchBranch, selectedBranchId]);

  const loadWorkspace = useCallback(
    async (branchId: string) => {
      if (!canViewInventory || !branchId) {
        setItemsLoading(false);
        setItems([]);
        setSummary(null);
        setSelectedItemId(null);
        setMovements([]);
        return;
      }

      setItemsLoading(true);
      setItemsError(null);

      try {
        const [itemsResponse, summaryResponse] = await Promise.all([
          listInventoryItems({ branchId }),
          getInventorySummary({ branchId }),
        ]);

        setItems(itemsResponse.items);
        setSummary(summaryResponse);
        setSelectedItemId(current => {
          if (current && itemsResponse.items.some(item => item.inventoryItemId === current)) {
            return current;
          }

          return itemsResponse.items[0]?.inventoryItemId ?? null;
        });
      } catch (caughtError) {
        setItems([]);
        setSummary(null);
        setMovements([]);
        setItemsError(getSafeInventoryErrorMessage(caughtError, t('inventory.couldNotLoadItems')));
        if (isApiError(caughtError) && caughtError.status === 401) {
          void auth.logout();
        }
      } finally {
        setItemsLoading(false);
      }
    },
    [auth, canViewInventory]
  );

  const loadMovements = useCallback(
    async (itemId: string) => {
      if (!itemId) {
        setMovements([]);
        return;
      }

      setMovementsLoading(true);
      setMovementsError(null);

      try {
        const response = await listInventoryMovements(itemId);
        setMovements(response.items);
      } catch (caughtError) {
        setMovements([]);
        setMovementsError(getSafeInventoryErrorMessage(caughtError, t('inventory.couldNotLoadMovements')));
        if (isApiError(caughtError) && caughtError.status === 401) {
          void auth.logout();
        }
      } finally {
        setMovementsLoading(false);
      }
    },
    [auth]
  );

  const loadMenuItems = useCallback(async () => {
    if (!canViewMenuItems) {
      setMenuItems([]);
      setMenuItemsError(null);
      setMenuItemsLoading(false);
      return;
    }

    setMenuItemsLoading(true);
    setMenuItemsError(null);

    try {
      const response = await listMenuItems({ status: 'Active' });
      setMenuItems(response.items);
    } catch (caughtError) {
      setMenuItems([]);
      setMenuItemsError(getSafeInventoryErrorMessage(caughtError, t('inventory.couldNotLoadMenuItems')));
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setMenuItemsLoading(false);
    }
  }, [auth, canViewMenuItems, t]);

  const loadBatchProductions = useCallback(
    async (branchId: string, businessDate: string) => {
      if (!branchId) {
        setBatchProductions([]);
        setBatchProductionsError(null);
        return;
      }

      setBatchProductionsLoading(true);
      setBatchProductionsError(null);

      try {
        const response = await listBatchProductions({
          branchId,
          fromBusinessDate: businessDate,
          toBusinessDate: businessDate,
        });
        setBatchProductions(response.items);
      } catch (caughtError) {
        setBatchProductions([]);
        setBatchProductionsError(getSafeInventoryErrorMessage(caughtError, t('inventory.couldNotLoadBatchProductions')));
        if (isApiError(caughtError) && caughtError.status === 401) {
          void auth.logout();
        }
      } finally {
        setBatchProductionsLoading(false);
      }
    },
    [auth, t]
  );

  useEffect(() => {
    if (!canSwitchBranch) {
      return;
    }

    void loadBranches();
  }, [canSwitchBranch, loadBranches]);

  useEffect(() => {
    if (!canViewInventory || !selectedBranchId) {
      return;
    }

    void loadWorkspace(selectedBranchId);
  }, [canViewInventory, loadWorkspace, selectedBranchId]);

  useEffect(() => {
    if (!canViewMenuItems) {
      setMenuItems([]);
      setMenuItemsError(null);
      setMenuItemsLoading(false);
      return;
    }

    void loadMenuItems();
  }, [canViewMenuItems, loadMenuItems, selectedBranchId]);

  useEffect(() => {
    if (!canViewInventory || !selectedBranchId) {
      setBatchProductions([]);
      setBatchProductionsError(null);
      setBatchProductionsLoading(false);
      return;
    }

    void loadBatchProductions(selectedBranchId, batchProductionForm.businessDate);
  }, [batchProductionForm.businessDate, canViewInventory, loadBatchProductions, selectedBranchId]);

  useEffect(() => {
    if (!selectedItem) {
      setMovements([]);
      setMovementsError(null);
      return;
    }

    setItemForm({
      name: selectedItem.name,
      category: selectedItem.category,
      unitOfMeasure: selectedItem.unitOfMeasure,
      lowStockThreshold: selectedItem.lowStockThreshold.toString(),
      isActive: selectedItem.isActive,
    });

    if (canViewInventory) {
      void loadMovements(selectedItem.inventoryItemId);
    }
  }, [canViewInventory, loadMovements, selectedItem]);

  useEffect(() => {
    if (mode === 'create') {
      setItemForm(emptyItemForm());
      setItemErrors({});
      return;
    }

    if (selectedItem) {
      setItemForm({
        name: selectedItem.name,
        category: selectedItem.category,
        unitOfMeasure: selectedItem.unitOfMeasure,
        lowStockThreshold: selectedItem.lowStockThreshold.toString(),
        isActive: selectedItem.isActive,
      });
    }
  }, [mode, selectedItem]);

  useEffect(() => {
    if (!batchProductionForm.menuItemId && menuItems.length > 0) {
      const defaultBatchPrepared = menuItems.find(
        item => item.inventoryDeductionMode === 'BatchPrepared' && item.stockInventoryItemId
      );
      if (defaultBatchPrepared) {
        setBatchProductionForm(current => ({ ...current, menuItemId: defaultBatchPrepared.menuItemId }));
      }
    }
  }, [batchProductionForm.menuItemId, menuItems]);

  useEffect(() => {
    if (!wastageForm.menuItemId && menuItems.length > 0) {
      const defaultBatchPrepared = menuItems.find(
        item => item.inventoryDeductionMode === 'BatchPrepared' && item.stockInventoryItemId
      );
      if (defaultBatchPrepared) {
        setWastageForm(current => ({ ...current, menuItemId: defaultBatchPrepared.menuItemId }));
      }
    }
  }, [menuItems, wastageForm.menuItemId]);

  useEffect(() => {
    if (pendingScrollTarget === null) {
      return;
    }

    if (pendingScrollTarget === 'item-form') {
      if (typeof itemNameInputRef.current?.scrollIntoView === 'function') {
        itemNameInputRef.current.scrollIntoView({ behavior: 'smooth', block: 'start' });
      }
      itemNameInputRef.current?.focus({ preventScroll: true });
      setPendingScrollTarget(null);
      return;
    }

    if (pendingScrollTarget === 'adjustment-dialog') {
      if (!selectedAdjustmentItem) {
        return;
      }

      if (typeof adjustmentQuantityInputRef.current?.scrollIntoView === 'function') {
        adjustmentQuantityInputRef.current.scrollIntoView({ behavior: 'smooth', block: 'start' });
      }
      adjustmentQuantityInputRef.current?.focus({ preventScroll: true });
      setPendingScrollTarget(null);
    }
  }, [pendingScrollTarget, selectedAdjustmentItem]);

  const visibleItems = filteredItems;
  const lowStockAlertRows = useMemo(() => buildAlertRows(summary?.lowStockItems ?? []), [summary?.lowStockItems]);
  const outOfStockAlertRows = useMemo(() => buildAlertRows(summary?.outOfStockItems ?? []), [summary?.outOfStockItems]);

  const handleStatusFilterChange = useCallback(
    (value: string) => {
      setStatusFilter(value);
      const nextParams = new URLSearchParams(searchParams);
      if (value) {
        nextParams.set('status', value);
      } else {
        nextParams.delete('status');
      }

      setSearchParams(nextParams, { replace: true });
    },
    [searchParams, setSearchParams]
  );

  const openCreateMode = useCallback(() => {
    setMode('create');
    setSelectedItemId(null);
    setNotice(null);
    setPendingScrollTarget('item-form');
    setItemForm(emptyItemForm());
    setItemErrors({});
    setMovements([]);
    setMovementsError(null);
    setAdjustmentTargetItemId(null);
    setAdjustmentForm(emptyAdjustmentForm());
    setAdjustmentErrors({});
  }, []);

  const openEditMode = useCallback(
    (itemId: string) => {
      setMode(canAdjustInventory ? 'edit' : 'view');
      setSelectedItemId(itemId);
      setNotice(null);
      setItemErrors({});
      setAdjustmentTargetItemId(null);
      setAdjustmentForm(emptyAdjustmentForm());
      setAdjustmentErrors({});
    },
    [canAdjustInventory]
  );

  const openAdjustmentDialog = useCallback(
    (itemId?: string) => {
      const targetItemId = itemId ?? selectedItem?.inventoryItemId ?? items[0]?.inventoryItemId ?? null;
      if (!targetItemId) {
        return;
      }

      setAdjustmentTargetItemId(targetItemId);
      setAdjustmentForm(emptyAdjustmentForm());
      setAdjustmentErrors({});
      setNotice(null);
      setSelectedItemId(targetItemId);
      setPendingScrollTarget('adjustment-dialog');
    },
    [items, selectedItem?.inventoryItemId]
  );

  const closeAdjustmentDialog = useCallback(() => {
    setAdjustmentTargetItemId(null);
    setAdjustmentForm(emptyAdjustmentForm());
    setAdjustmentErrors({});
    setPendingScrollTarget(null);
  }, []);

  const handleItemSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const nextErrors = validateItemForm(itemForm, canSwitchBranch, selectedBranchId, t as (key: string) => string);
    setItemErrors(nextErrors);

    if (Object.keys(nextErrors).length > 0) {
      setNotice({
        tone: 'warning',
        message: t('inventory.fixItemFormNotice'),
      });
      return;
    }

    setItemSubmitting(true);
    setNotice(null);

    try {
      const request = {
        branchId: canSwitchBranch ? selectedBranchId : undefined,
        name: itemForm.name,
        category: itemForm.category,
        unitOfMeasure: itemForm.unitOfMeasure,
        lowStockThreshold: Number(itemForm.lowStockThreshold),
        isActive: itemForm.isActive,
      };

      if (mode === 'create') {
        const created = await createInventoryItem(request);
        setNotice({
          tone: 'success',
          message: t('inventory.itemCreated').replace('{name}', created.name),
        });
      } else if (selectedItem) {
        const updated = await updateInventoryItem(selectedItem.inventoryItemId, request);
        setNotice({
          tone: 'success',
          message: t('inventory.itemUpdated').replace('{name}', updated.name),
        });
      }

      await loadWorkspace(selectedBranchId);
      if (selectedItemId) {
        await loadMovements(selectedItemId);
      }
    } catch (caughtError) {
      setNotice({
        tone: 'danger',
        message: getSafeInventoryErrorMessage(caughtError, t('inventory.couldNotSaveItem')),
      });
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setItemSubmitting(false);
    }
  };

  const handleAdjustmentSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    if (!selectedAdjustmentItem) {
      return;
    }

    const nextErrors = validateAdjustmentForm(adjustmentForm, selectedAdjustmentItem.currentStock, t as (key: string) => string);
    setAdjustmentErrors(nextErrors);

    if (Object.keys(nextErrors).length > 0) {
      setNotice({
        tone: 'warning',
        message: t('inventory.fixAdjustmentFormNotice'),
      });
      return;
    }

    setAdjustmentSubmitting(true);
    setNotice(null);

    try {
      await recordInventoryMovement(selectedAdjustmentItem.inventoryItemId, {
        movementType: adjustmentForm.movementType,
        quantity: Number(adjustmentForm.quantity),
        reason: adjustmentForm.reason,
        notes: adjustmentForm.notes.trim() === '' ? null : adjustmentForm.notes.trim(),
      });

      closeAdjustmentDialog();
      setNotice({
        tone: 'success',
        message: t('inventory.adjustmentRecorded'),
      });
      await loadWorkspace(selectedBranchId);
      await loadMovements(selectedAdjustmentItem.inventoryItemId);
    } catch (caughtError) {
      setNotice({
        tone: 'danger',
        message: getSafeInventoryErrorMessage(caughtError, t('inventory.couldNotRecordAdjustment')),
      });
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setAdjustmentSubmitting(false);
    }
  };

  const handleBatchProductionSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const nextErrors = validateBatchProductionForm(batchProductionForm, t as (key: string) => string);
    setBatchProductionErrors(nextErrors);

    if (Object.keys(nextErrors).length > 0) {
      setNotice({
        tone: 'warning',
        message: t('inventory.fixBatchProductionFormNotice'),
      });
      return;
    }

    setBatchProductionSubmitting(true);
    setNotice(null);

    try {
      await createBatchProduction({
        branchId: selectedBranchId || undefined,
        menuItemId: batchProductionForm.menuItemId,
        quantityProduced: Number(batchProductionForm.quantityProduced),
        businessDate: batchProductionForm.businessDate,
        producedAtUtc: new Date(batchProductionForm.producedAtLocal).toISOString(),
        notes: batchProductionForm.notes.trim() === '' ? null : batchProductionForm.notes.trim(),
      });

      setBatchProductionForm(emptyBatchProductionForm());
      setBatchProductionErrors({});
      setNotice({
        tone: 'success',
        message: t('inventory.batchProductionRecorded'),
      });
      await loadWorkspace(selectedBranchId);
      await loadBatchProductions(selectedBranchId, batchProductionForm.businessDate);
      if (selectedItemId) {
        await loadMovements(selectedItemId);
      }
    } catch (caughtError) {
      setNotice({
        tone: 'danger',
        message: getSafeInventoryErrorMessage(caughtError, t('inventory.couldNotRecordBatchProduction')),
      });
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setBatchProductionSubmitting(false);
    }
  };

  const handleWastageSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const nextErrors = validatePreparedStockWastageForm(wastageForm, t as (key: string) => string);
    setWastageErrors(nextErrors);

    if (Object.keys(nextErrors).length > 0) {
      setNotice({
        tone: 'warning',
        message: t('inventory.fixWastageFormNotice'),
      });
      return;
    }

    setWastageSubmitting(true);
    setNotice(null);

    try {
      await recordPreparedStockWastage({
        branchId: selectedBranchId || undefined,
        menuItemId: wastageForm.menuItemId,
        quantity: Number(wastageForm.quantity),
        wastedAtUtc: new Date(wastageForm.wastedAtLocal).toISOString(),
        reason: wastageForm.reason.trim(),
        notes: wastageForm.notes.trim() === '' ? null : wastageForm.notes.trim(),
      });

      setWastageForm(emptyPreparedStockWastageForm());
      setWastageErrors({});
      setNotice({
        tone: 'success',
        message: t('inventory.wastageRecorded'),
      });
      await loadWorkspace(selectedBranchId);
      await loadBatchProductions(selectedBranchId, batchProductionForm.businessDate);
      if (selectedItemId) {
        await loadMovements(selectedItemId);
      }
    } catch (caughtError) {
      setNotice({
        tone: 'danger',
        message: getSafeInventoryErrorMessage(caughtError, t('inventory.couldNotRecordWastage')),
      });
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setWastageSubmitting(false);
    }
  };

  const selectedItemLabel = selectedItem?.name ?? t('inventory.chooseItemTitle');
  const canOperate = canAdjustInventory && Boolean(selectedBranchId);
  const adjustmentPreview = useMemo(() => {
    if (!selectedAdjustmentItem) {
      return { delta: null as number | null, previewQuantity: null as number | null };
    }

    const quantity = Number(adjustmentForm.quantity);
    if (Number.isNaN(quantity) || quantity <= 0) {
      return { delta: null as number | null, previewQuantity: null as number | null };
    }

    const delta = adjustmentForm.movementType === 'Decrease' ? -quantity : quantity;
    return {
      delta,
      previewQuantity: selectedAdjustmentItem.currentStock + delta,
    };
  }, [adjustmentForm.movementType, adjustmentForm.quantity, selectedAdjustmentItem]);
  const adjustmentConfirmDisabled = useMemo(
    () => Object.keys(validateAdjustmentForm(adjustmentForm, selectedAdjustmentItem?.currentStock ?? null)).length > 0,
    [adjustmentForm, selectedAdjustmentItem?.currentStock]
  );
  const batchPreparedMenuItems = useMemo(
    () =>
      menuItems
        .filter(item => item.inventoryDeductionMode === 'BatchPrepared' && Boolean(item.stockInventoryItemId))
        .sort((left, right) => left.name.localeCompare(right.name)),
    [menuItems]
  );
  const batchProductionRows = useMemo(() => buildBatchProductionRows(batchProductions), [batchProductions]);
  const batchProductionConfirmDisabled = useMemo(
    () => Object.keys(validateBatchProductionForm(batchProductionForm, t as (key: string) => string)).length > 0,
    [batchProductionForm, t]
  );
  const wastageConfirmDisabled = useMemo(
    () => Object.keys(validatePreparedStockWastageForm(wastageForm, t as (key: string) => string)).length > 0,
    [t, wastageForm]
  );
  const canManageBatchProduction = canAdjustInventory && canViewMenuItems;

  if (!canViewInventory) {
    return (
      <InventoryManagementLayout
        title={t('inventory.workspaceTitle')}
        description={t('inventory.workspaceDescriptionNoAccess')}
        breadcrumbs={['Dashboard', 'Inventory']}
        operatorLabel={operatorLabel}
        restaurantName={restaurantName}
        branchName={branchName}
        navItems={navItems}
      >
        <EmptyState
          title={t('inventory.notAuthorizedTitle')}
          description={t('inventory.notAuthorizedDescription')}
          tone="inventory"
        />
      </InventoryManagementLayout>
    );
  }

  return (
    <InventoryManagementLayout
      title={t('inventory.workspaceTitle')}
      description={t('inventory.workspaceDescription')}
      breadcrumbs={['Dashboard', 'Inventory']}
      operatorLabel={operatorLabel}
      restaurantName={restaurantName}
      branchName={selectedBranch?.name ?? branchName}
      navItems={navItems}
      actions={
        <>
          <Button variant="secondary" onClick={() => void loadWorkspace(selectedBranchId)} disabled={!selectedBranchId}>
            {t('inventory.refreshList')}
          </Button>
          {canAdjustInventory ? <Button onClick={openCreateMode}>{t('inventory.addItemButton')}</Button> : null}
        </>
      }
    >
      <div className="preview-sequence">
        <div className="summary-grid">
          <SummaryCard
            label={t('inventory.summaryTotalItems')}
            value={summary ? summary.totalItems.toString() : itemsLoading ? t('inventory.itemsLoading') : '0'}
            tone="inventory"
            detail={t('inventory.summaryTotalItemsDetail')}
          />
          <SummaryCard
            label={t('inventory.summaryLowStock')}
            value={summary ? summary.lowStockCount.toString() : '0'}
            tone="accent"
            detail={t('inventory.summaryLowStockDetail')}
          />
          <SummaryCard
            label={t('inventory.summaryOutOfStock')}
            value={summary ? summary.outOfStockCount.toString() : '0'}
            tone="admin"
            detail={t('inventory.summaryOutOfStockDetail')}
          />
          <SummaryCard
            label={t('inventory.summaryRecentlyAdjusted')}
            value={summary ? summary.recentlyAdjustedCount.toString() : '0'}
            tone="accent"
            detail={t('inventory.summaryRecentlyAdjustedDetail')}
          />
        </div>

        {notice ? (
          <div className={`admin-notice admin-notice--${notice.tone}`} role={notice.tone === 'danger' ? 'alert' : 'status'}>
            {notice.message}
          </div>
        ) : null}

        {canSwitchBranch ? (
          <Card
            title={t('inventory.branchSelectionTitle')}
            description={t('inventory.branchSelectionDescription')}
            tone="inventory"
            actions={<Badge tone={branchesLoading ? 'warning' : 'neutral'} label={branchesLoading ? t('inventory.branchLoading') : t('inventory.branchReady')} />}
          >
            <Select
              label={t('inventory.branchLabel')}
              value={selectedBranchId}
              onChange={event => {
                setSelectedBranchId(event.target.value);
                setSelectedItemId(null);
                closeAdjustmentDialog();
                setNotice(null);
              }}
              error={branchesError ?? undefined}
              helperText={t('inventory.branchHelperText')}
            >
              <option value="">{t('inventory.branchSelectPlaceholder')}</option>
              {branches.map(branch => (
                <option key={branch.branchId} value={branch.branchId}>
                  {branch.name}
                </option>
              ))}
            </Select>
          </Card>
        ) : null}

        {branchesError ? (
          <div className="admin-notice admin-notice--danger" role="alert">
            {branchesError}
          </div>
        ) : null}

        {itemsError ? (
          <div className="admin-notice admin-notice--danger" role="alert">
            {itemsError}
          </div>
        ) : null}

        <Card
          title={t('inventory.filtersTitle')}
          description={t('inventory.filtersDescription')}
          tone="inventory"
          actions={
            <Button
              type="button"
              variant="ghost"
              onClick={() => {
                setSearchTerm('');
                setCategoryFilter('');
                handleStatusFilterChange('');
              }}
            >
              {t('inventory.clearFilters')}
            </Button>
          }
        >
          <div className="admin-form-grid">
            <Input
              label={t('inventory.searchItemLabel')}
              value={searchTerm}
              onChange={event => setSearchTerm(event.target.value)}
              placeholder="Rice"
              helperText={t('inventory.searchItemHelper')}
            />
            <Select
              label={t('inventory.categoryLabel')}
              value={categoryFilter}
              onChange={event => setCategoryFilter(event.target.value)}
              helperText={t('inventory.categoryHelper')}
            >
              <option value="">{t('inventory.allCategories')}</option>
              {availableCategories.map(category => (
                <option key={category} value={category}>
                  {category}
                </option>
              ))}
            </Select>
            <Select
              label={t('inventory.stockStatusLabel')}
              value={statusFilter}
              onChange={event => handleStatusFilterChange(event.target.value)}
              helperText={t('inventory.stockStatusHelper')}
            >
              <option value="">{t('inventory.allStatuses')}</option>
              <option value="In stock">{t('inventory.statusInStock')}</option>
              <option value="Low stock">{t('inventory.statusLowStock')}</option>
              <option value="Out of stock">{t('inventory.statusOutOfStock')}</option>
              <option value="Inactive">{t('inventory.statusInactive')}</option>
            </Select>
          </div>
        </Card>

        <div className="preview-split preview-split--inventory">
          <div className="inventory-workspace__main">
            <Card
              title={t('inventory.itemsCardTitle')}
              description={t('inventory.itemsCardDescription')}
              tone="inventory"
              actions={<Badge tone="neutral" label={itemsLoading ? t('inventory.itemsLoading') : t('inventory.itemsLoaded', { count: visibleItems.length })} />}
            >
              <ResponsiveDataList
                rows={visibleItems.map(item => ({
                  id: item.inventoryItemId,
                  name: item.name,
                  category: item.category,
                  unit: item.unitOfMeasure,
                  currentStock: formatInventoryStock(item.currentStock),
                  threshold: formatInventoryStock(item.lowStockThreshold),
                  status: item.status,
                  action: canAdjustInventory ? (
                    <Button type="button" variant="secondary" size="sm" onClick={() => openAdjustmentDialog(item.inventoryItemId)}>
                      {t('inventory.adjustStockButton')}
                    </Button>
                  ) : (
                    '-'
                  ),
                }))}
                columns={[
                  { key: 'name', label: t('inventory.colItem') },
                  { key: 'category', label: t('inventory.colCategory') },
                  { key: 'unit', label: t('inventory.colUnit') },
                  { key: 'currentStock', label: t('inventory.colCurrentStock'), align: 'right' },
                  { key: 'threshold', label: t('inventory.colLowStockThreshold'), align: 'right' },
                  {
                    key: 'status',
                    label: t('inventory.colStatus'),
                    render: row => <StatusBadge status={row.status} label={row.status} />,
                  },
                  { key: 'action', label: t('inventory.colAction'), render: row => row.action },
                ]}
                mobileTitle={row => row.name}
                mobileDescription={row => `${row.category} · ${row.unit}`}
                emptyTitle={t('inventory.itemsEmptyTitle')}
                emptyDescription={t('inventory.itemsEmptyDescription')}
              />
            </Card>

            <Card
              title={t('inventory.lowStockAlertsTitle')}
              description={t('inventory.lowStockAlertsDescription')}
              tone="inventory"
            >
              {lowStockAlertRows.length > 0 ? (
                <ResponsiveDataList
                  rows={lowStockAlertRows}
                  columns={[
                    { key: 'name', label: t('inventory.lowStockAlertColItem') },
                    { key: 'category', label: t('inventory.lowStockAlertColCategory') },
                    { key: 'currentStock', label: t('inventory.lowStockAlertColCurrentStock'), align: 'right' },
                    { key: 'threshold', label: t('inventory.lowStockAlertColThreshold'), align: 'right' },
                    {
                      key: 'status',
                      label: t('inventory.lowStockAlertColStatus'),
                      render: row => <StatusBadge status={row.status} label={row.status} />,
                    },
                  ]}
                  mobileTitle={row => row.name}
                  mobileDescription={row => row.category}
                  emptyTitle={t('inventory.noLowStockTitle')}
                  emptyDescription={t('inventory.noLowStockDescription')}
                />
              ) : (
                <EmptyState
                  title={t('inventory.noLowStockTitle')}
                  description={t('inventory.noLowStockDescription')}
                  tone="inventory"
                />
              )}
            </Card>

            <Card
              title={t('inventory.outOfStockAlertsTitle')}
              description={t('inventory.outOfStockAlertsDescription')}
              tone="inventory"
            >
              {outOfStockAlertRows.length > 0 ? (
                <ResponsiveDataList
                  rows={outOfStockAlertRows}
                  columns={[
                    { key: 'name', label: t('inventory.lowStockAlertColItem') },
                    { key: 'category', label: t('inventory.lowStockAlertColCategory') },
                    { key: 'currentStock', label: t('inventory.lowStockAlertColCurrentStock'), align: 'right' },
                    {
                      key: 'status',
                      label: t('inventory.colStatus'),
                      render: row => <StatusBadge status={row.status} label={row.status} />,
                    },
                  ]}
                  mobileTitle={row => row.name}
                  mobileDescription={row => row.category}
                  emptyTitle={t('inventory.noOutOfStockTitle')}
                  emptyDescription={t('inventory.noOutOfStockDescription')}
                />
              ) : (
                <EmptyState
                  title={t('inventory.noOutOfStockTitle')}
                  description={t('inventory.noOutOfStockDescription')}
                  tone="inventory"
                />
              )}
            </Card>
          </div>

          <div ref={itemFormSectionRef} className="admin-workspace-stack scroll-target">
            <Card
              title={mode === 'create' ? t('inventory.addItemTitle') : t('inventory.editItemTitle', { name: selectedItem?.name ?? '' })}
              description={t('inventory.editItemDescription')}
              tone="inventory"
              actions={selectedItem ? <Badge tone="neutral" label={selectedItem.status} /> : undefined}
            >
              {canOperate ? (
                <form className="form-grid" onSubmit={handleItemSubmit} noValidate>
                  <Input
                    ref={itemNameInputRef}
                    className="scroll-target"
                    label={t('inventory.itemNameLabel')}
                    value={itemForm.name}
                    onChange={event => setItemForm(current => ({ ...current, name: event.target.value }))}
                    error={itemErrors.name}
                    placeholder="Rice"
                  />
                  <Input
                    label={t('inventory.itemCategoryLabel')}
                    value={itemForm.category}
                    onChange={event => setItemForm(current => ({ ...current, category: event.target.value }))}
                    error={itemErrors.category}
                    placeholder="Grains"
                  />
                  <Input
                    label={t('inventory.itemUnitLabel')}
                    value={itemForm.unitOfMeasure}
                    onChange={event => setItemForm(current => ({ ...current, unitOfMeasure: event.target.value }))}
                    error={itemErrors.unitOfMeasure}
                    placeholder="kg"
                  />
                  <Input
                    label={t('inventory.itemLowStockThresholdLabel')}
                    type="number"
                    min="0"
                    step="0.01"
                    value={itemForm.lowStockThreshold}
                    onChange={event => setItemForm(current => ({ ...current, lowStockThreshold: event.target.value }))}
                    error={itemErrors.lowStockThreshold}
                  />
                  <div className="inventory-item-active-toggle">
                    <Checkbox
                      label={t('inventory.itemActiveLabel')}
                      checked={itemForm.isActive}
                      onChange={event => setItemForm(current => ({ ...current, isActive: event.target.checked }))}
                    />
                  </div>

                  <div className="preview-checks">
                    <Button type="submit" disabled={itemSubmitting}>
                      {mode === 'create' ? t('inventory.createItemButton') : t('inventory.saveItemButton')}
                    </Button>
                    {mode === 'edit' ? (
                      <Button
                        type="button"
                        variant="secondary"
                      onClick={() => {
                          setMode('create');
                          setSelectedItemId(null);
                          setItemForm(emptyItemForm());
                          setMovements([]);
                          setNotice(null);
                        }}
                      >
                        {t('inventory.newItemButton')}
                      </Button>
                    ) : null}
                  </div>
                </form>
              ) : selectedItem ? (
                <div className="inventory-detail">
                  <div className="inventory-detail__row">
                    <span className="inventory-detail__label">{t('inventory.itemDetailCategoryLabel')}</span>
                    <strong>{selectedItem.category}</strong>
                  </div>
                  <div className="inventory-detail__row">
                    <span className="inventory-detail__label">{t('inventory.itemDetailUnitLabel')}</span>
                    <strong>{selectedItem.unitOfMeasure}</strong>
                  </div>
                  <div className="inventory-detail__row">
                    <span className="inventory-detail__label">{t('inventory.itemDetailCurrentStockLabel')}</span>
                    <strong>{formatInventoryStock(selectedItem.currentStock)}</strong>
                  </div>
                  <div className="inventory-detail__row">
                    <span className="inventory-detail__label">{t('inventory.itemDetailLowStockThresholdLabel')}</span>
                    <strong>{formatInventoryStock(selectedItem.lowStockThreshold)}</strong>
                  </div>
                  <div className="inventory-detail__row">
                    <span className="inventory-detail__label">{t('inventory.itemDetailStatusLabel')}</span>
                    <strong>{selectedItem.status}</strong>
                  </div>
                  <div className="inventory-detail__row">
                    <span className="inventory-detail__label">{t('inventory.itemDetailCreatedLabel')}</span>
                    <strong>{formatInventoryDateTime(selectedItem.createdAtUtc)}</strong>
                  </div>
                  <div className="inventory-detail__row">
                    <span className="inventory-detail__label">{t('inventory.itemDetailUpdatedLabel')}</span>
                    <strong>{formatInventoryDateTime(selectedItem.updatedAtUtc)}</strong>
                  </div>
                </div>
              ) : (
                <EmptyState
                  title={t('inventory.chooseItemTitle')}
                  description={t('inventory.chooseItemDescription')}
                  tone="inventory"
                />
              )}
            </Card>

            <Card
              title={t('inventory.stockAdjustmentsTitle')}
              description={t('inventory.stockAdjustmentsDescription')}
              tone="inventory"
            >
              {selectedItem ? (
                <div className="admin-form-section inventory-detail inventory-detail--adjustments">
                  <div className="inventory-detail__row">
                    <span className="inventory-detail__label">{t('inventory.itemDetailCurrentStockLabel')}</span>
                    <strong>{formatInventoryStock(selectedItem.currentStock)}</strong>
                  </div>
                  <div className="inventory-detail__row">
                    <span className="inventory-detail__label">{t('inventory.itemDetailLowStockThresholdLabel')}</span>
                    <strong>{formatInventoryStock(selectedItem.lowStockThreshold)}</strong>
                  </div>
                  <div className="inventory-detail__row">
                    <span className="inventory-detail__label">{t('inventory.itemDetailStatusLabel')}</span>
                    <strong>{selectedItem.status}</strong>
                  </div>
                  <div className="inventory-detail__row">
                    <span className="inventory-detail__label">{t('inventory.itemDetailUpdatedLabel')}</span>
                    <strong>{formatInventoryDateTime(selectedItem.updatedAtUtc)}</strong>
                  </div>
                  {canAdjustInventory ? (
                    <div className="admin-form-actions">
                      <Button
                        type="button"
                        onClick={() => {
                          openAdjustmentDialog(selectedItem.inventoryItemId);
                        }}
                      >
                        {t('inventory.adjustStockButton')}
                      </Button>
                    </div>
                  ) : (
                    <div className="admin-form-note">{t('inventory.adjustmentsRequirePermission')}</div>
                  )}
                </div>
              ) : (
                <EmptyState
                  title={canAdjustInventory ? t('inventory.chooseItemTitle') : t('inventory.readOnlyAccess')}
                  description={
                    canAdjustInventory
                      ? t('inventory.selectItemForAdjustment')
                      : t('inventory.adjustmentsRequirePermission')
                  }
                  tone="inventory"
                />
              )}
            </Card>

            <Card
              title={t('inventory.batchProductionTitle')}
              description={t('inventory.batchProductionDescription')}
              tone="inventory"
              actions={
                <Badge
                  tone={menuItemsLoading || batchProductionsLoading ? 'warning' : 'neutral'}
                  label={
                    menuItemsLoading
                      ? t('inventory.menuItemsLoading')
                      : batchProductionsLoading
                      ? t('inventory.batchProductionsLoading')
                      : t('inventory.batchProductionsLoaded', { count: batchProductions.length })
                  }
                />
              }
            >
              {menuItemsError ? (
                <div className="admin-notice admin-notice--danger" role="alert">
                  {menuItemsError}
                </div>
              ) : null}
              {batchProductionsError ? (
                <div className="admin-notice admin-notice--danger" role="alert">
                  {batchProductionsError}
                </div>
              ) : null}

              {canManageBatchProduction ? (
                <div className="admin-workspace-stack">
                  <form className="admin-form" onSubmit={handleBatchProductionSubmit} noValidate>
                    <div className="admin-form-grid">
                      <Select
                        label={t('inventory.batchProductionMenuItemLabel')}
                        value={batchProductionForm.menuItemId}
                        onChange={event => setBatchProductionForm(current => ({ ...current, menuItemId: event.target.value }))}
                        error={batchProductionErrors.menuItemId}
                        helperText={t('inventory.batchProductionMenuItemHelper')}
                      >
                        <option value="">{t('inventory.batchProductionSelectMenuItem')}</option>
                        {batchPreparedMenuItems.map(item => (
                          <option key={item.menuItemId} value={item.menuItemId}>
                            {item.name} ({item.stockInventoryItemName})
                          </option>
                        ))}
                      </Select>
                      <Input
                        label={t('inventory.batchProductionQuantityLabel')}
                        type="number"
                        min="0.01"
                        step="0.01"
                        value={batchProductionForm.quantityProduced}
                        onChange={event => setBatchProductionForm(current => ({ ...current, quantityProduced: event.target.value }))}
                        error={batchProductionErrors.quantity}
                        helperText={t('inventory.batchProductionQuantityHelper')}
                      />
                      <Input
                        label={t('inventory.batchProductionBusinessDateLabel')}
                        type="date"
                        value={batchProductionForm.businessDate}
                        onChange={event => setBatchProductionForm(current => ({ ...current, businessDate: event.target.value }))}
                        error={batchProductionErrors.businessDate}
                        helperText={t('inventory.batchProductionBusinessDateHelper')}
                      />
                      <Input
                        label={t('inventory.batchProductionProducedAtLabel')}
                        type="datetime-local"
                        value={batchProductionForm.producedAtLocal}
                        onChange={event => setBatchProductionForm(current => ({ ...current, producedAtLocal: event.target.value }))}
                        error={batchProductionErrors.producedAtLocal}
                        helperText={t('inventory.batchProductionProducedAtHelper')}
                      />
                    </div>
                    <Input
                      label={t('inventory.batchProductionNotesLabel')}
                      value={batchProductionForm.notes}
                      onChange={event => setBatchProductionForm(current => ({ ...current, notes: event.target.value }))}
                      helperText={t('inventory.batchProductionNotesHelper')}
                    />
                    <div className="admin-form-actions">
                      <Button type="submit" size="lg" disabled={batchProductionSubmitting || batchProductionConfirmDisabled || batchPreparedMenuItems.length === 0}>
                        {batchProductionSubmitting ? t('inventory.batchProductionSaving') : t('inventory.batchProductionSaveButton')}
                      </Button>
                    </div>
                  </form>

                  <form className="admin-form" onSubmit={handleWastageSubmit} noValidate>
                    <div className="admin-form-section">
                      <div className="admin-form-section__title-row">
                        <div>
                          <div className="admin-form-section__title">{t('inventory.wastageTitle')}</div>
                          <div className="admin-form-section__description">{t('inventory.wastageDescription')}</div>
                        </div>
                      </div>
                      <div className="admin-form-grid">
                        <Select
                          label={t('inventory.wastageMenuItemLabel')}
                          value={wastageForm.menuItemId}
                          onChange={event => setWastageForm(current => ({ ...current, menuItemId: event.target.value }))}
                          error={wastageErrors.menuItemId}
                          helperText={t('inventory.wastageMenuItemHelper')}
                        >
                          <option value="">{t('inventory.wastageSelectMenuItem')}</option>
                          {batchPreparedMenuItems.map(item => (
                            <option key={item.menuItemId} value={item.menuItemId}>
                              {item.name} ({item.stockInventoryItemName})
                            </option>
                          ))}
                        </Select>
                        <Input
                          label={t('inventory.wastageQuantityLabel')}
                          type="number"
                          min="0.01"
                          step="0.01"
                          value={wastageForm.quantity}
                          onChange={event => setWastageForm(current => ({ ...current, quantity: event.target.value }))}
                          error={wastageErrors.quantity}
                          helperText={t('inventory.wastageQuantityHelper')}
                        />
                        <Input
                          label={t('inventory.wastageReasonLabel')}
                          value={wastageForm.reason}
                          onChange={event => setWastageForm(current => ({ ...current, reason: event.target.value }))}
                          error={wastageErrors.reason}
                          helperText={t('inventory.wastageReasonHelper')}
                        />
                        <Input
                          label={t('inventory.wastageRecordedAtLabel')}
                          type="datetime-local"
                          value={wastageForm.wastedAtLocal}
                          onChange={event => setWastageForm(current => ({ ...current, wastedAtLocal: event.target.value }))}
                          error={wastageErrors.wastedAtLocal}
                          helperText={t('inventory.wastageRecordedAtHelper')}
                        />
                      </div>
                      <Input
                        label={t('inventory.wastageNotesLabel')}
                        value={wastageForm.notes}
                        onChange={event => setWastageForm(current => ({ ...current, notes: event.target.value }))}
                        helperText={t('inventory.wastageNotesHelper')}
                      />
                      <div className="admin-form-actions">
                        <Button type="submit" size="lg" disabled={wastageSubmitting || wastageConfirmDisabled || batchPreparedMenuItems.length === 0}>
                          {wastageSubmitting ? t('inventory.wastageSaving') : t('inventory.wastageSaveButton')}
                        </Button>
                      </div>
                    </div>
                  </form>

                  <ResponsiveDataList
                    rows={batchProductionRows}
                    columns={[
                      { key: 'menuItemName', label: t('inventory.batchProductionColMenuItem') },
                      { key: 'preparedStockName', label: t('inventory.batchProductionColPreparedStock') },
                      { key: 'quantityProduced', label: t('inventory.batchProductionColQuantityProduced'), align: 'right' },
                      { key: 'totalRawQuantityConsumed', label: t('inventory.batchProductionColRawConsumed'), align: 'right' },
                      { key: 'businessDate', label: t('inventory.batchProductionColBusinessDate') },
                      { key: 'producedAt', label: t('inventory.batchProductionColProducedAt') },
                      { key: 'producedBy', label: t('inventory.batchProductionColProducedBy') },
                      { key: 'notes', label: t('inventory.batchProductionColNotes') },
                    ]}
                    mobileTitle={row => row.menuItemName}
                    mobileDescription={row => `${row.quantityProduced} · ${row.preparedStockName}`}
                    emptyTitle={t('inventory.batchProductionsEmptyTitle')}
                    emptyDescription={t('inventory.batchProductionsEmptyDescription')}
                  />
                </div>
              ) : (
                <EmptyState
                  title={t('inventory.batchProductionUnavailableTitle')}
                  description={t('inventory.batchProductionUnavailableDescription')}
                  tone="inventory"
                />
              )}
            </Card>

            <Card
              title={t('inventory.movementHistoryTitle', { name: selectedItemLabel })}
              description={t('inventory.movementHistoryDescription')}
              tone="inventory"
            >
              {movementsError ? (
                <div className="admin-notice admin-notice--danger" role="alert">
                  {movementsError}
                </div>
              ) : null}
              {movementsLoading ? (
                <EmptyState
                  title={t('inventory.movementLoadingTitle')}
                  description={t('inventory.movementLoadingDescription')}
                  tone="inventory"
                />
              ) : selectedItem ? (
                <ResponsiveDataList
                  rows={movements.map(movement => ({
                    id: movement.inventoryMovementId,
                    type: formatInventoryMovementType(movement.movementType),
                    reason: movement.reason ?? movement.notes ?? '-',
                    delta: `${movement.delta > 0 ? '+' : '-'}${formatInventoryStock(Math.abs(movement.delta))}`,
                    previousStock: formatInventoryStock(movement.previousStock),
                    resultingStock: formatInventoryStock(movement.resultingStock),
                    movementDate: formatInventoryDateTime(movement.movementDate),
                    user: `${movement.recordedByUserName}${movement.recordedByUserMobile ? ` (${movement.recordedByUserMobile})` : ''}`,
                    status: movement.resultingStatus,
                  }))}
                  columns={[
                    { key: 'type', label: t('inventory.colType') },
                    { key: 'reason', label: t('inventory.colReason') },
                    { key: 'delta', label: t('inventory.colDelta'), align: 'right' },
                    { key: 'previousStock', label: t('inventory.colPreviousQty'), align: 'right' },
                    { key: 'resultingStock', label: t('inventory.colNewQty'), align: 'right' },
                    { key: 'movementDate', label: t('inventory.colDate') },
                    { key: 'user', label: t('inventory.colUser') },
                    {
                      key: 'status',
                      label: t('inventory.colStatus'),
                      render: row => <StatusBadge status={row.status} label={row.status} />,
                    },
                  ]}
                  mobileTitle={row => row.type}
                  mobileDescription={row => `${row.reason} · ${row.user}`}
                  emptyTitle={t('inventory.noMovementHistoryTitle')}
                  emptyDescription={t('inventory.noMovementHistoryDescription')}
                />
              ) : (
                <EmptyState
                  title={t('inventory.chooseItemForHistoryTitle')}
                  description={t('inventory.chooseItemForHistoryDescription')}
                  tone="inventory"
                />
              )}
            </Card>
          </div>
        </div>

        {canAdjustInventory && selectedAdjustmentItem ? (
          <InventoryAdjustmentDialog
            item={selectedAdjustmentItem}
            form={adjustmentForm}
            errors={adjustmentErrors}
            previewDelta={adjustmentPreview.delta}
            previewQuantity={adjustmentPreview.previewQuantity}
            confirmDisabled={adjustmentConfirmDisabled}
            submitting={adjustmentSubmitting}
            onSubmit={handleAdjustmentSubmit}
            onClose={closeAdjustmentDialog}
            onMovementTypeChange={value => setAdjustmentForm(current => ({ ...current, movementType: value }))}
            onQuantityChange={value => setAdjustmentForm(current => ({ ...current, quantity: value }))}
            onReasonChange={value => setAdjustmentForm(current => ({ ...current, reason: value }))}
            onNoteChange={value => setAdjustmentForm(current => ({ ...current, notes: value }))}
            dialogRef={adjustmentDialogRef}
            quantityInputRef={adjustmentQuantityInputRef}
          />
        ) : null}
      </div>
    </InventoryManagementLayout>
  );
};

export default InventoryPage;
