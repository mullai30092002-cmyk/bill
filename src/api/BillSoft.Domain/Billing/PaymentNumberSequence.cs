using BillSoft.Domain.Common;

namespace BillSoft.Domain.Billing;

public sealed class PaymentNumberSequence : BaseEntity
{
    public Guid PaymentNumberSequenceId { get; set; } = CreateId();

    public Guid RestaurantId { get; set; }

    public Guid BranchId { get; set; }

    public DateTime PaymentDate { get; set; }

    public int LastSequence { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = UtcNow();

    public DateTimeOffset? UpdatedAt { get; set; }
}
