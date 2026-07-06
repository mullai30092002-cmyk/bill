using BillSoft.Domain.Common;

namespace BillSoft.Domain.Inventory;

public sealed class InventoryItem : BaseEntity
{
    public Guid InventoryItemId { get; set; } = CreateId();

    public Guid RestaurantId { get; set; }

    public Guid BranchId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string UnitOfMeasure { get; set; } = string.Empty;

    public decimal LowStockThreshold { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; } = UtcNow();

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public ICollection<InventoryMovement> Movements { get; set; } = new List<InventoryMovement>();

    public void UpdateProfile(
        string name,
        string category,
        string unitOfMeasure,
        decimal lowStockThreshold,
        bool isActive,
        DateTimeOffset? updatedAtUtc = null)
    {
        Name = NormalizeRequiredText(name);
        NormalizedName = NormalizeKey(name);
        Category = NormalizeRequiredText(category);
        UnitOfMeasure = NormalizeRequiredText(unitOfMeasure);
        LowStockThreshold = lowStockThreshold;
        IsActive = isActive;
        UpdatedAtUtc = ResolveUpdatedAt(updatedAtUtc);
    }

    public void MarkUpdated(DateTimeOffset? updatedAtUtc = null)
    {
        UpdatedAtUtc = ResolveUpdatedAt(updatedAtUtc);
    }

    public static string NormalizeKey(string value)
    {
        return NormalizeRequiredText(value).ToUpperInvariant();
    }

    private static string NormalizeRequiredText(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
