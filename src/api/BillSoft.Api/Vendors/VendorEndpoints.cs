using System.Globalization;
using System.Security.Claims;
using BillSoft.Api.Security;
using BillSoft.Application.Auth;
using BillSoft.Application.Vendors;
using BillSoft.Domain.Security;
using Microsoft.AspNetCore.Http;

namespace BillSoft.Api.Vendors;

public static class VendorEndpoints
{
    public static IEndpointRouteBuilder MapVendorEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var vendorsGroup = app.MapGroup("/api/v1/vendors")
            .WithTags("Vendors")
            .RequireAuthorization();

        vendorsGroup.MapGet("", ListVendorsAsync)
            .RequireAnyPermission(SystemPermissions.InventoryAdjust, SystemPermissions.VendorBillUpload, SystemPermissions.VendorBillConfirm, SystemPermissions.VendorPaymentCreate, SystemPermissions.ReportView);

        vendorsGroup.MapPost("", CreateVendorAsync)
            .RequireAnyPermission(SystemPermissions.InventoryAdjust, SystemPermissions.VendorBillUpload, SystemPermissions.VendorBillConfirm);

        vendorsGroup.MapPut("/{vendorId:guid}", UpdateVendorAsync)
            .RequireAnyPermission(SystemPermissions.InventoryAdjust, SystemPermissions.VendorBillUpload, SystemPermissions.VendorBillConfirm);

        var vendorBillsGroup = app.MapGroup("/api/v1/vendor-bills")
            .WithTags("VendorBills")
            .RequireAuthorization();

        vendorBillsGroup.MapGet("", ListVendorBillsAsync)
            .RequireAnyPermission(SystemPermissions.VendorBillUpload, SystemPermissions.VendorBillConfirm, SystemPermissions.VendorPaymentCreate);

        vendorBillsGroup.MapGet("/{vendorBillId:guid}", GetVendorBillAsync)
            .RequireAnyPermission(SystemPermissions.VendorBillUpload, SystemPermissions.VendorBillConfirm, SystemPermissions.VendorPaymentCreate);

        vendorBillsGroup.MapPost("", CreateVendorBillAsync)
            .RequirePermission(SystemPermissions.VendorBillConfirm);

        vendorBillsGroup.MapPost("/{vendorBillId:guid}/settlements", RecordSettlementAsync)
            .RequireAnyPermission(SystemPermissions.VendorBillConfirm, SystemPermissions.VendorPaymentCreate);

        vendorsGroup.MapGet("/{vendorId:guid}/statement", GetVendorStatementAsync)
            .RequireAnyPermission(SystemPermissions.VendorBillUpload, SystemPermissions.VendorBillConfirm, SystemPermissions.VendorPaymentCreate, SystemPermissions.ReportView);

        vendorBillsGroup.MapPost("/{vendorBillId:guid}/cancel", CancelVendorBillAsync)
            .RequirePermission(SystemPermissions.VendorBillConfirm);

        return app;
    }

    private static async Task<IResult> ListVendorsAsync(
        Guid? branchId,
        ClaimsPrincipal principal,
        IAuthService authService,
        IVendorService vendorService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await vendorService.ListVendorsAsync(
                authService.GetCurrentUserContext(principal),
                new VendorListQuery(branchId),
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> CreateVendorAsync(
        CreateVendorRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IVendorService vendorService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await vendorService.CreateVendorAsync(
                authService.GetCurrentUserContext(principal),
                request ?? throw new InvalidOperationException("Request body is required."),
                cancellationToken),
            result => Results.Created($"/api/v1/vendors/{result.VendorId}", result));
    }

    private static async Task<IResult> UpdateVendorAsync(
        Guid vendorId,
        UpdateVendorRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IVendorService vendorService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await vendorService.UpdateVendorAsync(
                authService.GetCurrentUserContext(principal),
                vendorId,
                request ?? throw new InvalidOperationException("Request body is required."),
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> ListVendorBillsAsync(
        Guid? branchId,
        DateTime? fromDate,
        DateTime? toDate,
        string? status,
        ClaimsPrincipal principal,
        IAuthService authService,
        IVendorService vendorService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await vendorService.ListVendorBillsAsync(
                authService.GetCurrentUserContext(principal),
                new VendorBillListQuery(branchId, fromDate, toDate, status),
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> GetVendorBillAsync(
        Guid vendorBillId,
        ClaimsPrincipal principal,
        IAuthService authService,
        IVendorService vendorService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await vendorService.GetVendorBillAsync(
                authService.GetCurrentUserContext(principal),
                vendorBillId,
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> CreateVendorBillAsync(
        CreateVendorBillRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IVendorService vendorService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await vendorService.CreateVendorBillAsync(
                authService.GetCurrentUserContext(principal),
                request ?? throw new InvalidOperationException("Request body is required."),
                cancellationToken),
            result => Results.Created($"/api/v1/vendor-bills/{result.VendorBillId}", result));
    }

    private static async Task<IResult> RecordSettlementAsync(
        Guid vendorBillId,
        RecordVendorSettlementRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IVendorService vendorService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await vendorService.RecordSettlementAsync(
                authService.GetCurrentUserContext(principal),
                vendorBillId,
                request ?? throw new InvalidOperationException("Request body is required."),
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> GetVendorStatementAsync(
        Guid vendorId,
        Guid? branchId,
        string? fromDate,
        string? toDate,
        ClaimsPrincipal principal,
        IAuthService authService,
        IVendorService vendorService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await vendorService.GetVendorStatementAsync(
                authService.GetCurrentUserContext(principal),
                new VendorStatementQuery(vendorId, branchId, ParseDate(fromDate), ParseDate(toDate)),
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> CancelVendorBillAsync(
        Guid vendorBillId,
        CancelVendorBillRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IVendorService vendorService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await vendorService.CancelVendorBillAsync(
                authService.GetCurrentUserContext(principal),
                vendorBillId,
                request ?? throw new InvalidOperationException("Request body is required."),
                cancellationToken),
            result => Results.Ok(result));
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

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsedDate))
        {
            return parsedDate;
        }

        throw new InvalidOperationException("The date query parameter must use the yyyy-MM-dd format.");
    }
}
