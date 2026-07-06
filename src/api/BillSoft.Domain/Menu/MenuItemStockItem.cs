using BillSoft.Domain.Common;

namespace BillSoft.Domain.Menu;

public sealed class MenuItemStockItem : BaseEntity
{
    public Guid MenuItemStockItemId { get; set; } = CreateId();

    public Guid RestaurantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid MenuItemId { get; set; }

    public Guid InventoryItemId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = UtcNow();

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public void MarkUpdated(DateTimeOffset? updatedAtUtc = null)
    {
        UpdatedAtUtc = ResolveUpdatedAt(updatedAtUtc);
    }
}
