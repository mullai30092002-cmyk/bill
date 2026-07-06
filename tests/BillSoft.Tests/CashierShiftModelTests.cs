using BillSoft.Domain.Billing;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Security;
using BillSoft.Infrastructure.Persistence;
using BillSoft.Infrastructure.Seed;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BillSoft.Tests;

public sealed class CashierShiftModelTests
{
    [Fact]
    public void CashierShift_And_Movement_Entities_Should_Initialize_Ids_And_Timestamps()
    {
        var cashierShiftType = Type.GetType("BillSoft.Domain.Cashiering.CashierShift, BillSoft.Domain");
        var cashDrawerMovementType = Type.GetType("BillSoft.Domain.Cashiering.CashDrawerMovement, BillSoft.Domain");

        Assert.NotNull(cashierShiftType);
        Assert.NotNull(cashDrawerMovementType);

        var shift = Activator.CreateInstance(cashierShiftType!);
        var movement = Activator.CreateInstance(cashDrawerMovementType!);

        Assert.NotNull(shift);
        Assert.NotNull(movement);

        Assert.NotEqual(Guid.Empty, (Guid)cashierShiftType!.GetProperty("CashierShiftId")!.GetValue(shift)!);
        Assert.NotEqual(default, (DateTimeOffset)cashierShiftType.GetProperty("CreatedAt")!.GetValue(shift)!);

        Assert.NotEqual(Guid.Empty, (Guid)cashDrawerMovementType!.GetProperty("CashDrawerMovementId")!.GetValue(movement)!);
        Assert.NotEqual(default, (DateTimeOffset)cashDrawerMovementType.GetProperty("CreatedAt")!.GetValue(movement)!);
    }

    [Fact]
    public void DbContext_Should_Expose_Cashier_Shift_Tables_With_Required_FKs()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateContext(connection);

        Assert.NotNull(context.GetType().GetProperty("CashierShifts"));
        Assert.NotNull(context.GetType().GetProperty("CashDrawerMovements"));

        var cashierShiftType = context.Model.GetEntityTypes()
            .SingleOrDefault(entityType => string.Equals(entityType.ClrType.FullName, "BillSoft.Domain.Cashiering.CashierShift", StringComparison.Ordinal));
        var movementType = context.Model.GetEntityTypes()
            .SingleOrDefault(entityType => string.Equals(entityType.ClrType.FullName, "BillSoft.Domain.Cashiering.CashDrawerMovement", StringComparison.Ordinal));
        var paymentType = context.Model.GetEntityTypes()
            .SingleOrDefault(entityType => entityType.ClrType == typeof(Payment));

        Assert.NotNull(cashierShiftType);
        Assert.NotNull(movementType);
        Assert.NotNull(paymentType);

