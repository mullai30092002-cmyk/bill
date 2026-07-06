using BillSoft.Application.Auth;
using BillSoft.Application.Security;
using BillSoft.Domain.Security;
using BillSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillSoft.Infrastructure.Security;

public sealed class RolePermissionReadService : IRolePermissionReadService
{
    private readonly BillSoftDbContext _context;

    public RolePermissionReadService(BillSoftDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<RoleListResponse> ListRolesAsync(AuthUserContext currentUser, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var visibleRoles = await _context.Roles
            .AsNoTracking()
            .Where(role => role.RestaurantId == null || role.RestaurantId == restaurantId)
            .OrderBy(role => role.RestaurantId.HasValue)
            .ThenBy(role => role.Name)
            .ToListAsync(cancellationToken);

        var permissionCodesByRoleId = await LoadPermissionCodesByRoleIdAsync(visibleRoles.Select(role => role.RoleId), cancellationToken);
        var items = visibleRoles
            .Select(role => ToListItem(role, permissionCodesByRoleId.TryGetValue(role.RoleId, out var codes) ? codes : Array.Empty<string>(), currentUser))
            .ToArray();

        return new RoleListResponse(items);
    }

    public async Task<RoleDetail> GetRoleAsync(AuthUserContext currentUser, Guid roleId, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var role = await _context.Roles
            .AsNoTracking()
            .SingleOrDefaultAsync(entity =>
                entity.RoleId == roleId &&
                (entity.RestaurantId == null || entity.RestaurantId == restaurantId),
                cancellationToken);

        if (role is null)
        {
            throw new KeyNotFoundException("Role not found.");
        }

        var permissions = await LoadPermissionsByRoleIdAsync(role.RoleId, cancellationToken);
        var permissionCodes = permissions.Select(permission => permission.Code).ToArray();
        return ToDetail(role, permissions, permissionCodes, currentUser);
    }

    public async Task<PermissionCatalogResponse> GetPermissionCatalogAsync(CancellationToken cancellationToken)
    {
        var permissions = await _context.Permissions
            .AsNoTracking()
            .OrderBy(permission => permission.Module)
            .ThenBy(permission => permission.Code)
            .ToListAsync(cancellationToken);

        var modules = permissions
            .GroupBy(permission => permission.Module, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new PermissionModuleGroup(
                group.Key,
                group.Select(permission => new PermissionListItem(
                        permission.PermissionId,
                        permission.Code,
                        permission.Description,
                        permission.Module))
                    .OrderBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .ToArray();

        return new PermissionCatalogResponse(modules);
    }

    private async Task<Dictionary<Guid, IReadOnlyCollection<string>>> LoadPermissionCodesByRoleIdAsync(
        IEnumerable<Guid> roleIds,
        CancellationToken cancellationToken)
    {
        var idSet = roleIds.Distinct().ToArray();
        if (idSet.Length == 0)
        {
            return new Dictionary<Guid, IReadOnlyCollection<string>>();
        }

        var permissions = await (
                from rolePermission in _context.RolePermissions.AsNoTracking()
                join permission in _context.Permissions.AsNoTracking() on rolePermission.PermissionId equals permission.PermissionId
                where idSet.Contains(rolePermission.RoleId)
                select new { rolePermission.RoleId, permission.Code })
            .ToListAsync(cancellationToken);

        return permissions
            .GroupBy(item => item.RoleId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<string>)group.Select(item => item.Code)
                    .Where(code => !string.IsNullOrWhiteSpace(code))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
                    .ToArray());
    }

    private async Task<IReadOnlyCollection<PermissionListItem>> LoadPermissionsByRoleIdAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var permissions = await (
                from rolePermission in _context.RolePermissions.AsNoTracking()
                join permission in _context.Permissions.AsNoTracking() on rolePermission.PermissionId equals permission.PermissionId
                where rolePermission.RoleId == roleId
                select permission)
            .ToListAsync(cancellationToken);

        return permissions
            .OrderBy(permission => permission.Module, StringComparer.OrdinalIgnoreCase)
            .ThenBy(permission => permission.Code, StringComparer.OrdinalIgnoreCase)
            .Select(permission => new PermissionListItem(
                permission.PermissionId,
                permission.Code,
                permission.Description,
                permission.Module))
            .ToArray();
    }

    private static RoleListItem ToListItem(
        Role role,
        IReadOnlyCollection<string> permissionCodes,
        AuthUserContext currentUser)
    {
        var isAssignable = IsAssignable(role, currentUser, out var blockedReason);
        return new RoleListItem(
            role.RoleId,
            role.RestaurantId,
            role.Name,
            role.Description,
            role.IsSystemRole,
            isAssignable,
            blockedReason,
            permissionCodes);
    }

    private static RoleDetail ToDetail(
        Role role,
        IReadOnlyCollection<PermissionListItem> permissions,
        IReadOnlyCollection<string> permissionCodes,
        AuthUserContext currentUser)
    {
        var isAssignable = IsAssignable(role, currentUser, out var blockedReason);
        return new RoleDetail(
            role.RoleId,
            role.RestaurantId,
            role.Name,
            role.Description,
            role.IsSystemRole,
            isAssignable,
            blockedReason,
            permissionCodes,
            permissions);
    }

    private static bool IsAssignable(Role role, AuthUserContext currentUser, out string? blockedReason)
    {
        if (!string.Equals(role.Name, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
        {
            blockedReason = null;
            return true;
        }

        if (currentUser.Roles.Any(candidate => string.Equals(candidate, "SuperAdmin", StringComparison.OrdinalIgnoreCase)))
        {
            blockedReason = null;
            return true;
        }

        blockedReason = "SuperAdmin assignment requires a SuperAdmin principal.";
        return false;
    }

    private static Guid RequireRestaurantScope(AuthUserContext currentUser)
    {
        if (currentUser.RestaurantId == Guid.Empty)
        {
            throw new InvalidOperationException("Authentication context is missing the restaurant scope.");
        }

        return currentUser.RestaurantId;
    }
}
