using System.Security.Claims;
using BillSoft.Api.Security;
using BillSoft.Application.Auth;
using BillSoft.Application.Inventory;
using BillSoft.Domain.Security;
using Microsoft.AspNetCore.Http;

namespace BillSoft.Api.Inventory;

public static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/inventory")
            .WithTags("Inventory")
            .RequireAuthorization();

        group.MapGet("/items", ListItemsAsync)
            .RequireAnyPermission(SystemPermissions.InventoryView, SystemPermissions.InventoryAdjust);

        group.MapPost("/items", CreateItemAsync)
            .RequirePermission(SystemPermissions.InventoryAdjust);

        group.MapPut("/items/{itemId:guid}", UpdateItemAsync)
            .RequirePermission(SystemPermissions.InventoryAdjust);

        group.MapGet("/items/{itemId:guid}/movements", ListMovementsAsync)
            .RequireAnyPermission(SystemPermissions.InventoryView, SystemPermissions.InventoryAdjust);

        group.MapPost("/items/{itemId:guid}/movements", RecordMovementAsync)
            .RequirePermission(SystemPermissions.InventoryAdjust);

        group.MapGet("/summary", GetSummaryAsync)
            .RequireAnyPermission(SystemPermissions.InventoryView, SystemPermissions.InventoryAdjust);

        group.MapGet("/batch-productions", ListBatchProductionsAsync)
            .RequireAnyPermission(SystemPermissions.InventoryView, SystemPermissions.InventoryAdjust);

        group.MapPost("/batch-productions", CreateBatchProductionAsync)
            .RequirePermission(SystemPermissions.InventoryAdjust);

        group.MapPost("/prepared-stock/wastage", RecordPreparedStockWastageAsync)
            .RequirePermission(SystemPermissions.InventoryAdjust);

        return app;
    }

    private static async Task<IResult> ListItemsAsync(
        Guid? branchId,
        ClaimsPrincipal principal,
        IAuthService authService,
        IInventoryService inventoryService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await inventoryService.ListItemsAsync(
                authService.GetCurrentUserContext(principal),
                new InventoryItemListQuery(branchId),
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> CreateItemAsync(
        CreateInventoryItemRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IInventoryService inventoryService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await inventoryService.CreateItemAsync(
                authService.GetCurrentUserContext(principal),
                request ?? throw new InvalidOperationException("Request body is required."),
                cancellationToken),
            result => Results.Created($"/api/v1/inventory/items/{result.InventoryItemId}", result));
    }

    private static async Task<IResult> UpdateItemAsync(
        Guid itemId,
        UpdateInventoryItemRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IInventoryService inventoryService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await inventoryService.UpdateItemAsync(
                authService.GetCurrentUserContext(principal),
                itemId,
                request ?? throw new InvalidOperationException("Request body is required."),
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> ListMovementsAsync(
        Guid itemId,
        ClaimsPrincipal principal,
        IAuthService authService,
        IInventoryService inventoryService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await inventoryService.ListMovementsAsync(
                authService.GetCurrentUserContext(principal),
                itemId,
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> RecordMovementAsync(
        Guid itemId,
        CreateInventoryMovementRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IInventoryService inventoryService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await inventoryService.RecordMovementAsync(
                authService.GetCurrentUserContext(principal),
                itemId,
                request ?? throw new InvalidOperationException("Request body is required."),
                cancellationToken),
            result => Results.Created($"/api/v1/inventory/items/{result.InventoryItemId}/movements/{result.InventoryMovementId}", result));
    }

    private static async Task<IResult> GetSummaryAsync(
        Guid? branchId,
        ClaimsPrincipal principal,
        IAuthService authService,
        IInventoryService inventoryService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await inventoryService.GetSummaryAsync(
                authService.GetCurrentUserContext(principal),
                new InventorySummaryQuery(branchId),
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> ListBatchProductionsAsync(
        Guid? branchId,
        DateTime? fromBusinessDate,
        DateTime? toBusinessDate,
        ClaimsPrincipal principal,
        IAuthService authService,
        IInventoryService inventoryService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await inventoryService.ListBatchProductionsAsync(
                authService.GetCurrentUserContext(principal),
                new BatchProductionListQuery(branchId, fromBusinessDate, toBusinessDate),
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> CreateBatchProductionAsync(
        CreateBatchProductionRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IInventoryService inventoryService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await inventoryService.CreateBatchProductionAsync(
                authService.GetCurrentUserContext(principal),
                request ?? throw new InvalidOperationException("Request body is required."),
                cancellationToken),
            result => Results.Created($"/api/v1/inventory/batch-productions/{result.BatchProductionId}", result));
    }

    private static async Task<IResult> RecordPreparedStockWastageAsync(
        RecordPreparedStockWastageRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IInventoryService inventoryService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await inventoryService.RecordPreparedStockWastageAsync(
                authService.GetCurrentUserContext(principal),
                request ?? throw new InvalidOperationException("Request body is required."),
                cancellationToken),
            result => Results.Created($"/api/v1/inventory/items/{result.InventoryItemId}/movements/{result.InventoryMovementId}", result));
    }

    private static async Task<IResult> ExecuteAsync<T>(Func<Task<T>> action, Func<T, IResult> onSuccess)
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
