using BillSoft.Domain.Inventory;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Vendors;
using BillSoft.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BillSoft.Tests;

public sealed class VendorFoundationModelTests
{
    private static BillSoftDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<BillSoftDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new BillSoftDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public void Vendor_Entities_Should_Initialize_NonEmpty_Ids_And_Timestamps()
    {
        var vendor = new Vendor();
        var vendorBill = new VendorBill();
        var vendorBillLine = new VendorBillLine();
        var vendorSettlement = new VendorSettlement();

        Assert.NotEqual(Guid.Empty, vendor.VendorId);
        Assert.NotEqual(default, vendor.CreatedAtUtc);

        Assert.NotEqual(Guid.Empty, vendorBill.VendorBillId);
        Assert.NotEqual(default, vendorBill.CreatedAtUtc);

        Assert.NotEqual(Guid.Empty, vendorBillLine.VendorBillLineId);
        Assert.NotEqual(default, vendorBillLine.CreatedAtUtc);

        Assert.NotEqual(Guid.Empty, vendorSettlement.VendorSettlementId);
        Assert.NotEqual(default, vendorSettlement.CreatedAtUtc);
    }

    [Fact]
    public void DbContext_Should_Expose_Vendor_Tables()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateContext(connection);

        Assert.NotNull(context.Vendors);
        Assert.NotNull(context.VendorBills);
        Assert.NotNull(context.VendorBillLines);
        Assert.NotNull(context.VendorSettlements);

        Assert.NotNull(context.Model.FindEntityType(typeof(Vendor)));
        Assert.NotNull(context.Model.FindEntityType(typeof(VendorBill)));
        Assert.NotNull(context.Model.FindEntityType(typeof(VendorBillLine)));
        Assert.NotNull(context.Model.FindEntityType(typeof(VendorSettlement)));
    }

    [Fact]
    public void Vendor_Name_Unique_Indexes_Should_Support_Restaurant_Wide_And_Branch_Scoped_Vendors()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateContext(connection);

        var entityType = context.Model.FindEntityType(typeof(Vendor));
        Assert.NotNull(entityType);

        var branchScopedIndex = entityType!.GetIndexes()
            .SingleOrDefault(candidate =>
                candidate.IsUnique &&
                candidate.Properties.Select(property => property.Name)
                    .SequenceEqual([nameof(Vendor.RestaurantId), nameof(Vendor.BranchId), nameof(Vendor.NormalizedName)], StringComparer.Ordinal));

        var restaurantWideIndex = entityType.GetIndexes()
            .SingleOrDefault(candidate =>
                candidate.IsUnique &&
                candidate.Properties.Select(property => property.Name)
                    .SequenceEqual([nameof(Vendor.RestaurantId), nameof(Vendor.NormalizedName)], StringComparer.Ordinal));

        Assert.NotNull(branchScopedIndex);
        Assert.NotNull(restaurantWideIndex);
        Assert.Equal("[BranchId] IS NOT NULL", branchScopedIndex!.GetFilter());
        Assert.Equal("[BranchId] IS NULL", restaurantWideIndex!.GetFilter());
    }

    [Fact]
    public void Vendor_Mobile_And_Bill_Number_Unique_Indexes_Should_Be_Scoped_To_Restaurant_And_Vendor()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateContext(connection);

        var vendorEntity = context.Model.FindEntityType(typeof(Vendor));
        Assert.NotNull(vendorEntity);

        var mobileIndex = vendorEntity!.GetIndexes()
            .SingleOrDefault(candidate =>
                candidate.IsUnique &&
                candidate.Properties.Select(property => property.Name)
                    .SequenceEqual([nameof(Vendor.RestaurantId), nameof(Vendor.NormalizedMobileNumber)], StringComparer.Ordinal));

        Assert.NotNull(mobileIndex);
        Assert.Equal("[NormalizedMobileNumber] IS NOT NULL", mobileIndex!.GetFilter());

        var billEntity = context.Model.FindEntityType(typeof(VendorBill));
        Assert.NotNull(billEntity);

        var billNumberIndex = billEntity!.GetIndexes()
            .SingleOrDefault(candidate =>
                candidate.IsUnique &&
                candidate.Properties.Select(property => property.Name)
                    .SequenceEqual([nameof(VendorBill.RestaurantId), nameof(VendorBill.VendorId), nameof(VendorBill.NormalizedBillNumber)], StringComparer.Ordinal));

        Assert.NotNull(billNumberIndex);
        Assert.Equal("[NormalizedBillNumber] IS NOT NULL", billNumberIndex!.GetFilter());
    }

    [Fact]
    public void Branch_Mobile_Unique_Index_Should_Use_Normalized_Phone_Per_Restaurant()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateContext(connection);

        var entityType = context.Model.FindEntityType(typeof(Branch));
        Assert.NotNull(entityType);

        var normalizedPhoneProperty = entityType!.FindProperty("NormalizedPhone");
        Assert.NotNull(normalizedPhoneProperty);

        var phoneIndex = entityType.GetIndexes()
            .SingleOrDefault(candidate =>
                candidate.IsUnique &&
                candidate.Properties.Select(property => property.Name)
                    .SequenceEqual([nameof(Branch.RestaurantId), "NormalizedPhone"], StringComparer.Ordinal));

        Assert.NotNull(phoneIndex);
        Assert.Equal("[NormalizedPhone] IS NOT NULL", phoneIndex!.GetFilter());
    }

    [Fact]
    public void VendorBillLine_Should_Link_To_InventoryMovement_And_InventoryItem()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateContext(connection);

        var entityType = context.Model.FindEntityType(typeof(VendorBillLine));
        Assert.NotNull(entityType);

        Assert.Contains(entityType!.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(VendorBill) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(VendorBillLine.VendorBillId)], StringComparer.Ordinal));

        Assert.Contains(entityType.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(InventoryItem) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(VendorBillLine.InventoryItemId)], StringComparer.Ordinal));

        Assert.Contains(entityType.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(InventoryMovement) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(VendorBillLine.InventoryMovementId)], StringComparer.Ordinal));
    }

    [Fact]
    public void VendorSettlement_Should_Expose_Money_Precision_And_Required_FK()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateContext(connection);

        var entityType = context.Model.FindEntityType(typeof(VendorSettlement));
        Assert.NotNull(entityType);

        Assert.Equal(18, entityType!.FindProperty(nameof(VendorSettlement.Amount))!.GetPrecision());
        Assert.Equal(2, entityType.FindProperty(nameof(VendorSettlement.Amount))!.GetScale());

        Assert.Contains(entityType.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(VendorBill) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(VendorSettlement.VendorBillId)], StringComparer.Ordinal));
    }

    [Fact]
    public void VendorBill_Should_Only_Have_Expected_Foreign_Keys()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateContext(connection);

        var entityType = context.Model.FindEntityType(typeof(VendorBill));
        Assert.NotNull(entityType);

        Assert.Contains(entityType!.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Vendor) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(VendorBill.VendorId)], StringComparer.Ordinal));

        Assert.DoesNotContain(entityType.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Any(property => property.Name == "VendorId1"));
    }
}
