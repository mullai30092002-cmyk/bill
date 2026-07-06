export type SetupChecklistStatus = 'Complete' | 'Missing' | 'Warning';
export type SetupChecklistItemPriority = 'Required' | 'Recommended' | 'Optional';
export type SetupBusinessType = 'Restaurant' | 'JuiceShop' | 'Bakery' | 'DessertShop' | 'CafeTakeaway';

export type SetupChecklistItemKey =
  | 'restaurantProfile'
  | 'branchCreated'
  | 'staffUsersAdded'
  | 'menuCategoriesAdded'
  | 'menuItemsAdded'
  | 'inventoryItemsAdded'
  | 'recipesOrStockMappingsConfigured'
  | 'vendorsAdded'
  | 'firstPosOrderCompleted'
  | 'firstBillPaymentCompleted';

export interface SetupChecklistItem {
  key: SetupChecklistItemKey;
  title: string;
  description: string;
  status: SetupChecklistStatus;
  priority: SetupChecklistItemPriority;
  actionLabel: string;
  actionHref: string;
  count: number;
  warningCount: number | null;
}

export interface SetupChecklistResponse {
  restaurantId: string;
  restaurantName: string;
  businessType: SetupBusinessType;
  branchId: string | null;
  branchName: string | null;
  completionPercent: number;
  completedCount: number;
  totalCount: number;
  items: SetupChecklistItem[];
}
