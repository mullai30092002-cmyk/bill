using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BillSoft.Domain.Cashiering;
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

public sealed class CashierShiftEndpointTests
{
    [Fact]
    public async Task Cashier_Can_Open_Shift_With_Business_Date_And_Opening_Cash()
    {
        await using var fixture = await CashierShiftApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/cashier/shifts/open", new
        {
            branchId = admin.ActiveBranchId,
            businessDate = "2026-06-13",
            openingCashAmount = 100m
        });

        response.EnsureSuccessStatusCode();
        var openedShift = await response.Content.ReadFromJsonAsync<CashierShiftDetailDto>();
        Assert.NotNull(openedShift);
        Assert.Equal("Open", openedShift!.Status);
        Assert.Equal(admin.ActiveBranchId, openedShift.BranchId);
        Assert.Equal(admin.UserId, openedShift.CashierUserId);
        Assert.Equal("Admin User", openedShift.CashierName);
        Assert.Equal("Alpha Branch", openedShift.BranchName);
        Assert.Equal(new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc), openedShift.BusinessDate);
        Assert.Equal(100m, openedShift.OpeningCashAmount);
        Assert.Equal(100m, openedShift.ExpectedClosingCashAmount);
        Assert.Null(openedShift.DeclaredClosingCashAmount);
        Assert.Null(openedShift.CashVarianceAmount);
        Assert.Null(openedShift.ClosedAtUtc);

        var currentResponse = await fixture.Client.GetAsync($"/api/v1/cashier/shifts/current?branchId={admin.ActiveBranchId}");
        currentResponse.EnsureSuccessStatusCode();
        var currentShift = await currentResponse.Content.ReadFromJsonAsync<CashierShiftDetailDto>();
        Assert.NotNull(currentShift);
        Assert.Equal(openedShift.CashierShiftId, currentShift!.CashierShiftId);

        var listResponse = await fixture.Client.GetAsync("/api/v1/cashier/shifts?businessDate=2026-06-13&branchId=" + admin.ActiveBranchId);
        listResponse.EnsureSuccessStatusCode();
        var listPayload = await listResponse.Content.ReadFromJsonAsync<CashierShiftListResponseDto>();
        Assert.NotNull(listPayload);
        Assert.Single(listPayload!.Items);
        Assert.Equal(openedShift.CashierShiftId, listPayload.Items[0].CashierShiftId);
        Assert.Equal(openedShift.BusinessDate, listPayload.Items[0].BusinessDate);

        await using var auditScope = fixture.Services.CreateAsyncScope();
        var auditContext = auditScope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var openAudit = await auditContext.AuditLogs.SingleAsync(entity => entity.Action == "CashierShift.Opened");
        Assert.Equal(admin.RestaurantId, openAudit.RestaurantId);
        Assert.Equal(admin.ActiveBranchId, openAudit.BranchId);
        Assert.Equal(admin.UserId, openAudit.UserId);
    }

    [Fact]
    public async Task Cashier_Current_Shift_Should_Return_Empty_Response_When_No_Open_Shift_Exists()
    {
        await using var fixture = await CashierShiftApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var response = await fixture.Client.GetAsync($"/api/v1/cashier/shifts/current?branchId={admin.ActiveBranchId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, response.Content.Headers.ContentLength ?? 0);
    }

    [Fact]
    public async Task Cashier_Current_Shift_Should_Not_Return_Another_Cashiers_Open_Shift()
    {
        await using var fixture = await CashierShiftApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

        var otherUser = new User
        {
            RestaurantId = admin.RestaurantId,
            BranchId = admin.ActiveBranchId,
            FullName = "Other Cashier",
            MobileNumber = "90000009",
            Status = UserStatus.Active
        };

        context.Users.Add(otherUser);
        context.CashierShifts.Add(new CashierShift
        {
            RestaurantId = admin.RestaurantId,
            BranchId = admin.ActiveBranchId,
            OpenedByUserId = otherUser.UserId,
            BusinessDate = new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc),
            Status = CashierShiftStatus.Open,
            OpenedAtUtc = DateTimeOffset.UtcNow,
            OpeningCashAmount = 50m,
            ExpectedCashAmount = 50m,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        await context.SaveChangesAsync();

        var response = await fixture.Client.GetAsync($"/api/v1/cashier/shifts/current?branchId={admin.ActiveBranchId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Cashier_Cannot_Open_Duplicate_Open_Shift_For_The_Same_Branch()
    {
        await using var fixture = await CashierShiftApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var firstResponse = await fixture.Client.PostAsJsonAsync("/api/v1/cashier/shifts/open", new
        {
            branchId = admin.ActiveBranchId,
            businessDate = "2026-06-13",
            openingCashAmount = 100m
        });
        firstResponse.EnsureSuccessStatusCode();

        var secondResponse = await fixture.Client.PostAsJsonAsync("/api/v1/cashier/shifts/open", new
        {
            branchId = admin.ActiveBranchId,
            businessDate = "2026-06-13",
            openingCashAmount = 25m
        });

        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
    }

    [Fact]
    public async Task Cashier_Can_Close_Open_Shift_And_Calculate_Variance()
    {
        await using var fixture = await CashierShiftApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var openResponse = await fixture.Client.PostAsJsonAsync("/api/v1/cashier/shifts/open", new
        {
            branchId = admin.ActiveBranchId,
            businessDate = "2026-06-13",
            openingCashAmount = 100m
        });
        openResponse.EnsureSuccessStatusCode();
        var openedShift = await openResponse.Content.ReadFromJsonAsync<CashierShiftDetailDto>();
        Assert.NotNull(openedShift);

        var closeResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/cashier/shifts/{openedShift!.CashierShiftId}/close", new
        {
            declaredClosingCashAmount = 130m,
            closeNotes = "End of day"
        });

        closeResponse.EnsureSuccessStatusCode();
        var closedShift = await closeResponse.Content.ReadFromJsonAsync<CashierShiftDetailDto>();
        Assert.NotNull(closedShift);
        Assert.Equal("Closed", closedShift!.Status);
        Assert.Equal(100m, closedShift.ExpectedClosingCashAmount);
        Assert.Equal(130m, closedShift.DeclaredClosingCashAmount);
        Assert.Equal(30m, closedShift.CashVarianceAmount);
        Assert.NotNull(closedShift.ClosedAtUtc);
        Assert.Equal("End of day", closedShift.CloseNotes);

        await using var auditScope = fixture.Services.CreateAsyncScope();
        var auditContext = auditScope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var closeAudit = await auditContext.AuditLogs.SingleAsync(entity => entity.Action == "CashierShift.Closed");
        Assert.Equal(admin.RestaurantId, closeAudit.RestaurantId);
        Assert.Equal(admin.ActiveBranchId, closeAudit.BranchId);
        Assert.Equal(admin.UserId, closeAudit.UserId);
    }

    [Fact]
    public async Task Cashier_Cannot_Close_Shift_Twice()
    {
        await using var fixture = await CashierShiftApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var openResponse = await fixture.Client.PostAsJsonAsync("/api/v1/cashier/shifts/open", new
        {
            branchId = admin.ActiveBranchId,
            businessDate = "2026-06-13",
            openingCashAmount = 100m
        });
        openResponse.EnsureSuccessStatusCode();
        var openedShift = await openResponse.Content.ReadFromJsonAsync<CashierShiftDetailDto>();
        Assert.NotNull(openedShift);

        var firstClose = await fixture.Client.PostAsJsonAsync($"/api/v1/cashier/shifts/{openedShift!.CashierShiftId}/close", new
        {
            declaredClosingCashAmount = 110m,
            closeNotes = "First close"
        });
        firstClose.EnsureSuccessStatusCode();

        var secondClose = await fixture.Client.PostAsJsonAsync($"/api/v1/cashier/shifts/{openedShift.CashierShiftId}/close", new
        {
            declaredClosingCashAmount = 110m,
            closeNotes = "Second close"
        });

        Assert.Equal(HttpStatusCode.BadRequest, secondClose.StatusCode);
    }

    [Fact]
    public async Task Cashier_Cannot_Close_Another_Restaurants_Shift()
    {
        await using var fixture = await CashierShiftApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);
        var foreignShift = await fixture.InsertForeignOpenShiftAsync();

        var closeResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/cashier/shifts/{foreignShift.CashierShiftId}/close", new
        {
            declaredClosingCashAmount = 110m,
            closeNotes = "Not allowed"
        });

        Assert.Equal(HttpStatusCode.NotFound, closeResponse.StatusCode);
    }

    [Fact]
    public async Task Cashier_Shift_Audit_Log_Is_Written_For_Open_And_Close()
    {
        await using var fixture = await CashierShiftApiFactory.CreateAsync();
        var admin = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var openResponse = await fixture.Client.PostAsJsonAsync("/api/v1/cashier/shifts/open", new
        {
            branchId = admin.ActiveBranchId,
            businessDate = "2026-06-13",
            openingCashAmount = 75m
        });
        openResponse.EnsureSuccessStatusCode();
        var openedShift = await openResponse.Content.ReadFromJsonAsync<CashierShiftDetailDto>();
        Assert.NotNull(openedShift);

        var closeResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/cashier/shifts/{openedShift!.CashierShiftId}/close", new
        {
            declaredClosingCashAmount = 80m,
            closeNotes = "Closed cleanly"
        });
        closeResponse.EnsureSuccessStatusCode();

        await using var auditScope = fixture.Services.CreateAsyncScope();
        var auditContext = auditScope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        Assert.Equal(2, await auditContext.AuditLogs.CountAsync(entity => entity.EntityType == "CashierShift"));
    }

    private sealed class CashierShiftApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private HttpClient? _client;

        public HttpClient Client => _client ??= CreateClient();

        public static async Task<CashierShiftApiFactory> CreateAsync()
        {
            var factory = new CashierShiftApiFactory();
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

            var activeBranch = new Branch
            {
                Name = "Alpha Branch",
                RestaurantId = restaurant.RestaurantId,
                Status = BranchStatus.Active,
                Timezone = "Asia/Singapore",
                Currency = "SGD"
            };

            var inactiveBranch = new Branch
            {
                Name = "Beta Branch",
                RestaurantId = restaurant.RestaurantId,
                Status = BranchStatus.Inactive,
                Timezone = "Asia/Singapore",
                Currency = "SGD"
            };

            var user = new User
            {
                RestaurantId = restaurant.RestaurantId,
                BranchId = activeBranch.BranchId,
                FullName = $"{roleName} User",
                MobileNumber = "90000008",
                Status = UserStatus.Active
            };

            var hasher = new PasswordHasher<User>();
            user.PasswordHash = hasher.HashPassword(user, "Passw0rd!Passw0rd!");

            var assignedRole = await context.Roles.SingleAsync(role => role.RestaurantId == null && role.Name == roleName);

            context.Restaurants.Add(restaurant);
            context.Branches.AddRange(activeBranch, inactiveBranch);
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
                activeBranch.BranchId,
                inactiveBranch.BranchId,
                user.UserId,
                restaurant.NormalizedRestaurantCode,
                user.MobileNumber,
                "Passw0rd!Passw0rd!",
                user.FullName);
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

        public async Task<CashierShiftDetailDto> InsertForeignOpenShiftAsync()
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            await new FoundationSeedService(context).SeedAsync();

            var restaurant = new Restaurant
            {
                Name = "Foreign Restaurant",
                Status = RestaurantStatus.Active
            };
            restaurant.SetRestaurantCode("RESTFOREIGN01");

            var branch = new Branch
            {
                Name = "Foreign Branch",
                RestaurantId = restaurant.RestaurantId,
                Status = BranchStatus.Active,
                Timezone = "Asia/Singapore",
                Currency = "SGD"
            };

            var user = new User
            {
                RestaurantId = restaurant.RestaurantId,
                BranchId = branch.BranchId,
                FullName = "Foreign User",
                MobileNumber = "90000099",
                Status = UserStatus.Active
            };

            var hasher = new PasswordHasher<User>();
            user.PasswordHash = hasher.HashPassword(user, "Passw0rd!Passw0rd!");

            var role = await context.Roles.SingleAsync(entity => entity.RestaurantId == null && entity.Name == "Admin");

            var now = DateTimeOffset.UtcNow;
            var shift = new CashierShift
            {
                RestaurantId = restaurant.RestaurantId,
                BranchId = branch.BranchId,
                CashierUserId = user.UserId,
                BusinessDate = new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc),
                Status = CashierShiftStatus.Open,
                OpenedAtUtc = now,
                OpeningCashAmount = 50m,
                ExpectedClosingCashAmount = 50m,
                CreatedAtUtc = now
            };

            context.Restaurants.Add(restaurant);
            context.Branches.Add(branch);
            context.Users.Add(user);
            context.UserRoles.Add(new UserRole
            {
                UserId = user.UserId,
                RoleId = role.RoleId,
                AssignedAt = now
            });
            context.CashierShifts.Add(shift);
            await context.SaveChangesAsync();

            return new CashierShiftDetailDto(
                shift.CashierShiftId,
                shift.RestaurantId,
                shift.BranchId,
                shift.CashierUserId,
                "Foreign User",
                "Foreign Branch",
                shift.BusinessDate,
                shift.Status.ToString(),
                shift.OpenedAtUtc,
                shift.OpeningCashAmount,
                shift.ClosedAtUtc,
                shift.DeclaredClosingCashAmount,
                shift.ExpectedClosingCashAmount,
                shift.CashVarianceAmount,
                shift.CloseNotes,
                shift.CreatedAtUtc,
                shift.UpdatedAtUtc);
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

    private sealed record CashierShiftListResponseDto(CashierShiftListItemDto[] Items);

    private sealed record CashierShiftListItemDto(
        Guid CashierShiftId,
        Guid RestaurantId,
        Guid BranchId,
        Guid CashierUserId,
        string CashierName,
        string BranchName,
        DateTime BusinessDate,
        string Status,
        DateTimeOffset OpenedAtUtc,
        decimal OpeningCashAmount,
        DateTimeOffset? ClosedAtUtc,
        decimal? DeclaredClosingCashAmount,
        decimal ExpectedClosingCashAmount,
        decimal? CashVarianceAmount,
        string? CloseNotes,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? UpdatedAtUtc);

    private sealed record CashierShiftDetailDto(
        Guid CashierShiftId,
        Guid RestaurantId,
        Guid BranchId,
        Guid CashierUserId,
        string CashierName,
        string BranchName,
        DateTime BusinessDate,
        string Status,
        DateTimeOffset OpenedAtUtc,
        decimal OpeningCashAmount,
        DateTimeOffset? ClosedAtUtc,
        decimal? DeclaredClosingCashAmount,
        decimal ExpectedClosingCashAmount,
        decimal? CashVarianceAmount,
        string? CloseNotes,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? UpdatedAtUtc);
}
