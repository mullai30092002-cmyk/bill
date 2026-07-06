using BillSoft.Domain.Common;

namespace BillSoft.Domain.Menu;

public sealed class MenuItem : BaseEntity
{
    public Guid MenuItemId { get; set; } = CreateId();

    public Guid RestaurantId { get; set; }

    public Guid MenuCategoryId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Sku { get; set; }

    public decimal BasePrice { get; set; }

    public decimal TaxRate { get; set; }

    public bool IsVegetarian { get; set; }

    public bool IsAvailableForEatIn { get; set; }

    public bool IsAvailableForParcel { get; set; }

    public MenuItemInventoryDeductionMode InventoryDeductionMode { get; set; } = MenuItemInventoryDeductionMode.RecipeOnServe;

    public MenuItemStatus Status { get; set; } = MenuItemStatus.Active;

    public DateTimeOffset CreatedAt { get; set; } = UtcNow();

    public DateTimeOffset? UpdatedAt { get; set; }

    public void UpdateProfile(
        Guid menuCategoryId,
        string name,
        string? description,
        string? sku,
        decimal basePrice,
        decimal taxRate,
        bool isVegetarian,
        bool isAvailableForEatIn,
        bool isAvailableForParcel,
        MenuItemInventoryDeductionMode inventoryDeductionMode,
        DateTimeOffset? updatedAt = null)
    {
        MenuCategoryId = menuCategoryId;
        Name = name.Trim();
        Description = NormalizeOptional(description);
        Sku = NormalizeOptional(sku);
        BasePrice = basePrice;
        TaxRate = taxRate;
        IsVegetarian = isVegetarian;
        IsAvailableForEatIn = isAvailableForEatIn;
        IsAvailableForParcel = isAvailableForParcel;
        InventoryDeductionMode = inventoryDeductionMode;
        MarkUpdated(updatedAt);
    }

    public void Activate(DateTimeOffset? updatedAt = null)
    {
        Status = MenuItemStatus.Active;
        MarkUpdated(updatedAt);
    }

    public void Deactivate(DateTimeOffset? updatedAt = null)
    {
        Status = MenuItemStatus.Inactive;
        MarkUpdated(updatedAt);
    }

    public void MarkUpdated(DateTimeOffset? updatedAt = null)
    {
        UpdatedAt = ResolveUpdatedAt(updatedAt);
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
