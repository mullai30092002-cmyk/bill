using System.Text;

namespace BillSoft.Domain.Localization;

public static class MobileNumberNormalizer
{
    public static NormalizedMobileNumber Normalize(string? countryCode, string? mobileNumber)
    {
        var profile = CountryProfileCatalog.GetRequired(countryCode);
        return Normalize(profile, mobileNumber);
    }

    public static NormalizedMobileNumber Normalize(CountryProfile profile, string? mobileNumber)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (string.IsNullOrWhiteSpace(mobileNumber))
        {
            throw new InvalidOperationException("Mobile number is required.");
        }

        var digits = ExtractDigits(mobileNumber);
        if (string.IsNullOrWhiteSpace(digits))
        {
            throw new InvalidOperationException($"Mobile number cannot normalize for country code {profile.CountryCode}.");
        }

        var nationalNumber = profile.CountryCode switch
        {
            "SG" => NormalizeSingapore(digits),
            "IN" => NormalizeIndia(digits, profile.TrunkPrefixLength ?? 0),
            _ => throw new InvalidOperationException($"Unsupported country code '{profile.CountryCode}'.")
        };

        if (string.IsNullOrWhiteSpace(nationalNumber) || nationalNumber.Length != profile.NationalNumberLength)
        {
            throw new InvalidOperationException($"Mobile number cannot normalize for country code {profile.CountryCode}.");
        }

        return new NormalizedMobileNumber(
            profile.CountryCode,
            profile.DialCode,
            nationalNumber,
            $"{profile.DialCode}{nationalNumber}");
    }

    private static string NormalizeSingapore(string digits)
    {
        if (digits.Length == 8)
        {
            return digits;
        }

        if (digits.Length == 10 && digits.StartsWith("65", StringComparison.Ordinal))
        {
            return digits[2..];
        }

        return string.Empty;
    }

    private static string NormalizeIndia(string digits, int trunkPrefixLength)
    {
        if (digits.Length == 10)
        {
            return digits;
        }

        if (digits.Length == 12 && digits.StartsWith("91", StringComparison.Ordinal))
        {
            return digits[2..];
        }

        if (trunkPrefixLength > 0 && digits.Length == 11 && digits.StartsWith("0", StringComparison.Ordinal))
        {
            return digits[trunkPrefixLength..];
        }

        return string.Empty;
    }

    private static string ExtractDigits(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            if (char.IsDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
