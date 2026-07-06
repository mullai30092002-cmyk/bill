using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BillSoft.Application.Auth;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Security;
using BillSoft.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BillSoft.Infrastructure.Auth;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value ?? throw new InvalidOperationException("Jwt options are missing.");
    }

    public AuthTokenResult CreateAccessToken(
        User user,
        Restaurant restaurant,
        Branch? branch,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> permissions,
        string sessionId,
        string? activeRole = null,
        bool mustChangePassword = false)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(restaurant);

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("SessionId is required.", nameof(sessionId));
        }

        var signingKey = ResolveSigningKey();
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(_options.AccessTokenLifetimeMinutes);
        var claims = BuildClaims(user, restaurant, branch, roles, permissions, sessionId, activeRole, mustChangePassword);

        var token = new JwtSecurityToken(
            issuer: ResolveRequiredValue(_options.Issuer, "Issuer"),
            audience: ResolveRequiredValue(_options.Audience, "Audience"),
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        var encodedToken = new JwtSecurityTokenHandler().WriteToken(token);
        return new AuthTokenResult(encodedToken, expiresAt);
    }

    private SecurityKey ResolveSigningKey()
    {
        return JwtSigningKey.Create(_options.SigningKey);
    }

    private static string ResolveRequiredValue(string value, string settingName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Jwt:{settingName} is required before creating tokens.");
        }

        return value.Trim();
    }

    private static IReadOnlyCollection<Claim> BuildClaims(
        User user,
        Restaurant restaurant,
        Branch? branch,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> permissions,
        string sessionId,
        string? activeRole,
        bool mustChangePassword)
    {
        var normalizedRestaurantCode = string.IsNullOrWhiteSpace(restaurant.NormalizedRestaurantCode)
            ? restaurant.RestaurantCode.Trim().ToUpperInvariant()
            : restaurant.NormalizedRestaurantCode.Trim().ToUpperInvariant();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(AuthClaims.RestaurantId, restaurant.RestaurantId.ToString()),
            new(AuthClaims.RestaurantCode, normalizedRestaurantCode),
            new(AuthClaims.SessionId, sessionId.Trim()),
            new(AuthClaims.MustChangePassword, mustChangePassword ? bool.TrueString : bool.FalseString),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        if (branch is not null)
        {
            claims.Add(new Claim(AuthClaims.BranchId, branch.BranchId.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(user.MobileNumber))
        {
            claims.Add(new Claim(ClaimTypes.MobilePhone, user.MobileNumber.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, user.Email.Trim()));
        }

        claims.AddRange(roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(role => new Claim(ClaimTypes.Role, role.Trim())));

        claims.AddRange(permissions
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(permission => new Claim(AuthClaims.Permission, permission.Trim())));

        if (!string.IsNullOrWhiteSpace(activeRole))
        {
            claims.Add(new Claim(AuthClaims.ActiveRole, activeRole.Trim()));
        }

        return claims;
    }
}
