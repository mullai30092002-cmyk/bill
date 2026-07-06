using BillSoft.Domain.Common;

namespace BillSoft.Domain.Orders;

public sealed class PosOrderNumberSequence : BaseEntity
{
    public Guid PosOrderNumberSequenceId { get; set; } = CreateId();

    public Guid RestaurantId { get; set; }

    public Guid BranchId { get; set; }

    public DateTime OrderDate { get; set; }

    public int LastSequence { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = UtcNow();

    public DateTimeOffset? UpdatedAt { get; set; }
}
