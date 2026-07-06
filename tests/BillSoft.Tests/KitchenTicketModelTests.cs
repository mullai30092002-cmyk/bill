using BillSoft.Domain.Security;
using BillSoft.Infrastructure.Persistence;
using BillSoft.Infrastructure.Seed;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BillSoft.Tests;

public sealed class KitchenTicketModelTests
{
    [Fact]
    public void KitchenTicket_Entities_Should_Initialize_Ids_And_Timestamps()
    {
        var kitchenTicketType = ResolveType("BillSoft.Domain.Kitchen.KitchenTicket");
        var kitchenTicketLineType = ResolveType("BillSoft.Domain.Kitchen.KitchenTicketLine");
        var kitchenTicketSequenceType = ResolveType("BillSoft.Domain.Kitchen.KitchenTicketNumberSequence");

        Assert.NotNull(kitchenTicketType);
        Assert.NotNull(kitchenTicketLineType);
        Assert.NotNull(kitchenTicketSequenceType);

        var ticket = Activator.CreateInstance(kitchenTicketType!);
        var line = Activator.CreateInstance(kitchenTicketLineType!);
        var sequence = Activator.CreateInstance(kitchenTicketSequenceType!);

        Assert.NotEqual(Guid.Empty, (Guid)kitchenTicketType!.GetProperty("KitchenTicketId")!.GetValue(ticket)!);
        Assert.NotEqual(default, (DateTimeOffset)kitchenTicketType.GetProperty("CreatedAt")!.GetValue(ticket)!);
        Assert.Equal("NotDeducted", kitchenTicketType.GetProperty("InventoryDeductionStatus")!.GetValue(ticket)!.ToString());

        Assert.NotEqual(Guid.Empty, (Guid)kitchenTicketLineType!.GetProperty("KitchenTicketLineId")!.GetValue(line)!);
        Assert.NotEqual(default, (DateTimeOffset)kitchenTicketLineType.GetProperty("CreatedAt")!.GetValue(line)!);

        Assert.NotEqual(Guid.Empty, (Guid)kitchenTicketSequenceType!.GetProperty("KitchenTicketNumberSequenceId")!.GetValue(sequence)!);
        Assert.NotEqual(default, (DateTimeOffset)kitchenTicketSequenceType.GetProperty("CreatedAt")!.GetValue(sequence)!);
    }

    [Fact]
    public void DbContext_Should_Expose_Kitchen_Tables_And_Required_FKs()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateContext(connection);

        Assert.NotNull(context.GetType().GetProperty("KitchenTickets"));
        Assert.NotNull(context.GetType().GetProperty("KitchenTicketLines"));
        Assert.NotNull(context.GetType().GetProperty("KitchenTicketNumberSequences"));

        var ticketType = ResolveEntityType(context, "BillSoft.Domain.Kitchen.KitchenTicket");
        var lineType = ResolveEntityType(context, "BillSoft.Domain.Kitchen.KitchenTicketLine");
        var sequenceType = ResolveEntityType(context, "BillSoft.Domain.Kitchen.KitchenTicketNumberSequence");

        Assert.NotNull(ticketType);
        Assert.NotNull(lineType);
        Assert.NotNull(sequenceType);

