using System.Security.Claims;
using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using BillSoft.Application.Auth;
using BillSoft.Domain.Auditing;
using BillSoft.Domain.Localization;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Security;
using BillSoft.Domain.Users;
using BillSoft.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BillSoft.Infrastructure.Auth;

public sealed class AuthService : IAuthService
{
    private static readonly string[] RolePriority =
    [
        "SuperAdmin",
        "RestaurantOwner",
        "Admin",
        "Cashier",
        "Waiter",
        "KitchenUser",
        "InventoryUser",
        "AccountsUser"
    ];

    private readonly BillSoftDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        BillSoftDbContext context,
        IPasswordHasher<User> passwordHasher,
        IJwtTokenService jwtTokenService,
        IOptions<JwtOptions> jwtOptions,
        ILogger<AuthService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
        _jwtOptions = jwtOptions?.Value ?? throw new InvalidOperationException("Jwt options are missing.");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest? request, string ipAddress, CancellationToken cancellationToken)
    {
        var resolvedIpAddress = ResolveIpAddress(ipAddress);
        if (request is null)
        {
            await WriteFailedLoginAuditAsync(
                resolvedIpAddress,
                null,
                null,
                null,
                "Invalid credentials.",
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Invalid credentials.");
        }

        var normalizedRestaurantCode = NormalizeRestaurantCode(request.RestaurantCode);
        var password = request.Password;

        if (string.IsNullOrWhiteSpace(normalizedRestaurantCode) ||
            string.IsNullOrWhiteSpace(password))
        {
            await WriteFailedLoginAuditAsync(
                resolvedIpAddress,
                null,
                null,
                null,
                "Invalid credentials.",
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Invalid credentials.");
        }

        var restaurant = await _context.Restaurants
            .AsNoTracking()
            .SingleOrDefaultAsync(entity =>
                entity.NormalizedRestaurantCode == normalizedRestaurantCode &&
                entity.Status == RestaurantStatus.Active,
                cancellationToken);

        if (restaurant is null)
        {
            await WriteFailedLoginAuditAsync(resolvedIpAddress, null, null, null, "Invalid credentials.", cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Invalid credentials.");
        }

        NormalizedMobileNumber normalizedMobileNumber;
        try
        {
            normalizedMobileNumber = MobileNumberNormalizer.Normalize(
                CountryProfileCatalog.GetRequired(restaurant.CountryCode),
                request.MobileNumber);
        }
        catch (InvalidOperationException)
        {
            await WriteFailedLoginAuditAsync(resolvedIpAddress, restaurant, null, null, "Invalid credentials.", cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Invalid credentials.");
        }

        var user = await _context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(entity =>
                entity.RestaurantId == restaurant.RestaurantId &&
                entity.MobileE164 == normalizedMobileNumber.E164 &&
                entity.Status == UserStatus.Active,
                cancellationToken);

        if (user is null)
        {
            await WriteFailedLoginAuditAsync(resolvedIpAddress, restaurant, null, null, "Invalid credentials.", cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Invalid credentials.");
        }

        if (user.BranchId.HasValue)
        {
            var branch = await _context.Branches
                .AsNoTracking()
                .SingleOrDefaultAsync(entity =>
                    entity.BranchId == user.BranchId &&
                    entity.RestaurantId == restaurant.RestaurantId &&
                    entity.Status == BranchStatus.Active,
                    cancellationToken);

            if (branch is null)
            {
                await WriteFailedLoginAuditAsync(resolvedIpAddress, restaurant, null, user, "Invalid credentials.", cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                throw new InvalidOperationException("Invalid credentials.");
            }
        }

        var passwordVerification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash ?? string.Empty, password);
        if (passwordVerification == PasswordVerificationResult.Failed)
        {
            await WriteFailedLoginAuditAsync(resolvedIpAddress, restaurant, null, user, "Invalid credentials.", cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Invalid credentials.");
        }

        var roles = await ResolveRoleNamesAsync(user.UserId, cancellationToken);
        if (roles.Count == 0)
        {
            await WriteFailedLoginAuditAsync(resolvedIpAddress, restaurant, null, user, "Invalid credentials.", cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Invalid credentials.");
        }

        var permissions = await ResolvePermissionCodesAsync(user.UserId, cancellationToken);
        var activeRole = SelectPreferredRole(roles);
        var branchForResponse = user.BranchId.HasValue
            ? await _context.Branches.AsNoTracking().SingleOrDefaultAsync(entity => entity.BranchId == user.BranchId, cancellationToken)
            : null;

        var session = await IssueSessionAsync(
            user,
            restaurant,
            branchForResponse,
            roles,
            permissions,
            activeRole,
            resolvedIpAddress,
            cancellationToken);

        return session;
    }

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest? request, string ipAddress, CancellationToken cancellationToken)
    {
        var resolvedIpAddress = ResolveIpAddress(ipAddress);
        if (request is null || string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            await WriteFailedRefreshAuditAsync(resolvedIpAddress, null, null, null, "Invalid or expired refresh token.", cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Invalid or expired refresh token.");
        }

        var tokenHash = RefreshTokenHash.Compute(request.RefreshToken);
        var storedToken = await _context.RefreshTokens
            .SingleOrDefaultAsync(entity => entity.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null || storedToken.RevokedAt is not null || storedToken.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            await WriteFailedRefreshAuditAsync(resolvedIpAddress, null, null, null, "Invalid or expired refresh token.", cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Invalid or expired refresh token.");
        }

        var restaurant = await _context.Restaurants
            .AsNoTracking()
            .SingleOrDefaultAsync(entity =>
                entity.RestaurantId == storedToken.RestaurantId &&
                entity.Status == RestaurantStatus.Active,
                cancellationToken);
        var user = await _context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(entity =>
                entity.UserId == storedToken.UserId &&
                entity.RestaurantId == storedToken.RestaurantId &&
                entity.Status == UserStatus.Active,
                cancellationToken);
        var branch = storedToken.BranchId.HasValue
            ? await _context.Branches
                .AsNoTracking()
                .SingleOrDefaultAsync(entity =>
                    entity.BranchId == storedToken.BranchId &&
                    entity.RestaurantId == storedToken.RestaurantId &&
                    entity.Status == BranchStatus.Active,
                    cancellationToken)
            : null;

        if (restaurant is null || user is null || (storedToken.BranchId.HasValue && branch is null))
        {
            storedToken.RevokedAt = DateTimeOffset.UtcNow;
            storedToken.RevokedByIp = resolvedIpAddress;
            storedToken.LastActivityAt = DateTimeOffset.UtcNow;
            await WriteFailedRefreshAuditAsync(
                resolvedIpAddress,
                restaurant,
                branch,
                user,
                "Invalid or expired refresh token.",
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Invalid or expired refresh token.");
        }

        var roles = await ResolveRoleNamesAsync(user.UserId, cancellationToken);
        if (roles.Count == 0)
        {
            storedToken.RevokedAt = DateTimeOffset.UtcNow;
            storedToken.RevokedByIp = resolvedIpAddress;
            storedToken.LastActivityAt = DateTimeOffset.UtcNow;
            await WriteFailedRefreshAuditAsync(
                resolvedIpAddress,
                restaurant,
                branch,
                user,
                "Invalid or expired refresh token.",
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Invalid or expired refresh token.");
        }

        var permissions = await ResolvePermissionCodesAsync(user.UserId, cancellationToken);
        var activeRole = ResolveActiveRole(roles, storedToken.ActiveRole);
        var now = DateTimeOffset.UtcNow;
        var accessToken = _jwtTokenService.CreateAccessToken(
            user,
            restaurant,
            branch,
            roles,
            permissions,
            storedToken.SessionId,
            activeRole,
            mustChangePassword: false);
        var refreshPlain = GenerateRefreshToken();
        var refreshHash = RefreshTokenHash.Compute(refreshPlain);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        storedToken.RevokedAt = now;
        storedToken.RevokedByIp = resolvedIpAddress;
        storedToken.LastActivityAt = now;
        storedToken.ActiveRole = activeRole;
        storedToken.ReplacedByTokenHash = refreshHash;

        var newRefreshToken = new RefreshToken
        {
            RestaurantId = restaurant.RestaurantId,
            BranchId = branch?.BranchId,
            UserId = user.UserId,
            TokenHash = refreshHash,
            ExpiresAt = now.AddDays(_jwtOptions.RefreshTokenLifetimeDays),
            CreatedByIp = resolvedIpAddress,
            SessionId = storedToken.SessionId,
            ActiveRole = activeRole,
            CreatedAt = now,
            LastActivityAt = now
        };

        _context.RefreshTokens.Add(newRefreshToken);
        await WriteAuditAsync(
            action: "Authentication.RefreshSucceeded",
            restaurant,
            branch,
            user,
            "Refresh token rotated.",
            entityId: storedToken.RefreshTokenId.ToString(),
            resolvedIpAddress,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return BuildAuthResponse(
            accessToken,
            refreshPlain,
            newRefreshToken.ExpiresAt,
            restaurant,
            branch,
            user,
            roles,
            permissions,
            activeRole);
    }

    public async Task LogoutAsync(RefreshTokenRequest? request, string ipAddress, CancellationToken cancellationToken)
    {
        var resolvedIpAddress = ResolveIpAddress(ipAddress);
        if (request is null || string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return;
        }

        var tokenHash = RefreshTokenHash.Compute(request.RefreshToken);
        var storedToken = await _context.RefreshTokens
            .SingleOrDefaultAsync(entity => entity.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null || storedToken.RevokedAt is not null)
        {
            return;
        }

        var restaurant = await _context.Restaurants.AsNoTracking().SingleOrDefaultAsync(entity => entity.RestaurantId == storedToken.RestaurantId, cancellationToken);
        var user = await _context.Users.AsNoTracking().SingleOrDefaultAsync(entity => entity.UserId == storedToken.UserId, cancellationToken);
        var branch = storedToken.BranchId.HasValue
            ? await _context.Branches.AsNoTracking().SingleOrDefaultAsync(entity => entity.BranchId == storedToken.BranchId, cancellationToken)
            : null;

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        storedToken.RevokedAt = DateTimeOffset.UtcNow;
        storedToken.RevokedByIp = resolvedIpAddress;
        storedToken.LastActivityAt = DateTimeOffset.UtcNow;

        await WriteAuditAsync(
            action: "Authentication.Logout",
            restaurant,
            branch,
            user,
            "Refresh token revoked on logout.",
            entityId: storedToken.RefreshTokenId.ToString(),
            resolvedIpAddress,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public AuthUserContext GetCurrentUserContext(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (principal.Identity?.IsAuthenticated != true)
        {
            throw new InvalidOperationException("Authentication is required.");
        }

        var userId = ResolveRequiredGuid(principal, ClaimTypes.NameIdentifier, JwtRegisteredClaimNames.Sub, "user");
        var restaurantId = ResolveRequiredGuid(principal, AuthClaims.RestaurantId, null, "restaurant");
        var restaurantCode = ResolveRequiredClaim(principal, AuthClaims.RestaurantCode);
        var branchId = ResolveOptionalGuid(principal, AuthClaims.BranchId);
        var fullName = ResolveRequiredClaim(principal, ClaimTypes.Name);
        var mobileNumber = ResolveRequiredClaim(principal, ClaimTypes.MobilePhone);
        var roles = principal.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var permissions = principal.FindAll(AuthClaims.Permission)
            .Select(claim => claim.Value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var activeRole = ResolveRequiredClaim(principal, AuthClaims.ActiveRole);

        return new AuthUserContext(
            userId,
            restaurantId,
            restaurantCode,
            branchId,
            fullName,
            mobileNumber,
            roles,
            permissions,
            activeRole);
    }

    private async Task<AuthResponse> IssueSessionAsync(
        User user,
        Restaurant restaurant,
        Branch? branch,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> permissions,
        string activeRole,
        string ipAddress,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var sessionId = Guid.NewGuid().ToString("N");
        var refreshPlain = GenerateRefreshToken();
        var refreshHash = RefreshTokenHash.Compute(refreshPlain);
        var refreshExpiresAt = now.AddDays(_jwtOptions.RefreshTokenLifetimeDays);

        var accessToken = _jwtTokenService.CreateAccessToken(
            user,
            restaurant,
            branch,
            roles,
            permissions,
            sessionId,
            activeRole,
            user.Status != UserStatus.Active);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        _context.RefreshTokens.Add(new RefreshToken
        {
            RestaurantId = restaurant.RestaurantId,
            BranchId = branch?.BranchId,
            UserId = user.UserId,
            TokenHash = refreshHash,
            ExpiresAt = refreshExpiresAt,
            CreatedByIp = ipAddress,
            SessionId = sessionId,
            ActiveRole = activeRole,
            CreatedAt = now,
            LastActivityAt = now
        });

        await WriteAuditAsync(
            action: "Authentication.LoginSucceeded",
            restaurant,
            branch,
            user,
            "User authenticated successfully.",
            entityId: user.UserId.ToString(),
            ipAddress,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return BuildAuthResponse(
            accessToken,
            refreshPlain,
            refreshExpiresAt,
            restaurant,
            branch,
            user,
            roles,
            permissions,
            activeRole);
    }

    private static AuthResponse BuildAuthResponse(
        AuthTokenResult accessToken,
        string refreshPlain,
        DateTimeOffset refreshExpiresAt,
        Restaurant restaurant,
        Branch? branch,
        User user,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> permissions,
        string activeRole)
    {
        var orderedRoles = OrderRoles(roles);
        var orderedPermissions = permissions
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(permission => permission, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new AuthResponse(
            accessToken.AccessToken,
            refreshPlain,
            accessToken.ExpiresAtUtc,
            refreshExpiresAt,
            user.UserId,
            restaurant.RestaurantId,
            restaurant.NormalizedRestaurantCode,
            restaurant.CountryCode,
            restaurant.CurrencyCode,
            restaurant.TimeZoneId,
            branch?.BranchId,
            user.FullName,
            user.MobileNumber,
            orderedRoles,
            orderedPermissions,
            activeRole);
    }

    private async Task<IReadOnlyCollection<string>> ResolveRoleNamesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var roles = await (
                from userRole in _context.UserRoles.AsNoTracking()
                join role in _context.Roles.AsNoTracking() on userRole.RoleId equals role.RoleId
                where userRole.UserId == userId
                select role.Name)
            .ToListAsync(cancellationToken);

        return OrderRoles(roles);
    }

    private async Task<IReadOnlyCollection<string>> ResolvePermissionCodesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var permissions = await (
                from userRole in _context.UserRoles.AsNoTracking()
                join role in _context.Roles.AsNoTracking() on userRole.RoleId equals role.RoleId
                join rolePermission in _context.RolePermissions.AsNoTracking() on role.RoleId equals rolePermission.RoleId
                join permission in _context.Permissions.AsNoTracking() on rolePermission.PermissionId equals permission.PermissionId
                where userRole.UserId == userId
                select permission.Code)
            .ToListAsync(cancellationToken);

        return permissions
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(permission => permission, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyCollection<string> OrderRoles(IEnumerable<string> roles)
    {
        return roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(role => GetRolePriority(role))
            .ThenBy(role => role, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static int GetRolePriority(string role)
    {
        var index = Array.FindIndex(RolePriority, candidate =>
            string.Equals(candidate, role, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? int.MaxValue : index;
    }

    private static string SelectPreferredRole(IReadOnlyCollection<string> roles)
    {
        foreach (var candidate in RolePriority)
        {
            var matched = roles.FirstOrDefault(role =>
                string.Equals(role, candidate, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(matched))
            {
                return matched!;
            }
        }

        throw new InvalidOperationException("Invalid credentials.");
    }

    private async Task WriteFailedLoginAuditAsync(
        string ipAddress,
        Restaurant? restaurant,
        Branch? branch,
        User? user,
        string reason,
        CancellationToken cancellationToken)
    {
        await WriteAuditAsync(
            action: "Authentication.LoginFailed",
            restaurant,
            branch,
            user,
            reason,
            entityId: user?.UserId.ToString(),
            ipAddress,
            cancellationToken);
    }

    private async Task WriteFailedRefreshAuditAsync(
        string ipAddress,
        Restaurant? restaurant,
        Branch? branch,
        User? user,
        string reason,
        CancellationToken cancellationToken)
    {
        await WriteAuditAsync(
            action: "Authentication.RefreshFailed",
            restaurant,
            branch,
            user,
            reason,
            entityId: user?.UserId.ToString(),
            ipAddress,
            cancellationToken);
    }

    private Task WriteAuditAsync(
        string action,
        Restaurant? restaurant,
        Branch? branch,
        User? user,
        string reason,
        string? entityId,
        string ipAddress,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Auth audit {Action} restaurantId={RestaurantId} branchId={BranchId} userId={UserId} reason={Reason}",
            action,
            restaurant?.RestaurantId,
            branch?.BranchId,
            user?.UserId,
            reason);

        _context.AuditLogs.Add(new AuditLog
        {
            RestaurantId = restaurant?.RestaurantId,
            BranchId = branch?.BranchId,
            UserId = user?.UserId,
            Action = action,
            EntityType = "Authentication",
            EntityId = entityId,
            Reason = reason,
            RestaurantNameSnapshot = restaurant?.Name,
            BranchNameSnapshot = branch?.Name,
            UserNameSnapshot = user?.FullName,
            UserMobileSnapshot = user?.MobileNumber,
            IpAddress = ipAddress,
            CreatedAt = DateTimeOffset.UtcNow
        });

        return Task.CompletedTask;
    }

    private static string NormalizeRestaurantCode(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();

    private static string ResolveIpAddress(string ipAddress)
    {
        return string.IsNullOrWhiteSpace(ipAddress) ? "unknown" : ipAddress.Trim();
    }

    private static Guid ResolveRequiredGuid(ClaimsPrincipal principal, string primaryClaimType, string? fallbackClaimType, string contextName)
    {
        var value = ResolveRequiredClaim(principal, primaryClaimType, fallbackClaimType);
        if (!Guid.TryParse(value, out var result) || result == Guid.Empty)
        {
            throw new InvalidOperationException($"Invalid {contextName} context.");
        }

        return result;
    }

    private static Guid? ResolveOptionalGuid(ClaimsPrincipal principal, string claimType)
    {
        var value = principal.FindFirstValue(claimType);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Guid.TryParse(value, out var result) && result != Guid.Empty ? result : null;
    }

    private static string ResolveRequiredClaim(ClaimsPrincipal principal, string primaryClaimType, string? fallbackClaimType = null)
    {
        var value = principal.FindFirstValue(primaryClaimType);
        if (string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(fallbackClaimType))
        {
            value = principal.FindFirstValue(fallbackClaimType);
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Authentication context is missing.");
        }

        return value.Trim();
    }

    private static string GenerateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    private static string ResolveActiveRole(IReadOnlyCollection<string> roles, string? persistedActiveRole)
    {
        if (!string.IsNullOrWhiteSpace(persistedActiveRole))
        {
            var persisted = roles.FirstOrDefault(role =>
                string.Equals(role, persistedActiveRole.Trim(), StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(persisted))
            {
                return persisted!;
            }
        }

        return SelectPreferredRole(roles);
    }
}
