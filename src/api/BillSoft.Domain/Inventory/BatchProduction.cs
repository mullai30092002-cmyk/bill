using BillSoft.Domain.Common;

namespace BillSoft.Domain.Inventory;

public sealed class BatchProduction : BaseEntity
{
    public Guid BatchProductionId { get; set; } = CreateId();

    public Guid RestaurantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid MenuItemId { get; set; }

    public Guid PreparedInventoryItemId { get; set; }

    public decimal QuantityProduced { get; set; }

    public DateTime BusinessDate { get; set; }

    public DateTimeOffset ProducedAtUtc { get; set; } = UtcNow();

    public Guid ProducedByUserId { get; set; }

    public string? Notes { get; set; }

    public decimal? ShelfLifeHours { get; set; }

    public DateTimeOffset? ExpiresAtUtc { get; set; }

    public string? StorageNote { get; set; }

    public string? BatchReference { get; set; }

    public Guid? PreparedInventoryMovementId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = UtcNow();

    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
