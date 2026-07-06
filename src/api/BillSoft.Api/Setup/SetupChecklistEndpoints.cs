using System.Security.Claims;
using BillSoft.Api.Security;
using BillSoft.Application.Auth;
using BillSoft.Application.Setup;
using BillSoft.Domain.Security;
using BillSoft.Domain.Restaurants;
using BillSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace BillSoft.Api.Setup;

public static class SetupChecklistEndpoints
{
    public static IEndpointRouteBuilder MapSetupEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/setup")
            .WithTags("Setup")
            .RequireAuthorization();

        group.MapGet("/checklist", GetChecklistAsync)
            .RequireAnyPermission(SystemPermissions.ReportView, SystemPermissions.BranchManage, SystemPermissions.UserManage);

        group.MapPut("/business-type", UpdateBusinessTypeAsync)
            .RequireAnyPermission(SystemPermissions.BranchManage, SystemPermissions.UserManage);

        return app;
    }

    private static async Task<IResult> GetChecklistAsync(
        Guid? branchId,
        ClaimsPrincipal principal,
        IAuthService authService,
        ISetupChecklistService setupChecklistService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await setupChecklistService.GetSetupChecklistAsync(
                authService.GetCurrentUserContext(principal),
                branchId,
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> UpdateBusinessTypeAsync(
        SetupBusinessTypeRequest request,
        ClaimsPrincipal principal,
        IAuthService authService,
        BillSoftDbContext context,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () =>
            {
                if (!Enum.TryParse<RestaurantBusinessType>(request.BusinessType, ignoreCase: true, out var parsedBusinessType))
                {
                    throw new InvalidOperationException("Business type is invalid.");
                }

                var currentUser = authService.GetCurrentUserContext(principal);
                var restaurant = await context.Restaurants.SingleOrDefaultAsync(
                    entity => entity.RestaurantId == currentUser.RestaurantId,
                    cancellationToken) ?? throw new KeyNotFoundException("Restaurant not found.");

                restaurant.BusinessType = parsedBusinessType;
                restaurant.MarkUpdated();
                await context.SaveChangesAsync(cancellationToken);

                return Results.NoContent();
            },
            result => result);
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

    private sealed record SetupBusinessTypeRequest(string BusinessType);
}
