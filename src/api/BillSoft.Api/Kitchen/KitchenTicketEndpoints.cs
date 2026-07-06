using System.Security.Claims;
using BillSoft.Api.Security;
using BillSoft.Application.Auth;
using BillSoft.Application.Kitchen;
using BillSoft.Domain.Security;
using Microsoft.AspNetCore.Http;

namespace BillSoft.Api.Kitchen;

public static class KitchenTicketEndpoints
{
    public static IEndpointRouteBuilder MapKitchenTicketEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/kitchen/tickets")
            .WithTags("KitchenTickets")
            .RequireAuthorization();

        group.MapGet("", ListAsync)
            .RequireAnyPermission(SystemPermissions.KitchenTicketView, SystemPermissions.KitchenTicketManage, SystemPermissions.KitchenTicketUpdateStatus);

        group.MapGet("/{ticketId:guid}", GetAsync)
            .RequireAnyPermission(SystemPermissions.KitchenTicketView, SystemPermissions.KitchenTicketManage, SystemPermissions.KitchenTicketUpdateStatus);

        group.MapGet("/{ticketId:guid}/deduction-preview", GetDeductionPreviewAsync)
            .RequireAnyPermission(SystemPermissions.KitchenTicketView, SystemPermissions.KitchenTicketManage, SystemPermissions.KitchenTicketUpdateStatus);

        group.MapPost("", CreateAsync)
            .RequirePermission(SystemPermissions.KitchenTicketManage);

        group.MapPost("/{ticketId:guid}/status", UpdateStatusAsync)
            .RequireAnyPermission(SystemPermissions.KitchenTicketUpdateStatus, SystemPermissions.KitchenTicketManage);

        group.MapPost("/{ticketId:guid}/cancel", CancelAsync)
            .RequirePermission(SystemPermissions.KitchenTicketManage);

        return app;
    }

    private static async Task<IResult> ListAsync(
        Guid? branchId,
        string? status,
        DateTime? from,
        DateTime? to,
        ClaimsPrincipal principal,
        IAuthService authService,
        IKitchenTicketService kitchenTicketService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await kitchenTicketService.ListAsync(
                authService.GetCurrentUserContext(principal),
                new KitchenTicketListQuery(branchId, status, from, to),
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> GetAsync(
        Guid ticketId,
        ClaimsPrincipal principal,
        IAuthService authService,
        IKitchenTicketService kitchenTicketService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await kitchenTicketService.GetAsync(
                authService.GetCurrentUserContext(principal),
                ticketId,
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> GetDeductionPreviewAsync(
        Guid ticketId,
        ClaimsPrincipal principal,
        IAuthService authService,
        IKitchenTicketService kitchenTicketService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await kitchenTicketService.GetDeductionPreviewAsync(
                authService.GetCurrentUserContext(principal),
                ticketId,
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> CreateAsync(
        CreateKitchenTicketRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IKitchenTicketService kitchenTicketService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await kitchenTicketService.CreateAsync(
                authService.GetCurrentUserContext(principal),
                request ?? throw new InvalidOperationException("Request body is required."),
                cancellationToken),
            result => Results.Created($"/api/v1/kitchen/tickets/{result.KitchenTicketId}", result));
    }

    private static async Task<IResult> UpdateStatusAsync(
        Guid ticketId,
        UpdateKitchenTicketStatusRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IKitchenTicketService kitchenTicketService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await kitchenTicketService.UpdateStatusAsync(
                authService.GetCurrentUserContext(principal),
                ticketId,
                request ?? throw new InvalidOperationException("Request body is required."),
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> CancelAsync(
        Guid ticketId,
        CancelKitchenTicketRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IKitchenTicketService kitchenTicketService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await kitchenTicketService.CancelAsync(
                authService.GetCurrentUserContext(principal),
                ticketId,
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
