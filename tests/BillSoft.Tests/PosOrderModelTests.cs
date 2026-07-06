using BillSoft.Domain.Menu;
using BillSoft.Domain.Orders;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Security;
using BillSoft.Infrastructure.Persistence;
using BillSoft.Infrastructure.Seed;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BillSoft.Tests;

public sealed class PosOrderModelTests
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
    public void PosOrder_And_PosOrderLine_Should_Initialize_Ids_And_Timestamps()
    {
        var order = new PosOrder();
        var line = new PosOrderLine();

        Assert.NotEqual(Guid.Empty, order.PosOrderId);
        Assert.NotEqual(default, order.CreatedAt);

        Assert.NotEqual(Guid.Empty, line.PosOrderLineId);
        Assert.NotEqual(default, line.CreatedAt);
    }

    [Fact]
    public void DbContext_Should_Expose_PosOrders_And_PosOrderLines_With_Required_FKs()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateContext(connection);

        Assert.NotNull(context.PosOrders);
        Assert.NotNull(context.PosOrderLines);

        var orderType = context.Model.FindEntityType(typeof(PosOrder));
        var lineType = context.Model.FindEntityType(typeof(PosOrderLine));

        Assert.NotNull(orderType);
        Assert.NotNull(lineType);

        Assert.Contains(orderType!.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Restaurant) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(PosOrder.RestaurantId)], StringComparer.Ordinal));

        Assert.Contains(orderType.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Branch) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(PosOrder.BranchId)], StringComparer.Ordinal));

        Assert.Contains(lineType!.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(PosOrder) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(PosOrderLine.PosOrderId)], StringComparer.Ordinal));

        Assert.Contains(lineType.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(MenuItem) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(PosOrderLine.MenuItemId)], StringComparer.Ordinal));

        Assert.Contains(lineType.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(MenuCategory) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(PosOrderLine.MenuCategoryId)], StringComparer.Ordinal));
    }

    [Fact]
    public void DbContext_Should_Expose_PosOrderNumberSequences_With_Required_FKs()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateContext(connection);

        Assert.NotNull(context.PosOrderNumberSequences);

        var sequenceType = context.Model.FindEntityType(typeof(PosOrderNumberSequence));

        Assert.NotNull(sequenceType);

        Assert.Contains(sequenceType!.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Restaurant) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(PosOrderNumberSequence.RestaurantId)], StringComparer.Ordinal));

        Assert.Contains(sequenceType.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Branch) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(PosOrderNumberSequence.BranchId)], StringComparer.Ordinal));
    }

    [Fact]
    public void PosOrder_Decimal_Precision_Should_Be_Configured()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateContext(connection);

        var orderType = context.Model.FindEntityType(typeof(PosOrder));
        var lineType = context.Model.FindEntityType(typeof(PosOrderLine));

        Assert.NotNull(orderType);
        Assert.NotNull(lineType);

        Assert.Equal(18, orderType!.FindProperty(nameof(PosOrder.Subtotal))!.GetPrecision());
        Assert.Equal(2, orderType.FindProperty(nameof(PosOrder.Subtotal))!.GetScale());
        Assert.Equal(18, orderType.FindProperty(nameof(PosOrder.TaxTotal))!.GetPrecision());
        Assert.Equal(2, orderType.FindProperty(nameof(PosOrder.GrandTotal))!.GetScale());
        Assert.Equal(18, lineType!.FindProperty(nameof(PosOrderLine.UnitPrice))!.GetPrecision());
        Assert.Equal(2, lineType.FindProperty(nameof(PosOrderLine.UnitPrice))!.GetScale());
        Assert.Equal(5, lineType.FindProperty(nameof(PosOrderLine.TaxRate))!.GetPrecision());
        Assert.Equal(3, lineType.FindProperty(nameof(PosOrderLine.Quantity))!.GetScale());
    }

    [Fact]
    public void PosOrder_Should_Have_Unique_Index_For_Restaurant_Branch_And_OrderNumber()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateContext(connection);

        var orderType = context.Model.FindEntityType(typeof(PosOrder));

        Assert.NotNull(orderType);

        var index = orderType!.GetIndexes()
            .SingleOrDefault(candidate =>
                candidate.Properties.Select(property => property.Name)
                    .SequenceEqual([nameof(PosOrder.RestaurantId), nameof(PosOrder.BranchId), nameof(PosOrder.OrderNumber)], StringComparer.Ordinal));

        Assert.NotNull(index);
        Assert.True(index!.IsUnique);
    }

    [Fact]
    public void PosOrderNumberSequence_Should_Have_Unique_Index_For_Restaurant_Branch_And_OrderDate()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateContext(connection);

        var sequenceType = context.Model.FindEntityType(typeof(PosOrderNumberSequence));

        Assert.NotNull(sequenceType);

        var index = sequenceType!.GetIndexes()
            .SingleOrDefault(candidate =>
                candidate.Properties.Select(property => property.Name)
                    .SequenceEqual([nameof(PosOrderNumberSequence.RestaurantId), nameof(PosOrderNumberSequence.BranchId), nameof(PosOrderNumberSequence.OrderDate)], StringComparer.Ordinal));

        Assert.NotNull(index);
        Assert.True(index!.IsUnique);
        Assert.False(sequenceType.FindProperty(nameof(PosOrderNumberSequence.LastSequence))!.IsNullable);
    }

    [Fact]
    public void Migration_Should_Include_PosOrderFoundation()
    {
        var repositoryRoot = LocateRepositoryRoot();
        var migrationsDirectory = Path.Combine(repositoryRoot, "src", "api", "BillSoft.Infrastructure", "Migrations");
        Assert.True(Directory.Exists(migrationsDirectory), migrationsDirectory);

        var migrationFiles = Directory.GetFiles(migrationsDirectory, "*PosOrderFoundation*.cs", SearchOption.TopDirectoryOnly);

        Assert.Contains(migrationFiles, path => path.EndsWith("PosOrderFoundation.cs", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(migrationFiles, path => path.EndsWith("PosOrderFoundation.Designer.cs", StringComparison.OrdinalIgnoreCase));
    }

    private static string LocateRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    [Fact]
    public void Permission_Constants_Should_Exist_And_Be_Seeded()
    {
        var codes = SystemPermissions.Definitions.Select(definition => definition.Code).ToArray();

        Assert.Contains(SystemPermissions.OrderCreate, codes);
        Assert.Contains(SystemPermissions.OrderView, codes);
        Assert.Contains(SystemPermissions.OrderCancel, codes);

        var adminPermissionCodes = FoundationSeedData.RolePermissions
            .Where(item => item.RoleName == "Admin")
            .Select(item => item.PermissionCode)
            .ToArray();

        Assert.Contains(SystemPermissions.OrderCreate, adminPermissionCodes);
        Assert.Contains(SystemPermissions.OrderView, adminPermissionCodes);
        Assert.Contains(SystemPermissions.OrderCancel, adminPermissionCodes);
    }
}
