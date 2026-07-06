using System.Text.Json;
using BillSoft.Application.Auth;
using BillSoft.Application.Users;
using BillSoft.Domain.Auditing;
using BillSoft.Domain.Localization;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Security;
using BillSoft.Domain.Users;
using BillSoft.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BillSoft.Infrastructure.Users;

public sealed class UserAdminService : IUserAdminService
{
    private const int MinimumPasswordLength = 8;
    private const int MinimumPrivilegedPasswordLength = 12;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
    private const string DefaultPasswordResetReason = "Password reset by administrator.";
    private static readonly string[] PrivilegedRoleNames = ["SuperAdmin", "RestaurantOwner", "Admin", "AccountsUser", "InventoryUser"];

    private readonly BillSoftDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;

    public UserAdminService(
        BillSoftDbContext context,
        IPasswordHasher<User> passwordHasher)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
    }

    public async Task<UserListResponse> ListAsync(AuthUserContext currentUser, UserListQuery query, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var page = ResolvePage(query.Page);
        var pageSize = ResolvePageSize(query.PageSize);
        var normalizedSearch = NormalizeSearch(query.Search);
        var status = ResolveStatus(query.Status);

        var usersQuery = _context.Users
            .AsNoTracking()
            .Where(user => user.RestaurantId == restaurantId);

        if (query.BranchId.HasValue)
        {
            await EnsureActiveBranchAsync(restaurantId, query.BranchId.Value, cancellationToken);
            usersQuery = usersQuery.Where(user => user.BranchId == query.BranchId);
        }

        if (status.HasValue)
        {
            usersQuery = usersQuery.Where(user => user.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var searchTerm = query.Search!.Trim();
            var searchPattern = EscapeLikePattern(searchTerm);
            var normalizedEmailSearch = NormalizeEmail(searchTerm);

            usersQuery = usersQuery.Where(user =>
                EF.Functions.Like(user.FullName, $"%{searchPattern}%", "\\") ||
                EF.Functions.Like(user.MobileNumber, $"%{searchPattern}%", "\\") ||
                EF.Functions.Like(user.MobileNationalNumber, $"%{searchPattern}%", "\\") ||
                EF.Functions.Like(user.MobileE164, $"%{searchPattern}%", "\\") ||
                (user.NormalizedEmail != null &&
                 normalizedEmailSearch != null &&
                 EF.Functions.Like(user.NormalizedEmail, $"%{EscapeLikePattern(normalizedEmailSearch)}%", "\\")));
        }

        var totalCount = await usersQuery.CountAsync(cancellationToken);
        var pagedUsers = await usersQuery
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.MobileNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var roleNamesByUserId = await ResolveRoleNamesByUserIdAsync(pagedUsers.Select(user => user.UserId), cancellationToken);
        var items = pagedUsers
            .Select(user => new UserListItem(
                user.UserId,
                user.RestaurantId,
                user.BranchId,
                user.FullName,
                user.MobileNumber,
                user.Email,
                user.Status.ToString(),
                roleNamesByUserId.TryGetValue(user.UserId, out var roles) ? roles : Array.Empty<string>()))
            .ToArray();

        return new UserListResponse(items, totalCount, page, pageSize);
    }

    public async Task<UserDetail> GetAsync(AuthUserContext currentUser, Guid userId, CancellationToken cancellationToken)
    {
        var user = await LoadUserAsync(currentUser, userId, cancellationToken);
        var roleNames = await ResolveRoleNamesAsync(user.UserId, cancellationToken);
        return ToDetail(user, roleNames);
    }

    public async Task<UserDetail> CreateAsync(AuthUserContext currentUser, CreateUserRequest request, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var restaurantProfile = CountryProfileCatalog.GetRequired(restaurant.CountryCode);
        var now = DateTimeOffset.UtcNow;

        ValidateCreateRequest(request);
        var branch = request.BranchId.HasValue
            ? await EnsureActiveBranchAsync(restaurantId, request.BranchId.Value, cancellationToken)
            : null;

        var roleEntities = await ResolveRequestedRolesAsync(restaurantId, currentUser, request.RoleNames!, cancellationToken);
        var normalizedMobileNumber = MobileNumberNormalizer.Normalize(restaurantProfile, request.MobileNumber!);
        var email = NormalizeEmail(request.Email);
        await EnsureUniqueUserIdentifiersAsync(restaurantId, null, normalizedMobileNumber.E164, email, cancellationToken);

        var user = new User
        {
            RestaurantId = restaurantId,
            BranchId = branch?.BranchId,
            FullName = request.FullName!.Trim(),
            Status = UserStatus.Active,
        };
        user.SetMobileNumber(normalizedMobileNumber);
        user.SetEmail(email);
        user.PasswordHash = _passwordHasher.HashPassword(user, request.InitialPassword!);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        _context.Users.Add(user);
        AddUserRoles(user.UserId, roleEntities.Select(role => role.RoleId), currentUser.UserId, now);

        var detail = ToDetail(user, roleEntities.Select(role => role.Name).ToArray());
        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: branch,
            action: "User.Created",
            entityId: user.UserId.ToString(),
            reason: "User created.",
            oldValueJson: null,
            newValueJson: Serialize(detail));

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return detail;
    }

    public async Task<UserDetail> UpdateAsync(AuthUserContext currentUser, Guid userId, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        ValidateUpdateRequest(request);

        var user = await LoadTrackedUserAsync(restaurantId, userId, cancellationToken);
        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var restaurantProfile = CountryProfileCatalog.GetRequired(restaurant.CountryCode);
        var beforeRoles = await ResolveRoleNamesAsync(user.UserId, cancellationToken);
        var before = SnapshotUser(user, beforeRoles);
        var beforeStatus = user.Status;

        Branch? branch = null;
        if (request.BranchId.HasValue)
        {
            branch = await EnsureActiveBranchAsync(restaurantId, request.BranchId.Value, cancellationToken);
            user.BranchId = branch.BranchId;
        }

        var normalizedMobileNumber = MobileNumberNormalizer.Normalize(restaurantProfile, request.MobileNumber!);
        var email = request.Email is null ? user.Email : NormalizeEmail(request.Email);
        await EnsureUniqueUserIdentifiersAsync(restaurantId, user.UserId, normalizedMobileNumber.E164, request.Email is null ? user.NormalizedEmail : email, cancellationToken);

        user.FullName = request.FullName!.Trim();
        user.SetMobileNumber(normalizedMobileNumber);
        if (request.Email is not null)
        {
            user.SetEmail(request.Email);
        }
        user.Status = ParseUserStatus(request.Status!);
        user.MarkUpdated();

        if (beforeStatus == UserStatus.Active && user.Status != UserStatus.Active)
        {
            await RevokeActiveRefreshTokensAsync(user.UserId, DateTimeOffset.UtcNow, cancellationToken);
        }

        var roleNames = beforeRoles;
        var detail = ToDetail(user, roleNames);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: branch ?? await ResolveUserBranchAsync(user, restaurantId, cancellationToken),
            action: "User.Updated",
            entityId: user.UserId.ToString(),
            reason: "User profile updated.",
            oldValueJson: Serialize(before),
            newValueJson: Serialize(detail));

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return detail;
    }

    public async Task<UserDetail> UpdateRolesAsync(AuthUserContext currentUser, Guid userId, UpdateUserRolesRequest request, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        ValidateRoleUpdateRequest(request);

        if (currentUser.UserId == userId)
        {
            throw new UnauthorizedAccessException("Editing your own roles is not allowed yet.");
        }

        var user = await LoadTrackedUserAsync(restaurantId, userId, cancellationToken);
        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var branch = await ResolveUserBranchAsync(user, restaurantId, cancellationToken);
        var beforeRoles = await ResolveRoleNamesAsync(user.UserId, cancellationToken);
        var roleEntities = await ResolveRequestedRolesAsync(restaurantId, currentUser, request.RoleNames!, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var existingMappings = await _context.UserRoles
            .Where(userRole => userRole.UserId == user.UserId)
            .ToListAsync(cancellationToken);
        _context.UserRoles.RemoveRange(existingMappings);
        AddUserRoles(user.UserId, roleEntities.Select(role => role.RoleId), currentUser.UserId, now);

        var detail = ToDetail(user, roleEntities.Select(role => role.Name).ToArray());

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        await RevokeActiveRefreshTokensAsync(user.UserId, DateTimeOffset.UtcNow, cancellationToken);

        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: branch,
            action: "User.RolesUpdated",
            entityId: user.UserId.ToString(),
            reason: "User roles updated.",
            oldValueJson: Serialize(new { RoleNames = beforeRoles }),
            newValueJson: Serialize(new { RoleNames = detail.RoleNames }));

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return detail;
    }

    public async Task<UserDetail> ActivateAsync(AuthUserContext currentUser, Guid userId, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var user = await LoadTrackedUserAsync(restaurantId, userId, cancellationToken);
        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var branch = await ResolveUserBranchAsync(user, restaurantId, cancellationToken);
        var beforeStatus = user.Status;

        user.Status = UserStatus.Active;
        user.MarkUpdated();
        var roleNames = await ResolveRoleNamesAsync(user.UserId, cancellationToken);
        var detail = ToDetail(user, roleNames);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: branch,
            action: "User.Activated",
            entityId: user.UserId.ToString(),
            reason: "User activated.",
            oldValueJson: Serialize(new { Status = beforeStatus.ToString() }),
            newValueJson: Serialize(new { Status = UserStatus.Active.ToString() }));

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return detail;
    }

    public async Task<UserDetail> DeactivateAsync(AuthUserContext currentUser, Guid userId, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);

        if (currentUser.UserId == userId)
        {
            throw new UnauthorizedAccessException("Self-deactivation is not allowed.");
        }

        var user = await LoadTrackedUserAsync(restaurantId, userId, cancellationToken);
        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var branch = await ResolveUserBranchAsync(user, restaurantId, cancellationToken);
        var beforeStatus = user.Status;

        user.Status = UserStatus.Inactive;
        user.MarkUpdated();
        var roleNames = await ResolveRoleNamesAsync(user.UserId, cancellationToken);
        var detail = ToDetail(user, roleNames);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        await RevokeActiveRefreshTokensAsync(user.UserId, DateTimeOffset.UtcNow, cancellationToken);

        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: branch,
            action: "User.Deactivated",
            entityId: user.UserId.ToString(),
            reason: "User deactivated.",
            oldValueJson: Serialize(new { Status = beforeStatus.ToString() }),
            newValueJson: Serialize(new { Status = UserStatus.Inactive.ToString() }));

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return detail;
    }

    public async Task<ResetUserPasswordResponse> ResetPasswordAsync(
        AuthUserContext currentUser,
        Guid userId,
        ResetUserPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);

        if (currentUser.UserId == userId)
        {
            throw new InvalidOperationException("You cannot reset your own password from this screen.");
        }

        var user = await LoadTrackedUserAsync(restaurantId, userId, cancellationToken);
        var roleNames = await ResolveRoleNamesAsync(user.UserId, cancellationToken);
        ValidateResetPasswordRequest(request, roleNames);

        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var branch = await ResolveCurrentBranchAsync(currentUser, restaurantId, cancellationToken);
        var before = SnapshotUser(user, roleNames);
        var now = DateTimeOffset.UtcNow;

        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword!);
        user.MarkUpdated(now);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        await RevokeActiveRefreshTokensAsync(user.UserId, now, cancellationToken);

        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: branch,
            action: "User.PasswordReset",
            entityId: user.UserId.ToString(),
            reason: DefaultPasswordResetReason,
            oldValueJson: Serialize(before),
            newValueJson: Serialize(new
            {
                TargetUserId = user.UserId,
                TargetUserName = user.FullName,
                TargetMobileSnapshot = user.MobileNumber,
                Reason = DefaultPasswordResetReason
            }));

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ResetUserPasswordResponse(user.UserId, "Password was reset.");
    }

    private static Guid RequireRestaurantScope(AuthUserContext currentUser)
    {
        if (currentUser.RestaurantId == Guid.Empty)
        {
            throw new InvalidOperationException("Authentication context is missing the restaurant scope.");
        }

        return currentUser.RestaurantId;
    }

    private async Task<Restaurant> LoadRestaurantAsync(Guid restaurantId, CancellationToken cancellationToken)
    {
        var restaurant = await _context.Restaurants.AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.RestaurantId == restaurantId, cancellationToken);

        if (restaurant is null)
        {
            throw new KeyNotFoundException("Restaurant not found.");
        }

        return restaurant;
    }

    private async Task<User> LoadUserAsync(AuthUserContext currentUser, Guid userId, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var user = await _context.Users.AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.UserId == userId && entity.RestaurantId == restaurantId, cancellationToken);

        if (user is null)
        {
            throw new KeyNotFoundException("User not found.");
        }

        return user;
    }

    private async Task<User> LoadTrackedUserAsync(Guid restaurantId, Guid userId, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .SingleOrDefaultAsync(entity => entity.UserId == userId && entity.RestaurantId == restaurantId, cancellationToken);

        if (user is null)
        {
            throw new KeyNotFoundException("User not found.");
        }

        return user;
    }

    private async Task<Branch> EnsureActiveBranchAsync(Guid restaurantId, Guid branchId, CancellationToken cancellationToken)
    {
        var branch = await _context.Branches
            .AsNoTracking()
            .SingleOrDefaultAsync(entity =>
                entity.BranchId == branchId &&
                entity.RestaurantId == restaurantId &&
                entity.Status == BranchStatus.Active,
                cancellationToken);

        if (branch is null)
        {
            throw new InvalidOperationException("Branch must exist and be active within the current restaurant.");
        }

        return branch;
    }

    private async Task<Branch?> ResolveUserBranchAsync(User user, Guid restaurantId, CancellationToken cancellationToken)
    {
        if (!user.BranchId.HasValue)
        {
            return null;
        }

        return await _context.Branches.AsNoTracking()
            .SingleOrDefaultAsync(entity =>
                entity.BranchId == user.BranchId &&
                entity.RestaurantId == restaurantId,
                cancellationToken);
    }

    private async Task<Branch?> ResolveCurrentBranchAsync(AuthUserContext currentUser, Guid restaurantId, CancellationToken cancellationToken)
    {
        if (!currentUser.BranchId.HasValue)
        {
            return null;
        }

        return await _context.Branches.AsNoTracking()
            .SingleOrDefaultAsync(entity =>
                entity.BranchId == currentUser.BranchId &&
                entity.RestaurantId == restaurantId,
                cancellationToken);
    }

    private async Task EnsureUniqueUserIdentifiersAsync(
        Guid restaurantId,
        Guid? userId,
        string mobileE164,
        string? normalizedEmail,
        CancellationToken cancellationToken)
    {
        var duplicateMobile = await _context.Users.AsNoTracking()
            .AnyAsync(user =>
                user.RestaurantId == restaurantId &&
                user.UserId != userId &&
                user.MobileE164 == mobileE164,
                cancellationToken);

        if (duplicateMobile)
        {
            throw new InvalidOperationException("Mobile number already exists in this restaurant.");
        }

        if (!string.IsNullOrWhiteSpace(normalizedEmail))
        {
            var duplicateEmail = await _context.Users.AsNoTracking()
                .AnyAsync(user =>
                    user.RestaurantId == restaurantId &&
                    user.UserId != userId &&
                    user.NormalizedEmail == normalizedEmail,
                    cancellationToken);

            if (duplicateEmail)
            {
                throw new InvalidOperationException("Email already exists in this restaurant.");
            }
        }
    }

    private async Task<IReadOnlyCollection<Role>> ResolveRequestedRolesAsync(
        Guid restaurantId,
        AuthUserContext currentUser,
        IReadOnlyCollection<string> requestedRoleNames,
        CancellationToken cancellationToken)
    {
        var cleanedRoleNames = requestedRoleNames
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (cleanedRoleNames.Length == 0)
        {
            throw new InvalidOperationException("At least one role is required.");
        }

        if (cleanedRoleNames.Any(role => string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase)) &&
            !currentUser.Roles.Any(role => string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase)))
        {
            throw new UnauthorizedAccessException("Assigning SuperAdmin requires an existing SuperAdmin principal.");
        }

        var roles = await _context.Roles.AsNoTracking()
            .Where(role =>
                role.RestaurantId == null || role.RestaurantId == restaurantId)
            .ToListAsync(cancellationToken);

        var rolesByName = roles
            .GroupBy(role => role.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(role => role.RestaurantId.HasValue).First(),
                StringComparer.OrdinalIgnoreCase);

        var resolvedRoles = new List<Role>(cleanedRoleNames.Length);
        foreach (var roleName in cleanedRoleNames)
        {
            if (!rolesByName.TryGetValue(roleName, out var resolvedRole))
            {
                throw new InvalidOperationException("One or more roles were not found for this restaurant.");
            }

            resolvedRoles.Add(resolvedRole);
        }

        return resolvedRoles;
    }

    private async Task<IReadOnlyCollection<string>> ResolveRoleNamesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var roles = await (
                from userRole in _context.UserRoles.AsNoTracking()
                join role in _context.Roles.AsNoTracking() on userRole.RoleId equals role.RoleId
                where userRole.UserId == userId
                select role.Name)
            .ToListAsync(cancellationToken);

        return OrderRoles(roles);
    }

    private async Task<Dictionary<Guid, IReadOnlyCollection<string>>> ResolveRoleNamesByUserIdAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken)
    {
        var idSet = userIds.Distinct().ToArray();
        if (idSet.Length == 0)
        {
            return new Dictionary<Guid, IReadOnlyCollection<string>>();
        }

        var data = await (
                from userRole in _context.UserRoles.AsNoTracking()
                join role in _context.Roles.AsNoTracking() on userRole.RoleId equals role.RoleId
                where idSet.Contains(userRole.UserId)
                select new { userRole.UserId, role.Name })
            .ToListAsync(cancellationToken);

        return data
            .GroupBy(item => item.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<string>)OrderRoles(group.Select(item => item.Name).ToArray()));
    }

    private static UserDetail ToDetail(User user, IReadOnlyCollection<string> roleNames)
    {
        return new UserDetail(
            user.UserId,
            user.RestaurantId,
            user.BranchId,
            user.FullName,
            user.MobileNumber,
            user.Email,
            user.Status.ToString(),
            OrderRoles(roleNames),
            user.CreatedAt,
            user.UpdatedAt);
    }

    private static UserLifecycleSnapshot SnapshotUser(User user, IReadOnlyCollection<string> roleNames)
    {
        return new UserLifecycleSnapshot(
            user.UserId,
            user.FullName,
            user.MobileNumber,
            user.Email,
            user.Status.ToString(),
            OrderRoles(roleNames),
            user.BranchId);
    }

    private void AddUserRoles(Guid userId, IEnumerable<Guid> roleIds, Guid? actorUserId, DateTimeOffset assignedAt)
    {
        foreach (var roleId in roleIds)
        {
            _context.UserRoles.Add(new UserRole
            {
                UserId = userId,
                RoleId = roleId,
                AssignedByUserId = actorUserId,
                AssignedAt = assignedAt
            });
        }
    }

    private void AddAudit(
        AuthUserContext actor,
        Restaurant restaurant,
        Branch? branch,
        string action,
        string entityId,
        string reason,
        string? oldValueJson,
        string? newValueJson)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            RestaurantId = restaurant.RestaurantId,
            BranchId = branch?.BranchId,
            UserId = actor.UserId,
            Action = action,
            EntityType = "User",
            EntityId = entityId,
            Reason = reason,
            OldValueJson = oldValueJson,
            NewValueJson = newValueJson,
            RestaurantNameSnapshot = restaurant.Name,
            BranchNameSnapshot = branch?.Name,
            UserNameSnapshot = actor.FullName,
            UserMobileSnapshot = actor.MobileNumber,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private static void ValidateCreateRequest(CreateUserRequest request)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Request is required.");
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new InvalidOperationException("Full name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.MobileNumber))
        {
            throw new InvalidOperationException("Mobile number is required.");
        }

        if (request.RoleNames is null || request.RoleNames.Count == 0)
        {
            throw new InvalidOperationException("At least one role is required.");
        }

        var minimumPasswordLength = ResolveMinimumPasswordLength(request.RoleNames);

        if (string.IsNullOrWhiteSpace(request.InitialPassword) || request.InitialPassword.Length < minimumPasswordLength)
        {
            throw new InvalidOperationException($"Initial password must be at least {minimumPasswordLength} characters long.");
        }
    }

    private static void ValidateUpdateRequest(UpdateUserRequest request)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Request is required.");
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new InvalidOperationException("Full name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.MobileNumber))
        {
            throw new InvalidOperationException("Mobile number is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Status) || !Enum.TryParse<UserStatus>(request.Status, true, out _))
        {
            throw new InvalidOperationException("Status must be Active, Inactive, or Locked.");
        }
    }

    private static void ValidateRoleUpdateRequest(UpdateUserRolesRequest request)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Request is required.");
        }

        if (request.RoleNames is null || request.RoleNames.Count == 0)
        {
            throw new InvalidOperationException("At least one role is required.");
        }
    }

    private static void ValidateResetPasswordRequest(ResetUserPasswordRequest request, IReadOnlyCollection<string> roleNames)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Request is required.");
        }

        var minimumPasswordLength = ResolveMinimumPasswordLength(roleNames);

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < minimumPasswordLength)
        {
            throw new InvalidOperationException($"New password must be at least {minimumPasswordLength} characters long.");
        }

        if (request.ConfirmPassword is not null &&
            !string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Confirm password must match the new password.");
        }
    }

    private static UserStatus ParseUserStatus(string status)
    {
        if (!Enum.TryParse<UserStatus>(status, true, out var parsed))
        {
            throw new InvalidOperationException("Status must be Active, Inactive, or Locked.");
        }

        return parsed;
    }

    private static UserStatus? ResolveStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        if (!Enum.TryParse<UserStatus>(status, true, out var parsed))
        {
            throw new InvalidOperationException("Status filter must be Active, Inactive, or Locked.");
        }

        return parsed;
    }

    private static string? NormalizeEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToUpperInvariant();
    }

    private static string? NormalizeSearch(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        return search.Trim().ToUpperInvariant();
    }

    private async Task RevokeActiveRefreshTokensAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var activeTokens = await _context.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = now;
            token.RevokedByIp = null;
            token.LastActivityAt = now;
        }
    }

    private static bool RequiresPrivilegedPasswordPolicy(IReadOnlyCollection<string>? roleNames)
    {
        if (roleNames is null)
        {
            return false;
        }

        return roleNames.Any(role =>
            PrivilegedRoleNames.Any(privilegedRole =>
                string.Equals(privilegedRole, role?.Trim(), StringComparison.OrdinalIgnoreCase)));
    }

    private static int ResolveMinimumPasswordLength(IReadOnlyCollection<string>? roleNames) =>
        RequiresPrivilegedPasswordPolicy(roleNames) ? MinimumPrivilegedPasswordLength : MinimumPasswordLength;

    private static string? NormalizeOptionalText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new InvalidOperationException($"Value must be {maxLength} characters or fewer.");
        }

        return trimmed;
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
    }

    private static int ResolvePage(int page) => page <= 0 ? 1 : page;

    private static int ResolvePageSize(int pageSize)
    {
        if (pageSize <= 0)
        {
            return DefaultPageSize;
        }

        return Math.Min(pageSize, MaxPageSize);
    }

    private static IReadOnlyCollection<string> OrderRoles(IEnumerable<string> roles)
    {
        return roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(role => role, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string Serialize(object? value)
    {
        return value is null ? string.Empty : JsonSerializer.Serialize(value);
    }

    private sealed record UserLifecycleSnapshot(
        Guid UserId,
        string FullName,
        string MobileNumber,
        string? Email,
        string Status,
        IReadOnlyCollection<string> RoleNames,
        Guid? BranchId);
}
