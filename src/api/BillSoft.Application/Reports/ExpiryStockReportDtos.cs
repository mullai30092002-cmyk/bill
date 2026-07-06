namespace BillSoft.Application.Reports;

public sealed record ExpiryStockReportQuery(
    Guid? BranchId,
    DateTime? AsOfDate);

public sealed record ExpiryStockReportResponse(
    Guid BranchId,
    string BranchName,
    string AsOfDate,
    ExpiryStockReportTotals Totals,
    IReadOnlyCollection<ExpiryStockReportRow> Rows);

public sealed record ExpiryStockReportTotals(
    int FreshCount,
    int NearExpiryCount,
    int ExpiredCount,
    int NoExpiryCount,
    int TotalTrackedItems);

public sealed record ExpiryStockReportRow(
    Guid InventoryItemId,
    string InventoryItemName,
    string UnitOfMeasure,
    string SourceType,
    string? BatchReference,
    decimal Quantity,
    DateTimeOffset? ProducedOrReceivedAt,
    DateTimeOffset? ExpiresAtUtc,
    string ExpiryStatus,
    string? WarningReason,
    string? SourceReference);
