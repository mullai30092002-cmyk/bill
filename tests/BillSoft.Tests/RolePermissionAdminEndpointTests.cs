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

public sealed class RolePermissionAdminEndpointTests
{
    [Fact]
    public async Task Role_List_Should_Return_401_When_Unauthenticated()
    {
        await using var fixture = await RolePermissionApiFactory.CreateAsync();

        var response = await fixture.Client.GetAsync("/api/v1/admin/roles");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Role_List_Should_Return_403_When_User_Lacks_Role_And_User_Manage()
    {
        await using var fixture = await RolePermissionApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Cashier");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.GetAsync("/api/v1/admin/roles");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Role_List_Should_Succeed_For_User_Manage()
    {
        await using var fixture = await RolePermissionApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        var customRole = await fixture.SeedCustomRoleAsync(seed, "FrontDesk", "Bill.Create");
        var foreignRole = await fixture.SeedForeignRoleAsync();
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.GetAsync("/api/v1/admin/roles");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<RoleListResponseDto>();
        Assert.NotNull(payload);

        Assert.Contains(payload!.Items, item => item.Name == "SuperAdmin" && item.IsSystemRole);
        Assert.Contains(payload.Items, item => item.Name == "Cashier" && item.IsSystemRole);
        Assert.Contains(payload.Items, item => item.Name == customRole.RoleName && item.RestaurantId == seed.RestaurantId);
        Assert.DoesNotContain(payload.Items, item => item.Name == foreignRole.RoleName);

        var customRoleItem = payload.Items.Single(item => item.Name == customRole.RoleName);
        Assert.Contains("Bill.Create", customRoleItem.PermissionCodes);
    }

    [Fact]
    public async Task Role_List_Should_Succeed_For_Role_Manage()
    {
        await using var fixture = await RolePermissionApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("RestaurantOwner");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.GetAsync("/api/v1/admin/roles");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Role_List_Should_Mark_SuperAdmin_As_Not_Assignable_For_Non_SuperAdmin()
    {
        await using var fixture = await RolePermissionApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.GetAsync("/api/v1/admin/roles");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<RoleListResponseDto>();
        var superAdmin = payload!.Items.Single(item => item.Name == "SuperAdmin");

        Assert.False(superAdmin.IsAssignable);
        Assert.False(string.IsNullOrWhiteSpace(superAdmin.AssignmentBlockedReason));
    }

    [Fact]
    public async Task Role_List_Should_Mark_SuperAdmin_As_Assignable_For_SuperAdmin()
    {
        await using var fixture = await RolePermissionApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("SuperAdmin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.GetAsync("/api/v1/admin/roles");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<RoleListResponseDto>();
        var superAdmin = payload!.Items.Single(item => item.Name == "SuperAdmin");

        Assert.True(superAdmin.IsAssignable);
        Assert.Null(superAdmin.AssignmentBlockedReason);
    }

    [Fact]
    public async Task Role_Detail_Should_Return_404_For_Other_Restaurant_Role()
    {
        await using var fixture = await RolePermissionApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        var foreignRole = await fixture.SeedForeignRoleAsync();
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.GetAsync($"/api/v1/admin/roles/{foreignRole.RoleId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Permission_Catalog_Should_Return_403_When_User_Lacks_Permission_View_And_Role_Manage()
    {
        await using var fixture = await RolePermissionApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Cashier");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.GetAsync("/api/v1/admin/permissions");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Permission_Catalog_Should_Succeed_For_Role_Manage()
    {
        await using var fixture = await RolePermissionApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("RestaurantOwner");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.GetAsync("/api/v1/admin/permissions");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<PermissionCatalogResponseDto>();
        Assert.NotNull(payload);

        Assert.NotEmpty(payload!.Modules);
    }

    [Fact]
    public async Task Permission_Catalog_Should_Succeed_For_Permission_View()
    {
        await using var fixture = await RolePermissionApiFactory.CreateAsync();
        var seed = await fixture.SeedPermissionViewUserAsync();
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.GetAsync("/api/v1/admin/permissions");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Permission_Catalog_Should_Be_Grouped_And_Sorted()
    {
        await using var fixture = await RolePermissionApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("RestaurantOwner");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.GetAsync("/api/v1/admin/permissions");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<PermissionCatalogResponseDto>();
        Assert.NotNull(payload);

        var modules = payload!.Modules.ToArray();
        var moduleNames = modules.Select(module => module.Module).ToArray();
        Assert.Equal(moduleNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase), moduleNames);

        foreach (var module in modules)
        {
            var codes = module.Permissions.Select(permission => permission.Code).ToArray();
            Assert.Equal(codes.OrderBy(code => code, StringComparer.OrdinalIgnoreCase), codes);
        }
    }

    [Theory]
    [InlineData("POST", "/api/v1/admin/roles")]
    [InlineData("PUT", "/api/v1/admin/roles")]
    public async Task Role_Mutation_Routes_Should_Not_Exist_For_Collection(string method, string path)
    {
        await using var fixture = await RolePermissionApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"Expected 404 or 405, got {(int)response.StatusCode}.");
    }

    [Theory]
    [InlineData("DELETE")]
    [InlineData("PUT")]
    public async Task Role_Mutation_Routes_Should_Not_Exist_For_Item(string method)
    {
        await using var fixture = await RolePermissionApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        var foreignRole = await fixture.SeedForeignRoleAsync();
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), $"/api/v1/admin/roles/{foreignRole.RoleId}"));

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"Expected 404 or 405, got {(int)response.StatusCode}.");
    }

    [Fact]
    public void Permission_Constants_Should_Exist_In_SystemPermissions_Definitions()
    {
        var codes = SystemPermissions.Definitions.Select(definition => definition.Code).ToArray();

        Assert.Contains(SystemPermissions.UserManage, codes);
        Assert.Contains(SystemPermissions.RoleManage, codes);
        Assert.Contains(SystemPermissions.PermissionView, codes);
    }

    private sealed class RolePermissionApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private HttpClient? _client;

        public HttpClient Client => _client ??= CreateClient();

        public static async Task<RolePermissionApiFactory> CreateAsync()
        {
            var factory = new RolePermissionApiFactory();
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

            var branch = new Branch
            {
                Name = "Main Branch",
                RestaurantId = restaurant.RestaurantId,
                Status = BranchStatus.Active,
                Timezone = "Asia/Singapore",
                Currency = "SGD"
            };

            var user = new User
            {
                RestaurantId = restaurant.RestaurantId,
                BranchId = branch.BranchId,
                FullName = $"{roleName} User",
                MobileNumber = roleName == "Cashier" ? "90000031" : "90000032",
                Status = UserStatus.Active
            };

            var hasher = new PasswordHasher<User>();
            user.PasswordHash = hasher.HashPassword(user, "Passw0rd!Passw0rd!");

            var assignedRole = await context.Roles.SingleAsync(role => role.RestaurantId == null && role.Name == roleName);

            context.Restaurants.Add(restaurant);
            context.Branches.Add(branch);
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
                branch.BranchId,
                user.UserId,
                restaurant.NormalizedRestaurantCode,
                user.MobileNumber,
                "Passw0rd!Passw0rd!",
                user.FullName,
                roleName,
                Guid.Empty,
                Guid.Empty);
        }

        public async Task<SeedResult> SeedCustomRoleAsync(SeedResult restaurantSeed, string roleName, string permissionCode)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            var restaurant = await context.Restaurants.SingleAsync(entity => entity.RestaurantId == restaurantSeed.RestaurantId);
            var permission = await context.Permissions.SingleAsync(entity => entity.Code == permissionCode);

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
                BranchId = restaurantSeed.BranchId,
                FullName = $"{roleName} User",
                MobileNumber = "90000033",
                Status = UserStatus.Active
            };

