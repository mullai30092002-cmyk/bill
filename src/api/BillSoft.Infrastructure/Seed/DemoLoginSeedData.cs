namespace BillSoft.Infrastructure.Seed;

public static class DemoLoginSeedData
{
    public const string RestaurantCode = "DEMO";
    public const string RestaurantName = "Demo Restaurant";
    public const string RestaurantCountryCode = "IN";
    public const string RestaurantCurrencyCode = "INR";
    public const string RestaurantTimeZoneId = "Asia/Kolkata";
    public const string BranchName = "Main Branch";
    public const string BranchCountryCode = "IN";
    public const string BranchTimezone = "Asia/Kolkata";
    public const string BranchCurrency = "INR";
    public const string FullName = "Demo Owner";
    public const string MobileNumber = "9123456789";
    public const string MobileCountryCode = "IN";
    public const string MobileDialCode = "+91";
    public const string MobileNationalNumber = "9123456789";
    public const string MobileE164 = "+919123456789";
    public const string Email = "owner@demo.billsoft.local";
    public const string Password = "DemoOwner123!";
    public const string RoleName = "RestaurantOwner";

    public const string InventoryQaFullName = "Demo Inventory";
    public const string InventoryQaMobileNumber = "9000000002";
    public const string InventoryQaMobileCountryCode = "IN";
    public const string InventoryQaMobileE164 = "+919000000002";
    public const string InventoryQaEmail = "inventory@demo.billsoft.local";
    public const string InventoryQaPassword = "DemoInventory123!";
    public const string InventoryQaRoleName = "InventoryUser";

    public sealed record DemoMenuCategorySeed(string Name, int DisplayOrder);

    public sealed record DemoMenuItemSeed(
        string Name,
        string Sku,
        string CategoryName,
        string? Description,
        decimal BasePrice,
        decimal TaxRate,
        bool IsVegetarian,
        bool IsAvailableForEatIn,
        bool IsAvailableForParcel);

    public static readonly DemoMenuCategorySeed[] MenuCategories =
    [
        new("Breakfast", 1),
        new("Sides", 2)
    ];

    public static readonly DemoMenuItemSeed[] MenuItems =
    [
        new("Masala Dosa", "DOSA-01", "Breakfast", "Crisp rice crepe", 2.50m, 0m, true, true, true),
        new("Parcel Snack", "SNACK-01", "Sides", "Parcel only snack", 1.75m, 0m, false, false, true),
        new("Eat-In Special", "SPECIAL-01", "Sides", "Eat-in only special", 3.00m, 0m, false, true, false)
    ];
}
