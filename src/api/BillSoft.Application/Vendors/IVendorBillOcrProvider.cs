namespace BillSoft.Application.Vendors;

public interface IVendorBillOcrProvider
{
    Task<VendorBillOcrProviderResult> ExtractAsync(VendorBillOcrProviderRequest request, CancellationToken cancellationToken);
}

public sealed record VendorBillOcrProviderRequest(
    Stream DocumentStream,
    string FileName,
    string ContentType,
    long Length,
    Guid RestaurantId,
    Guid BranchId,
    Guid DraftId);

public sealed record VendorBillOcrFieldValue<T>(
    T? Value,
    decimal? ConfidenceScore);

public sealed record VendorBillOcrLineExtraction(
    VendorBillOcrFieldValue<string>? Description,
    VendorBillOcrFieldValue<decimal>? Quantity,
    VendorBillOcrFieldValue<decimal>? UnitCost,
    VendorBillOcrFieldValue<decimal>? LineTotal);

public sealed record VendorBillOcrExtraction(
    VendorBillOcrFieldValue<string>? VendorName,
    VendorBillOcrFieldValue<string>? BillNumber,
    VendorBillOcrFieldValue<DateTime>? BillDate,
    VendorBillOcrFieldValue<decimal>? TotalAmount,
    IReadOnlyCollection<VendorBillOcrLineExtraction> Lines);

public sealed record VendorBillOcrProviderResult(
    bool IsSuccess,
    string? SanitizedErrorCode,
    string? SanitizedErrorMessage,
    string? ProviderCorrelationId,
    decimal? OverallConfidence,
    IReadOnlyCollection<string> Warnings,
    VendorBillOcrExtraction? Extraction);
