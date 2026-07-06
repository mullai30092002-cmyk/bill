using System.Globalization;
using Azure;
using Azure.AI.DocumentIntelligence;
using Azure.Core;
using Microsoft.Extensions.Options;

namespace BillSoft.Infrastructure.Vendors;

internal sealed class AzureDocumentIntelligenceAnalyzer : IAzureDocumentIntelligenceAnalyzer
{
    private readonly DocumentIntelligenceClient _client;

    public AzureDocumentIntelligenceAnalyzer(IOptions<OcrOptions> options)
    {
        var ocrOptions = options?.Value ?? throw new ArgumentNullException(nameof(options));
        var azureOptions = ocrOptions.AzureDocumentIntelligence;

        _client = new DocumentIntelligenceClient(
            new Uri(azureOptions.Endpoint, UriKind.Absolute),
            new AzureKeyCredential(azureOptions.ApiKey));
    }

    public async Task<AzureDocumentIntelligenceAnalysisResult> AnalyzeInvoiceAsync(
        Stream documentStream,
        string modelId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documentStream);

        if (string.IsNullOrWhiteSpace(modelId))
        {
            return Failure("InvalidDocument", "OCR extraction failed.");
        }

        if (documentStream.CanSeek)
        {
            documentStream.Position = 0;
        }

        try
        {
            var operation = await _client.AnalyzeDocumentAsync(WaitUntil.Completed, modelId, BinaryData.FromStream(documentStream), cancellationToken);
            var result = operation.Value;

            if (result.Documents is null || result.Documents.Count == 0)
            {
                return Failure("InvalidDocument", "OCR could not extract useful data.");
            }

            var analyzedDocument = result.Documents[0];
            var fields = analyzedDocument.Fields;
            var extraction = BuildExtraction(fields);
            var warnings = BuildWarnings(extraction);

            if (!HasUsefulData(extraction))
            {
                return Failure("InvalidDocument", "OCR could not extract useful data.");
            }

            return new AzureDocumentIntelligenceAnalysisResult(
                true,
                null,
                null,
                null,
                warnings,
                extraction);
        }
        catch (RequestFailedException ex)
        {
            return MapRequestFailure(ex);
        }
        catch (InvalidDataException)
        {
            return Failure("InvalidDocument", "OCR could not extract useful data.");
        }
        catch (FormatException)
        {
            return Failure("InvalidDocument", "OCR could not extract useful data.");
        }
        catch (IOException)
        {
            return Failure("TransientFailure", "OCR service is temporarily unavailable.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Failure("UnknownProviderFailure", "OCR service is temporarily unavailable.");
        }
    }

    private static AzureDocumentIntelligenceAnalysisResult MapRequestFailure(RequestFailedException ex)
    {
        var (code, message) = ex.Status switch
        {
            429 => ("Throttled", "OCR service is temporarily unavailable."),
            503 or 502 or 504 => ("Unavailable", "OCR service is temporarily unavailable."),
            415 => ("UnsupportedFormat", "OCR could not extract useful data."),
            400 or 404 or 422 => ("InvalidDocument", "OCR could not extract useful data."),
            >= 500 => ("TransientFailure", "OCR service is temporarily unavailable."),
            _ => ("UnknownProviderFailure", "OCR service is temporarily unavailable.")
        };

        return Failure(code, message);
    }

    private static AzureDocumentIntelligenceAnalysisResult Failure(string errorCode, string errorMessage)
    {
        return new AzureDocumentIntelligenceAnalysisResult(
            false,
            errorCode,
            errorMessage,
            null,
            Array.Empty<string>(),
            null);
    }

    private static AzureDocumentIntelligenceExtractionResult BuildExtraction(IReadOnlyDictionary<string, DocumentField> fields)
    {
        var vendorName = GetStringField(fields, "VendorName", "SupplierName");
        var billNumber = GetStringField(fields, "BillNumber", "InvoiceId", "InvoiceNumber");
        var billDate = GetDateField(fields, "BillDate", "InvoiceDate", "DueDate");
        var totalAmount = GetDecimalField(fields, "TotalAmount", "InvoiceTotal", "AmountDue", "InvoiceTotalAmount");
        var lines = GetLineItems(fields);

        return new AzureDocumentIntelligenceExtractionResult(vendorName, billNumber, billDate, totalAmount, lines);
    }

