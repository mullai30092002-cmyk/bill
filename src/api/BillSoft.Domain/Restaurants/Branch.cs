using BillSoft.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace BillSoft.Domain.Restaurants;

public sealed class Branch : BaseEntity
{
    public Guid BranchId { get; set; } = CreateId();

    public Guid RestaurantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public string? NormalizedPhone { get; set; }

    public string CountryCode { get; set; } = "IN";

    public string CurrencyCode { get; set; } = "INR";

    public string TimeZoneId { get; set; } = "Asia/Kolkata";

    [NotMapped]
    public string Timezone
    {
        get => TimeZoneId;
        set => TimeZoneId = value.Trim();
    }

    [NotMapped]
    public string Currency
    {
        get => CurrencyCode;
        set => CurrencyCode = value.Trim().ToUpperInvariant();
    }

    public BranchStatus Status { get; set; } = BranchStatus.Active;

    public DateTimeOffset CreatedAt { get; set; } = UtcNow();

    public DateTimeOffset? UpdatedAt { get; set; }

    public void UpdateProfile(
        string name,
        string? address,
        string? phone,
        string timezone,
        string currency,
        DateTimeOffset? updatedAt = null)
    {
        Name = name.Trim();
        Address = NormalizeOptional(address);
        Phone = NormalizeOptional(phone);
        NormalizedPhone = NormalizeOptional(phone)?.ToUpperInvariant();
        TimeZoneId = timezone.Trim();
        CurrencyCode = currency.Trim().ToUpperInvariant();
        MarkUpdated(updatedAt);
    }

    public void Activate(DateTimeOffset? updatedAt = null)
    {
        Status = BranchStatus.Active;
        MarkUpdated(updatedAt);
    }

    public void Deactivate(DateTimeOffset? updatedAt = null)
    {
        Status = BranchStatus.Inactive;
        MarkUpdated(updatedAt);
    }

    public void MarkUpdated(DateTimeOffset? updatedAt = null)
    {
        UpdatedAt = ResolveUpdatedAt(updatedAt);
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
