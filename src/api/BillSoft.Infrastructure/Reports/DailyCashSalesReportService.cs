using System.Data;
using System.Globalization;
using BillSoft.Application.Auth;
using BillSoft.Application.Reports;
using BillSoft.Domain.Billing;
using BillSoft.Domain.Cashiering;
using BillSoft.Domain.Restaurants;
using BillSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillSoft.Infrastructure.Reports;

public sealed class DailyCashSalesReportService : IDailyCashSalesReportService
{
    private const string CashVarianceExplanation = "Counted closing cash minus expected cash (opening cash + recorded cash payments).";

    private readonly BillSoftDbContext _context;

    public DailyCashSalesReportService(BillSoftDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<DailyCashSalesReportResponse> GetDailyCashSalesReportAsync(
        AuthUserContext currentUser,
        DateTime? businessDate,
        Guid? branchId,
        CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var branchScope = branchId.HasValue ? await LoadBranchAsync(restaurantId, branchId.Value, cancellationToken) : null;
        var timeZone = ResolveTimeZone(branchScope?.TimeZoneId ?? restaurant.TimeZoneId);
        var reportDate = ResolveBusinessDate(businessDate, timeZone, DateTimeOffset.UtcNow);
        var reportBusinessDate = DateTime.SpecifyKind(reportDate.Date, DateTimeKind.Utc);

        var branchSnapshots = await _context.Branches
            .AsNoTracking()
            .Where(entity => entity.RestaurantId == restaurantId)
            .Select(entity => new BranchSnapshot(
                entity.BranchId,
                entity.Name,
                entity.CurrencyCode,
                entity.TimeZoneId))
            .ToListAsync(cancellationToken);

        var branchLookup = branchSnapshots.ToDictionary(snapshot => snapshot.BranchId, snapshot => snapshot);

        var billSnapshots = await _context.Bills
            .AsNoTracking()
            .Where(entity =>
                entity.RestaurantId == restaurantId &&
                entity.BusinessDate == reportBusinessDate &&
                (!branchId.HasValue || entity.BranchId == branchId.Value))
            .Select(entity => new BillSnapshot(
                entity.BillId,
                entity.BranchId,
                entity.BillNumber,
                entity.Status,
                entity.Subtotal,
                entity.TaxTotal,
                entity.GrandTotal,
                entity.AmountPaid,
                entity.BalanceDue,
                entity.CancelReason,
                entity.CreatedAt))
            .ToArrayAsync(cancellationToken);

        var billLookup = billSnapshots.ToDictionary(entity => entity.BillId, entity => entity);

        var paymentSnapshots = await (
            from payment in _context.Payments.AsNoTracking()
            join bill in _context.Bills.AsNoTracking() on payment.BillId equals bill.BillId
            where bill.RestaurantId == restaurantId &&
                  bill.BusinessDate == reportBusinessDate &&
                  (!branchId.HasValue || bill.BranchId == branchId.Value)
            select new PaymentSnapshot(
                payment.PaymentId,
                bill.BranchId,
                payment.PaymentNumber,
                payment.PaymentMode,
                payment.Status,
                payment.Amount,
                payment.CancelReason,
                payment.CreatedAt))
            .ToArrayAsync(cancellationToken);

        var printSnapshots = await (
            from printEvent in _context.BillPrintEvents.AsNoTracking()
            join bill in _context.Bills.AsNoTracking() on printEvent.BillId equals bill.BillId
            where bill.RestaurantId == restaurantId &&
                  bill.BusinessDate == reportBusinessDate &&
                  (!branchId.HasValue || bill.BranchId == branchId.Value)
            select new PrintSnapshot(
                printEvent.BillPrintEventId,
                bill.BranchId,
                printEvent.BillId,
                bill.BillNumber,
                printEvent.PrintSequence,
                printEvent.CreatedAt))
            .ToArrayAsync(cancellationToken);

        var shiftEntities = await _context.CashierShifts
            .AsNoTracking()
            .Include(entity => entity.CashDrawerMovements)
            .Include(entity => entity.Payments)
            .Where(entity =>
                entity.RestaurantId == restaurantId &&
                entity.BusinessDate == reportBusinessDate &&
                (!branchId.HasValue || entity.BranchId == branchId.Value))
            .ToArrayAsync(cancellationToken);

        var billSummary = BuildBillSummary(billSnapshots);
        var paymentBreakdown = BuildPaymentBreakdown(paymentSnapshots);
        var cashShiftSummaries = BuildCashShiftSummaries(shiftEntities, branchLookup);
        var paymentTotals = BuildPaymentTotals(paymentSnapshots);
        var exceptions = BuildExceptions(billSnapshots, billLookup, paymentSnapshots, printSnapshots, cashShiftSummaries, branchLookup);

        var grossSales = RoundMoney(billSnapshots.Sum(entity => entity.GrandTotal));
        var grossBillTotal = RoundMoney(billSnapshots.Where(entity => entity.Status != BillStatus.Cancelled).Sum(entity => entity.GrandTotal));
        var cancelledBillAmount = RoundMoney(billSnapshots.Where(entity => entity.Status == BillStatus.Cancelled).Sum(entity => entity.GrandTotal));
        var netSales = grossBillTotal;
        var totalAmountPaid = paymentTotals.RecordedTotal;
        var totalBalanceDue = RoundMoney(billSnapshots.Where(entity => entity.Status != BillStatus.Cancelled).Sum(entity => entity.BalanceDue));
        var receiptPrints = printSnapshots.Length;
        var receiptReprints = CountReprints(printSnapshots);
        var openShiftCount = cashShiftSummaries.Count(entity => entity.Status == CashierShiftStatus.Open.ToString());
        var closedShiftCount = cashShiftSummaries.Count(entity => entity.Status == CashierShiftStatus.Closed.ToString());
        var openingCashTotal = RoundMoney(cashShiftSummaries.Sum(entity => entity.OpeningCashAmount));
        var declaredClosingCashTotal = RoundMoney(cashShiftSummaries
            .Where(entity => entity.Status == CashierShiftStatus.Closed.ToString())
            .Sum(entity => entity.CountedCashAmount ?? 0m));
        var expectedCashTotal = RoundMoney(cashShiftSummaries.Sum(entity => entity.ExpectedCashAmount));
        var cashVarianceTotal = RoundMoney(cashShiftSummaries
            .Where(entity => entity.CashVarianceAmount.HasValue)
            .Sum(entity => entity.CashVarianceAmount ?? 0m));

        return new DailyCashSalesReportResponse(
            restaurant.RestaurantId,
            restaurant.RestaurantCode,
            restaurant.Name,
            branchScope?.BranchId,
            branchScope?.Name,
            reportDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            branchScope?.CurrencyCode ?? restaurant.CurrencyCode,
            DateTimeOffset.UtcNow,
            new DailyCashSalesReportSummary(
                billSummary.TotalBills,
                billSummary.PaidBills,
                billSummary.PartiallyPaidBills,
                billSummary.UnpaidBills,
                billSummary.CancelledBills,
                grossSales,
                grossBillTotal,
                cancelledBillAmount,
                netSales,
                totalAmountPaid,
                totalBalanceDue,
                paymentTotals.CashTotal,
                paymentTotals.UpiTotal,
                paymentTotals.CardTotal,
                paymentTotals.OtherTotal,
                paymentTotals.NonCashTotal,
                openShiftCount,
                closedShiftCount,
                openingCashTotal,
                declaredClosingCashTotal,
                expectedCashTotal,
                receiptPrints,
                receiptReprints,
                cashVarianceTotal),
            paymentBreakdown,
            cashShiftSummaries,
            exceptions);
    }

    private static DailyCashSalesReportSummaryCounts BuildBillSummary(IReadOnlyCollection<BillSnapshot> bills)
    {
        return new DailyCashSalesReportSummaryCounts(
            TotalBills: bills.Count,
            PaidBills: bills.Count(entity => entity.Status == BillStatus.Paid),
            PartiallyPaidBills: bills.Count(entity => entity.Status == BillStatus.PartiallyPaid),
            UnpaidBills: bills.Count(entity => entity.Status == BillStatus.Unpaid),
            CancelledBills: bills.Count(entity => entity.Status == BillStatus.Cancelled));
    }

    private static IReadOnlyCollection<DailyCashSalesPaymentBreakdown> BuildPaymentBreakdown(IReadOnlyCollection<PaymentSnapshot> payments)
    {
        var paymentModeOrder = new[]
        {
            PaymentMode.Cash,
            PaymentMode.Upi,
            PaymentMode.Card,
            PaymentMode.Other
        };

        return paymentModeOrder
            .Select(mode =>
            {
                var items = payments.Where(entity => entity.PaymentMode == mode).ToArray();
                var recordedItems = items.Where(entity => entity.Status == PaymentStatus.Recorded).ToArray();
                var cancelledItems = items.Where(entity => entity.Status == PaymentStatus.Cancelled).ToArray();

                return new DailyCashSalesPaymentBreakdown(
                    mode.ToString(),
                    RoundMoney(recordedItems.Sum(entity => entity.Amount)),
                    RoundMoney(cancelledItems.Sum(entity => entity.Amount)),
                    RoundMoney(recordedItems.Sum(entity => entity.Amount) - cancelledItems.Sum(entity => entity.Amount)),
                    recordedItems.Length + cancelledItems.Length,
                    cancelledItems.Length);
            })
            .ToArray();
    }

    private static PaymentTotals BuildPaymentTotals(IReadOnlyCollection<PaymentSnapshot> payments)
    {
        decimal cash = 0m;
        decimal upi = 0m;
        decimal card = 0m;
        decimal other = 0m;

        foreach (var payment in payments.Where(entity => entity.Status == PaymentStatus.Recorded))
        {
            switch (payment.PaymentMode)
            {
                case PaymentMode.Cash:
                    cash += payment.Amount;
                    break;
                case PaymentMode.Upi:
                    upi += payment.Amount;
                    break;
                case PaymentMode.Card:
                    card += payment.Amount;
                    break;
                case PaymentMode.Other:
                    other += payment.Amount;
                    break;
            }
        }

        cash = RoundMoney(cash);
        upi = RoundMoney(upi);
        card = RoundMoney(card);
        other = RoundMoney(other);

        return new PaymentTotals(
            cash,
            upi,
            card,
            other,
            RoundMoney(upi + card + other),
            RoundMoney(cash + upi + card + other));
    }

    private static IReadOnlyCollection<DailyCashSalesCashShiftSummary> BuildCashShiftSummaries(
        IReadOnlyCollection<CashierShift> shifts,
        IReadOnlyDictionary<Guid, BranchSnapshot> branchLookup)
    {
        return shifts
            .OrderByDescending(entity => entity.OpenedAt)
            .ThenByDescending(entity => entity.CashierShiftId)
            .Select(entity =>
            {
                var branch = branchLookup[entity.BranchId];
                var cashMovementTotal = RoundMoney(entity.CashDrawerMovements.Sum(movement => ResolveCashEffect(movement.MovementType, movement.Amount)));
                var cashPaymentTotal = RoundMoney(entity.Payments
                    .Where(payment => payment.PaymentMode == PaymentMode.Cash && payment.Status == PaymentStatus.Recorded)
                    .Sum(payment => payment.Amount));
                var expectedCashAmount = RoundMoney(entity.OpeningCashAmount + cashPaymentTotal);
                decimal? cashVarianceAmount = entity.Status == CashierShiftStatus.Closed && entity.CountedCashAmount.HasValue
                    ? RoundMoney(entity.CountedCashAmount.Value - expectedCashAmount)
                    : null;

                return new DailyCashSalesCashShiftSummary(
                    entity.CashierShiftId,
                    entity.BranchId,
                    branch.Name,
                    entity.Status.ToString(),
                    entity.OpenedAt,
                    entity.ClosedAt,
                    entity.OpeningCashAmount,
                    expectedCashAmount,
                    entity.CountedCashAmount,
                    cashVarianceAmount,
                    cashMovementTotal,
                    cashPaymentTotal);
            })
            .ToArray();
    }

    private static DailyCashSalesReportExceptions BuildExceptions(
        IReadOnlyCollection<BillSnapshot> bills,
        IReadOnlyDictionary<Guid, BillSnapshot> billLookup,
        IReadOnlyCollection<PaymentSnapshot> payments,
        IReadOnlyCollection<PrintSnapshot> printSnapshots,
        IReadOnlyCollection<DailyCashSalesCashShiftSummary> cashShiftSummaries,
        IReadOnlyDictionary<Guid, BranchSnapshot> branchLookup)
    {
        var unpaidBills = bills
            .Where(entity => entity.Status != BillStatus.Cancelled && entity.BalanceDue > 0m)
            .OrderByDescending(entity => entity.CreatedAt)
            .ThenByDescending(entity => entity.BillNumber)
            .Select(entity =>
            {
                var branch = branchLookup[entity.BranchId];
                return new DailyCashSalesExceptionItem(
                    entity.BillId.ToString(),
                    entity.BillNumber,
                    entity.BranchId,
                    branch.Name,
                    entity.BalanceDue,
                    entity.Status.ToString(),
                    entity.CreatedAt,
                    $"Balance due {FormatMoney(entity.BalanceDue)}",
                    "Medium",
                    BalanceDue: entity.BalanceDue);
            })
            .ToArray();

        var cancelledBills = bills
            .Where(entity => entity.Status == BillStatus.Cancelled)
            .OrderByDescending(entity => entity.CreatedAt)
            .ThenByDescending(entity => entity.BillNumber)
            .Select(entity =>
            {
                var branch = branchLookup[entity.BranchId];
                return new DailyCashSalesExceptionItem(
                    entity.BillId.ToString(),
                    entity.BillNumber,
                    entity.BranchId,
                    branch.Name,
                    entity.GrandTotal,
                    entity.Status.ToString(),
                    entity.CreatedAt,
                    entity.CancelReason ?? "Cancelled bill",
                    "Medium");
            })
            .ToArray();

        var cancelledPayments = payments
            .Where(entity => entity.Status == PaymentStatus.Cancelled)
            .OrderByDescending(entity => entity.CreatedAt)
            .ThenByDescending(entity => entity.PaymentNumber)
            .Select(entity =>
            {
                var branch = branchLookup[entity.BranchId];
                return new DailyCashSalesExceptionItem(
                    entity.PaymentId.ToString(),
                    entity.PaymentNumber,
                    entity.BranchId,
                    branch.Name,
                    entity.Amount,
                    entity.Status.ToString(),
                    entity.CreatedAt,
                    entity.CancelReason ?? "Cancelled payment",
                    "Medium");
            })
            .ToArray();

        var receiptReprints = printSnapshots
            .GroupBy(entity => entity.BillId)
            .Where(group => group.Count() > 1)
            .Select(group =>
            {
                var printCount = group.Count();
                var reprintCount = Math.Max(0, printCount - 1);
                var latest = group.OrderByDescending(entity => entity.CreatedAt).First();
                var branch = branchLookup[latest.BranchId];
                var severity = reprintCount > 1 ? "Medium" : "Low";
                var bill = billLookup[latest.BillId];
                var reprintLabel = reprintCount == 1 ? "reprint" : "reprints";

                return new DailyCashSalesExceptionItem(
                    latest.BillId.ToString(),
                    bill.BillNumber,
                    latest.BranchId,
                    branch.Name,
                    null,
                    "Reprinted",
                    latest.CreatedAt,
                    $"Printed {printCount} times ({reprintCount} {reprintLabel})",
                    severity,
                    PrintCount: printCount,
                    ReprintCount: reprintCount);
            })
            .OrderByDescending(entity => entity.OccurredAt)
            .ThenByDescending(entity => entity.ReferenceNumber)
            .ToArray();

        var cashVariances = cashShiftSummaries
            .Where(entity => entity.Status == CashierShiftStatus.Closed.ToString() && entity.CashVarianceAmount.HasValue && entity.CashVarianceAmount.Value != 0m)
            .OrderByDescending(entity => entity.ClosedAt)
            .ThenByDescending(entity => entity.CashierShiftId)
            .Select(entity =>
            {
                var branch = branchLookup[entity.BranchId];
                var variance = entity.CashVarianceAmount!.Value;
                return new DailyCashSalesExceptionItem(
                    entity.CashierShiftId.ToString(),
                    entity.CashierShiftId.ToString(),
                    entity.BranchId,
                    branch.Name,
                    variance,
                    entity.Status,
                    entity.ClosedAt ?? entity.OpenedAt,
                    CashVarianceExplanation,
                    "High",
                    ExpectedCashAmount: entity.ExpectedCashAmount,
                    CountedCashAmount: entity.CountedCashAmount,
                    VarianceAmount: variance);
            })
            .ToArray();

        var openShifts = cashShiftSummaries
            .Where(entity => entity.Status == CashierShiftStatus.Open.ToString())
            .OrderByDescending(entity => entity.OpenedAt)
            .ThenByDescending(entity => entity.CashierShiftId)
            .Select(entity =>
            {
                var branch = branchLookup[entity.BranchId];
                return new DailyCashSalesExceptionItem(
                    entity.CashierShiftId.ToString(),
                    entity.CashierShiftId.ToString(),
                    entity.BranchId,
                    branch.Name,
                    null,
                    entity.Status,
                    entity.OpenedAt,
                    "Shift is still open after the selected business date.",
                    "Medium");
            })
            .ToArray();

        return new DailyCashSalesReportExceptions(
            unpaidBills,
            cancelledBills,
            cancelledPayments,
            receiptReprints,
            cashVariances,
            openShifts);
    }

    private static int CountReprints(IReadOnlyCollection<PrintSnapshot> printSnapshots)
    {
        var receiptReprints = 0;
        var printGroups = new Dictionary<Guid, int>();

        foreach (var printSnapshot in printSnapshots)
        {
            if (printGroups.TryGetValue(printSnapshot.BillId, out var printCount))
            {
                printGroups[printSnapshot.BillId] = printCount + 1;
            }
            else
            {
                printGroups[printSnapshot.BillId] = 1;
            }
        }

        foreach (var printCount in printGroups.Values)
        {
            receiptReprints += Math.Max(0, printCount - 1);
        }

        return receiptReprints;
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

        return new RestaurantSnapshot(
            restaurant.RestaurantId,
            restaurant.Name,
            restaurant.RestaurantCode,
            restaurant.CurrencyCode,
            restaurant.TimeZoneId);
    }

    private async Task<BranchSnapshot> LoadBranchAsync(Guid restaurantId, Guid branchId, CancellationToken cancellationToken)
    {
        var branch = await _context.Branches
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.BranchId == branchId && entity.RestaurantId == restaurantId, cancellationToken);

        if (branch is null)
        {
            throw new KeyNotFoundException("Branch not found.");
        }

        return new BranchSnapshot(branch.BranchId, branch.Name, branch.CurrencyCode, branch.TimeZoneId);
    }

