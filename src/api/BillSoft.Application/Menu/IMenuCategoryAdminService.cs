using BillSoft.Application.Auth;

namespace BillSoft.Application.Menu;

public interface IMenuCategoryAdminService
{
    Task<MenuCategoryListResponse> ListAsync(AuthUserContext currentUser, CancellationToken cancellationToken);

    Task<MenuCategoryDetail> GetAsync(AuthUserContext currentUser, Guid categoryId, CancellationToken cancellationToken);

    Task<MenuCategoryDetail> CreateAsync(AuthUserContext currentUser, CreateMenuCategoryRequest request, CancellationToken cancellationToken);

    Task<MenuCategoryDetail> UpdateAsync(AuthUserContext currentUser, Guid categoryId, UpdateMenuCategoryRequest request, CancellationToken cancellationToken);

    Task<MenuCategoryDetail> ActivateAsync(AuthUserContext currentUser, Guid categoryId, CancellationToken cancellationToken);

    Task<MenuCategoryDetail> DeactivateAsync(AuthUserContext currentUser, Guid categoryId, CancellationToken cancellationToken);
}
