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

public sealed class DemoLoginSeedServiceTests
{
    [Fact]
    public async Task Seed_Should_Create_Demo_Restaurant_Branch_User_And_Role_Assignment()
    {
        await using var fixture = await DemoLoginSeedApiFactory.CreateAsync();

        var result = await fixture.SeedDemoAsync();

        Assert.Equal("DEMO", result.RestaurantCode);
        Assert.True(result.RestaurantCreated);
        Assert.True(result.BranchCreated);
        Assert.True(result.UserCreated);
        Assert.True(result.RoleAssignmentCreated);

        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

        var restaurant = await context.Restaurants.SingleAsync(entity => entity.NormalizedRestaurantCode == "DEMO");
        var branch = await context.Branches.SingleAsync(entity => entity.RestaurantId == restaurant.RestaurantId && entity.Name == "Main Branch");
        var user = await context.Users.SingleAsync(entity => entity.RestaurantId == restaurant.RestaurantId && entity.MobileNumber == "9123456789");

        Assert.Equal(RestaurantStatus.Active, restaurant.Status);
        Assert.Equal(BranchStatus.Active, branch.Status);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal("Demo Owner", user.FullName);
        Assert.Equal("owner@demo.billsoft.local", user.Email);
        Assert.False(string.IsNullOrWhiteSpace(user.PasswordHash));

        Assert.Equal(2, await context.MenuCategories.CountAsync(entity => entity.RestaurantId == restaurant.RestaurantId));
        Assert.Equal(3, await context.MenuItems.CountAsync(entity => entity.RestaurantId == restaurant.RestaurantId));

        var demoCategoryNames = await context.MenuCategories
            .Where(entity => entity.RestaurantId == restaurant.RestaurantId)
            .Select(entity => entity.Name)
            .ToArrayAsync();

        Assert.Contains("Breakfast", demoCategoryNames);
        Assert.Contains("Sides", demoCategoryNames);

        var demoItemNames = await context.MenuItems
            .Where(entity => entity.RestaurantId == restaurant.RestaurantId)
            .Select(entity => entity.Name)
            .ToArrayAsync();

        Assert.Contains("Masala Dosa", demoItemNames);
        Assert.Contains("Parcel Snack", demoItemNames);
        Assert.Contains("Eat-In Special", demoItemNames);

        var assignedRole = await (
                from userRole in context.UserRoles.AsNoTracking()
                join role in context.Roles.AsNoTracking() on userRole.RoleId equals role.RoleId
                where userRole.UserId == user.UserId
                select role.Name)
            .SingleAsync();

        Assert.Equal("RestaurantOwner", assignedRole);
    }

    [Fact]
    public async Task Seed_Should_Create_Inventory_QA_User_With_Vendor_Bill_Permissions()
    {
        await using var fixture = await DemoLoginSeedApiFactory.CreateAsync();

        await fixture.SeedDemoAsync();

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            restaurantCode = "DEMO",
            mobileNumber = DemoLoginSeedData.InventoryQaMobileNumber,
            password = DemoLoginSeedData.InventoryQaPassword
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

        Assert.NotNull(payload);
        Assert.Equal("Demo Inventory", payload!.FullName);
        Assert.Equal(DemoLoginSeedData.InventoryQaMobileNumber, payload.MobileNumber);
        Assert.Contains("InventoryUser", payload.Roles);
        Assert.Contains(SystemPermissions.VendorBillUpload, payload.Permissions);
        Assert.Contains(SystemPermissions.VendorBillConfirm, payload.Permissions);
        Assert.Equal("InventoryUser", payload.ActiveRole);
    }

    [Fact]
    public async Task Seed_Should_Be_Idempotent()
    {
        await using var fixture = await DemoLoginSeedApiFactory.CreateAsync();

        await fixture.SeedDemoAsync();
        await fixture.SeedDemoAsync();

        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var restaurantId = await context.Restaurants
            .Where(entity => entity.NormalizedRestaurantCode == "DEMO")
            .Select(entity => entity.RestaurantId)
            .SingleAsync();
        var ownerRoleId = await context.Roles
            .Where(entity => entity.RestaurantId == null && entity.Name == "RestaurantOwner")
            .Select(entity => entity.RoleId)
            .SingleAsync();
        var inventoryRoleId = await context.Roles
            .Where(entity => entity.RestaurantId == null && entity.Name == "InventoryUser")
            .Select(entity => entity.RoleId)
            .SingleAsync();

        Assert.Equal(1, await context.Branches.CountAsync(entity => entity.RestaurantId == restaurantId));
        Assert.Equal(2, await context.Users.CountAsync(entity => entity.RestaurantId == restaurantId));
        Assert.Equal(1, await context.Users.CountAsync(entity => entity.RestaurantId == restaurantId && entity.MobileNumber == "9123456789"));
        Assert.Equal(1, await context.Users.CountAsync(entity => entity.RestaurantId == restaurantId && entity.MobileNumber == DemoLoginSeedData.InventoryQaMobileNumber));
        Assert.Equal(1, await context.UserRoles.CountAsync(entity => entity.RoleId == ownerRoleId));
        Assert.Equal(1, await context.UserRoles.CountAsync(entity => entity.RoleId == inventoryRoleId));
        Assert.Equal(2, await context.MenuCategories.CountAsync(entity => entity.RestaurantId == restaurantId));
        Assert.Equal(3, await context.MenuItems.CountAsync(entity => entity.RestaurantId == restaurantId));
    }

