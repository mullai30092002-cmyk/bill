namespace BillSoft.Application.Users;

public sealed record UserListQuery(
    string? Status,
    Guid? BranchId,
    string? Search,
    int Page = 1,
    int PageSize = 20);

public sealed record UserListItem(
    Guid UserId,
    Guid RestaurantId,
    Guid? BranchId,
    string FullName,
    string MobileNumber,
    string? Email,
    string Status,
    IReadOnlyCollection<string> RoleNames);

public sealed record UserListResponse(
    IReadOnlyCollection<UserListItem> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record UserDetail(
    Guid UserId,
    Guid RestaurantId,
    Guid? BranchId,
    string FullName,
    string MobileNumber,
    string? Email,
    string Status,
    IReadOnlyCollection<string> RoleNames,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CreateUserRequest(
    Guid? BranchId,
    string? FullName,
    string? MobileNumber,
    string? Email,
    string? InitialPassword,
    IReadOnlyCollection<string>? RoleNames);

public sealed record UpdateUserRequest(
    Guid? BranchId,
    string? FullName,
    string? MobileNumber,
    string? Email,
    string? Status);

public sealed record UpdateUserRolesRequest(
    IReadOnlyCollection<string>? RoleNames);

public sealed record ResetUserPasswordRequest(
    string? NewPassword,
    string? ConfirmPassword);

public sealed record ResetUserPasswordResponse(
    Guid UserId,
    string Message);
