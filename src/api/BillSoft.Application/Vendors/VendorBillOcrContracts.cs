namespace BillSoft.Application.Vendors;

public sealed record VendorBillOcrDraftListQuery(Guid? BranchId);

public sealed record VendorBillOcrDraftListItem(
    Guid VendorBillOcrDraftId,
    Guid RestaurantId,
    Guid BranchId,
    string OriginalFileName,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record VendorBillOcrDraftListResponse(IReadOnlyCollection<VendorBillOcrDraftListItem> Items);

public sealed record VendorBillOcrDraftLineDetail(
    Guid VendorBillOcrDraftLineId,
    int LineNumber,
    string ExtractedDescription,
    decimal? ExtractedQuantity,
    decimal? ExtractedUnitCost,
    decimal? ExtractedLineTotal,
    decimal? ConfidenceScore,
    Guid? SelectedInventoryItemId,
    bool IsIgnored,
    string? ReviewedDescription,
    decimal? ReviewedQuantity,
    decimal? ReviewedUnitCost,
    decimal? ReviewedLineTotal,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record VendorBillOcrDraftDetail(
    Guid VendorBillOcrDraftId,
    Guid RestaurantId,
    Guid BranchId,
    Guid UploadedByUserId,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    string Status,
    string? ExtractedVendorName,
    string? ExtractedBillNumber,
    DateTime? ExtractedBillDate,
    decimal? ExtractedTotalAmount,
    decimal? ExtractedConfidenceScore,
    IReadOnlyCollection<string> ProviderWarnings,
    bool HasDuplicateReceipt,
    string? DuplicateReceiptWarning,
    bool CanOverrideDuplicateReceipt,
    Guid? ReviewedVendorId,
    string? ReviewedBillNumber,
    DateTime? ReviewedBillDate,
    decimal? ReviewedTotalAmount,
    string? SafeErrorMessage,
    Guid? ConfirmedVendorBillId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ConfirmedAtUtc,
    IReadOnlyCollection<VendorBillOcrDraftLineDetail> Lines);

public sealed record UpdateVendorBillOcrDraftLineRequest(
    Guid VendorBillOcrDraftLineId,
    string? ReviewedDescription,
    decimal? ReviewedQuantity,
    decimal? ReviewedUnitCost,
    decimal? ReviewedLineTotal,
    Guid? SelectedInventoryItemId,
    bool IsIgnored);

public sealed record CreateVendorBillOcrDraftLineRequest(
    string ReviewedDescription,
    decimal ReviewedQuantity,
    decimal ReviewedUnitCost,
    decimal ReviewedLineTotal,
    Guid? SelectedInventoryItemId,
    bool IsIgnored);

public sealed record UpdateVendorBillOcrDraftRequest(
    Guid? ReviewedVendorId,
    string? ReviewedBillNumber,
    DateTime? ReviewedBillDate,
    decimal? ReviewedTotalAmount,
    IReadOnlyCollection<UpdateVendorBillOcrDraftLineRequest>? Lines,
    IReadOnlyCollection<CreateVendorBillOcrDraftLineRequest>? AddedLines,
    IReadOnlyCollection<Guid>? RemovedLineIds);
