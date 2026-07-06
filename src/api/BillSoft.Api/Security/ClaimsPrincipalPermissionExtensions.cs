using System.Security.Claims;
using BillSoft.Domain.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace BillSoft.Api.Security;

public static class ClaimsPrincipalPermissionExtensions
{
    public static bool HasPermission(this ClaimsPrincipal principal, string permissionCode)
    {
        return principal.HasAnyPermission(permissionCode);
    }

    public static bool HasAnyPermission(this ClaimsPrincipal principal, params string[] permissionCodes)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (permissionCodes is null || permissionCodes.Length == 0)
        {
            return false;
        }

        var normalizedCodes = permissionCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .ToArray();

        if (normalizedCodes.Length == 0)
        {
            return false;
        }

        return principal.FindAll(AuthClaims.Permission)
            .Any(claim => normalizedCodes.Any(code => string.Equals(claim.Value.Trim(), code, StringComparison.OrdinalIgnoreCase)));
    }

    public static bool HasRole(this ClaimsPrincipal principal, string roleName)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return false;
        }

        return principal.FindAll(ClaimTypes.Role)
            .Any(claim => string.Equals(claim.Value.Trim(), roleName.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static RouteHandlerBuilder RequirePermission(this RouteHandlerBuilder builder, string permissionCode)
    {
        return builder.RequireAnyPermission(permissionCode);
    }

    public static RouteHandlerBuilder RequireAnyPermission(this RouteHandlerBuilder builder, params string[] permissionCodes)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddEndpointFilter(async (context, next) =>
        {
            var principal = context.HttpContext.User;
            if (principal.Identity?.IsAuthenticated != true)
            {
                return Results.Unauthorized();
            }

            if (!principal.HasAnyPermission(permissionCodes))
            {
                return Results.Forbid();
            }

            return await next(context);
        });
    }
}
