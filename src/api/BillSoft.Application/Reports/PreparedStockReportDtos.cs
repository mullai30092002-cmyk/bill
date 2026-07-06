namespace BillSoft.Application.Reports;

public sealed record PreparedStockReportQuery(
    Guid? BranchId,
    DateTime? BusinessDate);

public sealed record PreparedStockReportResponse(
    Guid BranchId,
    string BranchName,
    string BusinessDate,
    PreparedStockReportTotals Totals,
    IReadOnlyCollection<PreparedStockReportRow> Rows);

public sealed record PreparedStockReportTotals(
    decimal ProducedQuantity,
    decimal ServedQuantity,
    decimal WastedQuantity,
    decimal RemainingQuantity,
    int ItemCount,
    int WarningCount);

public sealed record PreparedStockReportRow(
    Guid MenuItemId,
    string? MenuItemName,
    Guid? PreparedInventoryItemId,
    string? PreparedInventoryItemName,
    string? UnitOfMeasure,
    decimal ProducedQuantity,
    decimal ServedQuantity,
    decimal WastedQuantity,
    decimal RemainingQuantity,
    bool HasWarning,
    string? WarningReason);
