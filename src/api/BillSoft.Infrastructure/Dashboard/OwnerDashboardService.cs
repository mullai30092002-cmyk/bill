using System.Globalization;
using BillSoft.Application.Auth;
using BillSoft.Application.Dashboard;
using BillSoft.Application.Inventory;
using BillSoft.Application.Reports;
using BillSoft.Domain.Vendors;
using BillSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillSoft.Infrastructure.Dashboard;

public sealed class OwnerDashboardService : IOwnerDashboardService
{
    private const string CashVarianceExplanation = "Counted closing cash minus expected cash (opening cash + recorded cash payments).";
    private const int InventoryCriticalItemLimit = 5;

    private static readonly IReadOnlyCollection<OwnerDashboardQuickLink> QuickLinks =
        [
            new OwnerDashboardQuickLink("Daily Report", "/reports/daily-cash-sales", "Open the detailed daily cash sales report."),
            new OwnerDashboardQuickLink("Billing", "/billing", "Review bills, payments, and receipts."),
            new OwnerDashboardQuickLink("Cashier Shifts", "/cashier/shifts", "Inspect shift status and cash control."),
            new OwnerDashboardQuickLink("Kitchen Tickets", "/kitchen/tickets", "Jump to kitchen ticket workflow."),
            new OwnerDashboardQuickLink("POS Orders", "/pos/orders", "Open the order capture workspace.")
        ];

    private readonly IDailyCashSalesReportService _dailyCashSalesReportService;
    private readonly IInventoryService _inventoryService;
    private readonly BillSoftDbContext _context;

