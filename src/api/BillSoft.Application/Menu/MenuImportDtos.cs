namespace BillSoft.Application.Menu;

public sealed record MenuImportPreviewRequest(
    string? CsvText,
    string? ImportName = null);

public sealed record MenuImportRowDecision(
    int RowNumber,
    string Action);

public sealed record MenuImportConfirmRequest(
    string? CsvText,
    string? ImportName = null,
    IReadOnlyCollection<MenuImportRowDecision>? Decisions = null);

public sealed record MenuImportPreviewRow(
    int RowNumber,
    string Category,
    string ItemName,
    string? Description,
    decimal? EatInPrice,
    bool? Available,
    string? BranchName,
    string Status,
    string Message,
    IReadOnlyCollection<string> Errors,
    IReadOnlyCollection<string> Warnings,
    bool IsDuplicate,
    string? ExistingCategoryName,
    string? ExistingMenuItemId,
    string SuggestedAction);

public sealed record MenuImportSummary(
    int TotalRows,
    int ReadyRows,
    int DuplicateRows,
    int InvalidRows,
    int ImportedRows,
    int UpdatedRows,
    int SkippedRows,
    int FailedRows);

public sealed record MenuImportResponse(
    string? ImportName,
    MenuImportSummary Summary,
    IReadOnlyCollection<MenuImportPreviewRow> Rows);
