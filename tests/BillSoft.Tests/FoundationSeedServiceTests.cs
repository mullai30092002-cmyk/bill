using BillSoft.Infrastructure.Seed;
using BillSoft.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BillSoft.Tests;

public sealed class FoundationSeedServiceTests
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
    public async Task FoundationSeedService_Should_Insert_Missing_Roles_And_Permissions_Idempotently()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateContext(connection);

        var service = new FoundationSeedService(context);

        var firstResult = await service.SeedAsync();

        Assert.Equal(FoundationSeedData.Permissions.Count, firstResult.PermissionsInserted);
        Assert.Equal(FoundationSeedData.Roles.Count, firstResult.RolesInserted);
        Assert.Equal(FoundationSeedData.RolePermissions.Count, firstResult.RolePermissionsInserted);
        Assert.True(firstResult.StartedAtUtc <= firstResult.CompletedAtUtc);

        Assert.Equal(FoundationSeedData.Permissions.Count, await context.Permissions.CountAsync());
        Assert.Equal(FoundationSeedData.Roles.Count, await context.Roles.CountAsync());
        Assert.Equal(FoundationSeedData.RolePermissions.Count, await context.RolePermissions.CountAsync());

        var secondResult = await service.SeedAsync();

        Assert.Equal(0, secondResult.PermissionsInserted);
        Assert.Equal(0, secondResult.RolesInserted);
        Assert.Equal(0, secondResult.RolePermissionsInserted);
        Assert.True(secondResult.StartedAtUtc <= secondResult.CompletedAtUtc);

        Assert.Equal(FoundationSeedData.Permissions.Count, await context.Permissions.CountAsync());
        Assert.Equal(FoundationSeedData.Roles.Count, await context.Roles.CountAsync());
        Assert.Equal(FoundationSeedData.RolePermissions.Count, await context.RolePermissions.CountAsync());
    }
}
