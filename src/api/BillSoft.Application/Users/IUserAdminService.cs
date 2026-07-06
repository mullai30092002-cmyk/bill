using BillSoft.Application.Auth;

namespace BillSoft.Application.Users;

public interface IUserAdminService
{
    Task<UserListResponse> ListAsync(AuthUserContext currentUser, UserListQuery query, CancellationToken cancellationToken);

    Task<UserDetail> GetAsync(AuthUserContext currentUser, Guid userId, CancellationToken cancellationToken);

    Task<UserDetail> CreateAsync(AuthUserContext currentUser, CreateUserRequest request, CancellationToken cancellationToken);

    Task<UserDetail> UpdateAsync(AuthUserContext currentUser, Guid userId, UpdateUserRequest request, CancellationToken cancellationToken);

    Task<UserDetail> UpdateRolesAsync(AuthUserContext currentUser, Guid userId, UpdateUserRolesRequest request, CancellationToken cancellationToken);

    Task<UserDetail> ActivateAsync(AuthUserContext currentUser, Guid userId, CancellationToken cancellationToken);

    Task<UserDetail> DeactivateAsync(AuthUserContext currentUser, Guid userId, CancellationToken cancellationToken);

    Task<ResetUserPasswordResponse> ResetPasswordAsync(AuthUserContext currentUser, Guid userId, ResetUserPasswordRequest request, CancellationToken cancellationToken);
}
