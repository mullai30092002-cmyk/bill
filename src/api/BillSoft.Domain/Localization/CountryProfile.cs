namespace BillSoft.Domain.Localization;

public sealed record CountryProfile(
    string CountryCode,
    string CurrencyCode,
    string TimeZoneId,
    string DialCode,
    int NationalNumberLength,
    int? TrunkPrefixLength = null);
