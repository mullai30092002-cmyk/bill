namespace BillSoft.Application.Reports;

public sealed record DailyCashSalesReportResponse(
    Guid RestaurantId,
    string RestaurantCode,
    string RestaurantName,
    Guid? BranchId,
    string? BranchName,
    string BusinessDate,
    string CurrencyCode,
    DateTimeOffset GeneratedAt,
    DailyCashSalesReportSummary Summary,
    IReadOnlyCollection<DailyCashSalesPaymentBreakdown> PaymentBreakdown,
    IReadOnlyCollection<DailyCashSalesCashShiftSummary> CashShiftSummaries,
    DailyCashSalesReportExceptions Exceptions);

public sealed record DailyCashSalesReportSummary(
    int TotalBills,
    int PaidBills,
    int PartiallyPaidBills,
    int UnpaidBills,
    int CancelledBills,
    decimal GrossSales,
    decimal GrossBillTotal,
    decimal CancelledBillAmount,
    decimal NetSales,
    decimal TotalAmountPaid,
    decimal TotalBalanceDue,
    decimal CashPayments,
    decimal UpiPayments,
    decimal CardPayments,
    decimal OtherPayments,
    decimal NonCashPayments,
    int OpenShifts,
    int ClosedShifts,
    decimal OpeningCashTotal,
    decimal DeclaredClosingCashTotal,
    decimal ExpectedCashTotal,
    int ReceiptPrints,
    int ReceiptReprints,
    decimal CashVarianceTotal);

public sealed record DailyCashSalesPaymentBreakdown(
    string PaymentMode,
    decimal RecordedAmount,
    decimal CancelledAmount,
    decimal NetAmount,
    int PaymentCount,
    int CancelledCount);

public sealed record DailyCashSalesCashShiftSummary(
    Guid CashierShiftId,
    Guid BranchId,
    string BranchName,
    string Status,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    decimal OpeningCashAmount,
    decimal ExpectedCashAmount,
    decimal? CountedCashAmount,
    decimal? CashVarianceAmount,
    decimal CashMovementTotal,
    decimal CashPaymentTotal);

public sealed record DailyCashSalesReportExceptions(
    IReadOnlyCollection<DailyCashSalesExceptionItem> UnpaidBills,
    IReadOnlyCollection<DailyCashSalesExceptionItem> CancelledBills,
    IReadOnlyCollection<DailyCashSalesExceptionItem> CancelledPayments,
    IReadOnlyCollection<DailyCashSalesExceptionItem> ReceiptReprints,
    IReadOnlyCollection<DailyCashSalesExceptionItem> CashVariances,
    IReadOnlyCollection<DailyCashSalesExceptionItem> OpenShifts);

public sealed record DailyCashSalesExceptionItem(
    string Id,
    string ReferenceNumber,
    Guid BranchId,
    string BranchName,
    decimal? Amount,
    string Status,
    DateTimeOffset OccurredAt,
    string? Reason,
    string Severity,
    int? PrintCount = null,
    int? ReprintCount = null,
    decimal? BalanceDue = null,
    decimal? ExpectedCashAmount = null,
    decimal? CountedCashAmount = null,
    decimal? VarianceAmount = null);
