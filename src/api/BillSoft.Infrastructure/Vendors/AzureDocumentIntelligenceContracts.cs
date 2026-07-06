namespace BillSoft.Infrastructure.Vendors;

public interface IAzureDocumentIntelligenceAnalyzer
{
    Task<AzureDocumentIntelligenceAnalysisResult> AnalyzeInvoiceAsync(
        Stream documentStream,
        string modelId,
        CancellationToken cancellationToken);
}

public sealed record AzureDocumentIntelligenceAnalysisResult(
    bool IsSuccess,
    string? SanitizedErrorCode,
    string? SanitizedErrorMessage,
    string? ProviderCorrelationId,
    IReadOnlyCollection<string> Warnings,
    AzureDocumentIntelligenceExtractionResult? Extraction);

public sealed record AzureDocumentIntelligenceFieldResult(
    object? Value,
    decimal? ConfidenceScore);

public sealed record AzureDocumentIntelligenceLineResult(
    AzureDocumentIntelligenceFieldResult? Description,
    AzureDocumentIntelligenceFieldResult? Quantity,
    AzureDocumentIntelligenceFieldResult? UnitCost,
    AzureDocumentIntelligenceFieldResult? LineTotal);

public sealed record AzureDocumentIntelligenceExtractionResult(
    AzureDocumentIntelligenceFieldResult? VendorName,
    AzureDocumentIntelligenceFieldResult? BillNumber,
    AzureDocumentIntelligenceFieldResult? BillDate,
    AzureDocumentIntelligenceFieldResult? TotalAmount,
    IReadOnlyCollection<AzureDocumentIntelligenceLineResult> Lines);
