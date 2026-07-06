using BillSoft.Application.Auth;

namespace BillSoft.Application.Menu;

public interface IMenuItemAdminService
{
    Task<MenuItemListResponse> ListAsync(AuthUserContext currentUser, MenuItemListQuery query, CancellationToken cancellationToken);

    Task<MenuItemDetail> GetAsync(AuthUserContext currentUser, Guid itemId, CancellationToken cancellationToken);

    Task<MenuItemDetail> CreateAsync(AuthUserContext currentUser, CreateMenuItemRequest request, CancellationToken cancellationToken);

    Task<MenuItemDetail> UpdateAsync(AuthUserContext currentUser, Guid itemId, UpdateMenuItemRequest request, CancellationToken cancellationToken);

    Task<MenuItemDetail> ActivateAsync(AuthUserContext currentUser, Guid itemId, CancellationToken cancellationToken);

    Task<MenuItemDetail> DeactivateAsync(AuthUserContext currentUser, Guid itemId, CancellationToken cancellationToken);

    Task<MenuItemPriceHistoryResponse> GetPriceHistoryAsync(AuthUserContext currentUser, Guid itemId, CancellationToken cancellationToken);

    Task<MenuItemRecipeResponse> GetRecipeAsync(AuthUserContext currentUser, Guid itemId, CancellationToken cancellationToken);

    Task<MenuItemRecipeResponse> UpdateRecipeAsync(AuthUserContext currentUser, Guid itemId, UpdateMenuItemRecipeRequest request, CancellationToken cancellationToken);
}
