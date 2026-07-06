namespace BillSoft.Application.Reports;

public sealed record VendorPayablesReportResponse(
    Guid RestaurantId,
    string RestaurantCode,
    string RestaurantName,
    Guid? BranchId,
    string? BranchName,
    string FromDate,
    string ToDate,
    string CurrencyCode,
    DateTimeOffset GeneratedAt,
    VendorPayablesReportSummary Summary,
    IReadOnlyCollection<VendorPayablesVendorBalance> VendorBalances,
    IReadOnlyCollection<VendorPayablesOverdueBillItem> OverdueBills,
    IReadOnlyCollection<VendorPayablesSettlementItem> RecentSettlements,
    IReadOnlyCollection<VendorPayablesInventoryPurchaseTotal> InventoryPurchaseTotals);

public sealed record VendorPayablesReportSummary(
    int TotalVendorBills,
    decimal TotalPurchaseAmount,
    decimal TotalPaidAmount,
    decimal TotalOutstandingAmount,
    int UnpaidBillCount,
    int PartiallyPaidBillCount,
    int PaidBillCount,
    int CancelledBillCount,
    int OverdueBillCount);

public sealed record VendorPayablesVendorBalance(
    Guid VendorId,
    string VendorName,
    string VendorType,
    int TotalBills,
    decimal PurchaseAmount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    int UnpaidCount,
    int PartiallyPaidCount,
    int OverdueCount);

public sealed record VendorPayablesOverdueBillItem(
    string? BillNumber,
    string VendorName,
    string VendorType,
    string? BranchName,
    DateTime BillDate,
    DateTime? DueDate,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    string Status);

public sealed record VendorPayablesSettlementItem(
    string VendorName,
    string? BillNumber,
    string? BranchName,
    DateTimeOffset PaidAtUtc,
    decimal Amount,
    string PaymentMode,
    string? ReferenceNumberMasked);

public sealed record VendorPayablesInventoryPurchaseTotal(
    string InventoryItemName,
    decimal Quantity,
    decimal Amount);
