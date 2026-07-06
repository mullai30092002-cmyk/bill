using BillSoft.Domain.Common;

namespace BillSoft.Domain.Vendors;

public sealed class Vendor : BaseEntity
{
    public Guid VendorId { get; set; } = CreateId();

    public Guid RestaurantId { get; set; }

    public Guid? BranchId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public VendorType VendorType { get; set; } = VendorType.Other;

    public string? ContactName { get; set; }

    public string? MobileNumber { get; set; }

    public string? NormalizedMobileNumber { get; set; }

    public string? Address { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; } = UtcNow();

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public ICollection<VendorBill> VendorBills { get; set; } = new List<VendorBill>();

    public void UpdateProfile(
        string name,
        VendorType vendorType,
        string? contactName,
        string? mobileNumber,
        string? address,
        string? notes,
        bool isActive,
        DateTimeOffset? updatedAtUtc = null)
    {
        Name = NormalizeRequiredText(name);
        NormalizedName = NormalizeKey(name);
        VendorType = vendorType;
        ContactName = NormalizeOptionalText(contactName);
        MobileNumber = NormalizeOptionalText(mobileNumber);
        NormalizedMobileNumber = NormalizeOptionalText(mobileNumber)?.ToUpperInvariant();
        Address = NormalizeOptionalText(address);
        Notes = NormalizeOptionalText(notes);
        IsActive = isActive;
        UpdatedAtUtc = ResolveUpdatedAt(updatedAtUtc);
    }

    public static string NormalizeKey(string value) => NormalizeRequiredText(value).ToUpperInvariant();

    private static string NormalizeRequiredText(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
