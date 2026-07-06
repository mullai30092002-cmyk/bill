using BillSoft.Domain.Common;

namespace BillSoft.Domain.Inventory;

public sealed class InventoryLotAllocation : BaseEntity
{
    public Guid InventoryLotAllocationId { get; set; } = CreateId();

    public Guid RestaurantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid InventoryItemId { get; set; }

    public Guid InventoryLotId { get; set; }

    public Guid InventoryMovementId { get; set; }

    public decimal QuantityAllocated { get; set; }

    public string AllocationReason { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = UtcNow();
}
