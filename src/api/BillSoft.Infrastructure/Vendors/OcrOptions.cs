namespace BillSoft.Infrastructure.Vendors;

public static class OcrProviderNames
{
    public const string Fake = "Fake";
    public const string AzureDocumentIntelligence = "AzureDocumentIntelligence";
}

public sealed class OcrOptions
{
    public const string SectionName = "Ocr";

    public string Provider { get; set; } = OcrProviderNames.Fake;

    public long MaxUploadBytes { get; set; } = 10 * 1024 * 1024;

    public string StorageRootPath { get; set; } = string.Empty;

    public AzureDocumentIntelligenceOptions AzureDocumentIntelligence { get; set; } = new();
}

public sealed class AzureDocumentIntelligenceOptions
{
    public string Endpoint { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string ModelId { get; set; } = "prebuilt-invoice";
}

internal static class OcrOptionsValidator
{
    public static void Validate(OcrOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaxUploadBytes <= 0)
        {
            throw new InvalidOperationException("Ocr:MaxUploadBytes must be greater than zero.");
        }

        if (string.Equals(options.Provider, OcrProviderNames.AzureDocumentIntelligence, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(options.AzureDocumentIntelligence.Endpoint))
            {
                throw new InvalidOperationException("Ocr:AzureDocumentIntelligence:Endpoint is required when Ocr:Provider is AzureDocumentIntelligence.");
            }

            if (string.IsNullOrWhiteSpace(options.AzureDocumentIntelligence.ApiKey))
            {
                throw new InvalidOperationException("Ocr:AzureDocumentIntelligence:ApiKey is required when Ocr:Provider is AzureDocumentIntelligence.");
            }

            if (string.IsNullOrWhiteSpace(options.AzureDocumentIntelligence.ModelId))
            {
                throw new InvalidOperationException("Ocr:AzureDocumentIntelligence:ModelId is required when Ocr:Provider is AzureDocumentIntelligence.");
            }
        }
    }
}
