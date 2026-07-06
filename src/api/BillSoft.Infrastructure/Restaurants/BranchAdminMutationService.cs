using System.Text.Json;
using BillSoft.Application.Auth;
using BillSoft.Application.Restaurants;
using BillSoft.Domain.Auditing;
using BillSoft.Domain.Localization;
using BillSoft.Domain.Restaurants;
using BillSoft.Infrastructure.Persistence;
using BillSoft.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace BillSoft.Infrastructure.Restaurants;

public sealed class BranchAdminMutationService : IBranchAdminMutationService
{
    private readonly BillSoftDbContext _context;

    public BranchAdminMutationService(BillSoftDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<BranchDetail> CreateAsync(AuthUserContext currentUser, CreateBranchRequest request, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        ValidateRequest(request);

        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var restaurantProfile = CountryProfileCatalog.GetRequired(restaurant.CountryCode);
        var now = DateTimeOffset.UtcNow;
        var name = NormalizeRequiredText(request.Name, "Name is required.");
        var address = NormalizeOptionalText(request.Address);
        var phone = NormalizeOptionalText(request.Phone);
        var countryProfile = ResolveCountryProfile(request.CountryCode, restaurantProfile);
        var timezone = NormalizeOrDefaultText(request.Timezone, countryProfile.TimeZoneId);
        var currency = NormalizeOrDefaultText(request.Currency, countryProfile.CurrencyCode).ToUpperInvariant();

        await EnsureUniqueBranchNameAsync(restaurantId, null, name, cancellationToken);
        await EnsureUniqueBranchPhoneAsync(restaurantId, null, phone, cancellationToken);

        var branch = new Branch
        {
            RestaurantId = restaurantId,
            CountryCode = countryProfile.CountryCode,
            Name = name,
            Address = address,
            Phone = phone,
            TimeZoneId = timezone,
            CurrencyCode = currency,
            Status = BranchStatus.Active
        };

        var detail = ToDetail(branch);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        _context.Branches.Add(branch);
        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: branch,
            action: "Branch.Created",
            reason: "Branch created.",
            entityId: branch.BranchId.ToString(),
            oldValueJson: null,
            newValueJson: Serialize(detail),
            createdAt: now);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return detail;
    }

    public async Task<BranchDetail> UpdateAsync(AuthUserContext currentUser, Guid branchId, UpdateBranchRequest request, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        ValidateRequest(request);

        var branch = await LoadTrackedBranchAsync(restaurantId, branchId, cancellationToken);
        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var restaurantProfile = CountryProfileCatalog.GetRequired(restaurant.CountryCode);
        var before = ToDetail(branch);
        var name = NormalizeRequiredText(request.Name, "Name is required.");
        var address = NormalizeOptionalText(request.Address);
        var phone = NormalizeOptionalText(request.Phone);
        var countryProfile = ResolveCountryProfile(request.CountryCode, restaurantProfile);
        var timezone = NormalizeOrDefaultText(request.Timezone, countryProfile.TimeZoneId);
        var currency = NormalizeOrDefaultText(request.Currency, countryProfile.CurrencyCode).ToUpperInvariant();

        await EnsureUniqueBranchNameAsync(restaurantId, branch.BranchId, name, cancellationToken);
        await EnsureUniqueBranchPhoneAsync(restaurantId, branch.BranchId, phone, cancellationToken);

        branch.CountryCode = countryProfile.CountryCode;
        branch.UpdateProfile(name, address, phone, timezone, currency, DateTimeOffset.UtcNow);
        var detail = ToDetail(branch);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: branch,
            action: "Branch.Updated",
            reason: "Branch profile updated.",
            entityId: branch.BranchId.ToString(),
            oldValueJson: Serialize(before),
            newValueJson: Serialize(detail),
            createdAt: DateTimeOffset.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return detail;
    }

    public async Task<BranchDetail> ActivateAsync(AuthUserContext currentUser, Guid branchId, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var branch = await LoadTrackedBranchAsync(restaurantId, branchId, cancellationToken);
        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var before = ToDetail(branch);

        branch.Activate(DateTimeOffset.UtcNow);
        var detail = ToDetail(branch);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: branch,
            action: "Branch.Activated",
            reason: "Branch activated.",
            entityId: branch.BranchId.ToString(),
            oldValueJson: Serialize(before),
            newValueJson: Serialize(detail),
            createdAt: DateTimeOffset.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return detail;
    }

    public async Task<BranchDetail> DeactivateAsync(AuthUserContext currentUser, Guid branchId, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var branch = await LoadTrackedBranchAsync(restaurantId, branchId, cancellationToken);
        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);

        if (await HasActiveUsersAsync(restaurantId, branch.BranchId, cancellationToken))
        {
            throw new InvalidOperationException("Branch cannot be deactivated while active users are assigned.");
        }

