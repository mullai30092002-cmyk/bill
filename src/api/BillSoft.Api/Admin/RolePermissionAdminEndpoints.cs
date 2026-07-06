using System.Security.Claims;
using BillSoft.Api.Security;
using BillSoft.Application.Auth;
using BillSoft.Application.Security;
using BillSoft.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace BillSoft.Api.Admin;

public static class RolePermissionAdminEndpoints
{
    public static IEndpointRouteBuilder MapRolePermissionAdminEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/admin")
            .WithTags("AdminRolesPermissions")
            .RequireAuthorization();

        group.MapGet("/roles", ListRolesAsync)
            .RequireAnyPermission(SystemPermissions.RoleManage, SystemPermissions.UserManage);

        group.MapGet("/roles/{roleId:guid}", GetRoleAsync)
            .RequireAnyPermission(SystemPermissions.RoleManage, SystemPermissions.UserManage);

        group.MapGet("/permissions", GetPermissionsAsync)
            .RequireAnyPermission(SystemPermissions.PermissionView, SystemPermissions.RoleManage);

        return app;
    }

    private static async Task<IResult> ListRolesAsync(
        ClaimsPrincipal principal,
        IAuthService authService,
        IRolePermissionReadService rolePermissionReadService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await rolePermissionReadService.ListRolesAsync(
                authService.GetCurrentUserContext(principal),
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> GetRoleAsync(
        Guid roleId,
        ClaimsPrincipal principal,
        IAuthService authService,
        IRolePermissionReadService rolePermissionReadService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await rolePermissionReadService.GetRoleAsync(
                authService.GetCurrentUserContext(principal),
                roleId,
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> GetPermissionsAsync(
        IRolePermissionReadService rolePermissionReadService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await rolePermissionReadService.GetPermissionCatalogAsync(cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> ExecuteAsync<T>(
        Func<Task<T>> action,
        Func<T, IResult> onSuccess)
    {
        try
        {
            return onSuccess(await action());
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private static IResult BadRequest(string detail) =>
        Results.Problem(
            title: "Bad Request",
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest,
            type: "https://datatracker.ietf.org/doc/html/rfc7807");

    private static IResult NotFound(string detail) =>
        Results.Problem(
            title: "Not Found",
            detail: detail,
            statusCode: StatusCodes.Status404NotFound,
            type: "https://datatracker.ietf.org/doc/html/rfc7807");
}
