using System.Security.Claims;
using BillSoft.Api.Security;
using BillSoft.Application.Auth;
using BillSoft.Application.Menu;
using BillSoft.Domain.Security;
using Microsoft.AspNetCore.Http;

namespace BillSoft.Api.Admin;

public static class MenuAdminEndpoints
{
    public static IEndpointRouteBuilder MapMenuAdminEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var categoryGroup = app.MapGroup("/api/v1/admin/menu/categories")
            .WithTags("AdminMenuCategories")
            .RequireAuthorization();

        categoryGroup.MapGet("", ListCategoriesAsync)
            .RequireAnyPermission(SystemPermissions.MenuItemView, SystemPermissions.MenuCategoryManage, SystemPermissions.MenuItemManage);

        categoryGroup.MapGet("/{categoryId:guid}", GetCategoryAsync)
            .RequireAnyPermission(SystemPermissions.MenuItemView, SystemPermissions.MenuCategoryManage, SystemPermissions.MenuItemManage);

        categoryGroup.MapPost("", CreateCategoryAsync)
            .RequirePermission(SystemPermissions.MenuCategoryManage);

        categoryGroup.MapPut("/{categoryId:guid}", UpdateCategoryAsync)
            .RequirePermission(SystemPermissions.MenuCategoryManage);

        categoryGroup.MapPost("/{categoryId:guid}/activate", ActivateCategoryAsync)
            .RequirePermission(SystemPermissions.MenuCategoryManage);

        categoryGroup.MapPost("/{categoryId:guid}/deactivate", DeactivateCategoryAsync)
            .RequirePermission(SystemPermissions.MenuCategoryManage);

        var itemGroup = app.MapGroup("/api/v1/admin/menu/items")
            .WithTags("AdminMenuItems")
            .RequireAuthorization();

        itemGroup.MapGet("", ListItemsAsync)
            .RequireAnyPermission(SystemPermissions.MenuItemView, SystemPermissions.MenuItemManage);

        itemGroup.MapGet("/{itemId:guid}", GetItemAsync)
            .RequireAnyPermission(SystemPermissions.MenuItemView, SystemPermissions.MenuItemManage);

        itemGroup.MapPost("", CreateItemAsync)
            .RequirePermission(SystemPermissions.MenuItemManage);

        itemGroup.MapPut("/{itemId:guid}", UpdateItemAsync)
            .RequirePermission(SystemPermissions.MenuItemManage);

        itemGroup.MapPost("/{itemId:guid}/activate", ActivateItemAsync)
            .RequirePermission(SystemPermissions.MenuItemManage);

        itemGroup.MapPost("/{itemId:guid}/deactivate", DeactivateItemAsync)
            .RequirePermission(SystemPermissions.MenuItemManage);

        itemGroup.MapGet("/{itemId:guid}/price-history", GetPriceHistoryAsync)
            .RequireAnyPermission(SystemPermissions.MenuItemView, SystemPermissions.MenuItemManage);

        itemGroup.MapGet("/{itemId:guid}/recipe", GetRecipeAsync)
            .RequireAnyPermission(SystemPermissions.MenuItemView, SystemPermissions.MenuItemManage);

        itemGroup.MapPut("/{itemId:guid}/recipe", UpdateRecipeAsync)
            .RequirePermission(SystemPermissions.MenuItemManage);

        var importGroup = app.MapGroup("/api/v1/admin/menu/import")
            .WithTags("AdminMenuImport")
            .RequireAuthorization();

        importGroup.MapPost("/preview", PreviewImportAsync)
            .RequirePermission(SystemPermissions.MenuItemManage);

        importGroup.MapPost("/confirm", ConfirmImportAsync)
            .RequirePermission(SystemPermissions.MenuItemManage);

