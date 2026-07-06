using BillSoft.Domain.Common;

namespace BillSoft.Domain.Vendors;

public sealed class VendorSettlement : BaseEntity
{
    public Guid VendorSettlementId { get; set; } = CreateId();

    public Guid RestaurantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid VendorBillId { get; set; }

    public VendorSettlementPaymentMode PaymentMode { get; set; }

    public decimal Amount { get; set; }

    public string? ReferenceNumber { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset PaidAtUtc { get; set; } = UtcNow();

    public Guid RecordedByUserId { get; set; }

    public VendorSettlementStatus Status { get; set; } = VendorSettlementStatus.Active;

    public decimal PreviousOutstandingAmount { get; set; }

    public decimal NewOutstandingAmount { get; set; }

    public DateTimeOffset? CancelledAtUtc { get; set; }

    public Guid? CancelledByUserId { get; set; }

    public string? CancellationReason { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = UtcNow();

    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
