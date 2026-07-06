using BillSoft.Domain.Common;

namespace BillSoft.Domain.Billing;

public sealed class Bill : BaseEntity
{
    public Guid BillId { get; set; } = CreateId();

    public Guid RestaurantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid PosOrderId { get; set; }

    public string BillNumber { get; set; } = string.Empty;

    public DateTime BusinessDate { get; set; } = DateTime.UtcNow.Date;

    public BillStatus Status { get; set; } = BillStatus.Unpaid;

    public decimal Subtotal { get; set; }

    public decimal TaxTotal { get; set; }

    public decimal GrandTotal { get; set; }

    public decimal AmountPaid { get; set; }

    public decimal BalanceDue { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public Guid? CancelledByUserId { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public string? CancelReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = UtcNow();

    public DateTimeOffset? UpdatedAt { get; set; }

    public ICollection<BillLine> BillLines { get; set; } = new List<BillLine>();

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public ICollection<BillPrintEvent> PrintEvents { get; set; } = new List<BillPrintEvent>();
}
