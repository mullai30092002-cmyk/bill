using BillSoft.Domain.Security;
using BillSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillSoft.Infrastructure.Seed;

public sealed class FoundationSeedService : IFoundationSeedService
{
    private readonly BillSoftDbContext _context;

    public FoundationSeedService(BillSoftDbContext context)
    {
        _context = context;
    }

    public async Task<FoundationSeedResult> SeedAsync(CancellationToken cancellationToken = default)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var permissionsInserted = await SeedPermissionsAsync(cancellationToken);
        var rolesInserted = await SeedRolesAsync(cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        var rolePermissionsInserted = await SeedRolePermissionsAsync(cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new FoundationSeedResult(
            StartedAtUtc: startedAtUtc,
            CompletedAtUtc: DateTimeOffset.UtcNow,
            PermissionsInserted: permissionsInserted,
            RolesInserted: rolesInserted,
            RolePermissionsInserted: rolePermissionsInserted);
    }

    private async Task<int> SeedPermissionsAsync(CancellationToken cancellationToken)
    {
        var existingPermissions = await _context.Permissions
            .AsNoTracking()
            .ToDictionaryAsync(permission => NormalizeKey(permission.Code), cancellationToken);

        var permissionsToAdd = FoundationSeedData.Permissions
            .Where(permission => !existingPermissions.ContainsKey(NormalizeKey(permission.Code)))
            .Select(permission => new Permission
            {
                PermissionId = permission.PermissionId,
                Code = permission.Code.Trim(),
                Description = permission.Description,
                Module = permission.Module
            })
            .ToArray();

        if (permissionsToAdd.Length > 0)
        {
            await _context.Permissions.AddRangeAsync(permissionsToAdd, cancellationToken);
        }

        return permissionsToAdd.Length;
    }

    private async Task<int> SeedRolesAsync(CancellationToken cancellationToken)
    {
        var existingSystemRoles = await _context.Roles
            .AsNoTracking()
            .Where(role => role.RestaurantId == null)
            .ToDictionaryAsync(role => NormalizeKey(role.Name), cancellationToken);

        var rolesToAdd = FoundationSeedData.Roles
            .Where(role => !existingSystemRoles.ContainsKey(NormalizeKey(role.Name)))
            .Select(role => new Role
            {
                RoleId = role.RoleId,
                RestaurantId = null,
                Name = role.Name.Trim(),
                Description = role.Description,
                IsSystemRole = role.IsSystemRole
            })
            .ToArray();

        if (rolesToAdd.Length > 0)
        {
            await _context.Roles.AddRangeAsync(rolesToAdd, cancellationToken);
        }

        return rolesToAdd.Length;
    }

    private async Task<int> SeedRolePermissionsAsync(CancellationToken cancellationToken)
    {
        var roleIdsByName = await _context.Roles
            .AsNoTracking()
            .Where(role => role.RestaurantId == null)
            .ToDictionaryAsync(role => NormalizeKey(role.Name), role => role.RoleId, cancellationToken);

        var permissionIdsByCode = await _context.Permissions
            .AsNoTracking()
            .ToDictionaryAsync(permission => NormalizeKey(permission.Code), permission => permission.PermissionId, cancellationToken);

        var existingPairs = await _context.RolePermissions
            .AsNoTracking()
            .Select(rolePermission => new { rolePermission.RoleId, rolePermission.PermissionId })
            .ToListAsync(cancellationToken);

        var existingPairSet = existingPairs
            .Select(pair => (pair.RoleId, pair.PermissionId))
            .ToHashSet();

        var rolePermissionsToAdd = FoundationSeedData.RolePermissions
            .Where(seed => roleIdsByName.ContainsKey(NormalizeKey(seed.RoleName)))
            .Where(seed => permissionIdsByCode.ContainsKey(NormalizeKey(seed.PermissionCode)))
            .Select(seed =>
            {
                var roleId = roleIdsByName[NormalizeKey(seed.RoleName)];
                var permissionId = permissionIdsByCode[NormalizeKey(seed.PermissionCode)];
                return new { seed.RolePermissionId, roleId, permissionId };
            })
            .Where(candidate => !existingPairSet.Contains((candidate.roleId, candidate.permissionId)))
            .Select(candidate => new RolePermission
            {
                RolePermissionId = candidate.RolePermissionId,
                RoleId = candidate.roleId,
                PermissionId = candidate.permissionId
            })
            .ToArray();

        if (rolePermissionsToAdd.Length > 0)
        {
            await _context.RolePermissions.AddRangeAsync(rolePermissionsToAdd, cancellationToken);
        }

        return rolePermissionsToAdd.Length;
    }

    private static string NormalizeKey(string value)
    {
        return value.Trim().ToUpperInvariant();
    }
}
