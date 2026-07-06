using BillSoft.Domain.Common;

namespace BillSoft.Domain.Kitchen;

public sealed class KitchenTicketNumberSequence : BaseEntity
{
    public Guid KitchenTicketNumberSequenceId { get; set; } = CreateId();

    public Guid RestaurantId { get; set; }

    public Guid BranchId { get; set; }

    public DateTime TicketDate { get; set; }

    public int LastSequence { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = UtcNow();

    public DateTimeOffset? UpdatedAt { get; set; }
}
