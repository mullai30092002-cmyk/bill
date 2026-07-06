using BillSoft.Application.Auth;
using BillSoft.Application.Restaurants;
using BillSoft.Domain.Restaurants;
using BillSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillSoft.Infrastructure.Restaurants;

public sealed class BranchAdminReadService : IBranchAdminReadService
{
    private readonly BillSoftDbContext _context;

    public BranchAdminReadService(BillSoftDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<BranchListResponse> ListAsync(AuthUserContext currentUser, BranchListQuery query, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var branchesQuery = _context.Branches
            .AsNoTracking()
            .Where(branch => branch.RestaurantId == restaurantId);

        var status = ResolveStatus(query.Status);
        if (status.HasValue)
        {
            branchesQuery = branchesQuery.Where(branch => branch.Status == status.Value);
        }

        var search = NormalizeSearch(query.Search);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchPattern = EscapeLikePattern(search);
            branchesQuery = branchesQuery.Where(branch =>
                EF.Functions.Like(branch.Name, $"%{searchPattern}%", "\\") ||
                (branch.Address != null && EF.Functions.Like(branch.Address, $"%{searchPattern}%", "\\")) ||
                (branch.Phone != null && EF.Functions.Like(branch.Phone, $"%{searchPattern}%", "\\")));
        }

        var branches = await branchesQuery
            .OrderBy(branch => branch.Status != BranchStatus.Active)
            .ThenBy(branch => branch.Name)
            .ToListAsync(cancellationToken);

        var items = branches
            .Select(branch => new BranchListItem(
                branch.BranchId,
                branch.RestaurantId,
                branch.CountryCode,
                branch.Name,
                branch.Address,
                branch.Phone,
                branch.TimeZoneId,
                branch.CurrencyCode,
                branch.Status.ToString()))
            .ToArray();

        return new BranchListResponse(items);
    }

    public async Task<BranchDetail> GetAsync(AuthUserContext currentUser, Guid branchId, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var branch = await _context.Branches
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.BranchId == branchId && entity.RestaurantId == restaurantId, cancellationToken);

        if (branch is null)
        {
            throw new KeyNotFoundException("Branch not found.");
        }

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

    private static Guid RequireRestaurantScope(AuthUserContext currentUser)
    {
        if (currentUser.RestaurantId == Guid.Empty)
        {
            throw new InvalidOperationException("Authentication context is missing the restaurant scope.");
        }

        return currentUser.RestaurantId;
    }

    private static BranchStatus? ResolveStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        if (!Enum.TryParse<BranchStatus>(status, true, out var parsed))
        {
            throw new InvalidOperationException(GetAllowedBranchStatusesMessage());
        }

        return parsed;
    }

    private static string GetAllowedBranchStatusesMessage()
    {
        var allowedStatuses = Enum.GetNames<BranchStatus>();
        return $"Status filter must be one of: {string.Join(", ", allowedStatuses)}.";
    }

    private static string? NormalizeSearch(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        return search.Trim();
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
    }
}
