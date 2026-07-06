using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Collections.Concurrent;
using System.Data.Common;
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

public sealed class BranchAdminMutationEndpointTests
{
    [Fact]
    public async Task Create_Should_Return_401_When_Unauthenticated()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/branches", new
        {
            name = "Main Outlet",
            address = "123 Market Street",
            phone = "60000001",
            timezone = "Asia/Singapore",
            currency = "SGD"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_Should_Return_403_When_User_Lacks_Branch_Manage()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Cashier");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/branches", new
        {
            name = "Main Outlet",
            address = "123 Market Street",
            phone = "60000001",
            timezone = "Asia/Singapore",
            currency = "SGD"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_Should_Return_403_When_User_Lacks_Branch_Manage()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Cashier");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PutAsJsonAsync($"/api/v1/admin/branches/{seed.ActiveBranchId}", new
        {
            name = "Updated Outlet",
            address = "456 Updated Street",
            phone = "60000002",
            timezone = "Asia/Singapore",
            currency = "SGD"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Activate_Should_Return_403_When_User_Lacks_Branch_Manage()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Cashier");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PostAsync($"/api/v1/admin/branches/{seed.ActiveBranchId}/activate", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_Should_Return_403_When_User_Lacks_Branch_Manage()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Cashier");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PostAsync($"/api/v1/admin/branches/{seed.ActiveBranchId}/deactivate", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_Should_Create_Branch_Under_Current_Restaurant_Trim_Text_Fields_Default_Active_And_Write_Audit_Log()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        var foreign = await fixture.SeedForeignRestaurantAsync();
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/branches", new
        {
            restaurantId = foreign.RestaurantId,
            name = "  Main Outlet  ",
            address = " 123 Market Street ",
            phone = " 60000001 ",
            timezone = " Asia/Singapore ",
            currency = " SGD "
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.StartsWith("/api/v1/admin/branches/", response.Headers.Location?.OriginalString ?? string.Empty, StringComparison.Ordinal);

        var payload = await response.Content.ReadFromJsonAsync<BranchDetailDto>();
        Assert.NotNull(payload);
        Assert.NotEqual(Guid.Empty, payload!.BranchId);
        Assert.Equal(seed.RestaurantId, payload.RestaurantId);
        Assert.NotEqual(foreign.RestaurantId, payload.RestaurantId);
        Assert.Equal("Main Outlet", payload.Name);
        Assert.Equal("123 Market Street", payload.Address);
        Assert.Equal("60000001", payload.Phone);
        Assert.Equal("Asia/Singapore", payload.Timezone);
        Assert.Equal("SGD", payload.Currency);
        Assert.Equal("Active", payload.Status);
        Assert.NotEqual(default, payload.CreatedAt);
        Assert.Null(payload.UpdatedAt);

        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var audit = await context.AuditLogs
            .Where(log => log.EntityType == "Branch" &&
                          log.EntityId == payload.BranchId.ToString() &&
                          log.Action == "Branch.Created")
            .ToListAsync();

        Assert.Single(audit);
    }

    [Fact]
    public async Task Create_Should_Default_Country_Currency_And_TimeZone_From_Restaurant_When_Omitted()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/branches", new
        {
            name = "Default Profile Outlet",
            address = "123 Default Street",
            phone = "60000011"
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<BranchDetailDto>();
        Assert.NotNull(payload);
        Assert.Equal("SG", payload!.CountryCode);
        Assert.Equal("SGD", payload.Currency);
        Assert.Equal("Asia/Singapore", payload.Timezone);
    }

    [Fact]
    public async Task Create_Should_Normalize_Lowercase_Currency_To_Uppercase()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/branches", new
        {
            name = "Currency Normalized Outlet",
            address = "123 Currency Street",
            phone = "60000012",
            currency = "sgd"
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<BranchDetailDto>();
        Assert.NotNull(payload);
        Assert.Equal("SGD", payload!.Currency);
    }

    [Fact]
    public async Task Create_Should_Reject_Blank_Name()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/branches", new
        {
            name = "   ",
            address = "123 Market Street",
            phone = "60000001",
            timezone = "Asia/Singapore",
            currency = "SGD"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_Should_Reject_Duplicate_Name_In_Same_Restaurant()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var first = await fixture.Client.PostAsJsonAsync("/api/v1/admin/branches", new
        {
            name = "Main Outlet",
            address = "123 Market Street",
            phone = "60000001",
            timezone = "Asia/Singapore",
            currency = "SGD"
        });

        first.EnsureSuccessStatusCode();

        var second = await fixture.Client.PostAsJsonAsync("/api/v1/admin/branches", new
        {
            name = " main outlet ",
            address = "456 Another Street",
            phone = "60000002",
            timezone = "Asia/Singapore",
            currency = "SGD"
        });

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Create_Should_Reject_Duplicate_Mobile_In_Same_Restaurant_And_Allow_Same_Number_In_Different_Restaurant()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var first = await fixture.Client.PostAsJsonAsync("/api/v1/admin/branches", new
        {
            name = "Main Outlet",
            address = "123 Market Street",
            phone = "60000001",
            timezone = "Asia/Singapore",
            currency = "SGD"
        });

        first.EnsureSuccessStatusCode();

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE [Branches] SET [NormalizedPhone] = NULL WHERE [RestaurantId] = {0} AND [Phone] = {1}",
                seed.RestaurantId,
                "60000001");
        }

        var duplicate = await fixture.Client.PostAsJsonAsync("/api/v1/admin/branches", new
        {
            name = "Second Outlet",
            address = "456 Market Street",
            phone = " 60000001 ",
            timezone = "Asia/Singapore",
            currency = "SGD"
        });

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        var duplicateBody = await duplicate.Content.ReadAsStringAsync();
        Assert.Contains("Branch mobile number already exists.", duplicateBody);

        await using var foreignFixture = await BranchAdminApiFactory.CreateAsync();
        var foreignSeed = await foreignFixture.SeedSystemUserAsync("Admin");
        await foreignFixture.AuthenticateAsync(foreignSeed);

        var foreign = await foreignFixture.Client.PostAsJsonAsync("/api/v1/admin/branches", new
        {
            name = "Foreign Outlet",
            address = "789 Market Street",
            phone = "60000001",
            timezone = "Asia/Singapore",
            currency = "SGD"
        });

        foreign.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Create_Duplicate_Name_Validation_Should_Not_Use_Ef_Side_Upper()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var existing = await fixture.Client.PostAsJsonAsync("/api/v1/admin/branches", new
        {
            name = "Main Outlet",
            address = "123 Market Street",
            phone = "60000001",
            timezone = "Asia/Singapore",
            currency = "SGD"
        });

        existing.EnsureSuccessStatusCode();

        fixture.SqlCapture.Clear();
        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/branches", new
        {
            name = " main outlet ",
            address = "123 Market Street",
            phone = "60000001",
            timezone = "Asia/Singapore",
            currency = "SGD"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(fixture.SqlCapture.Commands, command =>
            command.Contains("UPPER(", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Create_Should_Allow_Duplicate_Name_In_Different_Restaurant()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        var foreign = await fixture.SeedForeignSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var createCurrent = await fixture.Client.PostAsJsonAsync("/api/v1/admin/branches", new
        {
            name = "Main Outlet",
            address = "123 Market Street",
            phone = "60000001",
            timezone = "Asia/Singapore",
            currency = "SGD"
        });

        createCurrent.EnsureSuccessStatusCode();

        await fixture.AuthenticateAsync(foreign);
        var createForeign = await fixture.Client.PostAsJsonAsync("/api/v1/admin/branches", new
        {
            name = "Main Outlet",
            address = "987 Foreign Street",
            phone = "70000001",
            timezone = "Asia/Singapore",
            currency = "SGD"
        });

        createForeign.EnsureSuccessStatusCode();
        var payload = await createForeign.Content.ReadFromJsonAsync<BranchDetailDto>();
        Assert.NotNull(payload);
        Assert.Equal(foreign.RestaurantId, payload!.RestaurantId);
        Assert.Equal("Main Outlet", payload.Name);
    }

    [Fact]
    public async Task Update_Duplicate_Name_Validation_Should_Not_Use_Ef_Side_Upper()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        _ = await fixture.InsertBranchAsync(seed.RestaurantId, "Main Outlet", BranchStatus.Active);
        var duplicate = await fixture.InsertBranchAsync(seed.RestaurantId, "Secondary Outlet", BranchStatus.Active);

        fixture.SqlCapture.Clear();
        var response = await fixture.Client.PutAsJsonAsync($"/api/v1/admin/branches/{duplicate.BranchId}", new
        {
            name = " MAIN OUTLET ",
            address = "456 Updated Street",
            phone = "60000002",
            timezone = "Asia/Singapore",
            currency = "SGD"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(fixture.SqlCapture.Commands, command =>
            command.Contains("UPPER(", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Update_Should_Update_Profile_And_Preserve_Status()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var branch = await fixture.InsertBranchAsync(seed.RestaurantId, "Update Target", BranchStatus.Inactive);

        var response = await fixture.Client.PutAsJsonAsync($"/api/v1/admin/branches/{branch.BranchId}", new
        {
            name = "  Updated Outlet  ",
            address = " 456 Updated Street ",
            phone = " 60000002 ",
            timezone = " Asia/Singapore ",
            currency = " SGD "
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<BranchDetailDto>();
        Assert.NotNull(payload);
        Assert.Equal(branch.BranchId, payload!.BranchId);
        Assert.Equal(seed.RestaurantId, payload.RestaurantId);
        Assert.Equal("Updated Outlet", payload.Name);
        Assert.Equal("456 Updated Street", payload.Address);
        Assert.Equal("60000002", payload.Phone);
        Assert.Equal("Asia/Singapore", payload.Timezone);
        Assert.Equal("SGD", payload.Currency);
        Assert.Equal("Inactive", payload.Status);
        Assert.NotEqual(default, payload.UpdatedAt);

        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var audit = await context.AuditLogs
            .Where(log => log.EntityType == "Branch" &&
                          log.EntityId == branch.BranchId.ToString() &&
                          log.Action == "Branch.Updated")
            .ToListAsync();

        Assert.Single(audit);
    }

    [Fact]
    public async Task Update_Should_Normalize_Lowercase_Currency_To_Uppercase()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var branch = await fixture.InsertBranchAsync(seed.RestaurantId, "Currency Update Target", BranchStatus.Active);

        var response = await fixture.Client.PutAsJsonAsync($"/api/v1/admin/branches/{branch.BranchId}", new
        {
            name = "Currency Update Target",
            address = "456 Currency Street",
            phone = "60000013",
            currency = "sgd"
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<BranchDetailDto>();
        Assert.NotNull(payload);
        Assert.Equal("SGD", payload!.Currency);
    }

    [Fact]
    public async Task Update_Should_Return_404_For_Other_Restaurant_Branch()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        var foreign = await fixture.SeedForeignSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PutAsJsonAsync($"/api/v1/admin/branches/{foreign.ActiveBranchId}", new
        {
            name = "Updated Outlet",
            address = "456 Updated Street",
            phone = "60000002",
            timezone = "Asia/Singapore",
            currency = "SGD"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_Should_Reject_Duplicate_Name_In_Same_Restaurant()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        _ = await fixture.InsertBranchAsync(seed.RestaurantId, "Main Outlet", BranchStatus.Active);
        var duplicate = await fixture.InsertBranchAsync(seed.RestaurantId, "Secondary Outlet", BranchStatus.Active);

        var response = await fixture.Client.PutAsJsonAsync($"/api/v1/admin/branches/{duplicate.BranchId}", new
        {
            name = " main outlet ",
            address = "456 Updated Street",
            phone = "60000002",
            timezone = "Asia/Singapore",
            currency = "SGD"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_Should_Allow_Same_Name_With_Different_Casing_For_Same_Branch()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var branch = await fixture.InsertBranchAsync(seed.RestaurantId, "Stable Outlet", BranchStatus.Active);

        var response = await fixture.Client.PutAsJsonAsync($"/api/v1/admin/branches/{branch.BranchId}", new
        {
            name = " stable outlet ",
            address = " 789 Same Street ",
            phone = " 60000003 ",
            timezone = " Asia/Singapore ",
            currency = " SGD "
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<BranchDetailDto>();
        Assert.NotNull(payload);
        Assert.Equal("stable outlet", payload!.Name);
        Assert.Equal("Active", payload.Status);
    }

    [Fact]
    public async Task Activate_Should_Set_Branch_Active_And_Be_Idempotent()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var branch = await fixture.InsertBranchAsync(seed.RestaurantId, "Inactive Outlet", BranchStatus.Inactive);

        var first = await fixture.Client.PostAsync($"/api/v1/admin/branches/{branch.BranchId}/activate", null);
        first.EnsureSuccessStatusCode();

        var firstPayload = await first.Content.ReadFromJsonAsync<BranchDetailDto>();
        Assert.NotNull(firstPayload);
        Assert.Equal("Active", firstPayload!.Status);

        var second = await fixture.Client.PostAsync($"/api/v1/admin/branches/{branch.BranchId}/activate", null);
        second.EnsureSuccessStatusCode();

        var secondPayload = await second.Content.ReadFromJsonAsync<BranchDetailDto>();
        Assert.NotNull(secondPayload);
        Assert.Equal("Active", secondPayload!.Status);

        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var audit = await context.AuditLogs
            .Where(log => log.EntityType == "Branch" &&
                          log.EntityId == branch.BranchId.ToString() &&
                          log.Action == "Branch.Activated")
            .ToListAsync();

        Assert.Equal(2, audit.Count);
    }

    [Fact]
    public async Task Deactivate_Should_Set_Branch_Inactive_And_Be_Idempotent()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/branches", new
        {
            name = "Disposable Outlet",
            address = "123 Branch Street",
            phone = "61000001",
            timezone = "Asia/Singapore",
            currency = "SGD"
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<BranchDetailDto>();
        Assert.NotNull(payload);

        var first = await fixture.Client.PostAsync($"/api/v1/admin/branches/{payload!.BranchId}/deactivate", null);
        first.EnsureSuccessStatusCode();

        var firstPayload = await first.Content.ReadFromJsonAsync<BranchDetailDto>();
        Assert.NotNull(firstPayload);
        Assert.Equal("Inactive", firstPayload!.Status);

        var second = await fixture.Client.PostAsync($"/api/v1/admin/branches/{payload.BranchId}/deactivate", null);
        second.EnsureSuccessStatusCode();

        var secondPayload = await second.Content.ReadFromJsonAsync<BranchDetailDto>();
        Assert.NotNull(secondPayload);
        Assert.Equal("Inactive", secondPayload!.Status);

        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var audit = await context.AuditLogs
            .Where(log => log.EntityType == "Branch" &&
                          log.EntityId == payload.BranchId.ToString() &&
                          log.Action == "Branch.Deactivated")
            .ToListAsync();

        Assert.Equal(2, audit.Count);
    }

    [Fact]
    public async Task Deactivate_Should_Return_404_For_Other_Restaurant_Branch()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        var foreign = await fixture.SeedForeignSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PostAsync($"/api/v1/admin/branches/{foreign.ActiveBranchId}/deactivate", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_Should_Return_400_Or_409_When_Active_Users_Are_Assigned()
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var branch = await fixture.InsertBranchAsync(seed.RestaurantId, "Protected Outlet", BranchStatus.Active);
        await fixture.InsertActiveUserAsync(seed.RestaurantId, branch.BranchId);

        var response = await fixture.Client.PostAsync($"/api/v1/admin/branches/{branch.BranchId}/deactivate", null);

        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict,
            $"Expected 400 or 409, got {(int)response.StatusCode}.");
    }

    [Theory]
    [InlineData("DELETE", "/api/v1/admin/branches")]
    [InlineData("DELETE", "/api/v1/admin/branches/{id}")]
    public async Task Delete_Should_Return_404_Or_405(string method, string pathTemplate)
    {
        await using var fixture = await BranchAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedSystemUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var path = pathTemplate.Replace("{id}", seed.ActiveBranchId.ToString(), StringComparison.OrdinalIgnoreCase);
        var response = await fixture.Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"Expected 404 or 405, got {(int)response.StatusCode}.");
    }

    private sealed class BranchAdminApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly SqlCommandCaptureInterceptor _sqlCapture = new();
        private HttpClient? _client;

        public HttpClient Client => _client ??= CreateClient();

        public SqlCommandCaptureInterceptor SqlCapture => _sqlCapture;

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
                Name = roleName + " Restaurant",
                Status = RestaurantStatus.Active
            };
            restaurant.SetRestaurantCode("REST" + roleName[..1].ToUpperInvariant() + "01");
            restaurant.SetCountryProfile("SG");
            var profile = BillSoft.Domain.Localization.CountryProfileCatalog.GetRequired("SG");

            var branch = new Branch
            {
                Name = "Alpha Branch",
                RestaurantId = restaurant.RestaurantId,
                Status = BranchStatus.Active,
                CountryCode = profile.CountryCode,
                TimeZoneId = profile.TimeZoneId,
                CurrencyCode = profile.CurrencyCode
            };

            var inactiveBranch = new Branch
            {
                Name = "Beta Branch",
                RestaurantId = restaurant.RestaurantId,
                Status = BranchStatus.Inactive,
                CountryCode = profile.CountryCode,
                TimeZoneId = profile.TimeZoneId,
                CurrencyCode = profile.CurrencyCode
            };

            var user = new User
            {
                RestaurantId = restaurant.RestaurantId,
                BranchId = branch.BranchId,
                FullName = roleName + " User",
                Status = UserStatus.Active
            };
            user.SetMobileNumber(profile.CountryCode, roleName == "Cashier" ? "90000005" : "90000006");

            var hasher = new PasswordHasher<User>();
            user.PasswordHash = hasher.HashPassword(user, "Passw0rd!Passw0rd!");

            var assignedRole = await context.Roles.SingleAsync(role => role.RestaurantId == null && role.Name == roleName);

            context.Restaurants.Add(restaurant);
            context.Branches.AddRange(branch, inactiveBranch);
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
                inactiveBranch.BranchId,
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
                Name = "Foreign " + roleName + " Restaurant",
                Status = RestaurantStatus.Active
            };
            restaurant.SetRestaurantCode("RESTZ01");
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
                FullName = "Foreign " + roleName + " User",
                Status = UserStatus.Active
            };
            user.SetMobileNumber(profile.CountryCode, "90000007");

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
                branch.BranchId,
                user.UserId,
                restaurant.NormalizedRestaurantCode,
                user.MobileNumber,
                "Passw0rd!Passw0rd!",
                user.FullName);
        }

        public async Task<SeedResult> SeedForeignRestaurantAsync()
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

            return new SeedResult(
                restaurant.RestaurantId,
                Guid.Empty,
                Guid.Empty,
                Guid.Empty,
                restaurant.NormalizedRestaurantCode,
                string.Empty,
                string.Empty,
                restaurant.Name);
        }

        public async Task<Branch> InsertBranchAsync(Guid restaurantId, string name, BranchStatus status)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            var branch = new Branch
            {
                RestaurantId = restaurantId,
                Name = name,
                Status = status,
                Timezone = "Asia/Singapore",
                Currency = "SGD"
            };

            context.Branches.Add(branch);
            await context.SaveChangesAsync();
            return branch;
        }

        public async Task<User> InsertActiveUserAsync(Guid restaurantId, Guid branchId)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            var user = new User
            {
                RestaurantId = restaurantId,
                BranchId = branchId,
                FullName = "Assigned Active User",
                MobileNumber = "90000031",
                Status = UserStatus.Active
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
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

    private sealed class SqlCommandCaptureInterceptor : DbCommandInterceptor
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
}
