using BillSoft.Domain.Billing;
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

public sealed class BillingModelTests
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
    public void Bill_And_Payment_Entities_Should_Initialize_Ids_And_Timestamps()
    {
        var bill = new Bill();
        var billLine = new BillLine();
        var payment = new Payment();
        var printEvent = new BillPrintEvent();
        var billSequence = new BillNumberSequence();
        var paymentSequence = new PaymentNumberSequence();

        Assert.NotEqual(Guid.Empty, bill.BillId);
        Assert.NotEqual(default, bill.CreatedAt);

        Assert.NotEqual(Guid.Empty, billLine.BillLineId);
        Assert.NotEqual(default, billLine.CreatedAt);

        Assert.NotEqual(Guid.Empty, payment.PaymentId);
        Assert.NotEqual(default, payment.CreatedAt);

        Assert.NotEqual(Guid.Empty, printEvent.BillPrintEventId);
        Assert.NotEqual(default, printEvent.CreatedAt);

        Assert.NotEqual(Guid.Empty, billSequence.BillNumberSequenceId);
        Assert.NotEqual(default, billSequence.CreatedAt);

        Assert.NotEqual(Guid.Empty, paymentSequence.PaymentNumberSequenceId);
        Assert.NotEqual(default, paymentSequence.CreatedAt);
    }

    [Fact]
    public void DbContext_Should_Expose_Billing_Tables_With_Required_FKs()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateContext(connection);

        Assert.NotNull(context.Bills);
        Assert.NotNull(context.BillLines);
        Assert.NotNull(context.Payments);
        Assert.NotNull(context.BillPrintEvents);
        Assert.NotNull(context.BillNumberSequences);
        Assert.NotNull(context.PaymentNumberSequences);

        var billType = context.Model.FindEntityType(typeof(Bill));
        var billLineType = context.Model.FindEntityType(typeof(BillLine));
        var paymentType = context.Model.FindEntityType(typeof(Payment));
        var printEventType = context.Model.FindEntityType(typeof(BillPrintEvent));
        var billSequenceType = context.Model.FindEntityType(typeof(BillNumberSequence));
        var paymentSequenceType = context.Model.FindEntityType(typeof(PaymentNumberSequence));

        Assert.NotNull(billType);
        Assert.NotNull(billLineType);
        Assert.NotNull(paymentType);
        Assert.NotNull(printEventType);
        Assert.NotNull(billSequenceType);
        Assert.NotNull(paymentSequenceType);

        Assert.Contains(billType!.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Restaurant) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(Bill.RestaurantId)], StringComparer.Ordinal));

        Assert.Contains(billType.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Branch) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(Bill.BranchId)], StringComparer.Ordinal));

        Assert.Contains(billType.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(PosOrder) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(Bill.PosOrderId)], StringComparer.Ordinal));

        Assert.Contains(billLineType!.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Bill) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(BillLine.BillId)], StringComparer.Ordinal));

        Assert.Contains(billLineType.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(PosOrderLine) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(BillLine.PosOrderLineId)], StringComparer.Ordinal));

        Assert.Contains(paymentType!.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Bill) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(Payment.BillId)], StringComparer.Ordinal));

        Assert.Contains(printEventType!.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Bill) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(BillPrintEvent.BillId)], StringComparer.Ordinal));

        Assert.Contains(printEventType.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Restaurant) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(BillPrintEvent.RestaurantId)], StringComparer.Ordinal));

        Assert.Contains(printEventType.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Branch) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(BillPrintEvent.BranchId)], StringComparer.Ordinal));

        Assert.Contains(billSequenceType!.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Restaurant) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(BillNumberSequence.RestaurantId)], StringComparer.Ordinal));

        Assert.Contains(paymentSequenceType!.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Branch) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(PaymentNumberSequence.BranchId)], StringComparer.Ordinal));
    }

    [Fact]
    public void Bill_Should_Expose_Business_Date_As_A_Date_Column()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateContext(connection);

        var billType = context.Model.FindEntityType(typeof(Bill));

        Assert.NotNull(billType);
        var businessDate = billType!.FindProperty("BusinessDate");

        Assert.NotNull(businessDate);
        Assert.Equal("date", businessDate!.GetColumnType());
    }

    [Fact]
    public void Billing_Decimal_Precision_Should_Be_Configured()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateContext(connection);

        var billType = context.Model.FindEntityType(typeof(Bill));
        var billLineType = context.Model.FindEntityType(typeof(BillLine));
        var paymentType = context.Model.FindEntityType(typeof(Payment));

        Assert.NotNull(billType);
        Assert.NotNull(billLineType);
        Assert.NotNull(paymentType);

        Assert.Equal(18, billType!.FindProperty(nameof(Bill.Subtotal))!.GetPrecision());
        Assert.Equal(2, billType.FindProperty(nameof(Bill.GrandTotal))!.GetScale());
        Assert.Equal(18, billType.FindProperty(nameof(Bill.AmountPaid))!.GetPrecision());
        Assert.Equal(2, billType.FindProperty(nameof(Bill.BalanceDue))!.GetScale());

        Assert.Equal(18, billLineType!.FindProperty(nameof(BillLine.UnitPrice))!.GetPrecision());
        Assert.Equal(5, billLineType.FindProperty(nameof(BillLine.TaxRate))!.GetPrecision());
        Assert.Equal(3, billLineType.FindProperty(nameof(BillLine.Quantity))!.GetScale());

        Assert.Equal(18, paymentType!.FindProperty(nameof(Payment.Amount))!.GetPrecision());
        Assert.Equal(2, paymentType.FindProperty(nameof(Payment.Amount))!.GetScale());
    }

    [Fact]
    public void Billing_And_Payment_Number_Indexes_Should_Be_Unique()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateContext(connection);

        var billType = context.Model.FindEntityType(typeof(Bill));
        var paymentType = context.Model.FindEntityType(typeof(Payment));
        var billSequenceType = context.Model.FindEntityType(typeof(BillNumberSequence));
        var paymentSequenceType = context.Model.FindEntityType(typeof(PaymentNumberSequence));

        Assert.NotNull(billType);
        Assert.NotNull(paymentType);
        Assert.NotNull(billSequenceType);
        Assert.NotNull(paymentSequenceType);

        Assert.Contains(billType!.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(Bill.RestaurantId), nameof(Bill.BranchId), nameof(Bill.BillNumber)], StringComparer.Ordinal));

        Assert.Contains(billType.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(Bill.PosOrderId)], StringComparer.Ordinal));

        Assert.Contains(paymentType!.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(Payment.RestaurantId), nameof(Payment.BranchId), nameof(Payment.PaymentNumber)], StringComparer.Ordinal));

        Assert.Contains(context.Model.FindEntityType(typeof(BillPrintEvent))!.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(BillPrintEvent.BillId), nameof(BillPrintEvent.PrintSequence)], StringComparer.Ordinal));

        Assert.Contains(billSequenceType!.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(BillNumberSequence.RestaurantId), nameof(BillNumberSequence.BranchId), nameof(BillNumberSequence.BillDate)], StringComparer.Ordinal));

        Assert.Contains(paymentSequenceType!.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(PaymentNumberSequence.RestaurantId), nameof(PaymentNumberSequence.BranchId), nameof(PaymentNumberSequence.PaymentDate)], StringComparer.Ordinal));
    }

    [Fact]
    public void Migration_Should_Include_BillingPaymentFoundation()
    {
        var repositoryRoot = LocateRepositoryRoot();
        var migrationsDirectory = Path.Combine(repositoryRoot, "src", "api", "BillSoft.Infrastructure", "Migrations");
        Assert.True(Directory.Exists(migrationsDirectory), migrationsDirectory);

        var migrationFiles = Directory.GetFiles(migrationsDirectory, "*BillingPaymentFoundation*.cs", SearchOption.TopDirectoryOnly);

        Assert.Contains(migrationFiles, path => path.EndsWith("BillingPaymentFoundation.cs", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(migrationFiles, path => path.EndsWith("BillingPaymentFoundation.Designer.cs", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Migration_Should_Include_ReceiptPrintAuditFoundation()
    {
        var repositoryRoot = LocateRepositoryRoot();
        var migrationsDirectory = Path.Combine(repositoryRoot, "src", "api", "BillSoft.Infrastructure", "Migrations");
        Assert.True(Directory.Exists(migrationsDirectory), migrationsDirectory);

        var migrationFiles = Directory.GetFiles(migrationsDirectory, "*ReceiptPrintAuditFoundation*.cs", SearchOption.TopDirectoryOnly);

        Assert.Contains(migrationFiles, path => path.EndsWith("ReceiptPrintAuditFoundation.cs", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(migrationFiles, path => path.EndsWith("ReceiptPrintAuditFoundation.Designer.cs", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Permission_Constants_Should_Exist_And_Be_Seeded()
    {
        var codes = SystemPermissions.Definitions.Select(definition => definition.Code).ToArray();

        Assert.Contains(SystemPermissions.BillingView, codes);
        Assert.Contains(SystemPermissions.BillingManage, codes);
        Assert.Contains(SystemPermissions.PaymentRecord, codes);
        Assert.Contains(SystemPermissions.PaymentCancel, codes);

        var cashierPermissionCodes = FoundationSeedData.RolePermissions
            .Where(item => item.RoleName == "Cashier")
            .Select(item => item.PermissionCode)
            .ToArray();

        Assert.Contains(SystemPermissions.BillingView, cashierPermissionCodes);
        Assert.Contains(SystemPermissions.BillingManage, cashierPermissionCodes);
        Assert.Contains(SystemPermissions.PaymentRecord, cashierPermissionCodes);
        Assert.Contains(SystemPermissions.PaymentCancel, cashierPermissionCodes);

        var waiterPermissionCodes = FoundationSeedData.RolePermissions
            .Where(item => item.RoleName == "Waiter")
            .Select(item => item.PermissionCode)
            .ToArray();

        Assert.Contains(SystemPermissions.BillingView, waiterPermissionCodes);

        var accountsPermissionCodes = FoundationSeedData.RolePermissions
            .Where(item => item.RoleName == "AccountsUser")
            .Select(item => item.PermissionCode)
            .ToArray();

        Assert.Contains(SystemPermissions.BillingView, accountsPermissionCodes);
        Assert.Contains(SystemPermissions.CashShiftView, accountsPermissionCodes);
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
}