    private static Guid RequireRestaurantScope(AuthUserContext currentUser)
    {
        if (currentUser.RestaurantId == Guid.Empty)
        {
            throw new InvalidOperationException("Authentication context is missing the restaurant scope.");
        }

        return currentUser.RestaurantId;
    }

    private static DateTime ResolveBusinessDate(DateTime? requestedDate, TimeZoneInfo timeZone, DateTimeOffset nowUtc)
    {
        if (requestedDate.HasValue)
        {
            return DateTime.SpecifyKind(requestedDate.Value.Date, DateTimeKind.Unspecified);
        }

        return TimeZoneInfo.ConvertTime(nowUtc.UtcDateTime, timeZone).Date;
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return ResolveFallbackTimeZone(timeZoneId);
        }
        catch (InvalidTimeZoneException)
        {
            return ResolveFallbackTimeZone(timeZoneId);
        }
    }

    private static TimeZoneInfo ResolveFallbackTimeZone(string timeZoneId)
    {
        var mappedId = timeZoneId.Trim().ToUpperInvariant() switch
        {
            "ASIA/SINGAPORE" => "Singapore Standard Time",
            "ASIA/KOLKATA" => "India Standard Time",
            _ => "UTC"
        };

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(mappedId);
        }
        catch
        {
            return TimeZoneInfo.Utc;
        }
    }

    private static decimal ResolveCashEffect(CashDrawerMovementType movementType, decimal amount)
    {
        return movementType switch
        {
            CashDrawerMovementType.CashIn => amount,
            CashDrawerMovementType.CashOut => -amount,
            CashDrawerMovementType.SafeDrop => -amount,
            CashDrawerMovementType.Adjustment => amount,
            _ => throw new InvalidOperationException("Unsupported movement type.")
        };
    }

    private static decimal RoundMoney(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static string FormatMoney(decimal value)
    {
        return value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private sealed record RestaurantSnapshot(
        Guid RestaurantId,
        string Name,
        string RestaurantCode,
        string CurrencyCode,
        string TimeZoneId);

    private sealed record BranchSnapshot(
        Guid BranchId,
        string Name,
        string CurrencyCode,
        string TimeZoneId);

    private sealed record BillSnapshot(
        Guid BillId,
        Guid BranchId,
        string BillNumber,
        BillStatus Status,
        decimal Subtotal,
        decimal TaxTotal,
        decimal GrandTotal,
        decimal AmountPaid,
        decimal BalanceDue,
        string? CancelReason,
        DateTimeOffset CreatedAt);

    private sealed record PaymentSnapshot(
        Guid PaymentId,
        Guid BranchId,
        string PaymentNumber,
        PaymentMode PaymentMode,
        PaymentStatus Status,
        decimal Amount,
        string? CancelReason,
        DateTimeOffset CreatedAt);

    private sealed record PrintSnapshot(
        Guid BillPrintEventId,
        Guid BranchId,
        Guid BillId,
        string BillNumber,
        int PrintSequence,
        DateTimeOffset CreatedAt);

    private sealed record PaymentTotals(
        decimal CashTotal,
        decimal UpiTotal,
        decimal CardTotal,
        decimal OtherTotal,
        decimal NonCashTotal,
        decimal RecordedTotal);

    private sealed record DailyCashSalesReportSummaryCounts(
        int TotalBills,
        int PaidBills,
        int PartiallyPaidBills,
        int UnpaidBills,
        int CancelledBills);
}
