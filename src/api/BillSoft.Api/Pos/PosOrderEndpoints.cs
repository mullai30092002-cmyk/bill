using System.Security.Claims;
using BillSoft.Api.Security;
using BillSoft.Application.Auth;
using BillSoft.Application.Orders;
using BillSoft.Domain.Security;
using Microsoft.AspNetCore.Http;

namespace BillSoft.Api.Pos;

public static class PosOrderEndpoints
{
    public static IEndpointRouteBuilder MapPosOrderEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/pos/orders")
            .WithTags("PosOrders")
            .RequireAuthorization();

        group.MapGet("", ListAsync)
            .RequireAnyPermission(SystemPermissions.OrderView, SystemPermissions.OrderCreate);

        group.MapGet("/{orderId:guid}", GetAsync)
            .RequireAnyPermission(SystemPermissions.OrderView, SystemPermissions.OrderCreate);

        group.MapPost("", CreateAsync)
            .RequirePermission(SystemPermissions.OrderCreate);

        group.MapPut("/{orderId:guid}", UpdateAsync)
            .RequirePermission(SystemPermissions.OrderCreate);

        group.MapPost("/{orderId:guid}/confirm", ConfirmAsync)
            .RequirePermission(SystemPermissions.OrderCreate);

        group.MapPost("/{orderId:guid}/cancel", CancelAsync)
            .RequireAnyPermission(SystemPermissions.OrderCancel, SystemPermissions.OrderCreate);

        return app;
    }

    private static async Task<IResult> ListAsync(
        Guid? branchId,
        string? status,
        string? orderType,
        DateTime? from,
        DateTime? to,
        string? search,
        ClaimsPrincipal principal,
        IAuthService authService,
        IPosOrderService posOrderService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await posOrderService.ListAsync(
                authService.GetCurrentUserContext(principal),
                new PosOrderListQuery(branchId, status, orderType, from, to, search),
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> GetAsync(
        Guid orderId,
        ClaimsPrincipal principal,
        IAuthService authService,
        IPosOrderService posOrderService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await posOrderService.GetAsync(
                authService.GetCurrentUserContext(principal),
                orderId,
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> CreateAsync(
        CreatePosOrderRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IPosOrderService posOrderService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await posOrderService.CreateAsync(
                authService.GetCurrentUserContext(principal),
                request ?? throw new InvalidOperationException("Request body is required."),
                cancellationToken),
            result => Results.Created($"/api/v1/pos/orders/{result.PosOrderId}", result));
    }

    private static async Task<IResult> UpdateAsync(
        Guid orderId,
        UpdatePosOrderRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IPosOrderService posOrderService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await posOrderService.UpdateAsync(
                authService.GetCurrentUserContext(principal),
                orderId,
                request ?? throw new InvalidOperationException("Request body is required."),
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> ConfirmAsync(
        Guid orderId,
        ClaimsPrincipal principal,
        IAuthService authService,
        IPosOrderService posOrderService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await posOrderService.ConfirmAsync(
                authService.GetCurrentUserContext(principal),
                orderId,
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> CancelAsync(
        Guid orderId,
        CancelPosOrderRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IPosOrderService posOrderService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await posOrderService.CancelAsync(
                authService.GetCurrentUserContext(principal),
                orderId,
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
