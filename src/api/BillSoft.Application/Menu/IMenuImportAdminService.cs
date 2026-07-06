using BillSoft.Application.Auth;

namespace BillSoft.Application.Menu;

public interface IMenuImportAdminService
{
    Task<MenuImportResponse> PreviewAsync(AuthUserContext currentUser, MenuImportPreviewRequest request, CancellationToken cancellationToken);

    Task<MenuImportResponse> ConfirmAsync(AuthUserContext currentUser, MenuImportConfirmRequest request, CancellationToken cancellationToken);
}
