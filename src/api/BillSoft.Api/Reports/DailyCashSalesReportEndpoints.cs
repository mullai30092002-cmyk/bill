using System.Globalization;
using System.Security.Claims;
using BillSoft.Api.Security;
using BillSoft.Application.Auth;
using BillSoft.Application.Reports;
using BillSoft.Domain.Security;
using Microsoft.AspNetCore.Http;

namespace BillSoft.Api.Reports;

public static class DailyCashSalesReportEndpoints
{
    public static IEndpointRouteBuilder MapDailyCashSalesReportEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/reports")
            .WithTags("Reports")
            .RequireAuthorization();

        group.MapGet("/daily-cash-sales", GetDailyCashSalesReportAsync)
            .RequirePermission(SystemPermissions.ReportView);

        return app;
    }

    private static async Task<IResult> GetDailyCashSalesReportAsync(
        string? date,
        Guid? branchId,
        ClaimsPrincipal principal,
        IAuthService authService,
        IDailyCashSalesReportService reportService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await reportService.GetDailyCashSalesReportAsync(
                authService.GetCurrentUserContext(principal),
                ParseBusinessDate(date),
                branchId,
                cancellationToken),
            result => Results.Ok(result));
    }

    private static DateTime? ParseBusinessDate(string? date)
    {
        if (string.IsNullOrWhiteSpace(date))
        {
            return null;
        }

        if (DateTime.TryParseExact(
            date,
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
