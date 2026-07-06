using BillSoft.Domain.Common;

namespace BillSoft.Domain.Cashiering;

public sealed class CashDrawerMovement : BaseEntity
{
    public Guid CashDrawerMovementId { get; set; } = CreateId();

    public Guid RestaurantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid CashierShiftId { get; set; }

    public CashDrawerMovementType MovementType { get; set; }

    public decimal Amount { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = UtcNow();
}
