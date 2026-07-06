using System.Security.Claims;
using BillSoft.Api.Security;
using BillSoft.Application.Auth;
using BillSoft.Application.Vendors;
using BillSoft.Domain.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
namespace BillSoft.Api.Vendors;

public static class VendorBillOcrEndpoints
{
    public static IEndpointRouteBuilder MapVendorBillOcrEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/vendor-bill-ocr")
            .WithTags("VendorBillOcr")
            .RequireAuthorization();

        group.MapGet("/drafts", ListDraftsAsync)
            .RequireAnyPermission(SystemPermissions.VendorBillUpload, SystemPermissions.VendorBillReviewOcr, SystemPermissions.VendorBillOverrideOcr, SystemPermissions.VendorBillConfirm);

        group.MapGet("/drafts/{draftId:guid}", GetDraftAsync)
            .RequireAnyPermission(SystemPermissions.VendorBillUpload, SystemPermissions.VendorBillReviewOcr, SystemPermissions.VendorBillOverrideOcr, SystemPermissions.VendorBillConfirm);

        group.MapPost("/drafts", UploadDraftAsync)
            .RequirePermission(SystemPermissions.VendorBillUpload)
            .DisableAntiforgery();

        group.MapPut("/drafts/{draftId:guid}", UpdateDraftAsync)
            .RequireAnyPermission(SystemPermissions.VendorBillReviewOcr, SystemPermissions.VendorBillOverrideOcr);

        group.MapPost("/drafts/{draftId:guid}/confirm", ConfirmDraftAsync)
            .RequirePermission(SystemPermissions.VendorBillConfirm);

        group.MapPost("/drafts/{draftId:guid}/cancel", CancelDraftAsync)
            .RequireAnyPermission(SystemPermissions.VendorBillReviewOcr, SystemPermissions.VendorBillOverrideOcr);

        return app;
    }

    private static async Task<IResult> ListDraftsAsync(
        Guid? branchId,
        ClaimsPrincipal principal,
        IAuthService authService,
        IVendorBillOcrService ocrService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await ocrService.ListDraftsAsync(
                authService.GetCurrentUserContext(principal),
                new VendorBillOcrDraftListQuery(branchId),
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> GetDraftAsync(
        Guid draftId,
        ClaimsPrincipal principal,
        IAuthService authService,
        IVendorBillOcrService ocrService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await ocrService.GetDraftAsync(
                authService.GetCurrentUserContext(principal),
                draftId,
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> UploadDraftAsync(
        [FromForm] Guid? branchId,
        IFormFile? file,
        ClaimsPrincipal principal,
        IAuthService authService,
        IVendorBillOcrService ocrService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () =>
            {
                if (file is null)
                {
                    throw new InvalidOperationException("Uploaded file is required.");
                }

                await using var stream = file.OpenReadStream();
                return await ocrService.UploadDraftAsync(
                    authService.GetCurrentUserContext(principal),
                    branchId,
                    file.FileName,
                    file.ContentType,
                    stream,
                    file.Length,
                    cancellationToken);
            },
            result => Results.Created($"/api/v1/vendor-bill-ocr/drafts/{result.VendorBillOcrDraftId}", result));
    }

    private static async Task<IResult> UpdateDraftAsync(
        Guid draftId,
        UpdateVendorBillOcrDraftRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IVendorBillOcrService ocrService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await ocrService.UpdateDraftAsync(
                authService.GetCurrentUserContext(principal),
                draftId,
                request ?? throw new InvalidOperationException("Request body is required."),
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> ConfirmDraftAsync(
        Guid draftId,
        ClaimsPrincipal principal,
        IAuthService authService,
        IVendorBillOcrService ocrService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await ocrService.ConfirmDraftAsync(
                authService.GetCurrentUserContext(principal),
                draftId,
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> CancelDraftAsync(
        Guid draftId,
        string? reason,
        ClaimsPrincipal principal,
        IAuthService authService,
        IVendorBillOcrService ocrService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await ocrService.CancelDraftAsync(
                authService.GetCurrentUserContext(principal),
                draftId,
                reason ?? throw new InvalidOperationException("Cancellation reason is required."),
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
}
