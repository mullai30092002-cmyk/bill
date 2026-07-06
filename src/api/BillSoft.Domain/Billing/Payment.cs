using BillSoft.Domain.Common;

namespace BillSoft.Domain.Billing;

public sealed class Payment : BaseEntity
{
    public Guid PaymentId { get; set; } = CreateId();

    public Guid RestaurantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid BillId { get; set; }

    public Guid? CashierShiftId { get; set; }

    public string PaymentNumber { get; set; } = string.Empty;

    public PaymentMode PaymentMode { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Recorded;

    public decimal Amount { get; set; }

    public string? ReferenceNumber { get; set; }

    public string? Notes { get; set; }

    public Guid? RecordedByUserId { get; set; }

    public Guid? CancelledByUserId { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public string? CancelReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = UtcNow();

    public DateTimeOffset? UpdatedAt { get; set; }
}
