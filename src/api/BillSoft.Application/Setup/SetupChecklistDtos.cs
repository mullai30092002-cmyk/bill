namespace BillSoft.Application.Setup;

public sealed record SetupChecklistResponse(
    Guid RestaurantId,
    string RestaurantName,
    string BusinessType,
    Guid? BranchId,
    string? BranchName,
    int CompletionPercent,
    int CompletedCount,
    int TotalCount,
    IReadOnlyCollection<SetupChecklistItem> Items);

public sealed record SetupChecklistItem(
    string Key,
    string Title,
    string Description,
    string Status,
    string ActionLabel,
    string ActionHref,
    int Count,
    int? WarningCount,
    string Priority);
