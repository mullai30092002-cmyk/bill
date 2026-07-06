using BillSoft.Domain.Common;

namespace BillSoft.Domain.Kitchen;

public sealed class KitchenTicket : BaseEntity
{
    public Guid KitchenTicketId { get; set; } = CreateId();

    public Guid RestaurantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid PosOrderId { get; set; }

    public string TicketNumber { get; set; } = string.Empty;

    public KitchenTicketStatus Status { get; set; } = KitchenTicketStatus.Pending;

    public string OrderNumberSnapshot { get; set; } = string.Empty;

    public string OrderTypeSnapshot { get; set; } = string.Empty;

    public string? TableNameSnapshot { get; set; }

    public string? CustomerNameSnapshot { get; set; }

    public string? OrderNotesSnapshot { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public Guid? LastStatusChangedByUserId { get; set; }

    public Guid? CancelledByUserId { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public string? CancelReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = UtcNow();

    public DateTimeOffset? UpdatedAt { get; set; }

    public DateTimeOffset? PreparingAt { get; set; }

    public DateTimeOffset? ReadyAt { get; set; }

    public DateTimeOffset? ServedAt { get; set; }

    public KitchenTicketInventoryDeductionStatus InventoryDeductionStatus { get; set; } = KitchenTicketInventoryDeductionStatus.NotDeducted;

    public ICollection<KitchenTicketLine> KitchenTicketLines { get; set; } = new List<KitchenTicketLine>();
}
