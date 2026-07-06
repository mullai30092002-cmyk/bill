using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Security;
using BillSoft.Domain.Users;
using BillSoft.Infrastructure.Auth;
using BillSoft.Infrastructure.Persistence;
using BillSoft.Infrastructure.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace BillSoft.Tests;

public sealed class BranchAdminEndpointTests
{
    [Fact]
    public async Task List_Should_Return_401_When_Unauthenticated()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();

        var response = await fixture.Client.GetAsync("/api/v1/admin/branches");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_Should_Return_403_When_User_Lacks_Branch_And_User_Manage()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Cashier");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.GetAsync("/api/v1/admin/branches");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_Should_Succeed_For_Branch_Manage()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.GetAsync("/api/v1/admin/branches");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task List_Should_Succeed_For_User_Manage()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedCustomUserAsync("Cashier", SystemPermissions.UserManage);
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.GetAsync("/api/v1/admin/branches");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task List_Should_Return_Only_Current_Restaurant_Branches()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        var foreignBranch = await fixture.SeedForeignBranchAsync();
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.GetAsync("/api/v1/admin/branches");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<BranchListResponseDto>();
        Assert.NotNull(payload);
        Assert.All(payload!.Items, item => Assert.Equal(seed.RestaurantId, item.RestaurantId));
        Assert.DoesNotContain(payload.Items, item => item.BranchId == foreignBranch.BranchId);
    }

    [Fact]
    public async Task List_Should_Sort_Active_Branches_Before_Inactive_Then_By_Name()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.GetAsync("/api/v1/admin/branches");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<BranchListResponseDto>();
        Assert.NotNull(payload);

        var items = payload!.Items;
        Assert.Equal(new[]
        {
            "Alpha Branch",
            "Delta Branch",
            "Beta Branch",
            "Gamma Branch"
        }, items.Select(item => item.Name).ToArray());
        Assert.Equal(new[] { "Active", "Active", "Inactive", "Inactive" }, items.Select(item => item.Status).ToArray());
    }

    [Fact]
    public async Task List_Status_Filter_Should_Work()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.GetAsync("/api/v1/admin/branches?status=Inactive");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<BranchListResponseDto>();
        Assert.NotNull(payload);

        Assert.All(payload!.Items, item => Assert.Equal("Inactive", item.Status));
        Assert.Equal(new[] { "Beta Branch", "Gamma Branch" }, payload.Items.Select(item => item.Name).ToArray());
    }

    [Fact]
    public async Task List_Invalid_Status_Filter_Should_Return_400_With_All_Allowed_Status_Names()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.GetAsync("/api/v1/admin/branches?status=Archived");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(problem);

        var expectedStatuses = Enum.GetNames<BranchStatus>();
        Assert.Equal($"Status filter must be one of: {string.Join(", ", expectedStatuses)}.", problem!.Detail);
        foreach (var status in expectedStatuses)
        {
            Assert.Contains(status, problem.Detail, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task List_Search_Filter_Should_Match_Branch_Name()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.GetAsync("/api/v1/admin/branches?search=Delta");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<BranchListResponseDto>();
        Assert.NotNull(payload);

        Assert.Single(payload!.Items);
        Assert.Equal("Delta Branch", payload.Items[0].Name);
    }

    [Fact]
    public async Task Get_Should_Return_Current_Restaurant_Branch()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.GetAsync($"/api/v1/admin/branches/{seed.ActiveBranchId}");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<BranchDetailDto>();
        Assert.NotNull(payload);
        Assert.Equal(seed.ActiveBranchId, payload!.BranchId);
        Assert.Equal(seed.RestaurantId, payload.RestaurantId);
        Assert.Equal("Alpha Branch", payload.Name);
        Assert.Equal("Active", payload.Status);
        Assert.NotEqual(default, payload.CreatedAt);
    }

    [Fact]
    public async Task Read_Should_Return_Compatible_Timezone_And_Currency_Values()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var listResponse = await fixture.Client.GetAsync("/api/v1/admin/branches");
        listResponse.EnsureSuccessStatusCode();
        var listPayload = await listResponse.Content.ReadFromJsonAsync<BranchListResponseDto>();
        Assert.NotNull(listPayload);
        var listItem = Assert.Single(listPayload!.Items, item => item.BranchId == seed.ActiveBranchId);

        Assert.Equal("SG", listItem.CountryCode);
        Assert.Equal("Asia/Singapore", listItem.Timezone);
        Assert.Equal("SGD", listItem.Currency);

        var detailResponse = await fixture.Client.GetAsync($"/api/v1/admin/branches/{seed.ActiveBranchId}");
        detailResponse.EnsureSuccessStatusCode();
        var detailPayload = await detailResponse.Content.ReadFromJsonAsync<BranchDetailDto>();
        Assert.NotNull(detailPayload);
        Assert.Equal("SG", detailPayload!.CountryCode);
        Assert.Equal("Asia/Singapore", detailPayload.Timezone);
        Assert.Equal("SGD", detailPayload.Currency);
    }

    [Fact]
    public async Task Get_Should_Return_404_For_Other_Restaurant_Branch()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        var foreignBranch = await fixture.SeedForeignBranchAsync();
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.GetAsync($"/api/v1/admin/branches/{foreignBranch.BranchId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void Permission_Constants_Should_Exist_In_SystemPermissions_Definitions()
    {
        var codes = SystemPermissions.Definitions.Select(definition => definition.Code).ToArray();

        Assert.Contains(SystemPermissions.BranchManage, codes);
    }

    [Theory]
    [InlineData("DELETE", "/api/v1/admin/branches")]
    public async Task Collection_Delete_Route_Should_Not_Exist(string method, string path)
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"Expected 404 or 405, got {(int)response.StatusCode}.");
    }

    [Theory]
    [InlineData("DELETE")]
    public async Task Item_Delete_Route_Should_Not_Exist(string method)
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), $"/api/v1/admin/branches/{seed.ActiveBranchId}"));

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"Expected 404 or 405, got {(int)response.StatusCode}.");
    }

    private sealed class BranchAdminApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private HttpClient? _client;

        public HttpClient Client => _client ??= CreateClient();

        public static async Task<BranchAdminApiFactory> CreateAsync()
        {
            var factory = new BranchAdminApiFactory();
            await factory.InitializeAsync();
            return factory;
        }

        private async Task InitializeAsync()
        {
            await _connection.OpenAsync();
            _ = Services;
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            await context.Database.EnsureCreatedAsync();
        }

        public async Task<SeedResult> SeedSystemUserAsync(string roleName)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            await new FoundationSeedService(context).SeedAsync();

        var restaurant = new Restaurant
        {
            Name = $"{roleName} Restaurant",
            Status = RestaurantStatus.Active
        };
        restaurant.SetRestaurantCode($"REST{roleName[..1].ToUpperInvariant()}01");
        restaurant.SetCountryProfile("SG");
        var profile = BillSoft.Domain.Localization.CountryProfileCatalog.GetRequired("SG");

        var alphaBranch = new Branch
        {
            Name = "Alpha Branch",
            RestaurantId = restaurant.RestaurantId,
            Status = BranchStatus.Active,
            CountryCode = profile.CountryCode,
            TimeZoneId = profile.TimeZoneId,
            CurrencyCode = profile.CurrencyCode
        };

        var deltaBranch = new Branch
        {
            Name = "Delta Branch",
            RestaurantId = restaurant.RestaurantId,
            Status = BranchStatus.Active,
            CountryCode = profile.CountryCode,
            TimeZoneId = profile.TimeZoneId,
            CurrencyCode = profile.CurrencyCode
        };

        var betaBranch = new Branch
        {
            Name = "Beta Branch",
            RestaurantId = restaurant.RestaurantId,
            Status = BranchStatus.Inactive,
            CountryCode = profile.CountryCode,
            TimeZoneId = profile.TimeZoneId,
            CurrencyCode = profile.CurrencyCode
        };

        var gammaBranch = new Branch
        {
            Name = "Gamma Branch",
            RestaurantId = restaurant.RestaurantId,
            Status = BranchStatus.Inactive,
            CountryCode = profile.CountryCode,
            TimeZoneId = profile.TimeZoneId,
            CurrencyCode = profile.CurrencyCode
        };

            var user = new User
            {
                RestaurantId = restaurant.RestaurantId,
                BranchId = alphaBranch.BranchId,
                FullName = $"{roleName} User",
                Status = UserStatus.Active
            };
            user.SetMobileNumber(profile.CountryCode, roleName == "Cashier" ? "90000009" : "90000010");

            var hasher = new PasswordHasher<User>();
            user.PasswordHash = hasher.HashPassword(user, "Passw0rd!Passw0rd!");

            var assignedRole = await context.Roles.SingleAsync(role => role.RestaurantId == null && role.Name == roleName);

            context.Restaurants.Add(restaurant);
            context.Branches.AddRange(alphaBranch, deltaBranch, betaBranch, gammaBranch);
            context.Users.Add(user);
            context.UserRoles.Add(new UserRole
            {
                UserId = user.UserId,
                RoleId = assignedRole.RoleId,
                AssignedAt = DateTimeOffset.UtcNow
            });

            await context.SaveChangesAsync();

            return new SeedResult(
                restaurant.RestaurantId,
                alphaBranch.BranchId,
                betaBranch.BranchId,
                user.UserId,
                restaurant.NormalizedRestaurantCode,
                user.MobileNumber,
                "Passw0rd!Passw0rd!",
                user.FullName);
        }

        public async Task<SeedResult> SeedCustomUserAsync(string roleName, params string[] permissionCodes)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            await new FoundationSeedService(context).SeedAsync();

            var restaurant = new Restaurant
            {
                Name = $"{roleName} Restaurant",
                Status = RestaurantStatus.Active
            };
            restaurant.SetRestaurantCode($"RESTC{roleName[..1].ToUpperInvariant()}01");
            restaurant.SetCountryProfile("SG");
            var profile = BillSoft.Domain.Localization.CountryProfileCatalog.GetRequired("SG");

            var alphaBranch = new Branch
            {
                Name = "Alpha Branch",
                RestaurantId = restaurant.RestaurantId,
                Status = BranchStatus.Active,
                CountryCode = profile.CountryCode,
                TimeZoneId = profile.TimeZoneId,
                CurrencyCode = profile.CurrencyCode
            };

            var betaBranch = new Branch
            {
                Name = "Beta Branch",
                RestaurantId = restaurant.RestaurantId,
                Status = BranchStatus.Inactive,
                CountryCode = profile.CountryCode,
                TimeZoneId = profile.TimeZoneId,
                CurrencyCode = profile.CurrencyCode
            };

            var role = new Role
            {
                RestaurantId = restaurant.RestaurantId,
                Name = roleName,
                Description = $"{roleName} role",
                IsSystemRole = false
            };

            var user = new User
            {
                RestaurantId = restaurant.RestaurantId,
                BranchId = alphaBranch.BranchId,
                FullName = $"{roleName} User",
                Status = UserStatus.Active
            };
            user.SetMobileNumber(profile.CountryCode, "90000011");

            var hasher = new PasswordHasher<User>();
            user.PasswordHash = hasher.HashPassword(user, "Passw0rd!Passw0rd!");

            context.Restaurants.Add(restaurant);
            context.Branches.AddRange(alphaBranch, betaBranch);
            context.Roles.Add(role);

            foreach (var permissionCode in permissionCodes)
            {
                var permission = await context.Permissions.SingleAsync(entity => entity.Code == permissionCode);
                context.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.RoleId,
                    PermissionId = permission.PermissionId,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }

            context.Users.Add(user);
            context.UserRoles.Add(new UserRole
            {
                UserId = user.UserId,
                RoleId = role.RoleId,
                AssignedAt = DateTimeOffset.UtcNow
            });

            await context.SaveChangesAsync();

            return new SeedResult(
                restaurant.RestaurantId,
                alphaBranch.BranchId,
                betaBranch.BranchId,
                user.UserId,
                restaurant.NormalizedRestaurantCode,
                user.MobileNumber,
                "Passw0rd!Passw0rd!",
                user.FullName);
        }

        public async Task<Branch> SeedForeignBranchAsync()
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            var restaurant = new Restaurant
            {
                Name = "Foreign Restaurant",
                Status = RestaurantStatus.Active
            };
            restaurant.SetRestaurantCode("RESTZ01");
            restaurant.SetCountryProfile("SG");

            var branch = new Branch
            {
                Name = "Foreign Branch",
                RestaurantId = restaurant.RestaurantId,
                Status = BranchStatus.Active,
                CountryCode = "SG",
                Timezone = "Asia/Singapore",
                Currency = "SGD"
            };

            context.Restaurants.Add(restaurant);
            context.Branches.Add(branch);
            await context.SaveChangesAsync();

            return branch;
        }

        public async Task<string> AuthenticateAsync(SeedResult seed)
        {
            var payload = await LoginAsync(seed.RestaurantCode, seed.MobileNumber, seed.Password);

            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", payload.AccessToken);
            return payload.AccessToken;
        }

        public async Task<AuthLoginResponseDto> LoginAsync(string restaurantCode, string mobileNumber, string password)
        {
            var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                restaurantCode,
                mobileNumber,
                password
            });

            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<AuthLoginResponseDto>();
            Assert.NotNull(payload);

            return payload!;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Provider"] = "SqlServer",
                    ["Database:ConnectionString"] = "Server=(localdb)\\MSSQLLocalDB;Database=BillSoft;Trusted_Connection=True;TrustServerCertificate=True;",
                    ["Jwt:Issuer"] = "BillSoft",
                    ["Jwt:Audience"] = "BillSoft",
                    ["Jwt:SigningKey"] = "unit-test-signing-key-unit-test-signing-key",
                    ["Jwt:AccessTokenLifetimeMinutes"] = "15",
                    ["Jwt:RefreshTokenLifetimeDays"] = "7"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<BillSoftDbContext>>();
                services.RemoveAll<BillSoftDbContext>();
                services.AddDbContext<BillSoftDbContext>(options => options.UseSqlite(_connection));
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _client?.Dispose();
                _connection.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed record SeedResult(
        Guid RestaurantId,
        Guid ActiveBranchId,
        Guid InactiveBranchId,
        Guid UserId,
        string RestaurantCode,
        string MobileNumber,
        string Password,
        string FullName);

    private sealed record AuthLoginResponseDto(string AccessToken);

    private sealed record BranchListResponseDto(BranchListItemDto[] Items);

    private sealed record BranchListItemDto(
        Guid BranchId,
        Guid RestaurantId,
        string CountryCode,
        string Name,
        string? Address,
        string? Phone,
        string Timezone,
        string Currency,
        string Status);

    private sealed record BranchDetailDto(
        Guid BranchId,
        Guid RestaurantId,
        string CountryCode,
        string Name,
        string? Address,
        string? Phone,
        string Timezone,
        string Currency,
        string Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);

    private sealed record ProblemDetailsDto(string? Type, string? Title, int? Status, string? Detail, string? Instance);
}
