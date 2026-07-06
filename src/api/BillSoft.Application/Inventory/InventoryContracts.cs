namespace BillSoft.Application.Inventory;

public sealed record InventoryItemListQuery(Guid? BranchId);

public sealed record InventorySummaryQuery(Guid? BranchId);

public sealed record CreateInventoryItemRequest(
    Guid? BranchId,
    string? Name,
    string? Category,
    string? UnitOfMeasure,
    decimal LowStockThreshold,
    bool IsActive);

public sealed record UpdateInventoryItemRequest(
    string? Name,
    string? Category,
    string? UnitOfMeasure,
    decimal LowStockThreshold,
    bool IsActive);

public sealed record CreateInventoryMovementRequest(
    string? MovementType,
    decimal Quantity,
    decimal? UnitCost,
    string? ReferenceNumber,
    string? Reason,
    string? Notes,
    DateTimeOffset? MovementDate,
    DateTimeOffset? ExpiresAt,
    string? BatchReference);

public sealed record InventoryItemListItem(
    Guid InventoryItemId,
    Guid RestaurantId,
    Guid BranchId,
    string Name,
    string NormalizedName,
    string Category,
    string UnitOfMeasure,
    decimal LowStockThreshold,
    bool IsActive,
    decimal CurrentStock,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

public sealed record InventoryItemListResponse(IReadOnlyList<InventoryItemListItem> Items);

public sealed record InventoryAlertItem(
    Guid InventoryItemId,
    string Name,
    string Category,
    string UnitOfMeasure,
    decimal LowStockThreshold,
    decimal CurrentStock,
    string Status);

public sealed record InventorySummaryResponse(
    Guid RestaurantId,
    Guid BranchId,
    int TotalItems,
    int ActiveItems,
    int InactiveItems,
    int LowStockCount,
    int OutOfStockCount,
    decimal TotalCurrentStock,
    int RecentlyAdjustedCount,
    IReadOnlyList<InventoryAlertItem> LowStockItems,
    IReadOnlyList<InventoryAlertItem> OutOfStockItems);

public sealed record InventoryMovementItem(
    Guid InventoryMovementId,
    Guid InventoryItemId,
    Guid RestaurantId,
    Guid BranchId,
    string MovementType,
    decimal Quantity,
    decimal? UnitCost,
    string? ReferenceNumber,
    string? Reason,
    string? Notes,
    DateTimeOffset MovementDate,
    Guid RecordedByUserId,
    string RecordedByUserName,
    string RecordedByUserMobile,
    DateTimeOffset CreatedAtUtc,
    decimal PreviousStock,
    decimal Delta,
    decimal ResultingStock,
    string ResultingStatus,
    DateTimeOffset? ExpiresAtUtc,
    string? BatchReference);

public sealed record InventoryMovementListResponse(IReadOnlyList<InventoryMovementItem> Items);

public sealed record BatchProductionListQuery(
    Guid? BranchId,
    DateTime? FromBusinessDate,
    DateTime? ToBusinessDate);

public sealed record BatchProductionListResponse(IReadOnlyList<BatchProductionListItem> Items);

public sealed record BatchProductionListItem(
    Guid BatchProductionId,
    Guid RestaurantId,
    Guid BranchId,
    Guid MenuItemId,
    string MenuItemName,
    Guid PreparedInventoryItemId,
    string PreparedInventoryItemName,
    decimal QuantityProduced,
    DateTime BusinessDate,
    DateTimeOffset ProducedAtUtc,
    Guid ProducedByUserId,
    string ProducedByUserName,
    string? Notes,
    decimal TotalRawQuantityConsumed,
    DateTimeOffset CreatedAtUtc,
    decimal? ShelfLifeHours,
    DateTimeOffset? ExpiresAtUtc,
    string? StorageNote,
    string? BatchReference);

public sealed record BatchProductionIngredientConsumptionItem(
    Guid BatchProductionIngredientConsumptionId,
    Guid InventoryItemId,
    string InventoryItemName,
    decimal QuantityConsumed,
    Guid InventoryMovementId,
    DateTimeOffset CreatedAtUtc);

public sealed record BatchProductionDetail(
    Guid BatchProductionId,
    Guid RestaurantId,
    Guid BranchId,
    Guid MenuItemId,
    string MenuItemName,
    Guid PreparedInventoryItemId,
    string PreparedInventoryItemName,
    decimal QuantityProduced,
    DateTime BusinessDate,
    DateTimeOffset ProducedAtUtc,
    Guid ProducedByUserId,
    string ProducedByUserName,
    string? Notes,
    Guid? PreparedInventoryMovementId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    IReadOnlyCollection<BatchProductionIngredientConsumptionItem> IngredientConsumptions,
    decimal? ShelfLifeHours,
    DateTimeOffset? ExpiresAtUtc,
    string? StorageNote,
    string? BatchReference);

public sealed record CreateBatchProductionRequest(
    Guid? BranchId,
    Guid MenuItemId,
    decimal QuantityProduced,
    DateTime? BusinessDate,
    DateTimeOffset? ProducedAtUtc,
    string? Notes,
    decimal? ShelfLifeHours,
    DateTimeOffset? ExpiresAt,
    string? StorageNote,
    string? BatchReference);

public sealed record RecordPreparedStockWastageRequest(
    Guid? BranchId,
    Guid MenuItemId,
    decimal Quantity,
    DateTimeOffset? WastedAtUtc,
    string? Reason,
    string? Notes);
