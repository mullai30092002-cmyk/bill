using System.Globalization;
using BillSoft.Application.Auth;
using BillSoft.Application.Reports;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Vendors;
using BillSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillSoft.Infrastructure.Reports;

public sealed class VendorPayablesReportService : IVendorPayablesReportService
{
    private readonly BillSoftDbContext _context;

    public VendorPayablesReportService(BillSoftDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<VendorPayablesReportResponse> GetVendorPayablesReportAsync(
        AuthUserContext currentUser,
        DateTime? fromDate,
        DateTime? toDate,
        Guid? branchId,
        CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var branchScope = await ResolveBranchScopeAsync(currentUser, restaurantId, branchId, cancellationToken);
        var scopedBranchId = branchScope?.BranchId;
        var currentBranchId = currentUser.BranchId;
        var (reportFromDate, reportToDate) = ResolveReportDateRange(fromDate, toDate);
        var settlementFrom = new DateTimeOffset(reportFromDate.Date, TimeSpan.Zero);
        var settlementToExclusive = new DateTimeOffset(reportToDate.Date.AddDays(1), TimeSpan.Zero);

        var billsQuery = _context.VendorBills
            .AsNoTracking()
            .Include(entity => entity.Lines)
            .Where(entity =>
                entity.RestaurantId == restaurantId &&
                entity.BillDate >= reportFromDate.Date &&
                entity.BillDate <= reportToDate.Date);

        if (branchScope is not null)
        {
            billsQuery = billsQuery.Where(entity => entity.BranchId == branchScope.BranchId);
        }
        else if (currentUser.BranchId.HasValue)
        {
            billsQuery = billsQuery.Where(entity => entity.BranchId == currentUser.BranchId.Value);
        }

        var bills = (await billsQuery.ToListAsync(cancellationToken))
            .OrderByDescending(entity => entity.BillDate)
            .ThenByDescending(entity => entity.CreatedAtUtc)
            .ToArray();

        var vendorIds = bills.Select(entity => entity.VendorId).Distinct().ToArray();
        var vendorLookup = vendorIds.Length == 0
            ? new Dictionary<Guid, VendorSnapshot>()
            : await _context.Vendors
                .AsNoTracking()
                .Where(entity => entity.RestaurantId == restaurantId && vendorIds.Contains(entity.VendorId))
                .Select(entity => new VendorSnapshot(
                    entity.VendorId,
                    entity.Name,
                    entity.VendorType.ToString()))
                .ToDictionaryAsync(entity => entity.VendorId, cancellationToken);

        var branchIds = bills.Select(entity => entity.BranchId).Distinct().ToArray();
        var branchLookup = branchIds.Length == 0
            ? new Dictionary<Guid, BranchSnapshot>()
            : await _context.Branches
                .AsNoTracking()
                .Where(entity => entity.RestaurantId == restaurantId && branchIds.Contains(entity.BranchId))
                .Select(entity => new BranchSnapshot(entity.BranchId, entity.Name, entity.CurrencyCode))
                .ToDictionaryAsync(entity => entity.BranchId, cancellationToken);

        var billLookup = bills.ToDictionary(entity => entity.VendorBillId);
        var billIds = bills.Select(entity => entity.VendorBillId).ToArray();

        var activeBills = bills.Where(entity => entity.Status != VendorBillStatus.Cancelled).ToArray();

        var settlementTotalsQuery = from settlement in _context.VendorSettlements
                .AsNoTracking()
                .Where(entity =>
                    entity.RestaurantId == restaurantId &&
                    entity.Status == VendorSettlementStatus.Active)
            join bill in _context.VendorBills.AsNoTracking() on settlement.VendorBillId equals bill.VendorBillId
            where bill.RestaurantId == restaurantId &&
                  bill.Status != VendorBillStatus.Cancelled &&
                  (!scopedBranchId.HasValue || bill.BranchId == scopedBranchId.Value) &&
                  (!currentBranchId.HasValue || bill.BranchId == currentBranchId.Value)
            select new
            {
                bill.VendorBillId,
                settlement.Amount
            };

        var settlementTotalsByBill = (await settlementTotalsQuery
                .ToListAsync(cancellationToken))
            .GroupBy(entity => entity.VendorBillId)
            .ToDictionary(group => group.Key, group => RoundMoney(group.Sum(entity => entity.Amount)));

        var settlementRows = billIds.Length == 0
            ? []
            : await _context.VendorSettlements
                .AsNoTracking()
                .Where(entity =>
                    entity.RestaurantId == restaurantId &&
                    entity.Status == VendorSettlementStatus.Active &&
                    billIds.Contains(entity.VendorBillId))
                .Select(entity => new SettlementRow(
                    entity.VendorSettlementId,
                    entity.VendorBillId,
                    entity.PaidAtUtc,
                    entity.PaymentMode,
                    entity.Amount,
                    entity.ReferenceNumber))
                .ToListAsync(cancellationToken);

        var settlementHistory = settlementRows
            .Where(entity => entity.PaidAtUtc >= settlementFrom && entity.PaidAtUtc < settlementToExclusive)
            .Select(entity =>
            {
                var bill = billLookup[entity.VendorBillId];
                vendorLookup.TryGetValue(bill.VendorId, out var vendor);
                branchLookup.TryGetValue(bill.BranchId, out var branch);
                return new SettlementSnapshot(
                    entity.VendorSettlementId,
                    bill.BillNumber,
                    vendor?.Name ?? string.Empty,
                    branch?.Name,
                    entity.PaidAtUtc,
                    entity.PaymentMode,
                    entity.Amount,
                    entity.ReferenceNumber);
            })
            .OrderByDescending(entity => entity.PaidAtUtc)
            .ThenByDescending(entity => entity.VendorSettlementId)
            .ToArray();

        var inventoryItemIds = activeBills
            .SelectMany(entity => entity.Lines)
            .Where(entity => entity.InventoryItemId.HasValue)
            .Select(entity => entity.InventoryItemId!.Value)
            .Distinct()
            .ToArray();

        var inventoryLookup = inventoryItemIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await _context.InventoryItems
                .AsNoTracking()
                .Where(entity => entity.RestaurantId == restaurantId && inventoryItemIds.Contains(entity.InventoryItemId))
                .Select(entity => new { entity.InventoryItemId, entity.Name })
                .ToDictionaryAsync(entity => entity.InventoryItemId, entity => entity.Name, cancellationToken);

        var activeBillTotals = BuildActiveBillTotals(activeBills, settlementTotalsByBill);
        var summary = BuildSummary(bills, activeBills, activeBillTotals, reportToDate);
        var vendorBalances = BuildVendorBalances(bills, activeBillTotals, vendorLookup, reportToDate);
        var overdueBills = BuildOverdueBills(activeBills, activeBillTotals, vendorLookup, branchLookup, reportToDate);
        var inventoryTotals = BuildInventoryTotals(activeBills, inventoryLookup);

        return new VendorPayablesReportResponse(
            restaurant.RestaurantId,
            restaurant.RestaurantCode,
            restaurant.Name,
            branchScope?.BranchId,
            branchScope?.Name,
            reportFromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            reportToDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            branchScope?.CurrencyCode ?? restaurant.CurrencyCode,
            DateTimeOffset.UtcNow,
            summary,
            vendorBalances,
            overdueBills,
            BuildSettlementHistory(settlementHistory),
            inventoryTotals);
    }

    private static VendorPayablesReportSummary BuildSummary(
        IReadOnlyCollection<VendorBill> bills,
        IReadOnlyCollection<VendorBill> activeBills,
        IReadOnlyDictionary<Guid, BillTotalSnapshot> activeBillTotals,
        DateTime reportToDate)
    {
        var totalPurchaseAmount = RoundMoney(activeBills.Sum(entity => entity.TotalAmount));
        var totalPaidAmount = RoundMoney(activeBillTotals.Values.Sum(entity => entity.PaidAmount));
        var totalOutstandingAmount = RoundMoney(activeBillTotals.Values.Sum(entity => entity.OutstandingAmount));

        return new VendorPayablesReportSummary(
            TotalVendorBills: bills.Count,
            TotalPurchaseAmount: totalPurchaseAmount,
            TotalPaidAmount: totalPaidAmount,
            TotalOutstandingAmount: totalOutstandingAmount,
            UnpaidBillCount: activeBills.Count(entity => entity.Status == VendorBillStatus.Unpaid),
            PartiallyPaidBillCount: activeBills.Count(entity => entity.Status == VendorBillStatus.PartiallyPaid),
            PaidBillCount: activeBills.Count(entity => entity.Status == VendorBillStatus.Paid),
            CancelledBillCount: bills.Count(entity => entity.Status == VendorBillStatus.Cancelled),
            OverdueBillCount: activeBills.Count(entity => entity.DueDate.HasValue && entity.DueDate.Value.Date < reportToDate.Date && activeBillTotals.TryGetValue(entity.VendorBillId, out var total) && total.OutstandingAmount > 0m));
    }

    private static IReadOnlyCollection<VendorPayablesVendorBalance> BuildVendorBalances(
        IReadOnlyCollection<VendorBill> bills,
        IReadOnlyDictionary<Guid, BillTotalSnapshot> activeBillTotals,
        IReadOnlyDictionary<Guid, VendorSnapshot> vendorLookup,
        DateTime reportToDate)
    {
        return bills
            .GroupBy(entity => entity.VendorId)
            .Select(group =>
            {
                vendorLookup.TryGetValue(group.Key, out var vendor);
                var activeGroup = group.Where(entity => entity.Status != VendorBillStatus.Cancelled).ToArray();
                var totals = activeGroup.Length == 0
                    ? Array.Empty<BillTotalSnapshot>()
                    : activeGroup.Select(entity => activeBillTotals[entity.VendorBillId]).ToArray();
                return new VendorPayablesVendorBalance(
                    group.Key,
                    vendor?.Name ?? string.Empty,
                    vendor?.VendorType ?? string.Empty,
                    group.Count(),
                    RoundMoney(activeGroup.Sum(entity => entity.TotalAmount)),
                    RoundMoney(totals.Sum(entity => entity.PaidAmount)),
                    RoundMoney(totals.Sum(entity => entity.OutstandingAmount)),
                    activeGroup.Count(entity => entity.Status == VendorBillStatus.Unpaid),
                    activeGroup.Count(entity => entity.Status == VendorBillStatus.PartiallyPaid),
                    activeGroup.Count(entity => entity.DueDate.HasValue && entity.DueDate.Value.Date < reportToDate.Date && activeBillTotals.TryGetValue(entity.VendorBillId, out var total) && total.OutstandingAmount > 0m));
            })
            .OrderByDescending(entity => entity.OutstandingAmount)
            .ThenBy(entity => entity.VendorName)
            .ToArray();
    }

    private static IReadOnlyCollection<VendorPayablesOverdueBillItem> BuildOverdueBills(
        IReadOnlyCollection<VendorBill> activeBills,
        IReadOnlyDictionary<Guid, BillTotalSnapshot> activeBillTotals,
        IReadOnlyDictionary<Guid, VendorSnapshot> vendorLookup,
        IReadOnlyDictionary<Guid, BranchSnapshot> branchLookup,
        DateTime reportToDate)
    {
        return activeBills
            .Where(entity => entity.DueDate.HasValue && entity.DueDate.Value.Date < reportToDate.Date && activeBillTotals.TryGetValue(entity.VendorBillId, out var total) && total.OutstandingAmount > 0m)
            .OrderBy(entity => entity.DueDate)
            .ThenByDescending(entity => entity.BillDate)
            .Select(entity =>
            {
                vendorLookup.TryGetValue(entity.VendorId, out var vendor);
                branchLookup.TryGetValue(entity.BranchId, out var branch);
                var totals = activeBillTotals[entity.VendorBillId];

                return new VendorPayablesOverdueBillItem(
                    entity.BillNumber,
                    vendor?.Name ?? string.Empty,
                    vendor?.VendorType ?? string.Empty,
                    branch?.Name,
                    entity.BillDate,
                    entity.DueDate,
                    totals.TotalAmount,
                    totals.PaidAmount,
                    totals.OutstandingAmount,
                    entity.Status.ToString());
            })
            .ToArray();
    }

    private static IReadOnlyCollection<VendorPayablesSettlementItem> BuildSettlementHistory(
        IReadOnlyCollection<SettlementSnapshot> settlements)
    {
        return settlements
            .OrderByDescending(entity => entity.PaidAtUtc)
            .ThenByDescending(entity => entity.VendorSettlementId)
            .Select(entity => new VendorPayablesSettlementItem(
                entity.VendorName,
                entity.BillNumber,
                entity.BranchName,
                entity.PaidAtUtc,
                entity.Amount,
                entity.PaymentMode.ToString(),
                MaskReferenceNumber(entity.ReferenceNumber)))
            .ToArray();
    }

    private static IReadOnlyCollection<VendorPayablesInventoryPurchaseTotal> BuildInventoryTotals(
        IReadOnlyCollection<VendorBill> activeBills,
        IReadOnlyDictionary<Guid, string> inventoryLookup)
    {
        return activeBills
            .SelectMany(entity => entity.Lines.Where(line => line.InventoryItemId.HasValue || !string.IsNullOrWhiteSpace(line.Description))
                .Select(line => new
                {
                    GroupKey = line.InventoryItemId.HasValue
                        ? $"item:{line.InventoryItemId.Value}"
                        : $"line:{line.Description.Trim()}",
                    InventoryItemName = line.InventoryItemId.HasValue && inventoryLookup.TryGetValue(line.InventoryItemId.Value, out var itemName)
                        ? itemName
                        : line.Description.Trim(),
                    line.Quantity,
                    line.LineTotal
                }))
            .GroupBy(entity => entity.GroupKey)
            .Select(group =>
            {
                return new VendorPayablesInventoryPurchaseTotal(
                    group.First().InventoryItemName,
                    RoundMoney(group.Sum(entity => entity.Quantity)),
                    RoundMoney(group.Sum(entity => entity.LineTotal)));
            })
            .OrderByDescending(entity => entity.Amount)
            .ThenBy(entity => entity.InventoryItemName)
            .ToArray();
    }

    private static IReadOnlyDictionary<Guid, BillTotalSnapshot> BuildActiveBillTotals(IReadOnlyCollection<VendorBill> activeBills)
    {
        throw new NotSupportedException("Use the overload that accepts settlement totals.");
    }

    private static IReadOnlyDictionary<Guid, BillTotalSnapshot> BuildActiveBillTotals(
        IReadOnlyCollection<VendorBill> activeBills,
        IReadOnlyDictionary<Guid, decimal> settlementTotalsByBill)
    {
        return activeBills.ToDictionary(
            entity => entity.VendorBillId,
            entity =>
            {
                var paidAmount = settlementTotalsByBill.TryGetValue(entity.VendorBillId, out var totalPaid)
                    ? RoundMoney(totalPaid)
                    : 0m;
                var outstandingAmount = RoundMoney(Math.Max(0m, entity.TotalAmount - paidAmount));
                return new BillTotalSnapshot(entity.TotalAmount, paidAmount, outstandingAmount);
            });
    }

    private static DateTime? ResolveDate(DateTime? date)
    {
        if (!date.HasValue)
        {
            return null;
        }

        return DateTime.SpecifyKind(date.Value.Date, DateTimeKind.Utc);
    }

    private static (DateTime FromDate, DateTime ToDate) ResolveReportDateRange(DateTime? fromDate, DateTime? toDate)
    {
        var today = DateTime.UtcNow.Date;
        var currentMonthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var resolvedFrom = ResolveDate(fromDate) ?? currentMonthStart;
        var resolvedTo = ResolveDate(toDate) ?? today;

        if (resolvedFrom > resolvedTo)
        {
            throw new InvalidOperationException("From date cannot be after to date.");
        }

        return (resolvedFrom, resolvedTo);
    }

    private async Task<RestaurantSnapshot> LoadRestaurantAsync(Guid restaurantId, CancellationToken cancellationToken)
    {
        var restaurant = await _context.Restaurants
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.RestaurantId == restaurantId, cancellationToken);

        if (restaurant is null)
        {
            throw new KeyNotFoundException("Restaurant not found.");
        }

        return new RestaurantSnapshot(restaurant.RestaurantId, restaurant.RestaurantCode, restaurant.Name, restaurant.CurrencyCode);
    }