    [Fact]
    public async Task Seed_Should_Reuse_Existing_Demo_User_When_Email_Remains_Canonical()
    {
        await using var fixture = await DemoLoginSeedApiFactory.CreateAsync();

        await fixture.SeedDemoAsync();

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            var user = await context.Users.SingleAsync(entity => entity.Email == DemoLoginSeedData.Email);
            user.MobileCountryCode = "IN";
            user.MobileNumber = "9000000000";
            user.MarkUpdated();
            await context.SaveChangesAsync();
        }

        await fixture.SeedDemoAsync();

        await using var verificationScope = fixture.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var restaurantId = await verificationContext.Restaurants
            .Where(entity => entity.NormalizedRestaurantCode == "DEMO")
            .Select(entity => entity.RestaurantId)
            .SingleAsync();
        var seededUser = await verificationContext.Users.SingleAsync(entity =>
            entity.RestaurantId == restaurantId &&
            entity.Email == DemoLoginSeedData.Email);

        Assert.Equal(DemoLoginSeedData.MobileNumber, seededUser.MobileNumber);
        Assert.Equal(DemoLoginSeedData.MobileE164, seededUser.MobileE164);
        Assert.Equal(1, await verificationContext.Users.CountAsync(entity => entity.RestaurantId == seededUser.RestaurantId && entity.Email == DemoLoginSeedData.Email));
    }

    [Fact]
    public async Task Seeded_Demo_Menu_Should_Be_Readable_And_Usable_For_Pos_Orders()
    {
        await using var fixture = await DemoLoginSeedApiFactory.CreateAsync();

        await fixture.SeedDemoAsync();
        await fixture.AuthenticateAsync(await fixture.LoginAsync(
            DemoLoginSeedData.RestaurantCode,
            DemoLoginSeedData.MobileNumber,
            DemoLoginSeedData.Password));

        var categoriesResponse = await fixture.Client.GetAsync("/api/v1/admin/menu/categories");
        categoriesResponse.EnsureSuccessStatusCode();
        var categoriesPayload = await categoriesResponse.Content.ReadFromJsonAsync<MenuCategoryListResponseDto>();
        Assert.NotNull(categoriesPayload);
        Assert.Equal(2, categoriesPayload!.Items.Length);
        Assert.Contains(categoriesPayload.Items, item => item.Name == "Breakfast" && item.Status == "Active");
        Assert.Contains(categoriesPayload.Items, item => item.Name == "Sides" && item.Status == "Active");

        var itemsResponse = await fixture.Client.GetAsync("/api/v1/admin/menu/items");
        itemsResponse.EnsureSuccessStatusCode();
        var itemsPayload = await itemsResponse.Content.ReadFromJsonAsync<MenuItemListResponseDto>();
        Assert.NotNull(itemsPayload);
        Assert.Equal(3, itemsPayload!.Items.Length);
        Assert.Contains(itemsPayload.Items, item =>
            item.Name == "Masala Dosa" &&
            item.Status == "Active" &&
            item.IsAvailableForEatIn &&
            item.IsAvailableForParcel);
        Assert.Contains(itemsPayload.Items, item =>
            item.Name == "Parcel Snack" &&
            item.Status == "Active" &&
            !item.IsAvailableForEatIn &&
            item.IsAvailableForParcel);
        Assert.Contains(itemsPayload.Items, item =>
            item.Name == "Eat-In Special" &&
            item.Status == "Active" &&
            item.IsAvailableForEatIn &&
            !item.IsAvailableForParcel);

        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var restaurant = await context.Restaurants.SingleAsync(entity => entity.NormalizedRestaurantCode == DemoLoginSeedData.RestaurantCode);
        var branch = await context.Branches.SingleAsync(entity => entity.RestaurantId == restaurant.RestaurantId && entity.Name == DemoLoginSeedData.BranchName);
        var masalaDosa = await context.MenuItems.SingleAsync(entity => entity.RestaurantId == restaurant.RestaurantId && entity.Name == "Masala Dosa");

        var orderResponse = await fixture.Client.PostAsJsonAsync("/api/v1/pos/orders", new
        {
            branchId = branch.BranchId,
            orderType = "EatIn",
            tableName = "T1",
            customerName = "Walk-in",
            lines = new[]
            {
                new
                {
                    menuItemId = masalaDosa.MenuItemId,
                    quantity = 2m,
                    notes = "Less spicy"
                }
            }
        });

        orderResponse.EnsureSuccessStatusCode();

        var createdOrder = await context.PosOrders.AsNoTracking()
            .SingleAsync(entity => entity.RestaurantId == restaurant.RestaurantId && entity.BranchId == branch.BranchId);
        var createdLines = await context.PosOrderLines.AsNoTracking()
            .Where(entity => entity.PosOrderId == createdOrder.PosOrderId)
            .ToListAsync();

        Assert.Single(createdLines);
        Assert.Equal("Masala Dosa", createdLines[0].MenuItemNameSnapshot);
        Assert.Equal("Breakfast", createdLines[0].MenuCategoryNameSnapshot);
        Assert.Equal("DOSA-01", createdLines[0].SkuSnapshot);
    }

    [Fact]
    public async Task Seeded_User_Should_Authenticate_With_Login_Endpoint()
    {
        await using var fixture = await DemoLoginSeedApiFactory.CreateAsync();

        await fixture.SeedDemoAsync();

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            restaurantCode = "DEMO",
            mobileNumber = DemoLoginSeedData.MobileNumber,
            password = "DemoOwner123!"
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

        Assert.NotNull(payload);
        Assert.Equal("DEMO", payload!.RestaurantCode);
        Assert.Equal("Demo Owner", payload.FullName);
        Assert.Equal(DemoLoginSeedData.MobileNumber, payload.MobileNumber);
        Assert.Contains("RestaurantOwner", payload.Roles);
        Assert.Contains(SystemPermissions.UserManage, payload.Permissions);
        Assert.Equal("RestaurantOwner", payload.ActiveRole);
    }

    [Fact]
    public async Task Normal_Startup_Should_Not_Auto_Seed_Demo_Data()
    {
        await using var fixture = await DemoLoginSeedApiFactory.CreateAsync();

        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

        Assert.False(await context.Restaurants.AnyAsync(entity => entity.NormalizedRestaurantCode == "DEMO"));
    }

    private sealed class DemoLoginSeedApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private HttpClient? _client;

        public HttpClient Client => _client ??= CreateClient();

        public static async Task<DemoLoginSeedApiFactory> CreateAsync()
        {
            var factory = new DemoLoginSeedApiFactory();
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

        public async Task<DemoLoginSeedResult> SeedDemoAsync()
        {
            using var scope = Services.CreateScope();
            var seedService = scope.ServiceProvider.GetRequiredService<IDemoLoginSeedService>();
            return await seedService.SeedAsync();
        }

        public Task<string> AuthenticateAsync(string accessToken)
        {
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return Task.FromResult(accessToken);
        }

        public async Task<string> AuthenticateAsync(AuthResponseDto payload)
        {
            return await AuthenticateAsync(payload.AccessToken);
        }

        public async Task<AuthResponseDto> LoginAsync(string restaurantCode, string mobileNumber, string password)
        {
            var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                restaurantCode,
                mobileNumber,
                password
            });

            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
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

    private sealed record AuthResponseDto(
        string AccessToken,
        string RefreshToken,
        DateTimeOffset AccessTokenExpiresAtUtc,
        DateTimeOffset RefreshTokenExpiresAtUtc,
        Guid UserId,
        Guid RestaurantId,
        string RestaurantCode,
        Guid? BranchId,
        string FullName,
        string MobileNumber,
        string[] Roles,
        string[] Permissions,
        string ActiveRole);

    private sealed record MenuCategoryListResponseDto(MenuCategoryDto[] Items);

    private sealed record MenuCategoryDto(
        Guid MenuCategoryId,
        Guid RestaurantId,
        string Name,
        int DisplayOrder,
        string Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);

    private sealed record MenuItemListResponseDto(MenuItemDto[] Items);

    private sealed record MenuItemDto(
        Guid MenuItemId,
        Guid RestaurantId,
        Guid MenuCategoryId,
        string CategoryName,
        string Name,
        string? Description,
        string? Sku,
        decimal BasePrice,
        decimal TaxRate,
        bool IsVegetarian,
        bool IsAvailableForEatIn,
        bool IsAvailableForParcel,
        string Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);
}
