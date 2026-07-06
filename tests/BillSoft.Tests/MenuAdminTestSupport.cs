using System.Collections.Concurrent;
using System.Data.Common;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BillSoft.Domain.Inventory;
using BillSoft.Domain.Menu;
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
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace BillSoft.Tests;

internal sealed class MenuAdminApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly SqlCommandCaptureInterceptor _sqlCapture = new();
    private HttpClient? _client;

    public HttpClient Client => _client ??= CreateClient();

    public SqlCommandCaptureInterceptor SqlCapture => _sqlCapture;

    public static async Task<MenuAdminApiFactory> CreateAsync()
    {
        var factory = new MenuAdminApiFactory();
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

        var user = new User
        {
            RestaurantId = restaurant.RestaurantId,
            BranchId = alphaBranch.BranchId,
            FullName = $"{roleName} User",
            Status = UserStatus.Active
        };
        user.SetMobileNumber(profile.CountryCode, roleName == "Cashier" ? "90000012" : "90000013");

        var hasher = new PasswordHasher<User>();
        user.PasswordHash = hasher.HashPassword(user, "Passw0rd!Passw0rd!");

        var assignedRole = await context.Roles.SingleAsync(role => role.RestaurantId == null && role.Name == roleName);

        context.Restaurants.Add(restaurant);
        context.Branches.Add(alphaBranch);
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
            user.UserId,
            restaurant.NormalizedRestaurantCode,
            user.MobileNumber,
            "Passw0rd!Passw0rd!",
            user.FullName);
    }

    public async Task<SeedResult> SeedForeignSystemUserAsync(string roleName)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

        await new FoundationSeedService(context).SeedAsync();

        var restaurant = new Restaurant
        {
            Name = $"Foreign {roleName} Restaurant",
            Status = RestaurantStatus.Active
        };
        restaurant.SetRestaurantCode($"RESTZ{roleName[..1].ToUpperInvariant()}01");
        restaurant.SetCountryProfile("SG");
        var profile = BillSoft.Domain.Localization.CountryProfileCatalog.GetRequired("SG");

        var branch = new Branch
        {
            Name = "Foreign Branch",
            RestaurantId = restaurant.RestaurantId,
            Status = BranchStatus.Active,
            CountryCode = profile.CountryCode,
            Timezone = "Asia/Singapore",
            Currency = "SGD"
        };

        var user = new User
        {
            RestaurantId = restaurant.RestaurantId,
            BranchId = branch.BranchId,
            FullName = $"Foreign {roleName} User",
            Status = UserStatus.Active
        };
        user.SetMobileNumber(profile.CountryCode, "90000014");

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
            user.FullName);
    }

    public async Task<SeedResult> SeedUserInRestaurantAsync(Guid restaurantId, Guid branchId, string roleName, string mobileNumber)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

        var restaurant = await context.Restaurants.AsNoTracking()
            .SingleAsync(entity => entity.RestaurantId == restaurantId);

        var user = new User
        {
            RestaurantId = restaurantId,
            BranchId = branchId,
            FullName = $"{roleName} User",
            MobileNumber = mobileNumber,
            Status = UserStatus.Active
        };

        var hasher = new PasswordHasher<User>();
        user.PasswordHash = hasher.HashPassword(user, "Passw0rd!Passw0rd!");

        var assignedRole = await context.Roles.SingleAsync(role => role.RestaurantId == null && role.Name == roleName);

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
            branchId,
            user.UserId,
            restaurant.NormalizedRestaurantCode,
            user.MobileNumber,
            "Passw0rd!Passw0rd!",
            user.FullName);
    }

    public async Task<Restaurant> SeedForeignRestaurantAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

        var restaurant = new Restaurant
        {
            Name = "Foreign Restaurant",
            Status = RestaurantStatus.Active
        };
        restaurant.SetRestaurantCode("RESTFOREIGN01");
        restaurant.SetCountryProfile("SG");

        context.Restaurants.Add(restaurant);
        await context.SaveChangesAsync();

        return restaurant;
    }

    public async Task<MenuCategory> InsertCategoryAsync(
        Guid restaurantId,
        string name,
        int displayOrder = 0,
        MenuCategoryStatus status = MenuCategoryStatus.Active)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

        var category = new MenuCategory
        {
            RestaurantId = restaurantId,
            Name = name,
            DisplayOrder = displayOrder,
            Status = status
        };

        context.MenuCategories.Add(category);
        await context.SaveChangesAsync();
        return category;
    }

    public async Task<MenuItem> InsertItemAsync(
        Guid restaurantId,
        Guid categoryId,
        string name,
        decimal basePrice = 2.50m,
        decimal taxRate = 0,
        string? sku = null,
        bool isVegetarian = true,
        bool isAvailableForEatIn = true,
        bool isAvailableForParcel = true,
        MenuItemStatus status = MenuItemStatus.Active)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

        var item = new MenuItem
        {
            RestaurantId = restaurantId,
            MenuCategoryId = categoryId,
            Name = name,
            Sku = sku,
            BasePrice = basePrice,
            TaxRate = taxRate,
            IsVegetarian = isVegetarian,
            IsAvailableForEatIn = isAvailableForEatIn,
            IsAvailableForParcel = isAvailableForParcel,
            Status = status
        };

        context.MenuItems.Add(item);
        await context.SaveChangesAsync();
        return item;
    }

    public async Task<InventoryItem> InsertInventoryItemAsync(
        Guid restaurantId,
        Guid branchId,
        string name,
        string category,
        string unitOfMeasure,
        decimal lowStockThreshold,
        bool isActive)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var now = DateTimeOffset.UtcNow;

        var item = new InventoryItem
        {
            RestaurantId = restaurantId,
            BranchId = branchId,
            Name = name.Trim(),
            NormalizedName = name.Trim().ToUpperInvariant(),
            Category = category.Trim(),
            UnitOfMeasure = unitOfMeasure.Trim(),
            LowStockThreshold = lowStockThreshold,
            IsActive = isActive,
            CreatedAtUtc = now
        };

        context.InventoryItems.Add(item);
        await context.SaveChangesAsync();
        return item;
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
            services.AddDbContext<BillSoftDbContext>(options =>
            {
                options.UseSqlite(_connection);
                options.AddInterceptors(_sqlCapture);
            });
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

