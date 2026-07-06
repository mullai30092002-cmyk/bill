using BillSoft.Application.Vendors;
using BillSoft.Infrastructure.Vendors;
using Microsoft.Extensions.Options;
using Xunit;

namespace BillSoft.Tests;

public sealed class VendorBillOcrProviderTests
{
    [Fact]
    public async Task Azure_Provider_Should_Map_Header_And_Line_Extraction()
    {
        var analyzer = new FakeAzureDocumentIntelligenceAnalyzer(
            new AzureDocumentIntelligenceAnalysisResult(
                true,
                null,
                null,
                "provider-correlation-id",
                ["checked"],
                new AzureDocumentIntelligenceExtractionResult(
                    new AzureDocumentIntelligenceFieldResult("Fresh Rice", 0.98m),
                    new AzureDocumentIntelligenceFieldResult("OCR-100", 0.97m),
                    new AzureDocumentIntelligenceFieldResult(new DateTime(2026, 6, 18), 0.96m),
                    new AzureDocumentIntelligenceFieldResult(100m, 0.95m),
                    [
                        new AzureDocumentIntelligenceLineResult(
                            new AzureDocumentIntelligenceFieldResult("Rice", 0.94m),
                            new AzureDocumentIntelligenceFieldResult(10m, 0.93m),
                            new AzureDocumentIntelligenceFieldResult(10m, 0.92m),
                            new AzureDocumentIntelligenceFieldResult(100m, 0.91m))
                    ])));

        var provider = new AzureDocumentIntelligenceVendorBillOcrProvider(
            analyzer,
            Options.Create(new OcrOptions
            {
                Provider = OcrProviderNames.AzureDocumentIntelligence,
                MaxUploadBytes = 10 * 1024 * 1024,
                AzureDocumentIntelligence = new AzureDocumentIntelligenceOptions
                {
                    Endpoint = "https://example.invalid/",
                    ApiKey = "unit-test-key",
                    ModelId = "prebuilt-invoice"
                }
            }));

        await using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = await provider.ExtractAsync(
            new VendorBillOcrProviderRequest(
                stream,
                "bill.pdf",
                "application/pdf",
                3,
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("provider-correlation-id", result.ProviderCorrelationId);
        Assert.Equal(0.91m, result.OverallConfidence);
        Assert.Equal("Fresh Rice", result.Extraction?.VendorName?.Value);
        Assert.Equal("OCR-100", result.Extraction?.BillNumber?.Value);
        Assert.Equal(new DateTime(2026, 6, 18), result.Extraction?.BillDate?.Value);
        Assert.Equal(100m, result.Extraction?.TotalAmount?.Value);
        Assert.Single(result.Extraction?.Lines ?? Array.Empty<VendorBillOcrLineExtraction>());

        var line = result.Extraction!.Lines.Single();
        Assert.Equal("Rice", line.Description?.Value);
        Assert.Equal(10m, line.Quantity?.Value);
        Assert.Equal(10m, line.UnitCost?.Value);
        Assert.Equal(100m, line.LineTotal?.Value);
    }

    private sealed class FakeAzureDocumentIntelligenceAnalyzer : IAzureDocumentIntelligenceAnalyzer
    {
        private readonly AzureDocumentIntelligenceAnalysisResult _result;

        public FakeAzureDocumentIntelligenceAnalyzer(AzureDocumentIntelligenceAnalysisResult result)
        {
            _result = result;
        }

        public Task<AzureDocumentIntelligenceAnalysisResult> AnalyzeInvoiceAsync(Stream documentStream, string modelId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_result);
        }
    }
}
