using BillSoft.Domain.Common;

namespace BillSoft.Domain.Orders;

public sealed class PosOrder : BaseEntity
{
    public Guid PosOrderId { get; set; } = CreateId();

    public Guid RestaurantId { get; set; }

    public Guid BranchId { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public PosOrderType OrderType { get; set; }

    public PosOrderStatus Status { get; set; } = PosOrderStatus.Draft;

    public string? TableName { get; set; }

    public string? CustomerName { get; set; }

    public string? CustomerMobile { get; set; }

    public string? Notes { get; set; }

    public decimal Subtotal { get; set; }

    public decimal TaxTotal { get; set; }

    public decimal GrandTotal { get; set; }

    public DateTimeOffset? ConfirmedAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public string? CancelReason { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public Guid? ConfirmedByUserId { get; set; }

    public Guid? CancelledByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = UtcNow();

    public DateTimeOffset? UpdatedAt { get; set; }

    public ICollection<PosOrderLine> PosOrderLines { get; set; } = new List<PosOrderLine>();
}
