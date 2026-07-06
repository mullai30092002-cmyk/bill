using BillSoft.Domain.Common;

namespace BillSoft.Domain.Inventory;

public sealed class BatchProductionIngredientConsumption : BaseEntity
{
    public Guid BatchProductionIngredientConsumptionId { get; set; } = CreateId();

    public Guid RestaurantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid BatchProductionId { get; set; }

    public Guid InventoryItemId { get; set; }

    public Guid InventoryMovementId { get; set; }

    public string InventoryItemNameSnapshot { get; set; } = string.Empty;

    public decimal QuantityConsumed { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = UtcNow();
}
