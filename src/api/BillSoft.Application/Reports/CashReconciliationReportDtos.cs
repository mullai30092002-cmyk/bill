namespace BillSoft.Application.Reports;

public sealed record CashReconciliationReportResponse(
    Guid RestaurantId,
    string RestaurantName,
    Guid? BranchId,
    string? BranchName,
    string BusinessDate,
    DateTimeOffset GeneratedAtUtc,
    string CurrencyCode,
    CashReconciliationReportTotals Totals,
    IReadOnlyCollection<CashReconciliationShiftRow> Shifts);

public sealed record CashReconciliationReportTotals(
    int ShiftCount,
    int OpenShiftCount,
    int ClosedShiftCount,
    decimal OpeningCashTotal,
    decimal CashPaymentTotal,
    decimal CashInTotal,
    decimal CashOutTotal,
    decimal AdjustmentTotal,
    decimal ExpectedCashTotal,
    decimal DeclaredCashTotal,
    decimal VarianceTotal,
    int MajorVarianceCount,
    int MinorVarianceCount,
    int BalancedShiftCount);

public sealed record CashReconciliationShiftRow(
    Guid CashierShiftId,
    Guid BranchId,
    string BranchName,
    Guid CashierUserId,
    string CashierName,
    string Status,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    decimal OpeningCashAmount,
    decimal CashPaymentTotal,
    decimal CashInTotal,
    decimal CashOutTotal,
    decimal AdjustmentTotal,
    decimal ExpectedCashAmount,
    decimal? DeclaredClosingCashAmount,
    decimal? VarianceAmount,
    string VarianceStatus,
    int PaymentCount,
    int MovementCount,
    string? ClosingNote);
