using BillSoft.Domain.Common;

namespace BillSoft.Domain.Kitchen;

public sealed class KitchenTicketInventoryDeduction : BaseEntity
{
    public Guid KitchenTicketInventoryDeductionId { get; set; } = CreateId();

    public Guid RestaurantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid KitchenTicketId { get; set; }

    public Guid InventoryItemId { get; set; }

    public Guid InventoryMovementId { get; set; }

    public decimal QuantityDeducted { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = UtcNow();
}
