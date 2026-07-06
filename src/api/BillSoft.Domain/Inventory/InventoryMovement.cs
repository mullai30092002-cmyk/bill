using BillSoft.Domain.Common;

namespace BillSoft.Domain.Inventory;

public sealed class InventoryMovement : BaseEntity
{
    public Guid InventoryMovementId { get; set; } = CreateId();

    public Guid RestaurantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid InventoryItemId { get; set; }

    public InventoryMovementType MovementType { get; set; }

    public decimal Quantity { get; set; }

    public decimal? UnitCost { get; set; }

    public string? ReferenceNumber { get; set; }

    public string? Reason { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset? ExpiresAtUtc { get; set; }

    public string? BatchReference { get; set; }

    public DateTimeOffset MovementDate { get; set; } = UtcNow();

    public Guid RecordedByUserId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = UtcNow();
}
