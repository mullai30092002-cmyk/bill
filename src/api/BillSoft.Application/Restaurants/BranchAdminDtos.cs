namespace BillSoft.Application.Restaurants;

public sealed record BranchListQuery(
    string? Status,
    string? Search);

public sealed record BranchListItem(
    Guid BranchId,
    Guid RestaurantId,
    string CountryCode,
    string Name,
    string? Address,
    string? Phone,
    string Timezone,
    string Currency,
    string Status);

public sealed record BranchListResponse(
    IReadOnlyCollection<BranchListItem> Items);

public sealed record BranchDetail(
    Guid BranchId,
    Guid RestaurantId,
    string CountryCode,
    string Name,
    string? Address,
    string? Phone,
    string Timezone,
    string Currency,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CreateBranchRequest(
    string? Name,
    string? Address,
    string? Phone,
    string? CountryCode,
    string? Timezone,
    string? Currency);

public sealed record UpdateBranchRequest(
    string? Name,
    string? Address,
    string? Phone,
    string? CountryCode,
    string? Timezone,
    string? Currency);
