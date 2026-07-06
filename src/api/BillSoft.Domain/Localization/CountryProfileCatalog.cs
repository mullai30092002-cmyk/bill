namespace BillSoft.Domain.Localization;

public static class CountryProfileCatalog
{
    private static readonly IReadOnlyDictionary<string, CountryProfile> Profiles =
        new Dictionary<string, CountryProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["SG"] = new CountryProfile("SG", "SGD", "Asia/Singapore", "+65", 8),
            ["IN"] = new CountryProfile("IN", "INR", "Asia/Kolkata", "+91", 10, 1)
        };

    public static CountryProfile GetRequired(string? countryCode)
    {
        var normalizedCountryCode = NormalizeCountryCode(countryCode);
        if (string.IsNullOrWhiteSpace(normalizedCountryCode) || !Profiles.TryGetValue(normalizedCountryCode, out var profile))
        {
            throw new InvalidOperationException(
                $"Unsupported country code '{countryCode?.Trim()}'. Supported country codes: {string.Join(", ", Profiles.Keys.OrderBy(code => code, StringComparer.Ordinal))}.");
        }

        return profile;
    }

    public static bool TryGetProfile(string? countryCode, out CountryProfile profile)
    {
        var normalizedCountryCode = NormalizeCountryCode(countryCode);
        if (!string.IsNullOrWhiteSpace(normalizedCountryCode) && Profiles.TryGetValue(normalizedCountryCode, out profile!))
        {
            return true;
        }

        profile = default!;
        return false;
    }

    private static string? NormalizeCountryCode(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return null;
        }

        var normalized = countryCode.Trim().ToUpperInvariant();
        return normalized.Length == 2 ? normalized : null;
    }
}
