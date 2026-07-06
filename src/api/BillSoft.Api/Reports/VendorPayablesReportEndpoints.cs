using System.Globalization;
using System.Security.Claims;
using BillSoft.Api.Security;
using BillSoft.Application.Auth;
using BillSoft.Application.Reports;
using BillSoft.Domain.Security;
using Microsoft.AspNetCore.Http;

namespace BillSoft.Api.Reports;

public static class VendorPayablesReportEndpoints
{
    public static IEndpointRouteBuilder MapVendorPayablesReportEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/reports")
            .WithTags("Reports")
            .RequireAuthorization();

        group.MapGet("/vendor-payables", GetVendorPayablesReportAsync)
            .RequirePermission(SystemPermissions.ReportView);

        return app;
    }

    private static async Task<IResult> GetVendorPayablesReportAsync(
        string? fromDate,
        string? toDate,
        Guid? branchId,
        ClaimsPrincipal principal,
        IAuthService authService,
        IVendorPayablesReportService reportService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await reportService.GetVendorPayablesReportAsync(
                authService.GetCurrentUserContext(principal),
                ParseDate(fromDate),
                ParseDate(toDate),
                branchId,
                cancellationToken),
            result => Results.Ok(result));
    }

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
