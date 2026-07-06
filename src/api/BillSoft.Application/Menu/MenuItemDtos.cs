namespace BillSoft.Application.Menu;

public enum MenuItemAvailabilityFilter
{
    All,
    EatIn,
    Parcel
}

public sealed record MenuItemListQuery(
    Guid? MenuCategoryId,
    string? Status,
    string? Search,
    string? Availability);

public sealed record MenuItemListResponse(
    IReadOnlyCollection<MenuItemDetail> Items);

public sealed record MenuItemDetail(
    Guid MenuItemId,
    Guid RestaurantId,
    Guid MenuCategoryId,
    string CategoryName,
    string Name,
    string? Description,
    string? Sku,
    decimal BasePrice,
    decimal TaxRate,
    bool IsVegetarian,
    bool IsAvailableForEatIn,
    bool IsAvailableForParcel,
    string InventoryDeductionMode,
    Guid? StockInventoryItemId,
    string? StockInventoryItemName,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CreateMenuItemRequest(
    Guid? MenuCategoryId,
    string? Name,
    string? Description,
    string? Sku,
    decimal BasePrice,
    decimal TaxRate,
    bool IsVegetarian,
    bool IsAvailableForEatIn,
    bool IsAvailableForParcel,
    string? InventoryDeductionMode,
    Guid? StockInventoryItemId);

public sealed record UpdateMenuItemRequest(
    Guid? MenuCategoryId,
    string? Name,
    string? Description,
    string? Sku,
    decimal BasePrice,
    decimal TaxRate,
    bool IsVegetarian,
    bool IsAvailableForEatIn,
    bool IsAvailableForParcel,
    string? InventoryDeductionMode,
    Guid? StockInventoryItemId);

public sealed record MenuItemPriceHistoryItem(
    Guid MenuItemPriceHistoryId,
    Guid MenuItemId,
    decimal OldPrice,
    decimal NewPrice,
    Guid? ChangedByUserId,
    DateTimeOffset ChangedAt,
    string? Reason);

public sealed record MenuItemPriceHistoryResponse(
    IReadOnlyCollection<MenuItemPriceHistoryItem> Items);

public sealed record MenuItemRecipeIngredientRequest(
    Guid InventoryItemId,
    decimal QuantityRequired);

public sealed record UpdateMenuItemRecipeRequest(
    IReadOnlyCollection<MenuItemRecipeIngredientRequest> Ingredients);

public sealed record MenuItemRecipeIngredientDetail(
    Guid MenuItemRecipeIngredientId,
    Guid InventoryItemId,
    string InventoryItemName,
    decimal QuantityRequired,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

public sealed record MenuItemRecipeResponse(
    Guid MenuItemId,
    string MenuItemName,
    Guid RestaurantId,
    Guid BranchId,
    IReadOnlyCollection<MenuItemRecipeIngredientDetail> Ingredients);
