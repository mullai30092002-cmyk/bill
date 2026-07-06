using BillSoft.Domain.Common;

namespace BillSoft.Domain.Kitchen;

public sealed class KitchenTicketLine : BaseEntity
{
    public Guid KitchenTicketLineId { get; set; } = CreateId();

    public Guid KitchenTicketId { get; set; }

    public Guid RestaurantId { get; set; }

    public Guid PosOrderLineId { get; set; }

    public Guid MenuItemId { get; set; }

    public Guid MenuCategoryId { get; set; }

    public string MenuItemNameSnapshot { get; set; } = string.Empty;

    public string MenuCategoryNameSnapshot { get; set; } = string.Empty;

    public string? SkuSnapshot { get; set; }

    public decimal Quantity { get; set; }

    public string? Notes { get; set; }

    public int DisplayOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = UtcNow();
}
