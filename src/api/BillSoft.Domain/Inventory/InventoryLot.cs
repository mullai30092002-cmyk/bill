using BillSoft.Domain.Common;

namespace BillSoft.Domain.Inventory;

public sealed class InventoryLot : BaseEntity
{
    public Guid InventoryLotId { get; set; } = CreateId();

    public Guid RestaurantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid InventoryItemId { get; set; }

    public Guid? SourceMovementId { get; set; }

    public Guid? SourceBatchProductionId { get; set; }

    public string? BatchReference { get; set; }

    public DateTimeOffset ReceivedAtUtc { get; set; } = UtcNow();

    public DateTimeOffset? ExpiresAtUtc { get; set; }

    public decimal InitialQuantity { get; set; }

    public decimal RemainingQuantity { get; set; }

    public string UnitOfMeasure { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = UtcNow();

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public DateTimeOffset? ClosedAtUtc { get; set; }
}