    public OwnerDashboardService(
        IDailyCashSalesReportService dailyCashSalesReportService,
        IInventoryService inventoryService,
        BillSoftDbContext context)
    {
        _dailyCashSalesReportService = dailyCashSalesReportService ?? throw new ArgumentNullException(nameof(dailyCashSalesReportService));
        _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<OwnerDashboardResponse> GetOwnerDashboardAsync(
        AuthUserContext currentUser,
        DateTime? businessDate,
        Guid? branchId,
        CancellationToken cancellationToken)
    {
        var report = await _dailyCashSalesReportService.GetDailyCashSalesReportAsync(
            currentUser,
            businessDate,
            branchId,
            cancellationToken);
        var inventoryAlerts = await BuildInventoryAlertsAsync(currentUser, report.BranchId ?? currentUser.BranchId, cancellationToken);
        var vendorDues = await BuildVendorDuesAsync(report, currentUser, cancellationToken);

        var cancelledPayments = report.Exceptions.CancelledPayments.Count;
        var cancelledActivityCount = report.Summary.CancelledBills + cancelledPayments;
        var cancelledActivityAmount = SumAmounts(report.Exceptions.CancelledBills) + SumAmounts(report.Exceptions.CancelledPayments);
        var unpaidBalanceAmount = report.Summary.TotalBalanceDue;
        var receiptReprintCount = report.Summary.ReceiptReprints;
        var cashVarianceAmount = report.Summary.CashVarianceTotal;
        var cashVarianceCount = report.Exceptions.CashVariances.Count;
        var openShiftCount = report.Exceptions.OpenShifts.Count;

        return new OwnerDashboardResponse(
            report.RestaurantId,
            report.RestaurantCode,
            report.RestaurantName,
            report.BranchId,
            report.BranchName,
            report.BusinessDate,
            report.CurrencyCode,
            report.GeneratedAt,
            new OwnerDashboardMetrics(
                report.Summary.GrossSales,
                report.Summary.NetSales,
                report.Summary.CashPayments,
                report.Summary.NonCashPayments,
                report.Summary.TotalAmountPaid,
                report.Summary.TotalBalanceDue,
                report.Summary.UnpaidBills,
                report.Summary.CancelledBills,
                cancelledPayments,
                report.Summary.ReceiptReprints,
                report.Summary.CashVarianceTotal,
                openShiftCount),
            BuildAlerts(
                report.Summary.UnpaidBills,
                unpaidBalanceAmount,
                cancelledActivityCount,
                cancelledActivityAmount,
                receiptReprintCount,
                cashVarianceAmount,
                cashVarianceCount,
                openShiftCount),
            inventoryAlerts,
            vendorDues,
            QuickLinks);
    }

    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private async Task<OwnerDashboardInventoryAlerts> BuildInventoryAlertsAsync(
        AuthUserContext currentUser,
        Guid? branchId,
        CancellationToken cancellationToken)
    {
        if (!branchId.HasValue)
        {
            return new OwnerDashboardInventoryAlerts(0, 0, 0, Array.Empty<OwnerDashboardInventoryAlertItem>());
        }

        var inventory = await _inventoryService.ListItemsAsync(
            currentUser,
            new InventoryItemListQuery(branchId),
            cancellationToken);

        var criticalItems = inventory.Items
            .Where(item => IsCriticalStatus(item.Status))
            .OrderBy(item => IsOutOfStock(item.Status) ? 0 : 1)
            .ThenBy(item => item.CurrentStock)
            .ThenBy(item => item.Name)
            .Take(InventoryCriticalItemLimit)
            .Select(item => new OwnerDashboardInventoryAlertItem(
                item.InventoryItemId,
                item.Name,
                item.Category,
                item.UnitOfMeasure,
                item.CurrentStock,
                item.LowStockThreshold,
                item.Status,
                item.UpdatedAtUtc))
            .ToArray();

        var lowStockCount = inventory.Items.Count(item => IsLowStock(item.Status));
        var outOfStockCount = inventory.Items.Count(item => IsOutOfStock(item.Status));

        return new OwnerDashboardInventoryAlerts(
            lowStockCount,
            outOfStockCount,
            lowStockCount + outOfStockCount,
            criticalItems);
    }

    private static bool IsCriticalStatus(string status) =>
        IsLowStock(status) || IsOutOfStock(status);

    private static bool IsLowStock(string status) =>
        string.Equals(status, "Low stock", StringComparison.OrdinalIgnoreCase);

    private static bool IsOutOfStock(string status) =>
        string.Equals(status, "Out of stock", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyCollection<OwnerDashboardAlert> BuildAlerts(
        int unpaidBillCount,
        decimal unpaidBalanceAmount,
        int cancelledActivityCount,
        decimal cancelledActivityAmount,
        int receiptReprintCount,
        decimal cashVarianceAmount,
        int cashVarianceCount,
        int openShiftCount)
    {
        var alerts = new List<OwnerDashboardAlert>(capacity: 5);

        if (unpaidBalanceAmount > 0m)
        {
            alerts.Add(new OwnerDashboardAlert(
                "UnpaidBills",
                "Unpaid bills need follow-up",
                $"{FormatMoney(unpaidBalanceAmount)} remains unpaid across the selected business date.",
                "High",
                Count: unpaidBillCount,
                unpaidBalanceAmount,
                "/reports/daily-cash-sales"));
        }

        if (cancelledActivityCount > 0)
        {
            alerts.Add(new OwnerDashboardAlert(
                "CancelledActivity",
                "Cancelled activity recorded",
                $"{cancelledActivityCount} cancelled bill or payment event(s) were recorded.",
                "Medium",
                cancelledActivityCount,
                cancelledActivityAmount,
                "/reports/daily-cash-sales"));
        }

        if (receiptReprintCount > 0)
        {
            alerts.Add(new OwnerDashboardAlert(
                "ReceiptReprints",
                "Receipt reprints detected",
                $"{receiptReprintCount} receipt reprint(s) were recorded for the selected date.",
                "Low",
                receiptReprintCount,
                null,
                "/reports/daily-cash-sales"));
        }

        if (cashVarianceAmount != 0m)
        {
            alerts.Add(new OwnerDashboardAlert(
                "CashVariance",
                "Closed shift variance needs review",
                $"Closed shift variance total is {FormatMoney(cashVarianceAmount)} for the selected date. {CashVarianceExplanation}",
                "High",
                cashVarianceCount,
                cashVarianceAmount,
                "/cashier/shifts"));
        }

        if (openShiftCount > 0)
        {
            alerts.Add(new OwnerDashboardAlert(
                "OpenShift",
                "Open shifts remain active",
                $"{openShiftCount} shift(s) are still open for the selected business date.",
                "Medium",
                openShiftCount,
                null,
                "/cashier/shifts"));
        }

        return alerts
            .OrderBy(alert => SeverityRank(alert.Severity))
            .ThenByDescending(alert => alert.Count)
            .ToArray();
    }

    private static int SeverityRank(string severity) =>
        severity switch
        {
            "High" => 0,
            "Medium" => 1,
            "Low" => 2,
            _ => 3
        };

    private static decimal SumAmounts(IEnumerable<DailyCashSalesExceptionItem> items)
    {
        decimal total = 0m;
        foreach (var item in items)
        {
            total += item.Amount ?? 0m;
        }

        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }

    private static string FormatMoney(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private async Task<OwnerDashboardVendorDues> BuildVendorDuesAsync(
        DailyCashSalesReportResponse report,
        AuthUserContext currentUser,
        CancellationToken cancellationToken)
    {
        var businessDate = DateTime.ParseExact(report.BusinessDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var businessDateEndExclusive = DateTime.SpecifyKind(businessDate.Date.AddDays(1), DateTimeKind.Utc);
        var branchScopeId = report.BranchId ?? currentUser.BranchId;

        var billsQuery = _context.VendorBills
            .AsNoTracking()
            .Where(entity =>
                entity.RestaurantId == report.RestaurantId &&
                entity.Status != VendorBillStatus.Cancelled &&
                entity.BillDate <= businessDate.Date);

        if (branchScopeId.HasValue)
        {
            billsQuery = billsQuery.Where(entity => entity.BranchId == branchScopeId.Value);
        }

        var bills = (await billsQuery
            .ToArrayAsync(cancellationToken))
            .OrderBy(entity => entity.BillDate)
            .ThenBy(entity => entity.CreatedAtUtc.UtcDateTime)
            .ToArray();

        if (bills.Length == 0)
        {
            return new OwnerDashboardVendorDues(0m, 0, 0, Array.Empty<OwnerDashboardCriticalVendorDue>());
        }

        var billIds = bills.Select(entity => entity.VendorBillId).ToArray();
        var vendorIds = bills.Select(entity => entity.VendorId).Distinct().ToArray();
        var branchIds = bills.Select(entity => entity.BranchId).Distinct().ToArray();

        var vendorLookup = await _context.Vendors
            .AsNoTracking()
            .Where(entity => entity.RestaurantId == report.RestaurantId && vendorIds.Contains(entity.VendorId))
            .Select(entity => new { entity.VendorId, entity.Name, entity.VendorType })
            .ToDictionaryAsync(entity => entity.VendorId, entity => entity, cancellationToken);

        var branchLookup = branchIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await _context.Branches
                .AsNoTracking()
                .Where(entity => entity.RestaurantId == report.RestaurantId && branchIds.Contains(entity.BranchId))
                .Select(entity => new { entity.BranchId, entity.Name })
                .ToDictionaryAsync(entity => entity.BranchId, entity => entity.Name, cancellationToken);

        var activeSettlements = await _context.VendorSettlements
            .AsNoTracking()
            .Where(entity =>
                entity.RestaurantId == report.RestaurantId &&
                entity.Status == VendorSettlementStatus.Active &&
                billIds.Contains(entity.VendorBillId))
            .Select(entity => new
            {
                entity.VendorBillId,
                entity.Amount,
                entity.PaidAtUtc
            })
            .ToArrayAsync(cancellationToken);

        var billOutstanding = bills.ToDictionary(
            bill => bill.VendorBillId,
            bill =>
            {
                var paidAmount = RoundMoney(activeSettlements
                    .Where(item => item.VendorBillId == bill.VendorBillId && item.PaidAtUtc < businessDateEndExclusive)
                    .Sum(item => item.Amount));
                return RoundMoney(Math.Max(0m, bill.TotalAmount - paidAmount));
            });

        var billRows = bills
            .Select(bill =>
            {
                vendorLookup.TryGetValue(bill.VendorId, out var vendor);
                branchLookup.TryGetValue(bill.BranchId, out var branchName);

                return new
                {
                    bill.VendorId,
                    VendorName = vendor?.Name ?? string.Empty,
                    VendorType = vendor?.VendorType.ToString() ?? string.Empty,
                    bill.BranchId,
                    BranchName = branchName,
                    bill.DueDate,
                    bill.BillDate,
                    bill.VendorBillId,
                    bill.Status,
                    bill.TotalAmount,
                    OutstandingAmount = billOutstanding[bill.VendorBillId]
                };
            })
            .Where(item => item.OutstandingAmount > 0m)
            .ToArray();

        var totalVendorOutstanding = RoundMoney(billRows.Sum(item => item.OutstandingAmount));
        var vendorsWithOutstandingCount = billRows.Select(item => item.VendorId).Distinct().Count();
        var overdueVendorCount = billRows
            .Where(item => item.DueDate.HasValue && item.DueDate.Value.Date < businessDate.Date)
            .Select(item => item.VendorId)
            .Distinct()
            .Count();

        var criticalVendors = billRows
            .GroupBy(item => item.VendorId)
            .Select(group =>
            {
                var ordered = group.OrderBy(item => item.DueDate ?? DateTime.MaxValue).ThenBy(item => item.BillDate).ToArray();
                var first = ordered[0];
                return new OwnerDashboardCriticalVendorDue(
                    group.Key,
                    first.VendorName,
                    first.VendorType,
                    branchScopeId,
                    branchScopeId.HasValue && branchLookup.TryGetValue(branchScopeId.Value, out var scopedBranchName) ? scopedBranchName : first.BranchName,
                    RoundMoney(group.Sum(item => item.OutstandingAmount)),
                    group.Where(item => item.DueDate.HasValue).Select(item => item.DueDate).Min(),
                    group.Count());
            })
            .OrderByDescending(item => item.OutstandingAmount)
            .ThenBy(item => item.OldestDueDate ?? DateTime.MaxValue)
            .Take(InventoryCriticalItemLimit)
            .ToArray();

        return new OwnerDashboardVendorDues(
            totalVendorOutstanding,
            overdueVendorCount,
            vendorsWithOutstandingCount,
            criticalVendors);
    }
}
