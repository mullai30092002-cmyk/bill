using System.Security.Claims;
using BillSoft.Api.Security;
using BillSoft.Application.Auth;
using BillSoft.Application.Billing;
using BillSoft.Domain.Security;
using Microsoft.AspNetCore.Http;

namespace BillSoft.Api.Billing;

public static class BillingEndpoints
{
    public static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/billing")
            .WithTags("Billing")
            .RequireAuthorization();

        group.MapGet("/bills", ListBillsAsync)
            .RequireAnyPermission(SystemPermissions.BillingView, SystemPermissions.BillingManage, SystemPermissions.PaymentRecord);

        group.MapGet("/bills/{billId:guid}", GetBillAsync)
            .RequireAnyPermission(SystemPermissions.BillingView, SystemPermissions.BillingManage, SystemPermissions.PaymentRecord);

        group.MapGet("/bills/{billId:guid}/receipt", GetBillReceiptAsync)
            .RequireAnyPermission(SystemPermissions.BillingView, SystemPermissions.BillingManage, SystemPermissions.PaymentRecord);

        group.MapPost("/bills/{billId:guid}/receipt/print-events", RecordBillReceiptPrintEventAsync)
            .RequireAnyPermission(SystemPermissions.BillingView, SystemPermissions.BillingManage, SystemPermissions.PaymentRecord);

        group.MapPost("/bills", CreateBillAsync)
            .RequirePermission(SystemPermissions.BillingManage);

        group.MapPost("/bills/{billId:guid}/cancel", CancelBillAsync)
            .RequirePermission(SystemPermissions.BillingManage);

        group.MapPost("/bills/{billId:guid}/payments", RecordPaymentAsync)
            .RequirePermission(SystemPermissions.PaymentRecord);

        group.MapPost("/payments/{paymentId:guid}/cancel", CancelPaymentAsync)
            .RequirePermission(SystemPermissions.PaymentCancel);

        return app;
    }

    private static async Task<IResult> ListBillsAsync(
        Guid? branchId,
        DateTime? businessDate,
        string? status,
        DateTime? from,
        DateTime? to,
        string? search,
        ClaimsPrincipal principal,
        IAuthService authService,
        IBillingService billingService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await billingService.ListBillsAsync(
                authService.GetCurrentUserContext(principal),
                new BillListQuery(branchId, businessDate, status, from, to, search),
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> GetBillAsync(
        Guid billId,
        ClaimsPrincipal principal,
        IAuthService authService,
        IBillingService billingService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await billingService.GetBillAsync(
                authService.GetCurrentUserContext(principal),
                billId,
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> GetBillReceiptAsync(
        Guid billId,
        ClaimsPrincipal principal,
        IAuthService authService,
        IBillingService billingService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await billingService.GetBillReceiptAsync(
                authService.GetCurrentUserContext(principal),
                billId,
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> RecordBillReceiptPrintEventAsync(
        Guid billId,
        RecordBillReceiptPrintEventRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IBillingService billingService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await billingService.RecordBillReceiptPrintEventAsync(
                authService.GetCurrentUserContext(principal),
                billId,
                request,
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> CreateBillAsync(
        CreateBillRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IBillingService billingService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await billingService.CreateBillAsync(
                authService.GetCurrentUserContext(principal),
                request ?? throw new InvalidOperationException("Request body is required."),
                cancellationToken),
            result => Results.Created($"/api/v1/billing/bills/{result.BillId}", result));
    }

    private static async Task<IResult> CancelBillAsync(
        Guid billId,
        CancelBillRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IBillingService billingService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await billingService.CancelBillAsync(
                authService.GetCurrentUserContext(principal),
                billId,
                request ?? throw new InvalidOperationException("Request body is required."),
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> RecordPaymentAsync(
        Guid billId,
        RecordPaymentRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IBillingService billingService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await billingService.RecordPaymentAsync(
                authService.GetCurrentUserContext(principal),
                billId,
                request ?? throw new InvalidOperationException("Request body is required."),
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> CancelPaymentAsync(
        Guid paymentId,
        CancelPaymentRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IBillingService billingService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await billingService.CancelPaymentAsync(
                authService.GetCurrentUserContext(principal),
                paymentId,
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
