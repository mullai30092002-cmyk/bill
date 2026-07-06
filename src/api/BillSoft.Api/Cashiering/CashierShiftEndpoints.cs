using System.Security.Claims;
using BillSoft.Api.Security;
using BillSoft.Application.Auth;
using BillSoft.Application.Cashiering;
using BillSoft.Domain.Security;
using Microsoft.AspNetCore.Http;

namespace BillSoft.Api.Cashiering;

public static class CashierShiftEndpoints
{
    public static IEndpointRouteBuilder MapCashierShiftEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/cashier/shifts")
            .WithTags("CashierShifts")
            .RequireAuthorization();

        group.MapGet("", ListShiftsAsync)
            .RequireAnyPermission(SystemPermissions.CashShiftView, SystemPermissions.CashShiftManage);

        group.MapGet("/current", GetCurrentShiftAsync)
            .RequireAnyPermission(SystemPermissions.CashShiftView, SystemPermissions.CashShiftManage);

        group.MapGet("/{shiftId:guid}", GetShiftAsync)
            .RequireAnyPermission(SystemPermissions.CashShiftView, SystemPermissions.CashShiftManage);

        group.MapPost("/open", OpenShiftAsync)
            .RequirePermission(SystemPermissions.CashShiftManage);

        group.MapPost("/{shiftId:guid}/close", CloseShiftAsync)
            .RequirePermission(SystemPermissions.CashShiftManage);

        return app;
    }

    private static async Task<IResult> ListShiftsAsync(
        DateTime? businessDate,
        Guid? branchId,
        ClaimsPrincipal principal,
        IAuthService authService,
        ICashierShiftService cashierShiftService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await cashierShiftService.ListShiftsAsync(
                authService.GetCurrentUserContext(principal),
                new CashierShiftListQuery(businessDate, branchId),
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> GetCurrentShiftAsync(
        Guid? branchId,
        ClaimsPrincipal principal,
        IAuthService authService,
        ICashierShiftService cashierShiftService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await cashierShiftService.GetCurrentShiftAsync(
                authService.GetCurrentUserContext(principal),
                branchId ?? throw new InvalidOperationException("Branch id is required."),
                cancellationToken),
            result => result is null ? Results.NoContent() : Results.Ok(result));
    }

    private static async Task<IResult> GetShiftAsync(
        Guid shiftId,
        ClaimsPrincipal principal,
        IAuthService authService,
        ICashierShiftService cashierShiftService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await cashierShiftService.GetShiftAsync(
                authService.GetCurrentUserContext(principal),
                shiftId,
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> OpenShiftAsync(
        OpenCashierShiftRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        ICashierShiftService cashierShiftService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await cashierShiftService.OpenShiftAsync(
                authService.GetCurrentUserContext(principal),
                request ?? throw new InvalidOperationException("Request body is required."),
                cancellationToken),
            result => Results.Created($"/api/v1/cashier/shifts/{result.CashierShiftId}", result));
    }

    private static async Task<IResult> CloseShiftAsync(
        Guid shiftId,
        CloseCashierShiftRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        ICashierShiftService cashierShiftService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await cashierShiftService.CloseShiftAsync(
                authService.GetCurrentUserContext(principal),
                shiftId,
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
