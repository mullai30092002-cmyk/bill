using BillSoft.Domain.Common;

namespace BillSoft.Domain.Vendors;

public sealed class VendorBillOcrDraftLine : BaseEntity
{
    public Guid VendorBillOcrDraftLineId { get; set; } = CreateId();

    public Guid RestaurantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid VendorBillOcrDraftId { get; set; }

    public int LineNumber { get; set; }

    public string ExtractedDescription { get; set; } = string.Empty;

    public decimal? ExtractedQuantity { get; set; }

    public decimal? ExtractedUnitCost { get; set; }

    public decimal? ExtractedLineTotal { get; set; }

    public decimal? ConfidenceScore { get; set; }

    public Guid? SelectedInventoryItemId { get; set; }

    public bool IsIgnored { get; set; }

    public string? ReviewedDescription { get; set; }

    public decimal? ReviewedQuantity { get; set; }

    public decimal? ReviewedUnitCost { get; set; }

    public decimal? ReviewedLineTotal { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = UtcNow();

    public DateTimeOffset UpdatedAtUtc { get; set; } = UtcNow();
}
