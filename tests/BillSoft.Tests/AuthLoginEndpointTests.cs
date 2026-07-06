using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using BillSoft.Domain.Localization;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Security;
using BillSoft.Domain.Users;
using BillSoft.Infrastructure.Auth;
using BillSoft.Infrastructure.Persistence;
using BillSoft.Infrastructure.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Authentication;
using Xunit;

namespace BillSoft.Tests;

public sealed class AuthLoginEndpointTests
{
    [Fact]
    public async Task Login_Endpoint_Should_Use_Rate_Limiting_Policy()
    {
        await using var fixture = await AuthTestFactory.CreateAsync();

        var endpointDataSource = fixture.Services.GetRequiredService<EndpointDataSource>();
        var endpoint = endpointDataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Single(candidate => string.Equals(candidate.RoutePattern.RawText, "/api/v1/auth/login", StringComparison.Ordinal));

        var rateLimitingMetadata = endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>();

        Assert.NotNull(rateLimitingMetadata);
        Assert.Equal("auth-login-fixed", rateLimitingMetadata!.PolicyName);
    }

    [Theory]
    [InlineData("http://localhost:3010")]
    [InlineData("http://localhost:3011")]
    [InlineData("http://localhost:3012")]
    [InlineData("http://localhost:3013")]
    public async Task Login_Preflight_Should_Return_Cors_Headers_For_Local_Web_Origin(string origin)
    {
        await using var fixture = await AuthTestFactory.CreateAsync();

        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        request.Headers.TryAddWithoutValidation("Origin", origin);
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "POST");
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Headers", "content-type");

