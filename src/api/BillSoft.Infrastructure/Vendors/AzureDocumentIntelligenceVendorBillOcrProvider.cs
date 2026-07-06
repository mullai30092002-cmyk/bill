using System.Globalization;
using BillSoft.Application.Vendors;
using Microsoft.Extensions.Options;

namespace BillSoft.Infrastructure.Vendors;

public sealed class AzureDocumentIntelligenceVendorBillOcrProvider : IVendorBillOcrProvider
{
    private readonly IAzureDocumentIntelligenceAnalyzer _analyzer;
    private readonly OcrOptions _options;

    public AzureDocumentIntelligenceVendorBillOcrProvider(
        IAzureDocumentIntelligenceAnalyzer analyzer,
        IOptions<OcrOptions> options)
    {
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<VendorBillOcrProviderResult> ExtractAsync(VendorBillOcrProviderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.DocumentStream is null)
        {
            throw new InvalidOperationException("Document stream is required.");
        }

        if (request.DocumentStream.CanSeek)
        {
            request.DocumentStream.Position = 0;
        }

        var analysis = await _analyzer.AnalyzeInvoiceAsync(request.DocumentStream, _options.AzureDocumentIntelligence.ModelId, cancellationToken);
        if (!analysis.IsSuccess || analysis.Extraction is null)
        {
            return new VendorBillOcrProviderResult(
                false,
                analysis.SanitizedErrorCode ?? "UnknownProviderFailure",
                analysis.SanitizedErrorMessage ?? "OCR extraction failed.",
                analysis.ProviderCorrelationId,
                null,
                analysis.Warnings ?? Array.Empty<string>(),
                null);
        }

        var extraction = analysis.Extraction;

        return new VendorBillOcrProviderResult(
            true,
            null,
            null,
            analysis.ProviderCorrelationId,
            CalculateOverallConfidence(analysis, extraction),
            analysis.Warnings ?? Array.Empty<string>(),
            new VendorBillOcrExtraction(
                ToField(extraction.VendorName),
                ToField(extraction.BillNumber),
                ToDateField(extraction.BillDate),
                ToNullableDecimalField(extraction.TotalAmount),
                extraction.Lines.Select(line => new VendorBillOcrLineExtraction(
                    ToStringField(line.Description),
                    ToDecimalField(line.Quantity),
                    ToDecimalField(line.UnitCost),
                    ToDecimalField(line.LineTotal))).ToArray()));
    }

    private static decimal? CalculateOverallConfidence(AzureDocumentIntelligenceAnalysisResult analysis, AzureDocumentIntelligenceExtractionResult extraction)
    {
        if (analysis.Extraction is null)
        {
            return analysis.Warnings.Count == 0 ? 0.95m : 0.80m;
        }

        var values = new List<decimal?>();

        if (extraction.VendorName?.ConfidenceScore is not null)
        {
            values.Add(extraction.VendorName.ConfidenceScore);
        }

        if (extraction.BillNumber?.ConfidenceScore is not null)
        {
            values.Add(extraction.BillNumber.ConfidenceScore);
        }

        if (extraction.BillDate?.ConfidenceScore is not null)
        {
            values.Add(extraction.BillDate.ConfidenceScore);
        }

        if (extraction.TotalAmount?.ConfidenceScore is not null)
        {
            values.Add(extraction.TotalAmount.ConfidenceScore);
        }

        foreach (var line in extraction.Lines)
        {
            values.Add(line.Description?.ConfidenceScore);
            values.Add(line.Quantity?.ConfidenceScore);
            values.Add(line.UnitCost?.ConfidenceScore);
            values.Add(line.LineTotal?.ConfidenceScore);
        }

        var numericValues = values.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        if (numericValues.Length == 0)
        {
            return analysis.Warnings.Count == 0 ? 0.95m : 0.80m;
        }

        return numericValues.Min();
    }

    private static VendorBillOcrFieldValue<string>? ToField(AzureDocumentIntelligenceFieldResult? field) =>
        ToTypedField<string>(field);

    private static VendorBillOcrFieldValue<string>? ToStringField(AzureDocumentIntelligenceFieldResult? field) =>
        ToTypedField<string>(field);

    private static VendorBillOcrFieldValue<DateTime>? ToDateField(AzureDocumentIntelligenceFieldResult? field) =>
        ToTypedField<DateTime>(field);

    private static VendorBillOcrFieldValue<decimal>? ToNullableDecimalField(AzureDocumentIntelligenceFieldResult? field) =>
        ToTypedField<decimal>(field);

    private static VendorBillOcrFieldValue<decimal>? ToDecimalField(AzureDocumentIntelligenceFieldResult? field) =>
        ToTypedField<decimal>(field);

    private static VendorBillOcrFieldValue<T>? ToTypedField<T>(AzureDocumentIntelligenceFieldResult? field)
    {
        if (field is null || field.Value is null)
        {
            return null;
        }

        if (field.Value is T typedValue)
        {
            return new VendorBillOcrFieldValue<T>(typedValue, field.ConfidenceScore);
        }

        if (typeof(T) == typeof(string))
        {
            var text = Convert.ToString(field.Value, CultureInfo.InvariantCulture)?.Trim();
            return string.IsNullOrWhiteSpace(text)
                ? null
                : new VendorBillOcrFieldValue<T>((T)(object)text, field.ConfidenceScore);
        }

        return null;
    }
}