            var hasher = new PasswordHasher<User>();
            user.PasswordHash = hasher.HashPassword(user, "Passw0rd!Passw0rd!");

            context.Roles.Add(role);
            context.RolePermissions.Add(new RolePermission
            {
                RoleId = role.RoleId,
                PermissionId = permission.PermissionId,
                CreatedAt = DateTimeOffset.UtcNow
            });
            context.Users.Add(user);
            context.UserRoles.Add(new UserRole
            {
                UserId = user.UserId,
                RoleId = role.RoleId,
                AssignedAt = DateTimeOffset.UtcNow
            });

            await context.SaveChangesAsync();

            return restaurantSeed with
            {
                UserId = user.UserId,
                MobileNumber = user.MobileNumber,
                Password = "Passw0rd!Passw0rd!",
                FullName = user.FullName,
                RoleName = role.Name,
                RoleId = role.RoleId,
                ForeignRestaurantId = Guid.Empty
            };
        }

        public async Task<SeedResult> SeedForeignRoleAsync()
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            var restaurant = new Restaurant
            {
                Name = "Foreign Restaurant",
                Status = RestaurantStatus.Active
            };
            restaurant.SetRestaurantCode("RESTZ01");

            var role = new Role
            {
                RestaurantId = restaurant.RestaurantId,
                Name = "ForeignRole",
                Description = "Foreign role",
                IsSystemRole = false
            };

            context.Restaurants.Add(restaurant);
            context.Roles.Add(role);
            await context.SaveChangesAsync();

            return new SeedResult(
                restaurant.RestaurantId,
                Guid.Empty,
                Guid.Empty,
                restaurant.NormalizedRestaurantCode,
                string.Empty,
                string.Empty,
                string.Empty,
                role.Name,
                role.RoleId,
                restaurant.RestaurantId);
        }

        public async Task<SeedResult> SeedPermissionViewUserAsync()
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            await new FoundationSeedService(context).SeedAsync();

            var restaurant = new Restaurant
            {
                Name = "Permission Viewer Restaurant",
                Status = RestaurantStatus.Active
            };
            restaurant.SetRestaurantCode("RESTPV01");
            restaurant.SetCountryProfile("SG");
            var profile = BillSoft.Domain.Localization.CountryProfileCatalog.GetRequired("SG");

            var branch = new Branch
            {
                Name = "Main Branch",
                RestaurantId = restaurant.RestaurantId,
                Status = BranchStatus.Active,
                CountryCode = profile.CountryCode,
                TimeZoneId = profile.TimeZoneId,
                CurrencyCode = profile.CurrencyCode
            };

            var role = await context.Roles.SingleAsync(entity => entity.RestaurantId == null && entity.Name == "Cashier");
            var permission = await context.Permissions.SingleAsync(entity => entity.Code == SystemPermissions.PermissionView);
            var user = new User
            {
                RestaurantId = restaurant.RestaurantId,
                BranchId = branch.BranchId,
                FullName = "Permission Viewer User",
                Status = UserStatus.Active
            };
            user.SetMobileNumber(profile.CountryCode, "90000034");

            var hasher = new PasswordHasher<User>();
            user.PasswordHash = hasher.HashPassword(user, "Passw0rd!Passw0rd!");

            context.Restaurants.Add(restaurant);
            context.Branches.Add(branch);
            context.RolePermissions.Add(new RolePermission
            {
                RoleId = role.RoleId,
                PermissionId = permission.PermissionId,
                CreatedAt = DateTimeOffset.UtcNow
            });
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
                branch.BranchId,
                user.UserId,
                restaurant.NormalizedRestaurantCode,
                user.MobileNumber,
                "Passw0rd!Passw0rd!",
                user.FullName,
                role.Name,
                role.RoleId,
                Guid.Empty);
        }

        public async Task<string> AuthenticateAsync(SeedResult seed)
        {
            var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                restaurantCode = seed.RestaurantCode,
                mobileNumber = seed.MobileNumber,
                password = seed.Password
            });

            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<AuthLoginResponseDto>();
            Assert.NotNull(payload);

            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", payload!.AccessToken);
            return payload.AccessToken;
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
        Guid BranchId,
        Guid UserId,
        string RestaurantCode,
        string MobileNumber,
        string Password,
        string FullName,
        string RoleName,
        Guid RoleId,
        Guid ForeignRestaurantId);

    private sealed record AuthLoginResponseDto(string AccessToken);

    private sealed record RoleListResponseDto(RoleListItemDto[] Items);

    private sealed record RoleListItemDto(
        Guid RoleId,
        Guid? RestaurantId,
        string Name,
        string? Description,
        bool IsSystemRole,
        bool IsAssignable,
        string? AssignmentBlockedReason,
        string[] PermissionCodes);

    private sealed record PermissionCatalogResponseDto(PermissionModuleGroupDto[] Modules);

    private sealed record PermissionModuleGroupDto(string Module, PermissionListItemDto[] Permissions);

    private sealed record PermissionListItemDto(Guid PermissionId, string Code, string? Description, string Module);
}