        Assert.Contains(ticketType!.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(KitchenTicketModelPropertyNames.RestaurantId)], StringComparer.Ordinal));

        Assert.Contains(ticketType.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(KitchenTicketModelPropertyNames.BranchId)], StringComparer.Ordinal));

        Assert.Contains(ticketType.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(KitchenTicketModelPropertyNames.PosOrderId)], StringComparer.Ordinal));

        Assert.Contains(ticketType.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(KitchenTicketModelPropertyNames.CreatedByUserId)], StringComparer.Ordinal));

        Assert.Contains(ticketType.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(KitchenTicketModelPropertyNames.LastStatusChangedByUserId)], StringComparer.Ordinal));

        Assert.Contains(ticketType.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(KitchenTicketModelPropertyNames.CancelledByUserId)], StringComparer.Ordinal));

        Assert.Contains(lineType!.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(KitchenTicketLineModelPropertyNames.KitchenTicketId)], StringComparer.Ordinal));

        Assert.Contains(lineType.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(KitchenTicketLineModelPropertyNames.RestaurantId)], StringComparer.Ordinal));

        Assert.Contains(lineType.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(KitchenTicketLineModelPropertyNames.PosOrderLineId)], StringComparer.Ordinal));

        Assert.Contains(lineType.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(KitchenTicketLineModelPropertyNames.MenuItemId)], StringComparer.Ordinal));

        Assert.Contains(lineType.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(KitchenTicketLineModelPropertyNames.MenuCategoryId)], StringComparer.Ordinal));

        Assert.Contains(sequenceType!.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(KitchenTicketNumberSequenceModelPropertyNames.RestaurantId)], StringComparer.Ordinal));

        Assert.Contains(sequenceType.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([nameof(KitchenTicketNumberSequenceModelPropertyNames.BranchId)], StringComparer.Ordinal));
    }

    [Fact]
    public void KitchenTicket_Decimal_Precision_Should_Be_Configured()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateContext(connection);

        var lineType = ResolveEntityType(context, "BillSoft.Domain.Kitchen.KitchenTicketLine");

        Assert.NotNull(lineType);
        Assert.Equal(18, lineType!.FindProperty(nameof(KitchenTicketLineModelPropertyNames.Quantity))!.GetPrecision());
        Assert.Equal(3, lineType.FindProperty(nameof(KitchenTicketLineModelPropertyNames.Quantity))!.GetScale());
    }

    [Fact]
    public void KitchenTicket_Should_Have_Unique_Index_For_Restaurant_Branch_And_TicketNumber()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateContext(connection);

        var ticketType = ResolveEntityType(context, "BillSoft.Domain.Kitchen.KitchenTicket");

        Assert.NotNull(ticketType);

        var index = ticketType!.GetIndexes()
            .SingleOrDefault(candidate =>
                candidate.Properties.Select(property => property.Name)
                    .SequenceEqual([nameof(KitchenTicketModelPropertyNames.RestaurantId), nameof(KitchenTicketModelPropertyNames.BranchId), nameof(KitchenTicketModelPropertyNames.TicketNumber)], StringComparer.Ordinal));

        Assert.NotNull(index);
        Assert.True(index!.IsUnique);
    }

    [Fact]
    public void KitchenTicket_Should_Have_Filtered_Unique_Index_For_Active_PosOrder()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateContext(connection);

        var ticketType = ResolveEntityType(context, "BillSoft.Domain.Kitchen.KitchenTicket");

        Assert.NotNull(ticketType);

        var index = ticketType!.GetIndexes()
            .SingleOrDefault(candidate =>
                candidate.Properties.Select(property => property.Name)
                    .SequenceEqual([nameof(KitchenTicketModelPropertyNames.PosOrderId)], StringComparer.Ordinal));

        Assert.NotNull(index);
        Assert.True(index!.IsUnique);
        Assert.Equal("[Status] <> 'Cancelled'", index.GetFilter());
    }

    [Fact]
    public void KitchenTicketNumberSequence_Should_Have_Unique_Index_For_Restaurant_Branch_And_TicketDate()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateContext(connection);

        var sequenceType = ResolveEntityType(context, "BillSoft.Domain.Kitchen.KitchenTicketNumberSequence");

        Assert.NotNull(sequenceType);

        var index = sequenceType!.GetIndexes()
            .SingleOrDefault(candidate =>
                candidate.Properties.Select(property => property.Name)
                    .SequenceEqual([nameof(KitchenTicketNumberSequenceModelPropertyNames.RestaurantId), nameof(KitchenTicketNumberSequenceModelPropertyNames.BranchId), nameof(KitchenTicketNumberSequenceModelPropertyNames.TicketDate)], StringComparer.Ordinal));

        Assert.NotNull(index);
        Assert.True(index!.IsUnique);
    }

    [Fact]
    public void Migration_Should_Include_KitchenTicketFoundation()
    {
        var repositoryRoot = LocateRepositoryRoot();
        var migrationsDirectory = Path.Combine(repositoryRoot, "src", "api", "BillSoft.Infrastructure", "Migrations");

        Assert.True(Directory.Exists(migrationsDirectory), migrationsDirectory);

        var migrationFiles = Directory.GetFiles(migrationsDirectory, "*KitchenTicketFoundation*.cs", SearchOption.TopDirectoryOnly);

        Assert.Contains(migrationFiles, path => path.EndsWith("KitchenTicketFoundation.cs", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(migrationFiles, path => path.EndsWith("KitchenTicketFoundation.Designer.cs", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Permission_Constants_Should_Exist_And_Be_Seeded()
    {
        var codes = SystemPermissions.Definitions.Select(definition => definition.Code).ToArray();

        Assert.Contains("KitchenTicket.View", codes);
        Assert.Contains("KitchenTicket.Manage", codes);
        Assert.Contains("KitchenTicket.UpdateStatus", codes);

        var adminPermissions = FoundationSeedData.RolePermissions
            .Where(item => item.RoleName == "Admin")
            .Select(item => item.PermissionCode)
            .ToArray();

        Assert.Contains("KitchenTicket.View", adminPermissions);
        Assert.Contains("KitchenTicket.Manage", adminPermissions);
        Assert.Contains("KitchenTicket.UpdateStatus", adminPermissions);

        var kitchenPermissions = FoundationSeedData.RolePermissions
            .Where(item => item.RoleName == "KitchenUser")
            .Select(item => item.PermissionCode)
            .ToArray();

        Assert.Contains("KitchenTicket.View", kitchenPermissions);
        Assert.Contains("KitchenTicket.UpdateStatus", kitchenPermissions);

        var cashierPermissions = FoundationSeedData.RolePermissions
            .Where(item => item.RoleName == "Cashier")
            .Select(item => item.PermissionCode)
            .ToArray();

        Assert.Contains("KitchenTicket.View", cashierPermissions);
        Assert.Contains("KitchenTicket.Manage", cashierPermissions);

        var waiterPermissions = FoundationSeedData.RolePermissions
            .Where(item => item.RoleName == "Waiter")
            .Select(item => item.PermissionCode)
            .ToArray();

        Assert.Contains("KitchenTicket.View", waiterPermissions);
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

    private static Microsoft.EntityFrameworkCore.Metadata.IEntityType ResolveEntityType(BillSoftDbContext context, string fullName)
    {
        return context.Model.GetEntityTypes()
            .SingleOrDefault(entityType => string.Equals(entityType.ClrType.FullName, fullName, StringComparison.Ordinal))!;
    }

    private static Type ResolveType(string fullName)
    {
        return Type.GetType($"{fullName}, BillSoft.Domain")!;
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

    private sealed class KitchenTicketModelPropertyNames
    {
        public Guid RestaurantId { get; set; }

        public Guid BranchId { get; set; }

        public Guid PosOrderId { get; set; }

        public Guid? CreatedByUserId { get; set; }

        public Guid? LastStatusChangedByUserId { get; set; }

        public Guid? CancelledByUserId { get; set; }

        public string TicketNumber { get; set; } = string.Empty;
    }

    private sealed class KitchenTicketLineModelPropertyNames
    {
        public Guid KitchenTicketId { get; set; }

        public Guid RestaurantId { get; set; }

        public Guid PosOrderLineId { get; set; }

        public Guid MenuItemId { get; set; }

        public Guid MenuCategoryId { get; set; }

        public decimal Quantity { get; set; }
    }

    private sealed class KitchenTicketNumberSequenceModelPropertyNames
    {
        public Guid RestaurantId { get; set; }

        public Guid BranchId { get; set; }

        public DateTime TicketDate { get; set; }
    }
}