internal sealed class SqlCommandCaptureInterceptor : DbCommandInterceptor
{
    private readonly ConcurrentQueue<string> _commands = new();

    public IReadOnlyCollection<string> Commands => _commands.ToArray();

    public void Clear()
    {
        while (_commands.TryDequeue(out _))
        {
        }
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        _commands.Enqueue(command.CommandText);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        _commands.Enqueue(command.CommandText);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        _commands.Enqueue(command.CommandText);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        _commands.Enqueue(command.CommandText);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }
}

internal sealed record SeedResult(
    Guid RestaurantId,
    Guid BranchId,
    Guid UserId,
    string RestaurantCode,
    string MobileNumber,
    string Password,
    string FullName);

internal sealed record AuthLoginResponseDto(string AccessToken, string RefreshToken);

internal sealed record MenuCategoryListResponseDto(MenuCategoryDetailDto[] Items);

internal sealed record MenuCategoryDetailDto(
    Guid MenuCategoryId,
    Guid RestaurantId,
    string Name,
    int DisplayOrder,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

internal sealed record MenuItemListResponseDto(MenuItemDetailDto[] Items);

internal sealed record MenuItemDetailDto(
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
    string InventoryDeductionMode,
    Guid? StockInventoryItemId,
    string? StockInventoryItemName,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

internal sealed record MenuItemPriceHistoryResponseDto(MenuItemPriceHistoryItemDto[] Items);

internal sealed record MenuItemPriceHistoryItemDto(
    Guid MenuItemPriceHistoryId,
    Guid MenuItemId,
    decimal OldPrice,
    decimal NewPrice,
    Guid? ChangedByUserId,
    DateTimeOffset ChangedAt,
    string? Reason);

internal sealed record ProblemDetailsDto(string? Type, string? Title, int? Status, string? Detail, string? Instance);