    private async Task<BranchSnapshot?> ResolveBranchScopeAsync(
        AuthUserContext currentUser,
        Guid restaurantId,
        Guid? requestedBranchId,
        CancellationToken cancellationToken)
    {
        if (currentUser.BranchId.HasValue)
        {
            if (requestedBranchId.HasValue && requestedBranchId.Value != currentUser.BranchId.Value)
            {
                throw new UnauthorizedAccessException("Branch access is restricted to the current branch.");
            }

            return await LoadBranchAsync(restaurantId, currentUser.BranchId.Value, cancellationToken);
        }

        if (requestedBranchId.HasValue)
        {
            return await LoadBranchAsync(restaurantId, requestedBranchId.Value, cancellationToken);
        }

        return null;
    }

    private async Task<BranchSnapshot> LoadBranchAsync(Guid restaurantId, Guid branchId, CancellationToken cancellationToken)
    {
        var branch = await _context.Branches
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.RestaurantId == restaurantId && entity.BranchId == branchId, cancellationToken);

        if (branch is null)
        {
            throw new KeyNotFoundException("Branch not found.");
        }

        return new BranchSnapshot(branch.BranchId, branch.Name, branch.CurrencyCode);
    }

    private static Guid RequireRestaurantScope(AuthUserContext currentUser)
    {
        if (currentUser.RestaurantId == Guid.Empty)
        {
            throw new InvalidOperationException("Authentication context is missing the restaurant scope.");
        }

        return currentUser.RestaurantId;
    }

    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string? MaskReferenceNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= 4)
        {
            return "****";
        }

        return $"****{trimmed[^4..]}";
    }

    private sealed record RestaurantSnapshot(Guid RestaurantId, string RestaurantCode, string Name, string CurrencyCode);

    private sealed record BranchSnapshot(Guid BranchId, string Name, string CurrencyCode);

    private sealed record VendorSnapshot(Guid VendorId, string Name, string VendorType);

    private sealed record SettlementSnapshot(
        Guid VendorSettlementId,
        string? BillNumber,
        string VendorName,
        string? BranchName,
        DateTimeOffset PaidAtUtc,
        VendorSettlementPaymentMode PaymentMode,
        decimal Amount,
        string? ReferenceNumber);

    private sealed record SettlementRow(
        Guid VendorSettlementId,
        Guid VendorBillId,
        DateTimeOffset PaidAtUtc,
        VendorSettlementPaymentMode PaymentMode,
        decimal Amount,
        string? ReferenceNumber);

    private sealed record BillTotalSnapshot(decimal TotalAmount, decimal PaidAmount, decimal OutstandingAmount);
}
