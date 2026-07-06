using BillSoft.Domain.Common;
using BillSoft.Domain.Localization;

namespace BillSoft.Domain.Users;

public sealed class User : BaseEntity
{
    public Guid UserId { get; set; } = CreateId();

    public Guid RestaurantId { get; set; }

    public Guid? BranchId { get; set; }

    public string FullName { get; set; } = string.Empty;

    private string _mobileNumber = string.Empty;
    private string _mobileCountryCode = "SG";

    public string MobileNumber
    {
        get => _mobileNumber;
        set
        {
            _mobileNumber = value?.Trim() ?? string.Empty;
            SyncMobileNumber();
        }
    }

    public string MobileCountryCode
    {
        get => _mobileCountryCode;
        set
        {
            _mobileCountryCode = string.IsNullOrWhiteSpace(value)
                ? "SG"
                : value.Trim().ToUpperInvariant();
            SyncMobileNumber();
        }
    }

    public string MobileDialCode { get; private set; } = "+65";

    public string MobileNationalNumber { get; private set; } = string.Empty;

    public string MobileE164 { get; private set; } = string.Empty;

    public string? Email { get; private set; }

    public string? NormalizedEmail { get; private set; }

    public string? PinHash { get; set; }

    public string? PasswordHash { get; set; }

    public UserStatus Status { get; set; } = UserStatus.Active;

    public DateTimeOffset CreatedAt { get; set; } = UtcNow();

    public DateTimeOffset? UpdatedAt { get; set; }

    public void SetEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            Email = null;
            NormalizedEmail = null;
            return;
        }

        Email = email.Trim();
        NormalizedEmail = Email.ToUpperInvariant();
    }

    public void SetMobileNumber(string countryCode, string mobileNumber)
    {
        MobileCountryCode = countryCode;
        MobileNumber = mobileNumber;
    }

    public void SetMobileNumber(NormalizedMobileNumber normalizedMobileNumber)
    {
        ArgumentNullException.ThrowIfNull(normalizedMobileNumber);
        ApplyNormalizedMobileNumber(normalizedMobileNumber);
    }

    private void ApplyNormalizedMobileNumber(NormalizedMobileNumber normalizedMobileNumber)
    {
        _mobileCountryCode = normalizedMobileNumber.CountryCode;
        MobileDialCode = normalizedMobileNumber.DialCode;
        MobileNationalNumber = normalizedMobileNumber.NationalNumber;
        MobileE164 = normalizedMobileNumber.E164;
        _mobileNumber = normalizedMobileNumber.NationalNumber;
    }

    private void SyncMobileNumber()
    {
        if (string.IsNullOrWhiteSpace(_mobileNumber))
        {
            MobileDialCode = string.IsNullOrWhiteSpace(_mobileCountryCode) || !CountryProfileCatalog.TryGetProfile(_mobileCountryCode, out var profile)
                ? string.Empty
                : profile.DialCode;
            MobileNationalNumber = string.Empty;
            MobileE164 = string.Empty;
            return;
        }

        var normalized = MobileNumberNormalizer.Normalize(_mobileCountryCode, _mobileNumber);
        ApplyNormalizedMobileNumber(normalized);
    }

    public void MarkUpdated(DateTimeOffset? updatedAt = null)
    {
        UpdatedAt = ResolveUpdatedAt(updatedAt);
    }
}
