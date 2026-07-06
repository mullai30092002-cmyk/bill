using BillSoft.Domain.Common;

namespace BillSoft.Domain.Vendors;

public sealed class VendorBill : BaseEntity
{
    public Guid VendorBillId { get; set; } = CreateId();

    public Guid RestaurantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid VendorId { get; set; }

    public string? BillNumber { get; set; }

    public string? NormalizedBillNumber { get; set; }

    public DateTime BillDate { get; set; } = DateTime.UtcNow.Date;

    public DateTime? DueDate { get; set; }

    public VendorBillStatus Status { get; set; } = VendorBillStatus.Unpaid;

    public decimal TotalAmount { get; set; }

    public decimal PaidAmount { get; set; }

    public decimal BalanceAmount { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset? CancelledAtUtc { get; set; }

    public Guid? CancelledByUserId { get; set; }

    public string? CancellationReason { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = UtcNow();

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public ICollection<VendorBillLine> Lines { get; set; } = new List<VendorBillLine>();

    public ICollection<VendorSettlement> Settlements { get; set; } = new List<VendorSettlement>();
}
