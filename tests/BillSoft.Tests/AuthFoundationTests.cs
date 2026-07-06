using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using BillSoft.Domain.Common;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Security;
using BillSoft.Domain.Users;
using BillSoft.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace BillSoft.Tests;

public sealed class AuthFoundationTests
{
    private static readonly Assembly DomainAssembly = typeof(BaseEntity).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(BillSoftDbContext).Assembly;
    private static readonly Assembly ApplicationAssembly = Assembly.Load("BillSoft.Application");

    [Fact]
    public void RefreshToken_Should_Initialize_NonEmpty_Id()
    {
        var refreshTokenType = DomainAssembly.GetType("BillSoft.Domain.Security.RefreshToken");

        Assert.NotNull(refreshTokenType);

        var refreshToken = Activator.CreateInstance(refreshTokenType!);

        Assert.NotNull(refreshToken);

        var refreshTokenId = refreshTokenType!.GetProperty("RefreshTokenId", BindingFlags.Instance | BindingFlags.Public);
        var createdAt = refreshTokenType.GetProperty("CreatedAt", BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(refreshTokenId);
        Assert.NotNull(createdAt);

        Assert.NotEqual(Guid.Empty, (Guid)refreshTokenId!.GetValue(refreshToken)!);
        Assert.NotEqual(default, (DateTimeOffset)createdAt!.GetValue(refreshToken)!);
    }

    [Fact]
    public void RefreshTokens_Should_Exist_In_Ef_Metadata()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<BillSoftDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new BillSoftDbContext(options);
        context.Database.EnsureCreated();

        var refreshTokenType = DomainAssembly.GetType("BillSoft.Domain.Security.RefreshToken");

        Assert.NotNull(refreshTokenType);
        Assert.NotNull(context.Model.FindEntityType(refreshTokenType!));
    }

    [Fact]
    public void RefreshToken_Should_Have_Unique_TokenHash_Index_And_No_Plaintext_Field()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<BillSoftDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new BillSoftDbContext(options);
        context.Database.EnsureCreated();

        var refreshTokenType = DomainAssembly.GetType("BillSoft.Domain.Security.RefreshToken");

        Assert.NotNull(refreshTokenType);

        var entityType = context.Model.FindEntityType(refreshTokenType!);
        Assert.NotNull(entityType);

        var tokenHashIndex = entityType!.GetIndexes()
            .SingleOrDefault(index =>
                index.Properties.Select(property => property.Name)
                    .SequenceEqual(["TokenHash"], StringComparer.Ordinal));

        Assert.NotNull(tokenHashIndex);
        Assert.True(tokenHashIndex!.IsUnique);

