using BillSoft.Domain.Common;

namespace BillSoft.Domain.Vendors;

public sealed class VendorBillOcrDraft : BaseEntity
{
    public Guid VendorBillOcrDraftId { get; set; } = CreateId();

    public Guid RestaurantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid UploadedByUserId { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string StoredFilePath { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public VendorBillOcrDraftStatus Status { get; set; } = VendorBillOcrDraftStatus.Uploaded;

    public string? ExtractedVendorName { get; set; }

    public string? ExtractedBillNumber { get; set; }

    public DateTime? ExtractedBillDate { get; set; }

    public decimal? ExtractedTotalAmount { get; set; }

    public decimal? ExtractedConfidenceScore { get; set; }

    public string? ProviderWarningsJson { get; set; }

    public Guid? ReviewedVendorId { get; set; }

    public string? ReviewedBillNumber { get; set; }

    public DateTime? ReviewedBillDate { get; set; }

    public decimal? ReviewedTotalAmount { get; set; }

    public string? SafeErrorMessage { get; set; }

    public Guid? ConfirmedVendorBillId { get; set; }

    public Guid? ConfirmedByUserId { get; set; }

    public DateTimeOffset? ConfirmedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = UtcNow();

    public DateTimeOffset UpdatedAtUtc { get; set; } = UtcNow();

    public ICollection<VendorBillOcrDraftLine> Lines { get; set; } = new List<VendorBillOcrDraftLine>();
}
