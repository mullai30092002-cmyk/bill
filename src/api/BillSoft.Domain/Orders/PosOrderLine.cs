using BillSoft.Domain.Common;

namespace BillSoft.Domain.Orders;

public sealed class PosOrderLine : BaseEntity
{
    public Guid PosOrderLineId { get; set; } = CreateId();

    public Guid PosOrderId { get; set; }

    public Guid RestaurantId { get; set; }

    public Guid MenuItemId { get; set; }

    public Guid MenuCategoryId { get; set; }

    public string MenuItemNameSnapshot { get; set; } = string.Empty;

    public string MenuCategoryNameSnapshot { get; set; } = string.Empty;

    public string? SkuSnapshot { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal TaxRate { get; set; }

    public decimal Quantity { get; set; }

    public decimal LineSubtotal { get; set; }

    public decimal LineTax { get; set; }

    public decimal LineTotal { get; set; }

    public string? Notes { get; set; }

    public int DisplayOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = UtcNow();

    public DateTimeOffset? UpdatedAt { get; set; }
}
