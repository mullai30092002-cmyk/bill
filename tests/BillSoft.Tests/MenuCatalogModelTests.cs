using BillSoft.Domain.Menu;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Security;
using BillSoft.Infrastructure.Persistence;
using BillSoft.Infrastructure.Seed;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BillSoft.Tests;

public sealed class MenuCatalogModelTests
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
    public void Menu_Entities_Should_Initialize_Ids_And_Timestamps()
    {
        var category = new MenuCategory();
        var item = new MenuItem();
        var history = new MenuItemPriceHistory();

        Assert.NotEqual(Guid.Empty, category.MenuCategoryId);
        Assert.NotEqual(default, category.CreatedAt);

        Assert.NotEqual(Guid.Empty, item.MenuItemId);
        Assert.NotEqual(default, item.CreatedAt);
        Assert.Equal(MenuItemInventoryDeductionMode.RecipeOnServe, item.InventoryDeductionMode);

        Assert.NotEqual(Guid.Empty, history.MenuItemPriceHistoryId);
        Assert.NotEqual(default, history.ChangedAt);
    }

    [Fact]
    public void DbContext_Should_Expose_Menu_DbSets_And_Required_FKs()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateContext(connection);

        Assert.NotNull(context.MenuCategories);
        Assert.NotNull(context.MenuItems);
        Assert.NotNull(context.MenuItemPriceHistory);

        var categoryType = context.Model.FindEntityType(typeof(MenuCategory));
        var itemType = context.Model.FindEntityType(typeof(MenuItem));
        var historyType = context.Model.FindEntityType(typeof(MenuItemPriceHistory));

        Assert.NotNull(categoryType);
        Assert.NotNull(itemType);
        Assert.NotNull(historyType);

        Assert.Contains(categoryType!.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Restaurant) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(MenuCategory.RestaurantId)], StringComparer.Ordinal));

        Assert.Contains(itemType!.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Restaurant) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(MenuItem.RestaurantId)], StringComparer.Ordinal));

        Assert.Contains(itemType.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(MenuCategory) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(MenuItem.MenuCategoryId)], StringComparer.Ordinal));

        Assert.Contains(historyType!.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(MenuItem) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(MenuItemPriceHistory.MenuItemId)], StringComparer.Ordinal));

        Assert.Contains(historyType.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Restaurant) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(MenuItemPriceHistory.RestaurantId)], StringComparer.Ordinal));

        Assert.Contains(historyType.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(BillSoft.Domain.Users.User) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(MenuItemPriceHistory.ChangedByUserId)], StringComparer.Ordinal));
    }

    [Fact]
    public void Decimal_Precision_Should_Be_Configured_For_Menu_Prices()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateContext(connection);

        var itemType = context.Model.FindEntityType(typeof(MenuItem));
        var historyType = context.Model.FindEntityType(typeof(MenuItemPriceHistory));

        Assert.NotNull(itemType);
        Assert.NotNull(historyType);

        Assert.Equal(18, itemType!.FindProperty(nameof(MenuItem.BasePrice))!.GetPrecision());
        Assert.Equal(2, itemType.FindProperty(nameof(MenuItem.BasePrice))!.GetScale());
        Assert.Equal(5, itemType.FindProperty(nameof(MenuItem.TaxRate))!.GetPrecision());
        Assert.Equal(2, itemType.FindProperty(nameof(MenuItem.TaxRate))!.GetScale());
        Assert.Equal(18, historyType!.FindProperty(nameof(MenuItemPriceHistory.OldPrice))!.GetPrecision());
        Assert.Equal(2, historyType.FindProperty(nameof(MenuItemPriceHistory.OldPrice))!.GetScale());
        Assert.Equal(18, historyType.FindProperty(nameof(MenuItemPriceHistory.NewPrice))!.GetPrecision());
        Assert.Equal(2, historyType.FindProperty(nameof(MenuItemPriceHistory.NewPrice))!.GetScale());
    }

    [Fact]
    public void Menu_Indexes_Should_Be_Configured_As_Expected()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateContext(connection);

        var categoryType = context.Model.FindEntityType(typeof(MenuCategory));
        var itemType = context.Model.FindEntityType(typeof(MenuItem));
        var historyType = context.Model.FindEntityType(typeof(MenuItemPriceHistory));

        Assert.NotNull(categoryType);
        Assert.NotNull(itemType);
        Assert.NotNull(historyType);

        Assert.Contains(categoryType!.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(MenuCategory.RestaurantId)], StringComparer.Ordinal));

        Assert.Contains(categoryType.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(MenuCategory.RestaurantId), nameof(MenuCategory.Name)], StringComparer.Ordinal) &&
            index.IsUnique);

        Assert.Contains(itemType!.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(MenuItem.RestaurantId)], StringComparer.Ordinal));

        Assert.Contains(itemType.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(MenuItem.MenuCategoryId)], StringComparer.Ordinal));

        Assert.Contains(itemType.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(MenuItem.RestaurantId), nameof(MenuItem.MenuCategoryId), nameof(MenuItem.Name)], StringComparer.Ordinal) &&
            index.IsUnique);

        Assert.Contains(itemType.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(MenuItem.RestaurantId), nameof(MenuItem.Sku)], StringComparer.Ordinal) &&
            index.IsUnique &&
            string.Equals(index.GetFilter(), "[Sku] IS NOT NULL", StringComparison.Ordinal));

        Assert.Contains(historyType!.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(MenuItemPriceHistory.RestaurantId), nameof(MenuItemPriceHistory.MenuItemId)], StringComparer.Ordinal));

        Assert.Contains(historyType.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(MenuItemPriceHistory.MenuItemId), nameof(MenuItemPriceHistory.ChangedAt)], StringComparer.Ordinal));
    }

    [Fact]
    public void Migration_Should_Include_Menu_Catalog_Foundation()
    {
        var migrationType = typeof(BillSoftDbContext).Assembly.GetTypes()
            .SingleOrDefault(type => string.Equals(type.Name, "MenuCatalogFoundation", StringComparison.Ordinal));

        Assert.NotNull(migrationType);
    }

    [Fact]
    public void Permission_Constants_Should_Exist_And_Be_Seeded()
    {
        var codes = SystemPermissions.Definitions.Select(definition => definition.Code).ToArray();

        Assert.Contains(SystemPermissions.MenuCategoryManage, codes);
        Assert.Contains(SystemPermissions.MenuItemManage, codes);
        Assert.Contains(SystemPermissions.MenuItemView, codes);

        var adminPermissionCodes = FoundationSeedData.RolePermissions
            .Where(item => item.RoleName == "Admin")
            .Select(item => item.PermissionCode)
            .ToArray();

        Assert.Contains(SystemPermissions.MenuCategoryManage, adminPermissionCodes);
        Assert.Contains(SystemPermissions.MenuItemManage, adminPermissionCodes);
        Assert.Contains(SystemPermissions.MenuItemView, adminPermissionCodes);
    }
}