        return app;
    }

    private static async Task<IResult> ListCategoriesAsync(
        ClaimsPrincipal principal,
        IAuthService authService,
        IMenuCategoryAdminService menuCategoryAdminService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await menuCategoryAdminService.ListAsync(authService.GetCurrentUserContext(principal), cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> GetCategoryAsync(
        Guid categoryId,
        ClaimsPrincipal principal,
        IAuthService authService,
        IMenuCategoryAdminService menuCategoryAdminService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await menuCategoryAdminService.GetAsync(authService.GetCurrentUserContext(principal), categoryId, cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> CreateCategoryAsync(
        CreateMenuCategoryRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IMenuCategoryAdminService menuCategoryAdminService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await menuCategoryAdminService.CreateAsync(
                authService.GetCurrentUserContext(principal),
                request ?? throw new InvalidOperationException("Request body is required."),
                cancellationToken),
            result => Results.Created($"/api/v1/admin/menu/categories/{result.MenuCategoryId}", result));
    }

    private static async Task<IResult> UpdateCategoryAsync(
        Guid categoryId,
        UpdateMenuCategoryRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IMenuCategoryAdminService menuCategoryAdminService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await menuCategoryAdminService.UpdateAsync(
                authService.GetCurrentUserContext(principal),
                categoryId,
                request ?? throw new InvalidOperationException("Request body is required."),
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> ActivateCategoryAsync(
        Guid categoryId,
        ClaimsPrincipal principal,
        IAuthService authService,
        IMenuCategoryAdminService menuCategoryAdminService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await menuCategoryAdminService.ActivateAsync(
                authService.GetCurrentUserContext(principal),
                categoryId,
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> DeactivateCategoryAsync(
        Guid categoryId,
        ClaimsPrincipal principal,
        IAuthService authService,
        IMenuCategoryAdminService menuCategoryAdminService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await menuCategoryAdminService.DeactivateAsync(
                authService.GetCurrentUserContext(principal),
                categoryId,
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> ListItemsAsync(
        Guid? categoryId,
        string? status,
        string? search,
        string? availability,
        ClaimsPrincipal principal,
        IAuthService authService,
        IMenuItemAdminService menuItemAdminService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await menuItemAdminService.ListAsync(
                authService.GetCurrentUserContext(principal),
                new MenuItemListQuery(categoryId, status, search, availability),
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> GetItemAsync(
        Guid itemId,
        ClaimsPrincipal principal,
        IAuthService authService,
        IMenuItemAdminService menuItemAdminService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await menuItemAdminService.GetAsync(authService.GetCurrentUserContext(principal), itemId, cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> CreateItemAsync(
        CreateMenuItemRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IMenuItemAdminService menuItemAdminService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await menuItemAdminService.CreateAsync(
                authService.GetCurrentUserContext(principal),
                request ?? throw new InvalidOperationException("Request body is required."),
                cancellationToken),
            result => Results.Created($"/api/v1/admin/menu/items/{result.MenuItemId}", result));
    }

    private static async Task<IResult> UpdateItemAsync(
        Guid itemId,
        UpdateMenuItemRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IMenuItemAdminService menuItemAdminService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await menuItemAdminService.UpdateAsync(
                authService.GetCurrentUserContext(principal),
                itemId,
                request ?? throw new InvalidOperationException("Request body is required."),
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> ActivateItemAsync(
        Guid itemId,
        ClaimsPrincipal principal,
        IAuthService authService,
        IMenuItemAdminService menuItemAdminService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await menuItemAdminService.ActivateAsync(
                authService.GetCurrentUserContext(principal),
                itemId,
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> DeactivateItemAsync(
        Guid itemId,
        ClaimsPrincipal principal,
        IAuthService authService,
        IMenuItemAdminService menuItemAdminService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await menuItemAdminService.DeactivateAsync(
                authService.GetCurrentUserContext(principal),
                itemId,
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> GetPriceHistoryAsync(
        Guid itemId,
        ClaimsPrincipal principal,
        IAuthService authService,
        IMenuItemAdminService menuItemAdminService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await menuItemAdminService.GetPriceHistoryAsync(
                authService.GetCurrentUserContext(principal),
                itemId,
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> GetRecipeAsync(
        Guid itemId,
        ClaimsPrincipal principal,
        IAuthService authService,
        IMenuItemAdminService menuItemAdminService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await menuItemAdminService.GetRecipeAsync(
                authService.GetCurrentUserContext(principal),
                itemId,
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> UpdateRecipeAsync(
        Guid itemId,
        UpdateMenuItemRecipeRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IMenuItemAdminService menuItemAdminService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await menuItemAdminService.UpdateRecipeAsync(
                authService.GetCurrentUserContext(principal),
                itemId,
                request ?? throw new InvalidOperationException("Request body is required."),
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> PreviewImportAsync(
        MenuImportPreviewRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IMenuImportAdminService menuImportAdminService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await menuImportAdminService.PreviewAsync(
                authService.GetCurrentUserContext(principal),
                request ?? throw new InvalidOperationException("Request body is required."),
                cancellationToken),
            result => Results.Ok(result));
    }

    private static async Task<IResult> ConfirmImportAsync(
        MenuImportConfirmRequest? request,
        ClaimsPrincipal principal,
        IAuthService authService,
        IMenuImportAdminService menuImportAdminService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            async () => await menuImportAdminService.ConfirmAsync(
                authService.GetCurrentUserContext(principal),
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
