import type { TranslationFunction } from '../../i18n/LanguageProvider';
import type { TranslationKey } from '../../i18n/translations';
import type {
  SetupChecklistItem,
  SetupChecklistItemKey,
  SetupChecklistItemPriority,
  SetupChecklistStatus,
} from './setupChecklistTypes';

interface SetupChecklistItemDefinition {
  titleKey: TranslationKey;
  descriptionKey: TranslationKey;
  actionLabelKey: TranslationKey;
  actionHref: string;
  countLabelKey?: TranslationKey;
  warningCountLabelKey?: TranslationKey;
}

export interface SetupChecklistViewItem {
  key: SetupChecklistItemKey;
  title: string;
  description: string;
  status: SetupChecklistStatus;
  statusLabel: string;
  priority: SetupChecklistItemPriority;
  priorityLabel: string;
  actionLabel: string;
  actionHref: string;
  countLabel: string | null;
  warningCountLabel: string | null;
}

export const setupChecklistItemDefinitions: Record<SetupChecklistItemKey, SetupChecklistItemDefinition> = {
  restaurantProfile: {
    titleKey: 'setupChecklist.restaurantProfileTitle',
    descriptionKey: 'setupChecklist.restaurantProfileDescription',
    actionLabelKey: 'setupChecklist.viewSetup',
    actionHref: '/owner/dashboard',
  },
  branchCreated: {
    titleKey: 'setupChecklist.branchCreatedTitle',
    descriptionKey: 'setupChecklist.branchCreatedDescription',
    actionLabelKey: 'setupChecklist.addBranch',
    actionHref: '/admin/branches',
    countLabelKey: 'setupChecklist.activeBranchesCount',
  },
  staffUsersAdded: {
    titleKey: 'setupChecklist.staffUsersAddedTitle',
    descriptionKey: 'setupChecklist.staffUsersAddedDescription',
    actionLabelKey: 'setupChecklist.addUsers',
    actionHref: '/admin/users',
    countLabelKey: 'setupChecklist.activeUsersCount',
  },
  menuCategoriesAdded: {
    titleKey: 'setupChecklist.menuCategoriesAddedTitle',
    descriptionKey: 'setupChecklist.menuCategoriesAddedDescription',
    actionLabelKey: 'setupChecklist.addMenu',
    actionHref: '/admin/menu',
    countLabelKey: 'setupChecklist.menuCategoriesCount',
  },
  menuItemsAdded: {
    titleKey: 'setupChecklist.menuItemsAddedTitle',
    descriptionKey: 'setupChecklist.menuItemsAddedDescription',
    actionLabelKey: 'setupChecklist.addMenu',
    actionHref: '/admin/menu',
    countLabelKey: 'setupChecklist.menuItemsCount',
  },
  inventoryItemsAdded: {
    titleKey: 'setupChecklist.inventoryItemsAddedTitle',
    descriptionKey: 'setupChecklist.inventoryItemsAddedDescription',
    actionLabelKey: 'setupChecklist.addInventory',
    actionHref: '/inventory',
    countLabelKey: 'setupChecklist.inventoryItemsCount',
  },
  recipesOrStockMappingsConfigured: {
    titleKey: 'setupChecklist.recipesOrStockMappingsConfiguredTitle',
    descriptionKey: 'setupChecklist.recipesOrStockMappingsConfiguredDescription',
    actionLabelKey: 'setupChecklist.addMenu',
    actionHref: '/admin/menu',
    countLabelKey: 'setupChecklist.recipeMappingsCount',
    warningCountLabelKey: 'setupChecklist.recipeMappingsMissingCount',
  },
  vendorsAdded: {
    titleKey: 'setupChecklist.vendorsAddedTitle',
    descriptionKey: 'setupChecklist.vendorsAddedDescription',
    actionLabelKey: 'setupChecklist.addVendors',
    actionHref: '/vendors',
    countLabelKey: 'setupChecklist.vendorsCount',
  },
  firstPosOrderCompleted: {
    titleKey: 'setupChecklist.firstPosOrderCompletedTitle',
    descriptionKey: 'setupChecklist.firstPosOrderCompletedDescription',
    actionLabelKey: 'setupChecklist.createTestOrder',
    actionHref: '/pos/orders',
    countLabelKey: 'setupChecklist.confirmedPosOrdersCount',
    warningCountLabelKey: 'setupChecklist.draftPosOrdersCount',
  },
  firstBillPaymentCompleted: {
    titleKey: 'setupChecklist.firstBillPaymentCompletedTitle',
    descriptionKey: 'setupChecklist.firstBillPaymentCompletedDescription',
    actionLabelKey: 'setupChecklist.completeFirstBill',
    actionHref: '/billing',
    countLabelKey: 'setupChecklist.completedBillsPaymentsCount',
    warningCountLabelKey: 'setupChecklist.openBillsCount',
  },
};

export const getSetupChecklistCardTone = (status: SetupChecklistStatus) => {
  switch (status) {
    case 'Complete':
      return 'dashboard' as const;
    case 'Warning':
      return 'admin' as const;
    default:
      return 'inventory' as const;
  }
};

export const getSetupChecklistStatusLabelKey = (status: SetupChecklistStatus): TranslationKey => {
  switch (status) {
    case 'Complete':
      return 'setupChecklist.statusComplete';
    case 'Warning':
      return 'setupChecklist.statusWarning';
    default:
      return 'setupChecklist.statusMissing';
  }
};

export const getSetupChecklistPriorityLabelKey = (priority: SetupChecklistItemPriority): TranslationKey => {
  switch (priority) {
    case 'Required':
      return 'setupChecklist.priorityRequired';
    case 'Recommended':
      return 'setupChecklist.priorityRecommended';
    default:
      return 'setupChecklist.priorityOptional';
  }
};

export const getSetupChecklistPriorityTone = (priority: SetupChecklistItemPriority) => {
  switch (priority) {
    case 'Required':
      return 'warning' as const;
    case 'Recommended':
      return 'info' as const;
    default:
      return 'neutral' as const;
  }
};

export const buildSetupChecklistViewItem = (item: SetupChecklistItem, t: TranslationFunction): SetupChecklistViewItem => {
  const definition = setupChecklistItemDefinitions[item.key];
  const statusLabel = t(getSetupChecklistStatusLabelKey(item.status));
  const priorityLabel = t(getSetupChecklistPriorityLabelKey(item.priority));

  if (!definition) {
    return {
      key: item.key,
      title: item.title,
      description: item.description,
      status: item.status,
      statusLabel,
      priority: item.priority,
      priorityLabel,
      actionLabel: item.actionLabel,
      actionHref: item.actionHref,
      countLabel: item.count > 0 ? String(item.count) : null,
      warningCountLabel: item.warningCount ? String(item.warningCount) : null,
    };
  }

  return {
    key: item.key,
    title: t(definition.titleKey),
    description: t(definition.descriptionKey),
    status: item.status,
    statusLabel,
    priority: item.priority,
    priorityLabel,
    actionLabel: t(definition.actionLabelKey),
    actionHref: definition.actionHref,
    countLabel:
      definition.countLabelKey && item.count > 0 ? t(definition.countLabelKey, { count: item.count }) : null,
    warningCountLabel:
      definition.warningCountLabelKey && item.warningCount && item.warningCount > 0
        ? t(definition.warningCountLabelKey, { count: item.warningCount })
        : null,
  };
};
