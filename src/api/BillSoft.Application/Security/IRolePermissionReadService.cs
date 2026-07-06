using BillSoft.Application.Auth;

namespace BillSoft.Application.Security;

public interface IRolePermissionReadService
{
    Task<RoleListResponse> ListRolesAsync(AuthUserContext currentUser, CancellationToken cancellationToken);

    Task<RoleDetail> GetRoleAsync(AuthUserContext currentUser, Guid roleId, CancellationToken cancellationToken);

    Task<PermissionCatalogResponse> GetPermissionCatalogAsync(CancellationToken cancellationToken);
}
