using BillSoft.Api.Security;
using BillSoft.Application.Auth;
using BillSoft.Application.Users;
using BillSoft.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace BillSoft.Api.Admin;

public static class UserAdminEndpoints
{
    public static IEndpointRouteBuilder MapUserAdminEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/admin/users")
            .WithTags("AdminUsers")
            .RequireAuthorization();

        group.MapGet("", ListAsync)
            .RequirePermission(SystemPermissions.UserManage);

        group.MapPost("", CreateAsync)
            .RequirePermission(SystemPermissions.UserManage);

        group.MapGet("/{userId:guid}", GetAsync)
            .RequirePermission(SystemPermissions.UserManage);

        group.MapPut("/{userId:guid}", UpdateAsync)
            .RequirePermission(SystemPermissions.UserManage);

        group.MapPut("/{userId:guid}/roles", UpdateRolesAsync)
            .RequirePermission(SystemPermissions.UserManage);

        group.MapPost("/{userId:guid}/activate", ActivateAsync)
            .RequirePermission(SystemPermissions.UserManage);

        group.MapPost("/{userId:guid}/deactivate", DeactivateAsync)
            .RequirePermission(SystemPermissions.UserManage);

        group.MapPost("/{userId:guid}/reset-password", ResetPasswordAsync)
            .RequirePermission(SystemPermissions.UserManage);

        return app;
    }

    private static async Task<IResult> ListAsync(
        string? status,
        Guid? branchId,
        string? search,
        ClaimsPrincipal principal,
        IAuthService authService,
        IUserAdminService userAdminService,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20)
    {
        return await ExecuteAsync(
            async () => await userAdminService.ListAsync(
                authService.GetCurrentUserContext(principal),
                new UserListQuery(status, branchId, search, page, pageSize),
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> CreateAsync(
        CreateUserRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IUserAdminService userAdminService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await userAdminService.CreateAsync(
                authService.GetCurrentUserContext(principal),
                request ?? throw new InvalidOperationException("Request body is required."),
                cancellationToken),
            result => Results.Created($"/api/v1/admin/users/{result.UserId}", result));
    }

    private static async Task<IResult> GetAsync(
        Guid userId,
        ClaimsPrincipal principal,
        IAuthService authService,
        IUserAdminService userAdminService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await userAdminService.GetAsync(
                authService.GetCurrentUserContext(principal),
                userId,
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> UpdateAsync(
        Guid userId,
        UpdateUserRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IUserAdminService userAdminService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await userAdminService.UpdateAsync(
                authService.GetCurrentUserContext(principal),
                userId,
                request ?? throw new InvalidOperationException("Request body is required."),
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> UpdateRolesAsync(
        Guid userId,
        UpdateUserRolesRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IUserAdminService userAdminService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await userAdminService.UpdateRolesAsync(
                authService.GetCurrentUserContext(principal),
                userId,
                request ?? throw new InvalidOperationException("Request body is required."),
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> ActivateAsync(
        Guid userId,
        ClaimsPrincipal principal,
        IAuthService authService,
        IUserAdminService userAdminService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await userAdminService.ActivateAsync(
                authService.GetCurrentUserContext(principal),
                userId,
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> DeactivateAsync(
        Guid userId,
        ClaimsPrincipal principal,
        IAuthService authService,
        IUserAdminService userAdminService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await userAdminService.DeactivateAsync(
                authService.GetCurrentUserContext(principal),
                userId,
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> ResetPasswordAsync(
        Guid userId,
        ResetUserPasswordRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IUserAdminService userAdminService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await userAdminService.ResetPasswordAsync(
                authService.GetCurrentUserContext(principal),
                userId,
                request ?? throw new InvalidOperationException("Request body is required."),
                cancellationToken),
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
