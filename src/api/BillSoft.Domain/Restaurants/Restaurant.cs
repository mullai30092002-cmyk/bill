using BillSoft.Domain.Common;
using BillSoft.Domain.Localization;

namespace BillSoft.Domain.Restaurants;

public sealed class Restaurant : BaseEntity
{
    public Guid RestaurantId { get; set; } = CreateId();

    public string Name { get; set; } = string.Empty;

    public RestaurantBusinessType BusinessType { get; set; } = RestaurantBusinessType.Restaurant;

    public string? LegalName { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string RestaurantCode { get; private set; } = string.Empty;

    public string NormalizedRestaurantCode { get; private set; } = string.Empty;

    public string CountryCode { get; set; } = "IN";

    public string CurrencyCode { get; set; } = "INR";

    public string TimeZoneId { get; set; } = "Asia/Kolkata";

    public string? Address { get; set; }

    public RestaurantStatus Status { get; set; } = RestaurantStatus.Active;

    public DateTimeOffset CreatedAt { get; set; } = UtcNow();

    public DateTimeOffset? UpdatedAt { get; set; }

    public void SetRestaurantCode(string restaurantCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(restaurantCode);

        RestaurantCode = restaurantCode.Trim();
        NormalizedRestaurantCode = RestaurantCode.ToUpperInvariant();
    }

    public void SetCountryProfile(string countryCode)
    {
        var profile = CountryProfileCatalog.GetRequired(countryCode);
        CountryCode = profile.CountryCode;
        CurrencyCode = profile.CurrencyCode;
        TimeZoneId = profile.TimeZoneId;
    }

    public void MarkUpdated(DateTimeOffset? updatedAt = null)
    {
        UpdatedAt = ResolveUpdatedAt(updatedAt);
    }
}
