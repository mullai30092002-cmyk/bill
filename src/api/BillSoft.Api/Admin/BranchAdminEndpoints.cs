using System.Security.Claims;
using BillSoft.Api.Security;
using BillSoft.Application.Auth;
using BillSoft.Application.Restaurants;
using BillSoft.Domain.Security;
using Microsoft.AspNetCore.Http;

namespace BillSoft.Api.Admin;

public static class BranchAdminEndpoints
{
    public static IEndpointRouteBuilder MapBranchAdminEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/admin/branches")
            .WithTags("AdminBranches")
            .RequireAuthorization();

        group.MapGet("", ListAsync)
            .RequireAnyPermission(SystemPermissions.BranchManage, SystemPermissions.UserManage);

        group.MapGet("/{branchId:guid}", GetAsync)
            .RequireAnyPermission(SystemPermissions.BranchManage, SystemPermissions.UserManage);

        group.MapPost("", CreateAsync)
            .RequirePermission(SystemPermissions.BranchManage);

        group.MapPut("/{branchId:guid}", UpdateAsync)
            .RequirePermission(SystemPermissions.BranchManage);

        group.MapPost("/{branchId:guid}/activate", ActivateAsync)
            .RequirePermission(SystemPermissions.BranchManage);

        group.MapPost("/{branchId:guid}/deactivate", DeactivateAsync)
            .RequirePermission(SystemPermissions.BranchManage);

        return app;
    }

    private static async Task<IResult> ListAsync(
        string? status,
        string? search,
        ClaimsPrincipal principal,
        IAuthService authService,
        IBranchAdminReadService branchAdminReadService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await branchAdminReadService.ListAsync(
                authService.GetCurrentUserContext(principal),
                new BranchListQuery(status, search),
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> GetAsync(
        Guid branchId,
        ClaimsPrincipal principal,
        IAuthService authService,
        IBranchAdminReadService branchAdminReadService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await branchAdminReadService.GetAsync(
                authService.GetCurrentUserContext(principal),
                branchId,
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> CreateAsync(
        CreateBranchRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IBranchAdminMutationService branchAdminMutationService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await branchAdminMutationService.CreateAsync(
                authService.GetCurrentUserContext(principal),
                request ?? throw new InvalidOperationException("Request body is required."),
                cancellationToken),
            result => Results.Created($"/api/v1/admin/branches/{result.BranchId}", result));
    }

    private static async Task<IResult> UpdateAsync(
        Guid branchId,
        UpdateBranchRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IBranchAdminMutationService branchAdminMutationService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await branchAdminMutationService.UpdateAsync(
                authService.GetCurrentUserContext(principal),
                branchId,
                request ?? throw new InvalidOperationException("Request body is required."),
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> ActivateAsync(
        Guid branchId,
        ClaimsPrincipal principal,
        IAuthService authService,
        IBranchAdminMutationService branchAdminMutationService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await branchAdminMutationService.ActivateAsync(
                authService.GetCurrentUserContext(principal),
                branchId,
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> DeactivateAsync(
        Guid branchId,
        ClaimsPrincipal principal,
        IAuthService authService,
        IBranchAdminMutationService branchAdminMutationService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await branchAdminMutationService.DeactivateAsync(
                authService.GetCurrentUserContext(principal),
                branchId,
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