    private static IReadOnlyCollection<string> BuildWarnings(AzureDocumentIntelligenceExtractionResult extraction)
    {
        var warnings = new List<string>();

        if (extraction.VendorName is null)
        {
            warnings.Add("Vendor name was not detected.");
        }

        if (extraction.BillNumber is null)
        {
            warnings.Add("Bill number was not detected.");
        }

        if (extraction.BillDate is null)
        {
            warnings.Add("Bill date was not detected.");
        }

        if (extraction.TotalAmount is null)
        {
            warnings.Add("Total amount was not detected.");
        }

        if (extraction.Lines.Count == 0)
        {
            warnings.Add("No line items were detected.");
        }

        return warnings;
    }

    private static bool HasUsefulData(AzureDocumentIntelligenceExtractionResult extraction)
    {
        return extraction.VendorName is not null
            || extraction.BillNumber is not null
            || extraction.BillDate is not null
            || extraction.TotalAmount is not null
            || extraction.Lines.Count > 0;
    }

    private static IReadOnlyCollection<AzureDocumentIntelligenceLineResult> GetLineItems(IReadOnlyDictionary<string, DocumentField> fields)
    {
        var lineField = GetField(fields, "Items", "InvoiceItems", "LineItems");
        if (lineField is null)
        {
            return Array.Empty<AzureDocumentIntelligenceLineResult>();
        }

        var list = GetValueList(lineField);
        if (list.Count == 0)
        {
            return Array.Empty<AzureDocumentIntelligenceLineResult>();
        }

        var lines = new List<AzureDocumentIntelligenceLineResult>();
        foreach (var itemField in list)
        {
            var itemFields = GetValueDictionary(itemField);
            if (itemFields.Count == 0)
            {
                continue;
            }

            var description = GetStringField(itemFields, "Description", "Item", "Name", "ProductName");
            var quantity = GetDecimalField(itemFields, "Quantity", "Qty");
            var unitCost = GetDecimalField(itemFields, "UnitPrice", "UnitCost", "Price");
            var lineTotal = GetDecimalField(itemFields, "Amount", "LineTotal", "Total");

            if (lineTotal is null && quantity?.Value is decimal quantityValue && unitCost?.Value is decimal unitCostValue)
            {
                lineTotal = new AzureDocumentIntelligenceFieldResult(
                    quantityValue * unitCostValue,
                    MinConfidence(quantity.ConfidenceScore, unitCost.ConfidenceScore));
            }

            if (description is null && quantity is null && unitCost is null && lineTotal is null)
            {
                continue;
            }

            lines.Add(new AzureDocumentIntelligenceLineResult(
                description,
                quantity,
                unitCost,
                lineTotal));
        }

        return lines;
    }

    private static AzureDocumentIntelligenceFieldResult? GetStringField(IReadOnlyDictionary<string, DocumentField> fields, params string[] names)
    {
        var field = GetField(fields, names);
        if (field is null)
        {
            return null;
        }

        var value = Normalize(field.Content);
        return string.IsNullOrWhiteSpace(value)
            ? null
            : new AzureDocumentIntelligenceFieldResult(value, GetConfidence(field));
    }

    private static AzureDocumentIntelligenceFieldResult? GetDateField(IReadOnlyDictionary<string, DocumentField> fields, params string[] names)
    {
        var field = GetField(fields, names);
        if (field is null)
        {
            return null;
        }

        if (!TryGetDateTimeValue(field, out var dateValue))
        {
            var content = Normalize(field.Content);
            if (!DateTime.TryParse(content, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dateValue))
            {
                return null;
            }
        }

        return new AzureDocumentIntelligenceFieldResult(dateValue.Date, GetConfidence(field));
    }