        Assert.Contains(cashierShiftType!.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Restaurant) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(CashierShiftModelPropertyNames.RestaurantId)], StringComparer.Ordinal));

        Assert.Contains(cashierShiftType.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Branch) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(CashierShiftModelPropertyNames.BranchId)], StringComparer.Ordinal));

        Assert.Contains(cashierShiftType.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(BillSoft.Domain.Users.User) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(CashierShiftModelPropertyNames.OpenedByUserId)], StringComparer.Ordinal));

        Assert.Contains(cashierShiftType.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(BillSoft.Domain.Users.User) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(CashierShiftModelPropertyNames.ClosedByUserId)], StringComparer.Ordinal));

        Assert.Contains(movementType!.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Restaurant) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(CashierMovementModelPropertyNames.RestaurantId)], StringComparer.Ordinal));

        Assert.Contains(movementType.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Branch) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(CashierMovementModelPropertyNames.BranchId)], StringComparer.Ordinal));

        Assert.Contains(movementType.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == cashierShiftType.ClrType &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(CashierMovementModelPropertyNames.CashierShiftId)], StringComparer.Ordinal));

        Assert.Contains(movementType.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(BillSoft.Domain.Users.User) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(CashierMovementModelPropertyNames.CreatedByUserId)], StringComparer.Ordinal));

        Assert.Contains(paymentType!.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(Payment.CashierShiftId)], StringComparer.Ordinal));
    }

    [Fact]
    public void Cashier_Shift_Decimal_Precision_And_Indexes_Should_Be_Configured()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateContext(connection);

        var cashierShiftType = context.Model.GetEntityTypes()
            .SingleOrDefault(entityType => string.Equals(entityType.ClrType.FullName, "BillSoft.Domain.Cashiering.CashierShift", StringComparison.Ordinal));
        var movementType = context.Model.GetEntityTypes()
            .SingleOrDefault(entityType => string.Equals(entityType.ClrType.FullName, "BillSoft.Domain.Cashiering.CashDrawerMovement", StringComparison.Ordinal));
        var paymentType = context.Model.GetEntityTypes()
            .SingleOrDefault(entityType => entityType.ClrType == typeof(Payment));

        Assert.NotNull(cashierShiftType);
        Assert.NotNull(movementType);
        Assert.NotNull(paymentType);

        Assert.Equal(18, cashierShiftType!.FindProperty("OpeningCashAmount")!.GetPrecision());
        Assert.Equal(2, cashierShiftType.FindProperty("OpeningCashAmount")!.GetScale());
        Assert.NotNull(cashierShiftType.FindProperty("BusinessDate"));
        Assert.Equal(18, cashierShiftType.FindProperty("ExpectedCashAmount")!.GetPrecision());
        Assert.Equal(2, cashierShiftType.FindProperty("CountedCashAmount")!.GetScale());
        Assert.Equal(18, cashierShiftType.FindProperty("CashVarianceAmount")!.GetPrecision());

        Assert.Equal(18, movementType!.FindProperty("Amount")!.GetPrecision());
        Assert.Equal(2, movementType.FindProperty("Amount")!.GetScale());

        Assert.Contains(cashierShiftType.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(["RestaurantId"], StringComparer.Ordinal));

        Assert.Contains(cashierShiftType.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(["RestaurantId", "BranchId", "Status"], StringComparer.Ordinal));

        Assert.Contains(cashierShiftType.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(["RestaurantId", "BranchId", "BusinessDate"], StringComparer.Ordinal));

        Assert.Contains(cashierShiftType.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(["RestaurantId", "BranchId", "OpenedByUserId"], StringComparer.Ordinal));

        Assert.Contains(cashierShiftType.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(["OpenedAt"], StringComparer.Ordinal));

        Assert.Contains(cashierShiftType.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(["ClosedAt"], StringComparer.Ordinal));

        Assert.Contains(movementType.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(["CashierShiftId"], StringComparer.Ordinal));

        Assert.Contains(movementType.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(["RestaurantId", "BranchId"], StringComparer.Ordinal));

        Assert.Contains(movementType.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(["MovementType"], StringComparer.Ordinal));

        Assert.Contains(movementType.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(["CreatedAt"], StringComparer.Ordinal));

        Assert.Contains(paymentType!.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(["CashierShiftId"], StringComparer.Ordinal));
    }

    [Fact]
    public void Migration_Should_Include_CashierShiftFoundation()
    {
        var repositoryRoot = LocateRepositoryRoot();
        var migrationsDirectory = Path.Combine(repositoryRoot, "src", "api", "BillSoft.Infrastructure", "Migrations");

        Assert.True(Directory.Exists(migrationsDirectory), migrationsDirectory);

        var migrationFiles = Directory.GetFiles(migrationsDirectory, "*CashierShiftFoundation*.cs", SearchOption.TopDirectoryOnly);

        Assert.Contains(migrationFiles, path => path.EndsWith("CashierShiftFoundation.cs", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(migrationFiles, path => path.EndsWith("CashierShiftFoundation.Designer.cs", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Permission_Constants_Should_Exist_And_Be_Seeded()
    {
        var codes = SystemPermissions.Definitions.Select(definition => definition.Code).ToArray();

        Assert.Contains("CashShift.View", codes);
        Assert.Contains("CashShift.Manage", codes);
        Assert.Contains("CashMovement.Record", codes);

        var adminPermissions = FoundationSeedData.RolePermissions
            .Where(item => item.RoleName == "Admin")
            .Select(item => item.PermissionCode)
            .ToArray();

        Assert.Contains("CashShift.View", adminPermissions);
        Assert.Contains("CashShift.Manage", adminPermissions);
        Assert.Contains("CashMovement.Record", adminPermissions);

        var cashierPermissions = FoundationSeedData.RolePermissions
            .Where(item => item.RoleName == "Cashier")
            .Select(item => item.PermissionCode)
            .ToArray();

        Assert.Contains("CashShift.View", cashierPermissions);
        Assert.Contains("CashShift.Manage", cashierPermissions);
        Assert.Contains("CashMovement.Record", cashierPermissions);

        var accountsPermissions = FoundationSeedData.RolePermissions
            .Where(item => item.RoleName == "AccountsUser")
            .Select(item => item.PermissionCode)
            .ToArray();

        Assert.Contains("CashShift.View", accountsPermissions);
    }

    private static BillSoftDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<BillSoftDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new BillSoftDbContext(options);
        context.Database.EnsureCreated();
        return context;
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

    private sealed class CashierShiftModelPropertyNames
    {
        public Guid RestaurantId { get; set; }

        public Guid BranchId { get; set; }

        public Guid OpenedByUserId { get; set; }

        public Guid? ClosedByUserId { get; set; }

        public DateTime BusinessDate { get; set; }
    }

    private sealed class CashierMovementModelPropertyNames
    {
        public Guid RestaurantId { get; set; }

        public Guid BranchId { get; set; }

        public Guid CashierShiftId { get; set; }

        public Guid CreatedByUserId { get; set; }
    }
}