        var before = ToDetail(branch);
        branch.Deactivate(DateTimeOffset.UtcNow);
        var detail = ToDetail(branch);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: branch,
            action: "Branch.Deactivated",
            reason: "Branch deactivated.",
            entityId: branch.BranchId.ToString(),
            oldValueJson: Serialize(before),
            newValueJson: Serialize(detail),
            createdAt: DateTimeOffset.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return detail;
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
        var restaurant = await _context.Restaurants
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.RestaurantId == restaurantId, cancellationToken);

        if (restaurant is null)
        {
            throw new KeyNotFoundException("Restaurant not found.");
        }

        return restaurant;
    }

    private async Task<Branch> LoadTrackedBranchAsync(Guid restaurantId, Guid branchId, CancellationToken cancellationToken)
    {
        var branch = await _context.Branches
            .SingleOrDefaultAsync(entity => entity.BranchId == branchId && entity.RestaurantId == restaurantId, cancellationToken);

        if (branch is null)
        {
            throw new KeyNotFoundException("Branch not found.");
        }

        return branch;
    }

    private async Task EnsureUniqueBranchNameAsync(
        Guid restaurantId,
        Guid? branchId,
        string normalizedName,
        CancellationToken cancellationToken)
    {
        var candidateNames = await _context.Branches
            .AsNoTracking()
            .Where(branch =>
                branch.RestaurantId == restaurantId &&
                branch.BranchId != branchId)
            .Select(branch => branch.Name)
            .ToListAsync(cancellationToken);

        var duplicateExists = candidateNames.Any(name =>
            string.Equals(name.Trim(), normalizedName, StringComparison.OrdinalIgnoreCase));

        if (duplicateExists)
        {
            throw new InvalidOperationException("Branch name already exists in this restaurant.");
        }
    }

    private async Task EnsureUniqueBranchPhoneAsync(
        Guid restaurantId,
        Guid? branchId,
        string? normalizedPhone,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(normalizedPhone))
        {
            return;
        }

        var candidatePhones = await _context.Branches
            .AsNoTracking()
            .Where(branch =>
                branch.RestaurantId == restaurantId &&
                branch.BranchId != branchId)
            .Select(branch => new
            {
                branch.Phone,
                branch.NormalizedPhone
            })
            .ToListAsync(cancellationToken);

        var duplicateExists = candidatePhones.Any(candidate =>
            MatchesNormalizedValue(candidate.NormalizedPhone, candidate.Phone, normalizedPhone));

        if (duplicateExists)
        {
            throw new InvalidOperationException("Branch mobile number already exists.");
        }
    }

    private async Task<bool> HasActiveUsersAsync(Guid restaurantId, Guid branchId, CancellationToken cancellationToken)
    {
        return await _context.Users.AsNoTracking()
            .AnyAsync(user =>
                user.RestaurantId == restaurantId &&
                user.BranchId == branchId &&
                user.Status == UserStatus.Active,
                cancellationToken);
    }

    private void AddAudit(
        AuthUserContext actor,
        Restaurant restaurant,
        Branch branch,
        string action,
        string reason,
        string entityId,
        string? oldValueJson,
        string? newValueJson,
        DateTimeOffset createdAt)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            RestaurantId = restaurant.RestaurantId,
            BranchId = branch.BranchId,
            UserId = actor.UserId,
            Action = action,
            EntityType = "Branch",
            EntityId = entityId,
            Reason = reason,
            OldValueJson = oldValueJson,
            NewValueJson = newValueJson,
            RestaurantNameSnapshot = restaurant.Name,
            BranchNameSnapshot = branch.Name,
            UserNameSnapshot = actor.FullName,
            UserMobileSnapshot = actor.MobileNumber,
            CreatedAt = createdAt
        });
    }

    private static BranchDetail ToDetail(Branch branch)
    {
        return new BranchDetail(
            branch.BranchId,
            branch.RestaurantId,
            branch.CountryCode,
            branch.Name,
            branch.Address,
            branch.Phone,
            branch.TimeZoneId,
            branch.CurrencyCode,
            branch.Status.ToString(),
            branch.CreatedAt,
            branch.UpdatedAt);
    }

    private static void ValidateRequest(CreateBranchRequest request)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Request is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Name is required.");
        }
    }

    private static void ValidateRequest(UpdateBranchRequest request)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Request is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Name is required.");
        }
    }

    private static string NormalizeRequiredText(string? value, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(errorMessage);
        }

        return value.Trim();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string NormalizeOrDefaultText(string? value, string defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return value.Trim();
    }

    private static bool MatchesNormalizedValue(string? candidateNormalizedValue, string? candidateRawValue, string requestedNormalizedValue)
    {
        if (!string.IsNullOrWhiteSpace(candidateNormalizedValue) &&
            string.Equals(candidateNormalizedValue, requestedNormalizedValue, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(candidateRawValue))
        {
            return false;
        }

        return string.Equals(candidateRawValue.Trim().ToUpperInvariant(), requestedNormalizedValue, StringComparison.OrdinalIgnoreCase);
    }

    private static CountryProfile ResolveCountryProfile(string? requestedCountryCode, CountryProfile restaurantProfile)
    {
        if (string.IsNullOrWhiteSpace(requestedCountryCode))
        {
            return restaurantProfile;
        }

        return CountryProfileCatalog.GetRequired(requestedCountryCode);
    }

    private static string Serialize(object? value)
    {
        return value is null ? string.Empty : JsonSerializer.Serialize(value);
    }
}
