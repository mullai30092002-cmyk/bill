using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BillSoft.Application.Auth;
using BillSoft.Application.Users;
using BillSoft.Domain.Localization;
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

public sealed class UserAdminEndpointTests
{
    [Fact]
    public async Task List_Should_Return_401_When_Unauthenticated()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();

        var response = await fixture.Client.GetAsync("/api/v1/admin/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_Should_Return_403_When_User_Lacks_User_Manage()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedUserAsync("Cashier");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.GetAsync("/api/v1/admin/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_Should_Create_User_With_Hashed_Password_And_Assigned_Role()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            branchId = seed.BranchId,
            fullName = "New Cashier",
            mobileNumber = "90000015",
            email = "new.cashier@example.com",
            initialPassword = "BillSoft123!",
            roleNames = new[] { "Cashier" }
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<UserDetailDto>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.UserId.ToString()));
        Assert.Equal("New Cashier", payload.FullName);
        Assert.Equal("90000015", payload.MobileNumber);
        Assert.Equal("Active", payload.Status);
        Assert.Contains("Cashier", payload.RoleNames);

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("PasswordHash", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PinHash", raw, StringComparison.OrdinalIgnoreCase);

        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var created = await context.Users.SingleAsync(user => user.MobileNumber == "90000015");

        Assert.False(string.IsNullOrWhiteSpace(created.PasswordHash));
        Assert.Null(created.PinHash);
    }

    [Fact]
    public async Task Create_Should_Store_Normalized_Singapore_Mobile_Fields()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            branchId = seed.BranchId,
            fullName = "SG Normalized User",
            mobileNumber = "90000002",
            email = "sg.normalized@example.com",
            initialPassword = "BillSoft123!",
            roleNames = new[] { "Cashier" }
        });

        response.EnsureSuccessStatusCode();

        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var created = await context.Users.SingleAsync(user => user.RestaurantId == seed.RestaurantId && user.MobileE164 == "+6590000002");

        Assert.Equal("SG", created.MobileCountryCode);
        Assert.Equal("+65", created.MobileDialCode);
        Assert.Equal("90000002", created.MobileNationalNumber);
        Assert.Equal("+6590000002", created.MobileE164);
        Assert.Equal("90000002", created.MobileNumber);
    }

    [Fact]
    public async Task Create_Should_Reject_Duplicate_Singapore_Equivalent_Mobile_In_Same_Restaurant()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var first = await fixture.Client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            branchId = seed.BranchId,
            fullName = "Existing SG User",
            mobileNumber = "90000002",
            email = "existing.sg@example.com",
            initialPassword = "BillSoft123!",
            roleNames = new[] { "Cashier" }
        });
        first.EnsureSuccessStatusCode();

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            branchId = seed.BranchId,
            fullName = "Duplicate SG User",
            mobileNumber = "+6590000002",
            email = "duplicate.sg@example.com",
            initialPassword = "BillSoft123!",
            roleNames = new[] { "Cashier" }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_Should_Store_Normalized_India_Mobile_Fields()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedUserAsync("Admin", countryCode: "IN", mobileNumber: "9876543211", restaurantCode: "RESTIN01");
        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var restaurant = await context.Restaurants.SingleAsync(entity => entity.RestaurantId == seed.RestaurantId);
        restaurant.SetCountryProfile("IN");

        var branch = await context.Branches.SingleAsync(entity => entity.BranchId == seed.BranchId);
        branch.CountryCode = "IN";
        branch.CurrencyCode = "INR";
        branch.TimeZoneId = "Asia/Kolkata";
        await context.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<IUserAdminService>();
        var currentUser = new AuthUserContext(
            seed.UserId,
            seed.RestaurantId,
            seed.RestaurantCode,
            seed.BranchId,
            seed.FullName,
            seed.MobileNumber,
            new[] { "Admin" },
            Array.Empty<string>(),
            "Admin");

        var detail = await service.CreateAsync(currentUser, new CreateUserRequest(
            seed.BranchId,
            "IN Normalized User",
            "9876543210",
            "in.normalized@example.com",
            "BillSoft123!",
            new[] { "Cashier" }), CancellationToken.None);

        Assert.Equal("9876543210", detail.MobileNumber);

        var created = await context.Users.SingleAsync(user => user.RestaurantId == seed.RestaurantId && user.MobileE164 == "+919876543210");

        Assert.Equal("IN", created.MobileCountryCode);
        Assert.Equal("+91", created.MobileDialCode);
        Assert.Equal("9876543210", created.MobileNationalNumber);
        Assert.Equal("+919876543210", created.MobileE164);
        Assert.Equal("9876543210", created.MobileNumber);
    }

    [Fact]
    public async Task Create_Should_Reject_Duplicate_India_Equivalent_Mobile_In_Same_Restaurant()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedUserAsync("Admin", countryCode: "IN", mobileNumber: "9876543211", restaurantCode: "RESTIN01");
        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var restaurant = await context.Restaurants.SingleAsync(entity => entity.RestaurantId == seed.RestaurantId);
        restaurant.SetCountryProfile("IN");

        var branch = await context.Branches.SingleAsync(entity => entity.BranchId == seed.BranchId);
        branch.CountryCode = "IN";
        branch.CurrencyCode = "INR";
        branch.TimeZoneId = "Asia/Kolkata";
        await context.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<IUserAdminService>();
        var currentUser = new AuthUserContext(
            seed.UserId,
            seed.RestaurantId,
            seed.RestaurantCode,
            seed.BranchId,
            seed.FullName,
            seed.MobileNumber,
            new[] { "Admin" },
            Array.Empty<string>(),
            "Admin");

        var existing = await service.CreateAsync(currentUser, new CreateUserRequest(
            seed.BranchId,
            "Existing IN User",
            "9876543210",
            "existing.in@example.com",
            "BillSoft123!",
            new[] { "Cashier" }), CancellationToken.None);

        Assert.Equal("9876543210", existing.MobileNumber);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(currentUser, new CreateUserRequest(
            seed.BranchId,
            "Duplicate IN User",
            "91 98765 43210",
            "duplicate.in@example.com",
            "BillSoft123!",
            new[] { "Cashier" }), CancellationToken.None));

        Assert.Contains("Mobile number already exists", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_Should_Allow_Same_Normalized_Mobile_In_Different_Restaurant()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedUserAsync("Admin");
        var foreign = await fixture.SeedForeignAdminUserAsync();
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            fullName = "Cross Restaurant User",
            mobileNumber = foreign.MobileNumber,
            email = "cross.restaurant@example.com",
            initialPassword = "BillSoft123!",
            roleNames = new[] { "Cashier" }
        });

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Create_Should_Reject_SuperAdmin_Assignment_When_Caller_Is_Not_SuperAdmin()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            fullName = "Super Admin Candidate",
            mobileNumber = "90000016",
            initialPassword = "BillSoft123!",
            roleNames = new[] { "SuperAdmin" }
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_Should_Require_Stronger_Password_For_Privileged_Roles()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            fullName = "Privileged User",
            mobileNumber = "90000017",
            initialPassword = "BillSoft12",
            roleNames = new[] { "Admin" }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("AccountsUser")]
    [InlineData("InventoryUser")]
    public async Task Create_Should_Reject_Short_Password_For_Additional_Privileged_Roles(string roleName)
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            fullName = $"{roleName} User",
            mobileNumber = roleName == "AccountsUser" ? "90000037" : "90000038",
            initialPassword = "BillSoft12",
            roleNames = new[] { roleName }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("AccountsUser", "90000029")]
    [InlineData("InventoryUser", "90000030")]
    public async Task Create_Should_Accept_12Plus_Password_For_Additional_Privileged_Roles(string roleName, string mobileNumber)
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            fullName = $"{roleName} User",
            mobileNumber,
            initialPassword = "BillSoft123!",
            roleNames = new[] { roleName }
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<UserDetailDto>();
        Assert.NotNull(payload);
        Assert.Contains(roleName, payload!.RoleNames);
    }

    [Fact]
    public async Task Create_Should_Allow_8Character_Password_For_Staff_Roles()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            fullName = "Cashier User",
            mobileNumber = "90000018",
            initialPassword = "Bill1234",
            roleNames = new[] { "Cashier" }
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<UserDetailDto>();
        Assert.NotNull(payload);
        Assert.Contains("Cashier", payload!.RoleNames);
    }

    [Fact]
    public async Task Create_Should_Reject_Branch_From_Another_Restaurant()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedUserAsync("Admin");
        var foreign = await fixture.SeedForeignAdminUserAsync();
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            branchId = foreign.BranchId,
            fullName = "Wrong Branch",
            mobileNumber = "90000019",
            initialPassword = "BillSoft123!",
            roleNames = new[] { "Cashier" }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_Should_Reject_Duplicate_Mobile_In_Same_Restaurant()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            fullName = "Duplicate Mobile",
            mobileNumber = seed.MobileNumber,
            initialPassword = "BillSoft123!",
            roleNames = new[] { "Cashier" }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_Should_Return_Only_Current_Restaurant_Users()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedUserAsync("Admin");
        await fixture.SeedForeignAdminUserAsync();
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.GetAsync("/api/v1/admin/users");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<UserListResponseDto>();
        Assert.NotNull(payload);
        Assert.All(payload!.Items, item => Assert.Equal(seed.RestaurantId, item.RestaurantId));
    }

    [Fact]
    public async Task Get_Should_Not_Expose_Password_Or_Pin_Hashes()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            fullName = "Profile User",
            mobileNumber = "90000020",
            initialPassword = "BillSoft123!",
            roleNames = new[] { "Cashier" }
        });
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<UserDetailDto>();
        Assert.NotNull(created);

        var get = await fixture.Client.GetAsync($"/api/v1/admin/users/{created!.UserId}");
        get.EnsureSuccessStatusCode();

        var raw = await get.Content.ReadAsStringAsync();
        Assert.DoesNotContain("PasswordHash", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PinHash", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_Should_Preserve_Password_And_Pin_Hash()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var create = await fixture.Client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            fullName = "Mutable User",
            mobileNumber = "90000021",
            initialPassword = "BillSoft123!",
            roleNames = new[] { "Cashier" }
        });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<UserDetailDto>();
        Assert.NotNull(created);

        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var before = await context.Users.SingleAsync(user => user.UserId == created!.UserId);
        before.PinHash = "PIN-HASH";
        await context.SaveChangesAsync();

        var update = await fixture.Client.PutAsJsonAsync($"/api/v1/admin/users/{created.UserId}", new
        {
            fullName = "Mutable User Updated",
            mobileNumber = "90000022",
            email = "mutable.updated@example.com",
            status = "Locked"
        });

        update.EnsureSuccessStatusCode();

        await context.Entry(before).ReloadAsync();
        var after = await context.Users.AsNoTracking().SingleAsync(user => user.UserId == created.UserId);
        Assert.Equal(before.PasswordHash, after.PasswordHash);
        Assert.Equal("PIN-HASH", after.PinHash);
    }

    [Fact]
    public async Task Update_Should_Recompute_Normalized_Mobile_Fields()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var create = await fixture.Client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            fullName = "Mobile Update Target",
            mobileNumber = "90000030",
            initialPassword = "BillSoft123!",
            roleNames = new[] { "Cashier" }
        });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<UserDetailDto>();
        Assert.NotNull(created);

        var update = await fixture.Client.PutAsJsonAsync($"/api/v1/admin/users/{created!.UserId}", new
        {
            fullName = "Mobile Update Target",
            mobileNumber = "+6590000031",
            email = "mobile.update@example.com",
            status = "Active"
        });

        update.EnsureSuccessStatusCode();

        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var after = await context.Users.AsNoTracking().SingleAsync(user => user.UserId == created.UserId);

        Assert.Equal("SG", after.MobileCountryCode);
        Assert.Equal("+65", after.MobileDialCode);
        Assert.Equal("90000031", after.MobileNationalNumber);
        Assert.Equal("+6590000031", after.MobileE164);
        Assert.Equal("90000031", after.MobileNumber);
    }

    [Fact]
    public async Task Update_Roles_Should_Replace_Existing_Assignments()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var create = await fixture.Client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            fullName = "Role Target",
            mobileNumber = "90000023",
            initialPassword = "BillSoft123!",
            roleNames = new[] { "Cashier" }
        });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<UserDetailDto>();
        Assert.NotNull(created);

        var updateRoles = await fixture.Client.PutAsJsonAsync($"/api/v1/admin/users/{created!.UserId}/roles", new
        {
            roleNames = new[] { "Waiter" }
        });

        updateRoles.EnsureSuccessStatusCode();
        var payload = await updateRoles.Content.ReadFromJsonAsync<UserDetailDto>();
        Assert.NotNull(payload);
        Assert.Contains("Waiter", payload!.RoleNames);
        Assert.DoesNotContain("Cashier", payload.RoleNames);
    }

    [Fact]
    public async Task Deactivate_Should_Block_Self_Deactivation()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PostAsync($"/api/v1/admin/users/{seed.UserId}/deactivate", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Activate_And_Deactivate_Should_Write_Audit_Logs()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var create = await fixture.Client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            fullName = "Audit User",
            mobileNumber = "90000024",
            initialPassword = "BillSoft123!",
            roleNames = new[] { "Cashier" }
        });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<UserDetailDto>();
        Assert.NotNull(created);

        var deactivate = await fixture.Client.PostAsync($"/api/v1/admin/users/{created!.UserId}/deactivate", null);
        deactivate.EnsureSuccessStatusCode();

        var activate = await fixture.Client.PostAsync($"/api/v1/admin/users/{created.UserId}/activate", null);
        activate.EnsureSuccessStatusCode();

        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var actions = await context.AuditLogs
            .Where(log => log.EntityId == created.UserId.ToString())
            .Select(log => log.Action)
            .ToListAsync();

        Assert.Contains("User.Created", actions);
        Assert.Contains("User.Deactivated", actions);
        Assert.Contains("User.Activated", actions);
    }

    [Fact]
    public async Task Deactivate_Should_Revoke_Active_Refresh_Tokens()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedUserAsync("Admin");
        await fixture.AuthenticateAsync(seed);

        var create = await fixture.Client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            fullName = "Token User",
            mobileNumber = "90000025",
            initialPassword = "BillSoft123!",
            roleNames = new[] { "Cashier" }
        });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<UserDetailDto>();
        Assert.NotNull(created);

        var login = await fixture.LoginAsync(seed.RestaurantCode, "90000025", "BillSoft123!");
        var refreshHash = RefreshTokenHash.Compute(login.RefreshToken);

        var deactivate = await fixture.Client.PostAsync($"/api/v1/admin/users/{created!.UserId}/deactivate", null);
        deactivate.EnsureSuccessStatusCode();

        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var token = await context.RefreshTokens.SingleAsync(entity => entity.TokenHash == refreshHash);

        Assert.NotNull(token.RevokedAt);
        Assert.Equal(created.UserId, token.UserId);

        var refreshResponse = await fixture.Client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = login.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task Reset_Password_Should_Return_401_When_Unauthenticated()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedUserAsync("Admin");
        var target = await CreateStaffUserAsync(fixture, seed.RestaurantId, seed.BranchId, "Target Cashier", "90000041", "OldPassword123!");

        var response = await fixture.Client.PostAsJsonAsync($"/api/v1/admin/users/{target.UserId}/reset-password", new
        {
            newPassword = "NewStrongPassword123!",
            confirmPassword = "NewStrongPassword123!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Reset_Password_Should_Return_403_When_User_Lacks_User_Manage()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var seed = await fixture.SeedUserAsync("Cashier");
        var target = await CreateStaffUserAsync(fixture, seed.RestaurantId, seed.BranchId, "Target Cashier", "90000042", "OldPassword123!");
        await fixture.AuthenticateAsync(seed);

        var response = await fixture.Client.PostAsJsonAsync($"/api/v1/admin/users/{target.UserId}/reset-password", new
        {
            newPassword = "NewStrongPassword123!",
            confirmPassword = "NewStrongPassword123!"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Reset_Password_Should_Reset_Another_User_And_Allow_Login_With_New_Password()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var admin = await fixture.SeedUserAsync("Admin");
        var target = await CreateStaffUserAsync(fixture, admin.RestaurantId, admin.BranchId, "Target Cashier", "90000043", "OldPassword123!");
        await fixture.AuthenticateAsync(admin);

        var resetResponse = await fixture.Client.PostAsJsonAsync($"/api/v1/admin/users/{target.UserId}/reset-password", new
        {
            newPassword = "NewStrongPassword123!",
            confirmPassword = "NewStrongPassword123!"
        });

        resetResponse.EnsureSuccessStatusCode();

        var newLogin = await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            restaurantCode = admin.RestaurantCode,
            mobileNumber = target.MobileNumber,
            password = "NewStrongPassword123!"
        });
        newLogin.EnsureSuccessStatusCode();

        var oldLogin = await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            restaurantCode = admin.RestaurantCode,
            mobileNumber = target.MobileNumber,
            password = target.Password
        });

        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
    }

    [Fact]
    public async Task Reset_Password_Should_Return_404_For_Cross_Restaurant_Target()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var admin = await fixture.SeedUserAsync("Admin");
        var foreignTarget = await fixture.SeedForeignAdminUserAsync();
        await fixture.AuthenticateAsync(admin);

        var response = await fixture.Client.PostAsJsonAsync($"/api/v1/admin/users/{foreignTarget.UserId}/reset-password", new
        {
            newPassword = "NewStrongPassword123!",
            confirmPassword = "NewStrongPassword123!"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reset_Password_Should_Return_404_For_Unknown_Target()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var admin = await fixture.SeedUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var response = await fixture.Client.PostAsJsonAsync($"/api/v1/admin/users/{Guid.NewGuid()}/reset-password", new
        {
            newPassword = "NewStrongPassword123!",
            confirmPassword = "NewStrongPassword123!"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reset_Password_Should_Block_Self_Reset()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var admin = await fixture.SeedUserAsync("Admin");
        await fixture.AuthenticateAsync(admin);

        var response = await fixture.Client.PostAsJsonAsync($"/api/v1/admin/users/{admin.UserId}/reset-password", new
        {
            newPassword = "NewStrongPassword123!",
            confirmPassword = "NewStrongPassword123!"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadAsStringAsync();
        Assert.Contains("own password", problem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reset_Password_Should_Reject_Blank_Password()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var admin = await fixture.SeedUserAsync("Admin");
        var target = await CreateStaffUserAsync(fixture, admin.RestaurantId, admin.BranchId, "Target Cashier", "90000044", "OldPassword123!");
        await fixture.AuthenticateAsync(admin);

        var response = await fixture.Client.PostAsJsonAsync($"/api/v1/admin/users/{target.UserId}/reset-password", new
        {
            newPassword = "   ",
            confirmPassword = "   "
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reset_Password_Should_Reject_Weak_Password()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var admin = await fixture.SeedUserAsync("Admin");
        var target = await CreateStaffUserAsync(fixture, admin.RestaurantId, admin.BranchId, "Target Cashier", "90000045", "OldPassword123!");
        await fixture.AuthenticateAsync(admin);

        var response = await fixture.Client.PostAsJsonAsync($"/api/v1/admin/users/{target.UserId}/reset-password", new
        {
            newPassword = "short",
            confirmPassword = "short"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reset_Password_Should_Reject_Confirm_Mismatch()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var admin = await fixture.SeedUserAsync("Admin");
        var target = await CreateStaffUserAsync(fixture, admin.RestaurantId, admin.BranchId, "Target Cashier", "90000045", "OldPassword123!");
        await fixture.AuthenticateAsync(admin);

        var response = await fixture.Client.PostAsJsonAsync($"/api/v1/admin/users/{target.UserId}/reset-password", new
        {
            newPassword = "NewStrongPassword123!",
            confirmPassword = "DifferentPassword123!"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadAsStringAsync();
        Assert.Contains("match", problem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reset_Password_Should_Write_Audit_Logs_Without_Plaintext_Or_Hash()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var admin = await fixture.SeedUserAsync("Admin");
        var target = await CreateStaffUserAsync(fixture, admin.RestaurantId, admin.BranchId, "Target Cashier", "90000046", "OldPassword123!");
        await fixture.AuthenticateAsync(admin);

        var response = await fixture.Client.PostAsJsonAsync($"/api/v1/admin/users/{target.UserId}/reset-password", new
        {
            newPassword = "NewStrongPassword123!",
            confirmPassword = "NewStrongPassword123!"
        });

        response.EnsureSuccessStatusCode();

        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var audit = await context.AuditLogs.SingleAsync(log => log.Action == "User.PasswordReset" && log.EntityId == target.UserId.ToString());

        Assert.Contains(target.UserId.ToString(), audit.NewValueJson ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("NewStrongPassword123!", audit.NewValueJson ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("PasswordHash", audit.NewValueJson ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reset_Password_Request_Should_Ignore_Extra_Restaurant_Id_Field()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var admin = await fixture.SeedUserAsync("Admin");
        var target = await CreateStaffUserAsync(fixture, admin.RestaurantId, admin.BranchId, "Target Cashier", "90000047", "OldPassword123!");
        await fixture.AuthenticateAsync(admin);

        var response = await fixture.Client.PostAsJsonAsync($"/api/v1/admin/users/{target.UserId}/reset-password", new
        {
            newPassword = "NewStrongPassword123!",
            confirmPassword = "NewStrongPassword123!",
            restaurantId = Guid.NewGuid()
        });

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Reset_Password_Should_Change_Hash_Without_Changing_Profile_Role_Or_Branch()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();
        var admin = await fixture.SeedUserAsync("Admin");
        var target = await CreateStaffUserAsync(fixture, admin.RestaurantId, admin.BranchId, "Target Cashier", "90000048", "OldPassword123!");
        await fixture.AuthenticateAsync(admin);

        await using var scopeBefore = fixture.Services.CreateAsyncScope();
        var contextBefore = scopeBefore.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var beforeUser = await contextBefore.Users.AsNoTracking().SingleAsync(user => user.UserId == target.UserId);
        var beforeRoleNames = await contextBefore.UserRoles
            .Where(userRole => userRole.UserId == target.UserId)
            .Join(contextBefore.Roles, userRole => userRole.RoleId, role => role.RoleId, (_, role) => role.Name)
            .ToListAsync();

        var response = await fixture.Client.PostAsJsonAsync($"/api/v1/admin/users/{target.UserId}/reset-password", new
        {
            newPassword = "NewStrongPassword123!",
            confirmPassword = "NewStrongPassword123!"
        });

        response.EnsureSuccessStatusCode();

        var newLogin = await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            restaurantCode = admin.RestaurantCode,
            mobileNumber = target.MobileNumber,
            password = "NewStrongPassword123!"
        });
        newLogin.EnsureSuccessStatusCode();

        await using var scopeAfter = fixture.Services.CreateAsyncScope();
        var contextAfter = scopeAfter.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var afterUser = await contextAfter.Users.AsNoTracking().SingleAsync(user => user.UserId == target.UserId);
        var afterRoleNames = await contextAfter.UserRoles
            .Where(userRole => userRole.UserId == target.UserId)
            .Join(contextAfter.Roles, userRole => userRole.RoleId, role => role.RoleId, (_, role) => role.Name)
            .ToListAsync();

        Assert.NotEqual(beforeUser.PasswordHash, afterUser.PasswordHash);
        Assert.Equal(beforeUser.Status, afterUser.Status);
        Assert.Equal(beforeUser.BranchId, afterUser.BranchId);
        Assert.Equal(beforeUser.RestaurantId, afterUser.RestaurantId);
        Assert.Equal(beforeUser.MobileNumber, afterUser.MobileNumber);
        Assert.Equal(beforeUser.FullName, afterUser.FullName);
        Assert.Equal(beforeRoleNames.OrderBy(name => name).ToArray(), afterRoleNames.OrderBy(name => name).ToArray());
        Assert.Equal(beforeUser.PinHash, afterUser.PinHash);
    }

    [Fact]
    public async Task Forgot_Password_Endpoint_Should_Not_Exist()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();

        var deleteEndpoints = fixture.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>()
            .Endpoints
            .OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
            .Where(endpoint => string.Equals(endpoint.RoutePattern.RawText, "/api/v1/admin/users/{userId}/forgot-password", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Empty(deleteEndpoints);
    }

    private static async Task<CreatedStaffUser> CreateStaffUserAsync(
        UserAdminApiFactory fixture,
        Guid restaurantId,
        Guid branchId,
        string fullName,
        string mobileNumber,
        string password)
    {
        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

        var role = await context.Roles.SingleAsync(candidate => candidate.RestaurantId == null && candidate.Name == "Cashier");
        var user = new User
        {
            RestaurantId = restaurantId,
            BranchId = branchId,
            FullName = fullName,
            Status = UserStatus.Active
        };
        user.SetMobileNumber("SG", mobileNumber);
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);

        context.Users.Add(user);
        context.UserRoles.Add(new UserRole
        {
            UserId = user.UserId,
            RoleId = role.RoleId,
            AssignedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        return new CreatedStaffUser(user.UserId, user.MobileNumber, password, user.FullName);
    }

    private sealed record CreatedStaffUser(
        Guid UserId,
        string MobileNumber,
        string Password,
        string FullName);

    [Fact]
    public async Task Delete_Endpoint_Should_Not_Exist()
    {
        await using var fixture = await UserAdminApiFactory.CreateAsync();

        var deleteEndpoints = fixture.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>()
            .Endpoints
            .OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
            .Where(endpoint => string.Equals(endpoint.RoutePattern.RawText, "/api/v1/admin/users/{userId}", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.DoesNotContain(deleteEndpoints, endpoint =>
            endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()?.HttpMethods.Contains("DELETE") == true);
    }

    private sealed class UserAdminApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private HttpClient? _client;

        public HttpClient Client => _client ??= CreateClient();

        public static async Task<UserAdminApiFactory> CreateAsync()
        {
            var factory = new UserAdminApiFactory();
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

        public async Task<SeedResult> SeedUserAsync(
            string roleName,
            string countryCode = "SG",
            string? mobileNumber = null,
            string? restaurantCode = null)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            await new FoundationSeedService(context).SeedAsync();

            var restaurant = new Restaurant
            {
                Name = roleName + " Restaurant",
                Status = RestaurantStatus.Active
            };
            restaurant.SetRestaurantCode(restaurantCode ?? "REST" + roleName.Substring(0, 1).ToUpperInvariant() + "01");
            restaurant.SetCountryProfile(countryCode);

            var profile = CountryProfileCatalog.GetRequired(countryCode);

            var branch = new Branch
            {
                Name = "Main Branch",
                RestaurantId = restaurant.RestaurantId,
                Status = BranchStatus.Active,
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
            user.SetMobileNumber(countryCode, mobileNumber ?? (roleName == "Cashier" ? "90000026" : "90000027"));
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

        public async Task<SeedResult> SeedForeignAdminUserAsync()
        {
            return await SeedUserAsync("Admin", countryCode: "SG", mobileNumber: "90000028", restaurantCode: "RESTZ01");
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

    public sealed record SeedResult(
        Guid RestaurantId,
        Guid BranchId,
        Guid UserId,
        string RestaurantCode,
        string MobileNumber,
        string Password,
        string FullName);

    private sealed record AuthLoginResponseDto(string AccessToken, string RefreshToken);

    private sealed record UserDetailDto(
        Guid UserId,
        Guid RestaurantId,
        Guid? BranchId,
        string FullName,
        string MobileNumber,
        string? Email,
        string Status,
        string[] RoleNames,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);

    private sealed record UserListResponseDto(
        UserListItemDto[] Items,
        int TotalCount,
        int Page,
        int PageSize);

    private sealed record UserListItemDto(
        Guid UserId,
        Guid RestaurantId,
        Guid? BranchId,
        string FullName,
        string MobileNumber,
        string? Email,
        string Status,
        string[] RoleNames);
}
