using System.Globalization;
using System.Text;
using BillSoft.Application.Vendors;

namespace BillSoft.Infrastructure.Vendors;

public sealed class FakeVendorBillOcrProvider : IVendorBillOcrProvider
{
    public Task<VendorBillOcrProviderResult> ExtractAsync(VendorBillOcrProviderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var reader = new StreamReader(request.DocumentStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = reader.ReadToEnd();

        if (text.Contains("FAIL", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(CreateFailure("InvalidDocument", "OCR extraction failed."));
        }

        var warnings = new List<string>();
        var lines = new List<VendorBillOcrLineExtraction>();
        VendorBillOcrFieldValue<string>? vendorName = null;
        VendorBillOcrFieldValue<string>? billNumber = null;
        VendorBillOcrFieldValue<DateTime>? billDate = null;
        VendorBillOcrFieldValue<decimal>? totalAmount = null;

        foreach (var rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (rawLine.StartsWith("Vendor:", StringComparison.OrdinalIgnoreCase))
            {
                var value = rawLine["Vendor:".Length..].Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    vendorName = new VendorBillOcrFieldValue<string>(value, 0.95m);
                }
                else
                {
                    warnings.Add("Vendor name was not detected.");
                }

                continue;
            }

            if (rawLine.StartsWith("BillNumber:", StringComparison.OrdinalIgnoreCase))
            {
                var value = rawLine["BillNumber:".Length..].Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    billNumber = new VendorBillOcrFieldValue<string>(value, 0.95m);
                }
                else
                {
                    warnings.Add("Bill number was not detected.");
                }

                continue;
            }

            if (rawLine.StartsWith("BillDate:", StringComparison.OrdinalIgnoreCase)
                && DateTime.TryParse(
                    rawLine["BillDate:".Length..].Trim(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsedBillDate))
            {
                billDate = new VendorBillOcrFieldValue<DateTime>(parsedBillDate.Date, 0.95m);
                continue;
            }

            if (rawLine.StartsWith("BillDate:", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add("Bill date was not detected.");
                continue;
            }

            if (rawLine.StartsWith("TotalAmount:", StringComparison.OrdinalIgnoreCase)
                && decimal.TryParse(rawLine["TotalAmount:".Length..].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedTotalAmount))
            {
                totalAmount = new VendorBillOcrFieldValue<decimal>(parsedTotalAmount, 0.95m);
                continue;
            }

            if (rawLine.StartsWith("TotalAmount:", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add("Total amount was not detected.");
                continue;
            }

            if (rawLine.StartsWith("Warning:", StringComparison.OrdinalIgnoreCase))
            {
                var warning = rawLine["Warning:".Length..].Trim();
                if (!string.IsNullOrWhiteSpace(warning))
                {
                    warnings.Add(warning);
                }

                continue;
            }

            if (rawLine.StartsWith("Line:", StringComparison.OrdinalIgnoreCase))
            {
                var payload = rawLine["Line:".Length..].Trim();
                var segments = payload.Split('|', StringSplitOptions.TrimEntries);
                if (segments.Length < 3)
                {
                    warnings.Add("A line item was not fully detected.");
                    continue;
                }

                var description = new VendorBillOcrFieldValue<string>(segments[0], 0.95m);

                if (!decimal.TryParse(segments[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var quantity))
                {
                    warnings.Add("A line quantity was not detected.");
                    continue;
                }

                if (!decimal.TryParse(segments[2], NumberStyles.Number, CultureInfo.InvariantCulture, out var unitCost))
                {
                    warnings.Add("A line unit cost was not detected.");
                    continue;
                }

                VendorBillOcrFieldValue<decimal>? lineTotal = null;
                if (segments.Length > 3 && decimal.TryParse(segments[3], NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedLineTotal))
                {
                    lineTotal = new VendorBillOcrFieldValue<decimal>(parsedLineTotal, 0.95m);
                }
                else
                {
                    lineTotal = new VendorBillOcrFieldValue<decimal>(quantity * unitCost, 0.90m);
                }

                lines.Add(new VendorBillOcrLineExtraction(
                    description,
                    new VendorBillOcrFieldValue<decimal>(quantity, 0.95m),
                    new VendorBillOcrFieldValue<decimal>(unitCost, 0.95m),
                    lineTotal));
            }
        }

        if (vendorName is null)
        {
            warnings.Add("Vendor name was not detected.");
        }

        if (billNumber is null && billDate is null && totalAmount is null && lines.Count == 0 && warnings.Count > 0)
        {
            return Task.FromResult(CreateFailure("InvalidDocument", "OCR could not extract useful data."));
        }

        if (billNumber is null && billDate is null && totalAmount is null && lines.Count == 0 && vendorName is null)
        {
            return Task.FromResult(CreateFailure("InvalidDocument", "OCR could not extract useful data."));
        }

        var overallConfidence = warnings.Count == 0 ? 0.95m : 0.80m;

        return Task.FromResult(
            new VendorBillOcrProviderResult(
                true,
                null,
                null,
                null,
                overallConfidence,
                warnings,
                new VendorBillOcrExtraction(vendorName, billNumber, billDate, totalAmount, lines)));
    }

    private static VendorBillOcrProviderResult CreateFailure(string sanitizedErrorCode, string sanitizedErrorMessage)
    {
        return new VendorBillOcrProviderResult(
            false,
            sanitizedErrorCode,
            sanitizedErrorMessage,
            null,
            null,
            Array.Empty<string>(),
            null);
    }
}
