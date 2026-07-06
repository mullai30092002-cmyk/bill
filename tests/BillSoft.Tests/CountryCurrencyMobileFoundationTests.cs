using System.Reflection;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Users;
using BillSoft.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BillSoft.Tests;

public sealed class CountryCurrencyMobileFoundationTests
{
    [Fact]
    public void Restaurant_Model_Should_Expose_Country_Currency_And_TimeZone_Fields()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var context = CreateContext(connection);

        var entityType = context.Model.FindEntityType(typeof(Restaurant));
        Assert.NotNull(entityType);

        Assert.Equal(2, entityType!.FindProperty(nameof(Restaurant.CountryCode))!.GetMaxLength());
        Assert.Equal(3, entityType.FindProperty(nameof(Restaurant.CurrencyCode))!.GetMaxLength());
        Assert.Equal(80, entityType.FindProperty(nameof(Restaurant.TimeZoneId))!.GetMaxLength());
        Assert.True(entityType.FindProperty(nameof(Restaurant.CountryCode))!.IsNullable == false);
        Assert.True(entityType.FindProperty(nameof(Restaurant.CurrencyCode))!.IsNullable == false);
        Assert.True(entityType.FindProperty(nameof(Restaurant.TimeZoneId))!.IsNullable == false);
    }

    [Fact]
    public void Branch_Model_Should_Expose_Country_Currency_And_TimeZone_Fields()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var context = CreateContext(connection);

        var entityType = context.Model.FindEntityType(typeof(Branch));
        Assert.NotNull(entityType);

        Assert.Equal(2, entityType!.FindProperty(nameof(Branch.CountryCode))!.GetMaxLength());
        Assert.Equal(3, entityType.FindProperty(nameof(Branch.CurrencyCode))!.GetMaxLength());
        Assert.Equal(80, entityType.FindProperty(nameof(Branch.TimeZoneId))!.GetMaxLength());
        Assert.True(entityType.FindProperty(nameof(Branch.CountryCode))!.IsNullable == false);
        Assert.True(entityType.FindProperty(nameof(Branch.CurrencyCode))!.IsNullable == false);
        Assert.True(entityType.FindProperty(nameof(Branch.TimeZoneId))!.IsNullable == false);
    }

    [Fact]
    public void User_Model_Should_Expose_Normalized_Mobile_Fields()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var context = CreateContext(connection);

        var entityType = context.Model.FindEntityType(typeof(User));
        Assert.NotNull(entityType);

        Assert.Equal(2, entityType!.FindProperty(nameof(User.MobileCountryCode))!.GetMaxLength());
        Assert.Equal(6, entityType.FindProperty(nameof(User.MobileDialCode))!.GetMaxLength());
        Assert.Equal(20, entityType.FindProperty(nameof(User.MobileNationalNumber))!.GetMaxLength());
        Assert.Equal(20, entityType.FindProperty(nameof(User.MobileE164))!.GetMaxLength());
        Assert.True(entityType.FindProperty(nameof(User.MobileCountryCode))!.IsNullable == false);
        Assert.True(entityType.FindProperty(nameof(User.MobileDialCode))!.IsNullable == false);
        Assert.True(entityType.FindProperty(nameof(User.MobileNationalNumber))!.IsNullable == false);
        Assert.True(entityType.FindProperty(nameof(User.MobileE164))!.IsNullable == false);
    }

    [Fact]
    public void User_Model_Should_Enforce_Unique_RestaurantId_And_MobileE164_Index()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var context = CreateContext(connection);

        var entityType = context.Model.FindEntityType(typeof(User));
        Assert.NotNull(entityType);

        var index = entityType!.GetIndexes()
            .SingleOrDefault(candidate => candidate.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(User.RestaurantId), nameof(User.MobileE164)], StringComparer.Ordinal));

        Assert.NotNull(index);
        Assert.True(index!.IsUnique);
    }

    [Fact]
    public void Migration_Should_Be_Named_CountryCurrencyMobileFoundation()
    {
        var migrationType = typeof(BillSoftDbContext).Assembly
            .GetTypes()
            .SingleOrDefault(type => string.Equals(type.Name, "CountryCurrencyMobileFoundation", StringComparison.Ordinal));

        Assert.NotNull(migrationType);
        Assert.True(typeof(Microsoft.EntityFrameworkCore.Migrations.Migration).IsAssignableFrom(migrationType!));
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
}