    private static AzureDocumentIntelligenceFieldResult? GetDecimalField(IReadOnlyDictionary<string, DocumentField> fields, params string[] names)
    {
        var field = GetField(fields, names);
        if (field is null)
        {
            return null;
        }

        if (!TryGetDecimalValue(field, out var decimalValue))
        {
            var content = Normalize(field.Content);
            if (!decimal.TryParse(content, NumberStyles.Number, CultureInfo.InvariantCulture, out decimalValue))
            {
                return null;
            }
        }

        return new AzureDocumentIntelligenceFieldResult(decimalValue, GetConfidence(field));
    }

    private static DocumentField? GetField(IReadOnlyDictionary<string, DocumentField> fields, params string[] names)
    {
        foreach (var name in names)
        {
            if (fields.TryGetValue(name, out var field))
            {
                return field;
            }
        }

        return null;
    }

    private static IReadOnlyDictionary<string, DocumentField> GetValueDictionary(DocumentField field)
    {
        var value = field.GetType().GetProperty("ValueDictionary")?.GetValue(field);
        return value as IReadOnlyDictionary<string, DocumentField> ?? new Dictionary<string, DocumentField>();
    }

    private static IReadOnlyList<DocumentField> GetValueList(DocumentField field)
    {
        var value = field.GetType().GetProperty("ValueList")?.GetValue(field);
        if (value is IReadOnlyList<DocumentField> typedList)
        {
            return typedList;
        }

        if (value is System.Collections.IEnumerable enumerable)
        {
            var list = new List<DocumentField>();
            foreach (var item in enumerable)
            {
                if (item is DocumentField documentField)
                {
                    list.Add(documentField);
                }
            }

            return list;
        }

        return Array.Empty<DocumentField>();
    }

    private static decimal? GetConfidence(DocumentField field) =>
        ConvertToDecimal(field.GetType().GetProperty("Confidence")?.GetValue(field));

    private static decimal? GetConfidence(IReadOnlyDictionary<string, DocumentField> fields, params string[] names)
    {
        var field = GetField(fields, names);
        return field is null ? null : GetConfidence(field);
    }

    private static bool TryGetDateTimeValue(DocumentField field, out DateTime value)
    {
        var propertyValue = field.GetType().GetProperty("ValueDate")?.GetValue(field);
        switch (propertyValue)
        {
            case DateTime dateTime:
                value = dateTime;
                return true;
            case DateOnly dateOnly:
                value = dateOnly.ToDateTime(TimeOnly.MinValue);
                return true;
            default:
                value = default;
                return false;
        }
    }

    private static bool TryGetDecimalValue(DocumentField field, out decimal value)
    {
        var currencyValue = field.GetType().GetProperty("ValueCurrency")?.GetValue(field);
        if (currencyValue is not null)
        {
            var amountProperty = currencyValue.GetType().GetProperty("Amount");
            var amount = amountProperty?.GetValue(currencyValue);
            var convertedAmount = ConvertToDecimal(amount);
            if (convertedAmount.HasValue)
            {
                value = convertedAmount.Value;
                return true;
            }
        }

        foreach (var propertyName in new[] { "ValueDouble", "ValueInt64", "ValueLong" })
        {
            var propertyValue = field.GetType().GetProperty(propertyName)?.GetValue(field);
            var converted = ConvertToDecimal(propertyValue);
            if (converted.HasValue)
            {
                value = converted.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static decimal? ConvertToDecimal(object? value)
    {
        return value switch
        {
            null => null,
            decimal decimalValue => decimalValue,
            float floatValue => (decimal)floatValue,
            double doubleValue => (decimal)doubleValue,
            int intValue => intValue,
            long longValue => longValue,
            short shortValue => shortValue,
            byte byteValue => byteValue,
            _ => decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedValue)
                ? parsedValue
                : null
        };
    }

    private static decimal? MinConfidence(decimal? first, decimal? second)
    {
        return first is null
            ? second
            : second is null
                ? first
                : Math.Min(first.Value, second.Value);
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
