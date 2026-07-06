namespace BillSoft.Application.Security;

public sealed record RoleListResponse(IReadOnlyCollection<RoleListItem> Items);

public sealed record RoleListItem(
    Guid RoleId,
    Guid? RestaurantId,
    string Name,
    string? Description,
    bool IsSystemRole,
    bool IsAssignable,
    string? AssignmentBlockedReason,
    IReadOnlyCollection<string> PermissionCodes);

public sealed record RoleDetail(
    Guid RoleId,
    Guid? RestaurantId,
    string Name,
    string? Description,
    bool IsSystemRole,
    bool IsAssignable,
    string? AssignmentBlockedReason,
    IReadOnlyCollection<string> PermissionCodes,
    IReadOnlyCollection<PermissionListItem> Permissions);

public sealed record PermissionListItem(
    Guid PermissionId,
    string Code,
    string? Description,
    string Module);

public sealed record PermissionModuleGroup(
    string Module,
    IReadOnlyCollection<PermissionListItem> Permissions);

public sealed record PermissionCatalogResponse(IReadOnlyCollection<PermissionModuleGroup> Modules);