        var propertyNames = entityType.GetProperties().Select(property => property.Name).ToArray();
        Assert.DoesNotContain("PlaintextToken", propertyNames);
        Assert.DoesNotContain("PlainTextToken", propertyNames);
        Assert.DoesNotContain("RefreshToken", propertyNames);
    }

    [Fact]
    public void RefreshTokenHash_Should_Be_Deterministic()
    {
        var hashType = InfrastructureAssembly.GetType("BillSoft.Infrastructure.Auth.RefreshTokenHash");

        Assert.NotNull(hashType);

        var hashMethod = hashType!.GetMethod("Compute", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(hashMethod);

        var first = (string)hashMethod!.Invoke(null, new object[] { "sample-token" })!;
        var second = (string)hashMethod.Invoke(null, new object[] { "sample-token" })!;
        var third = (string)hashMethod.Invoke(null, new object[] { "different-token" })!;

        Assert.Equal(first, second);
        Assert.NotEqual(first, third);
        Assert.Matches("^[A-F0-9]{64}$", first);
    }

    [Fact]
    public void RefreshTokenHash_Should_Respect_Whitespace_As_Part_Of_The_Secret()
    {
        var hashType = InfrastructureAssembly.GetType("BillSoft.Infrastructure.Auth.RefreshTokenHash");

        Assert.NotNull(hashType);

        var hashMethod = hashType!.GetMethod("Compute", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(hashMethod);

        var trimmed = (string)hashMethod!.Invoke(null, new object[] { "secret-token" })!;
        var spaced = (string)hashMethod.Invoke(null, new object[] { " secret-token " })!;

        Assert.NotEqual(trimmed, spaced);
    }

    [Fact]
    public void JwtTokenService_Should_Create_Access_Token_With_BillSoft_Claims()
    {
        var jwtOptionsType = InfrastructureAssembly.GetType("BillSoft.Infrastructure.Auth.JwtOptions");
        var serviceType = InfrastructureAssembly.GetType("BillSoft.Infrastructure.Auth.JwtTokenService");
        var resultType = ApplicationAssembly.GetType("BillSoft.Application.Auth.AuthTokenResult");

        Assert.NotNull(jwtOptionsType);
        Assert.NotNull(serviceType);
        Assert.NotNull(resultType);

        var jwtOptions = Activator.CreateInstance(jwtOptionsType!);
        Assert.NotNull(jwtOptions);

        jwtOptionsType!.GetProperty("Issuer")!.SetValue(jwtOptions, "BillSoft");
        jwtOptionsType.GetProperty("Audience")!.SetValue(jwtOptions, "BillSoft");
        jwtOptionsType.GetProperty("SigningKey")!.SetValue(jwtOptions, "unit-test-signing-key-unit-test-signing-key");
        jwtOptionsType.GetProperty("AccessTokenLifetimeMinutes")!.SetValue(jwtOptions, 15);
        jwtOptionsType.GetProperty("RefreshTokenLifetimeDays")!.SetValue(jwtOptions, 7);

        var optionsWrapperType = typeof(Options).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == nameof(Options.Create) && method.IsGenericMethodDefinition)
            .MakeGenericMethod(jwtOptionsType!);

        var options = optionsWrapperType.Invoke(null, new[] { jwtOptions });
        Assert.NotNull(options);

        var service = Activator.CreateInstance(serviceType!, options);
        Assert.NotNull(service);

        var restaurant = new Restaurant();
        restaurant.SetRestaurantCode("bs-001");

        var branch = new Branch();
        var user = new User
        {
            FullName = "Test User",
            MobileNumber = "91234567"
        };

        var createMethod = serviceType.GetMethod(
            "CreateAccessToken",
            BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(createMethod);

        var result = createMethod!.Invoke(service, new object?[]
        {
            user,
            restaurant,
            branch,
            new[] { "Cashier" },
            new[] { "Bill.Create", "Bill.CollectPayment" },
            "session-123",
            "Cashier",
            false
        });

        Assert.NotNull(result);

        var tokenProperty = resultType!.GetProperty("AccessToken");
        var expiresAtProperty = resultType.GetProperty("ExpiresAtUtc");

        Assert.NotNull(tokenProperty);
        Assert.NotNull(expiresAtProperty);

        var token = (string)tokenProperty!.GetValue(result!)!;
        var expiresAt = (DateTimeOffset)expiresAtProperty!.GetValue(result!)!;

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.True(expiresAt > DateTimeOffset.UtcNow);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        var claims = jwt.Claims.ToArray();

        Assert.Contains(claims, claim => claim.Type == ClaimTypes.NameIdentifier && claim.Value == user.UserId.ToString());
        Assert.Contains(claims, claim => claim.Type == "restaurant_code" && claim.Value == "BS-001");
        Assert.Contains(claims, claim => claim.Type == "restaurant_id" && claim.Value == restaurant.RestaurantId.ToString());
        Assert.Contains(claims, claim => claim.Type == "branch_id" && claim.Value == branch.BranchId.ToString());
        Assert.Contains(claims, claim => claim.Type == "session_id" && claim.Value == "session-123");
        Assert.Contains(claims, claim => claim.Type == "active_role" && claim.Value == "Cashier");
        Assert.Contains(claims, claim => claim.Type == "permission" && claim.Value == "Bill.Create");
        Assert.Contains(claims, claim => claim.Type == "permission" && claim.Value == "Bill.CollectPayment");
    }

    [Fact]
    public void JwtTokenService_Should_Reject_Missing_Or_TooShort_SigningKey()
    {
        var jwtOptionsType = InfrastructureAssembly.GetType("BillSoft.Infrastructure.Auth.JwtOptions");
        var serviceType = InfrastructureAssembly.GetType("BillSoft.Infrastructure.Auth.JwtTokenService");

        Assert.NotNull(jwtOptionsType);
        Assert.NotNull(serviceType);

        var jwtOptions = Activator.CreateInstance(jwtOptionsType!);
        Assert.NotNull(jwtOptions);

        jwtOptionsType!.GetProperty("Issuer")!.SetValue(jwtOptions, "BillSoft");
        jwtOptionsType.GetProperty("Audience")!.SetValue(jwtOptions, "BillSoft");
        jwtOptionsType.GetProperty("SigningKey")!.SetValue(jwtOptions, "short-key");
        jwtOptionsType.GetProperty("AccessTokenLifetimeMinutes")!.SetValue(jwtOptions, 15);
        jwtOptionsType.GetProperty("RefreshTokenLifetimeDays")!.SetValue(jwtOptions, 7);

        var optionsWrapperType = typeof(Options).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == nameof(Options.Create) && method.IsGenericMethodDefinition)
            .MakeGenericMethod(jwtOptionsType!);

        var options = optionsWrapperType.Invoke(null, new[] { jwtOptions });
        Assert.NotNull(options);

        var service = Activator.CreateInstance(serviceType!, options);
        Assert.NotNull(service);

        var restaurant = new Restaurant();
        restaurant.SetRestaurantCode("bs-001");
        var user = new User
        {
            FullName = "Test User",
            MobileNumber = "91234567"
        };

        var createMethod = serviceType.GetMethod("CreateAccessToken", BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(createMethod);

        var exception = Assert.Throws<TargetInvocationException>(() =>
            createMethod!.Invoke(service, new object?[]
            {
                user,
                restaurant,
                null,
                Array.Empty<string>(),
                Array.Empty<string>(),
                "session-123",
                null,
                false
            }));

        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Contains("SigningKey", exception.InnerException!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AuthenticationTokenFoundation_Migration_Should_Exist()
    {
        var migrationType = InfrastructureAssembly
            .GetTypes()
            .SingleOrDefault(type => string.Equals(type.Name, "AuthenticationTokenFoundation", StringComparison.Ordinal));

        Assert.NotNull(migrationType);
    }

    [Fact]
    public void SystemPermissions_Should_Expose_Read_Api_Constants()
    {
        var codes = SystemPermissions.Definitions.Select(definition => definition.Code).ToArray();

        Assert.Contains(SystemPermissions.UserManage, codes);
        Assert.Contains(SystemPermissions.RoleManage, codes);
        Assert.Contains(SystemPermissions.PermissionView, codes);
    }
}
