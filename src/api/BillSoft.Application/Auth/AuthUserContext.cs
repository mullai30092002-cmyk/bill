namespace BillSoft.Application.Auth;

public sealed record AuthUserContext(
    Guid UserId,
    Guid RestaurantId,
    string RestaurantCode,
    Guid? BranchId,
    string FullName,
    string MobileNumber,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    string ActiveRole);