        var response = await fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins));
        Assert.Contains(origin, origins);
    }

    [Fact]
    public async Task Login_Should_Return_Tokens_And_Context()
    {
        await using var fixture = await AuthTestFactory.CreateAsync();
        var seed = await fixture.SeedActiveUserAsync();

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            restaurantCode = seed.RestaurantCode,
            mobileNumber = seed.MobileNumber,
            password = seed.Password
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(payload.RefreshToken));
        Assert.Equal(seed.UserId, payload.UserId);
        Assert.Equal(seed.RestaurantId, payload.RestaurantId);
        Assert.Equal(seed.RestaurantCode, payload.RestaurantCode);
        Assert.Equal(seed.BranchId, payload.BranchId);
        Assert.Equal(seed.FullName, payload.FullName);
        Assert.Equal(seed.MobileNumber, payload.MobileNumber);
        Assert.Contains("Cashier", payload.Roles);
        Assert.Contains("Billing.Manage", payload.Permissions);
        Assert.False(string.IsNullOrWhiteSpace(payload.ActiveRole));
    }

    [Fact]
    public async Task Login_Should_Support_Singapore_E164_Mobile_Format()
    {
        await using var fixture = await AuthTestFactory.CreateAsync();
        var seed = await fixture.SeedActiveUserAsync();

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            restaurantCode = seed.RestaurantCode,
            mobileNumber = "+6590000001",
            password = seed.Password
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

        Assert.NotNull(payload);
        Assert.Equal(seed.UserId, payload!.UserId);
        Assert.Equal("90000001", payload.MobileNumber);
    }

    [Fact]
    public async Task Login_Should_Support_Singapore_Spaced_Mobile_Format()
    {
        await using var fixture = await AuthTestFactory.CreateAsync();
        var seed = await fixture.SeedActiveUserAsync();

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            restaurantCode = seed.RestaurantCode,
            mobileNumber = "65 9000 0001",
            password = seed.Password
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

        Assert.NotNull(payload);
        Assert.Equal(seed.UserId, payload!.UserId);
    }

    [Fact]
    public async Task Login_Should_Support_India_National_Mobile_Format()
    {
        await using var fixture = await AuthTestFactory.CreateAsync();
        var seed = await fixture.SeedIndianUserAsync();

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            restaurantCode = seed.RestaurantCode,
            mobileNumber = "9876543210",
            password = seed.Password
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

        Assert.NotNull(payload);
        Assert.Equal(seed.UserId, payload!.UserId);
        Assert.Equal("9876543210", payload.MobileNumber);
    }

    [Fact]
    public async Task Login_Should_Support_India_Spaced_Mobile_Format()
    {
        await using var fixture = await AuthTestFactory.CreateAsync();
        var seed = await fixture.SeedIndianUserAsync();

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            restaurantCode = seed.RestaurantCode,
            mobileNumber = "91 98765 43210",
            password = seed.Password
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

        Assert.NotNull(payload);
        Assert.Equal(seed.UserId, payload!.UserId);
    }

    [Fact]
    public async Task Login_Should_Support_India_Leading_Zero_Mobile_Format()
    {
        await using var fixture = await AuthTestFactory.CreateAsync();
        var seed = await fixture.SeedIndianUserAsync();

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            restaurantCode = seed.RestaurantCode,
            mobileNumber = "09876543210",
            password = seed.Password
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

        Assert.NotNull(payload);
        Assert.Equal(seed.UserId, payload!.UserId);
    }

    [Fact]
    public async Task Login_Should_Support_India_E164_Mobile_Format()
    {
        await using var fixture = await AuthTestFactory.CreateAsync();
        var seed = await fixture.SeedIndianUserAsync();

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            restaurantCode = seed.RestaurantCode,
            mobileNumber = "+919876543210",
            password = seed.Password
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

        Assert.NotNull(payload);
        Assert.Equal(seed.UserId, payload!.UserId);
    }

    [Fact]
    public async Task Login_Should_Fail_Generic_For_Invalid_Equivalent_Mobile_Number()
    {
        await using var fixture = await AuthTestFactory.CreateAsync();
        var seed = await fixture.SeedActiveUserAsync();

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            restaurantCode = seed.RestaurantCode,
            mobileNumber = "+6590000009",
            password = seed.Password
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(problem);
        Assert.Equal("Invalid credentials.", problem!.Detail);
    }

    [Fact]
    public async Task Login_Should_Save_Changes_Once_For_Audit_Persistence()
    {
        await using var fixture = await AuthTestFactory.CreateAsync();
        var seed = await fixture.SeedActiveUserAsync();
        fixture.ResetSaveChangesCount();

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            restaurantCode = seed.RestaurantCode,
            mobileNumber = seed.MobileNumber,
            password = seed.Password
        });

        response.EnsureSuccessStatusCode();

        Assert.Equal(1, fixture.SaveChangesCount);
    }

    [Fact]
    public async Task Login_Should_Ignore_X_Forwarded_For_When_Writing_Audit_Ip()
    {
        await using var fixture = await AuthTestFactory.CreateAsync();
        var seed = await fixture.SeedActiveUserAsync();
        var spoofedIp = "198.51.100.77";

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new
            {
                restaurantCode = seed.RestaurantCode,
                mobileNumber = seed.MobileNumber,
                password = seed.Password
            })
        };
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", spoofedIp);

        var response = await fixture.Client.SendAsync(request);

        response.EnsureSuccessStatusCode();

        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var auditEntry = await context.AuditLogs
            .SingleAsync(entity => entity.Action == "Authentication.LoginSucceeded" && entity.UserId == seed.UserId);

        Assert.NotEqual(spoofedIp, auditEntry.IpAddress);
    }

    [Fact]
    public async Task Login_Should_Fail_With_Generic_Message_On_Wrong_Password()
    {
        await using var fixture = await AuthTestFactory.CreateAsync();
        var seed = await fixture.SeedActiveUserAsync();

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            restaurantCode = seed.RestaurantCode,
            mobileNumber = seed.MobileNumber,
            password = "wrong-password"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(problem);
        Assert.Equal("Invalid credentials.", problem!.Detail);
    }

    [Fact]
    public async Task Login_Should_Fail_For_Inactive_User()
    {
        await using var fixture = await AuthTestFactory.CreateAsync();
        var seed = await fixture.SeedInactiveUserAsync();

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            restaurantCode = seed.RestaurantCode,
            mobileNumber = seed.MobileNumber,
            password = seed.Password
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(problem);
        Assert.Equal("Invalid credentials.", problem!.Detail);
    }

    [Fact]
    public async Task Login_Should_Fail_For_Inactive_Restaurant()
    {
        await using var fixture = await AuthTestFactory.CreateAsync();
        var seed = await fixture.SeedInactiveRestaurantUserAsync();

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            restaurantCode = seed.RestaurantCode,
            mobileNumber = seed.MobileNumber,
            password = seed.Password
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(problem);
        Assert.Equal("Invalid credentials.", problem!.Detail);
    }

    [Fact]
    public async Task Refresh_Should_Rotate_Refresh_Token()
    {
        await using var fixture = await AuthTestFactory.CreateAsync();
        var seed = await fixture.SeedActiveUserAsync();

        var login = await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            restaurantCode = seed.RestaurantCode,
            mobileNumber = seed.MobileNumber,
            password = seed.Password
        });
        login.EnsureSuccessStatusCode();
        var loginPayload = await login.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(loginPayload);
        Assert.Equal(seed.UserId, loginPayload!.UserId);

        var oldPlainToken = loginPayload!.RefreshToken;
        var oldHashedToken = RefreshTokenHash.Compute(oldPlainToken);

        var refresh = await fixture.Client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = oldPlainToken
        });
        refresh.EnsureSuccessStatusCode();

        var refreshPayload = await refresh.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(refreshPayload);
        Assert.Equal(seed.UserId, refreshPayload!.UserId);
        Assert.NotEqual(oldPlainToken, refreshPayload!.RefreshToken);

        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

        var oldToken = await context.RefreshTokens.SingleAsync(token => token.TokenHash == oldHashedToken);
        Assert.NotNull(oldToken.RevokedAt);
        Assert.False(string.IsNullOrWhiteSpace(oldToken.ReplacedByTokenHash));
        Assert.Equal(RefreshTokenHash.Compute(refreshPayload.RefreshToken), oldToken.ReplacedByTokenHash);

        var newToken = await context.RefreshTokens.SingleAsync(token => token.TokenHash == RefreshTokenHash.Compute(refreshPayload.RefreshToken));
        Assert.Null(newToken.RevokedAt);
    }

    [Fact]
    public async Task Logout_Should_Revoke_Refresh_Token_And_Be_Idempotent()
    {
        await using var fixture = await AuthTestFactory.CreateAsync();
        var seed = await fixture.SeedActiveUserAsync();

        var login = await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            restaurantCode = seed.RestaurantCode,
            mobileNumber = seed.MobileNumber,
            password = seed.Password
        });
        login.EnsureSuccessStatusCode();
        var loginPayload = await login.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(loginPayload);

        var refreshToken = loginPayload!.RefreshToken;
        var logout1 = await fixture.Client.PostAsJsonAsync("/api/v1/auth/logout", new
        {
            refreshToken
        });

        Assert.Equal(HttpStatusCode.NoContent, logout1.StatusCode);

        var logout2 = await fixture.Client.PostAsJsonAsync("/api/v1/auth/logout", new
        {
            refreshToken
        });

        Assert.Equal(HttpStatusCode.NoContent, logout2.StatusCode);

        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var token = await context.RefreshTokens.SingleAsync(entity => entity.TokenHash == RefreshTokenHash.Compute(refreshToken));
        Assert.NotNull(token.RevokedAt);
    }

    [Fact]
    public async Task Me_Should_Return_Claim_Derived_Context()
    {
        await using var fixture = await AuthTestFactory.CreateAsync();
        var seed = await fixture.SeedActiveUserAsync();

        var login = await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            restaurantCode = seed.RestaurantCode,
            mobileNumber = seed.MobileNumber,
            password = seed.Password
        });
        login.EnsureSuccessStatusCode();
        var loginPayload = await login.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(loginPayload);

        fixture.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginPayload!.AccessToken);

        var me = await fixture.Client.GetAsync("/api/v1/auth/me");
        me.EnsureSuccessStatusCode();

        var payload = await me.Content.ReadFromJsonAsync<AuthUserContextDto>();
        Assert.NotNull(payload);
        Assert.Equal(seed.UserId, payload!.UserId);
        Assert.Equal(seed.RestaurantId, payload.RestaurantId);
        Assert.Equal(seed.RestaurantCode, payload.RestaurantCode);
        Assert.Equal(seed.BranchId, payload.BranchId);
        Assert.Equal(seed.FullName, payload.FullName);
        Assert.Equal(seed.MobileNumber, payload.MobileNumber);
        Assert.Contains("Cashier", payload.Roles);
        Assert.Contains("Billing.Manage", payload.Permissions);
        Assert.Equal("Cashier", payload.ActiveRole);
    }

    [Fact]
    public async Task Health_Should_Remain_Anonymous()
    {
        await using var fixture = await AuthTestFactory.CreateAsync();

        var response = await fixture.Client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task JwtBearer_Should_Be_Registered_In_The_Test_Host()
    {
        await using var fixture = await AuthTestFactory.CreateAsync();

        var provider = fixture.Services.GetRequiredService<IAuthenticationSchemeProvider>();
        var schemes = await provider.GetAllSchemesAsync();

        Assert.Contains(schemes, scheme => string.Equals(scheme.Name, "Bearer", StringComparison.Ordinal));
    }

    [Fact]
    public async Task JwtBearer_Should_Authenticate_Access_Token_In_The_Test_Host()
    {
        await using var fixture = await AuthTestFactory.CreateAsync();
        var seed = await fixture.SeedActiveUserAsync();

        var login = await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            restaurantCode = seed.RestaurantCode,
            mobileNumber = seed.MobileNumber,
            password = seed.Password
        });

        login.EnsureSuccessStatusCode();
        var loginPayload = await login.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(loginPayload);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = fixture.Services
        };
        httpContext.Request.Headers.Authorization = $"Bearer {loginPayload!.AccessToken}";

        await using var scope = fixture.Services.CreateAsyncScope();
        var authenticationService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
        var result = await authenticationService.AuthenticateAsync(httpContext, "Bearer");

        Assert.True(result.Succeeded, result.Failure?.Message ?? "Authentication failed.");
        Assert.NotNull(result.Principal);
        Assert.True(result.Principal!.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task Login_Should_Persist_Refresh_Token_As_Hash_Only()
    {
        await using var fixture = await AuthTestFactory.CreateAsync();
        var seed = await fixture.SeedActiveUserAsync();

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            restaurantCode = seed.RestaurantCode,
            mobileNumber = seed.MobileNumber,
            password = seed.Password
        });
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(payload);

        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
        var tokenHash = RefreshTokenHash.Compute(payload!.RefreshToken);
        var stored = await context.RefreshTokens.SingleAsync(token => token.TokenHash == tokenHash);

        Assert.Equal(tokenHash, stored.TokenHash);
        Assert.NotEqual(payload.RefreshToken, stored.TokenHash);
    }

    private sealed class AuthTestFactory : WebApplicationFactory<Program>, IAsyncDisposable
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly SaveChangesCounterInterceptor _saveChangesCounter = new();
        private HttpClient? _client;
        private bool _initialized;

        public HttpClient Client => _client ??= CreateClient();

        public int SaveChangesCount => _saveChangesCounter.Count;

        public static async Task<AuthTestFactory> CreateAsync()
        {
            var factory = new AuthTestFactory();
            await factory.InitializeAsync();
            return factory;
        }

        public async Task InitializeAsync()
        {
            if (_initialized)
            {
                return;
            }

            await _connection.OpenAsync();
            _ = Services;
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();
            await context.Database.EnsureCreatedAsync();
            _initialized = true;
        }

        public void ResetSaveChangesCount()
        {
            _saveChangesCounter.Reset();
        }

        public async Task<AuthSeedResult> SeedActiveUserAsync()
        {
        return await SeedUserAsync();
    }

    public async Task<AuthSeedResult> SeedIndianUserAsync()
    {
            return await SeedUserAsync(
                restaurantCode: "RESTIN01",
                countryCode: "IN",
                mobileNumber: "9876543210");
    }

        public async Task<AuthSeedResult> SeedInactiveUserAsync()
        {
            return await SeedUserAsync(userStatus: UserStatus.Inactive);
        }

        public async Task<AuthSeedResult> SeedInactiveRestaurantUserAsync()
        {
            return await SeedUserAsync(restaurantStatus: RestaurantStatus.Inactive);
        }

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
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
                services.AddSingleton(_saveChangesCounter);
                services.AddDbContext<BillSoftDbContext>((_, options) =>
                {
                    options.UseSqlite(_connection);
                    options.AddInterceptors(_saveChangesCounter);
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

        public new ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        private async Task<AuthSeedResult> SeedUserAsync(
            RestaurantStatus restaurantStatus = RestaurantStatus.Active,
            UserStatus userStatus = UserStatus.Active,
            BranchStatus branchStatus = BranchStatus.Active,
            string restaurantCode = "REST001",
            string countryCode = "SG",
            string mobileNumber = "90000001")
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BillSoftDbContext>();

            await new FoundationSeedService(context).SeedAsync();

            var restaurant = new Restaurant
            {
                Name = "Test Restaurant",
                Status = restaurantStatus
            };
            restaurant.SetRestaurantCode(restaurantCode);
            restaurant.SetCountryProfile(countryCode);

            var profile = CountryProfileCatalog.GetRequired(countryCode);

            var branch = new Branch
            {
                Name = "Main Branch",
                RestaurantId = restaurant.RestaurantId,
                Status = branchStatus,
                CountryCode = profile.CountryCode,
                TimeZoneId = profile.TimeZoneId,
                CurrencyCode = profile.CurrencyCode
            };

            var user = new User
            {
                RestaurantId = restaurant.RestaurantId,
                BranchId = branch.BranchId,
                FullName = "Test User",
                Status = userStatus
            };
            user.SetMobileNumber(countryCode, mobileNumber);

            var hasher = new PasswordHasher<User>();
            user.PasswordHash = hasher.HashPassword(user, "Passw0rd!Passw0rd!");

            var cashierRole = await context.Roles.SingleAsync(role => role.RestaurantId == null && role.Name == "Cashier");

            context.Restaurants.Add(restaurant);
            context.Branches.Add(branch);
            context.Users.Add(user);
            context.UserRoles.Add(new UserRole
            {
                UserRoleId = Guid.NewGuid(),
                UserId = user.UserId,
                RoleId = cashierRole.RoleId,
                AssignedAt = DateTimeOffset.UtcNow
            });

            await context.SaveChangesAsync();

        return new AuthSeedResult(restaurant.RestaurantId, branch.BranchId, user.UserId, restaurant.NormalizedRestaurantCode, user.MobileNumber, "Passw0rd!Passw0rd!", user.FullName);
    }
    }

    private sealed class SaveChangesCounterInterceptor : SaveChangesInterceptor
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public void Reset() => Interlocked.Exchange(ref _count, 0);

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            Interlocked.Increment(ref _count);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed record AuthSeedResult(
        Guid RestaurantId,
        Guid BranchId,
        Guid UserId,
        string RestaurantCode,
        string MobileNumber,
        string Password,
        string FullName);

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

    private sealed record AuthUserContextDto(
        Guid UserId,
        Guid RestaurantId,
        string RestaurantCode,
        Guid? BranchId,
        string FullName,
        string MobileNumber,
        string[] Roles,
        string[] Permissions,
        string ActiveRole);

    private sealed record ProblemDetailsDto(string? Type, string? Title, int? Status, string? Detail, string? Instance);
}
