using BillSoft.Domain.Common;

namespace BillSoft.Domain.Vendors;

public sealed class VendorBillLine : BaseEntity
{
    public Guid VendorBillLineId { get; set; } = CreateId();

    public Guid RestaurantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid VendorBillId { get; set; }

    public Guid? InventoryItemId { get; set; }

    public Guid? InventoryMovementId { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal UnitCost { get; set; }

    public decimal LineTotal { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = UtcNow();

    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
