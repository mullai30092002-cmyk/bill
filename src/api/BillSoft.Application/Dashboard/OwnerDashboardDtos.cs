namespace BillSoft.Application.Dashboard;

public sealed record OwnerDashboardResponse(
    Guid RestaurantId,
    string RestaurantCode,
    string RestaurantName,
    Guid? BranchId,
    string? BranchName,
    string BusinessDate,
    string CurrencyCode,
    DateTimeOffset GeneratedAt,
    OwnerDashboardMetrics Metrics,
    IReadOnlyCollection<OwnerDashboardAlert> Alerts,
    OwnerDashboardInventoryAlerts InventoryAlerts,
    OwnerDashboardVendorDues VendorDues,
    IReadOnlyCollection<OwnerDashboardQuickLink> QuickLinks);

public sealed record OwnerDashboardMetrics(
    decimal GrossSales,
    decimal NetSales,
    decimal CashPayments,
    decimal NonCashPayments,
    decimal TotalAmountPaid,
    decimal TotalBalanceDue,
    int UnpaidBills,
    int CancelledBills,
    int CancelledPayments,
    int ReceiptReprints,
    decimal CashVarianceTotal,
    int OpenShifts);

public sealed record OwnerDashboardAlert(
    string Type,
    string Title,
    string Message,
    string Severity,
    int Count,
    decimal? Amount,
    string TargetPath);

public sealed record OwnerDashboardInventoryAlerts(
    int LowStockCount,
    int OutOfStockCount,
    int TotalAlertCount,
    IReadOnlyCollection<OwnerDashboardInventoryAlertItem> CriticalItems);

public sealed record OwnerDashboardInventoryAlertItem(
    Guid InventoryItemId,
    string Name,
    string Category,
    string Unit,
    decimal CurrentQuantity,
    decimal MinimumQuantity,
    string Status,
    DateTimeOffset? LastUpdatedAt);

public sealed record OwnerDashboardVendorDues(
    decimal TotalVendorOutstanding,
    int OverdueVendorCount,
    int VendorsWithOutstandingCount,
    IReadOnlyCollection<OwnerDashboardCriticalVendorDue> CriticalVendors);

public sealed record OwnerDashboardCriticalVendorDue(
    Guid VendorId,
    string VendorName,
    string VendorType,
    Guid? BranchId,
    string? BranchName,
    decimal OutstandingAmount,
    DateTime? OldestDueDate,
    int OpenBillCount);

public sealed record OwnerDashboardQuickLink(
    string Label,
    string Path,
    string Description);
