using BillSoft.Domain.Common;

namespace BillSoft.Domain.Billing;

public sealed class BillPrintEvent : BaseEntity
{
    public Guid BillPrintEventId { get; set; } = CreateId();

    public Guid RestaurantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid BillId { get; set; }

    public Guid? PrintedByUserId { get; set; }

    public int PrintSequence { get; set; }

    public string? PrintReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = UtcNow();
}
