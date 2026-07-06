namespace BillSoft.Domain.Localization;

public sealed record NormalizedMobileNumber(
    string CountryCode,
    string DialCode,
    string NationalNumber,
    string E164);
