using System.Globalization;
using BillSoft.Application.Auth;
using BillSoft.Application.Reports;
using BillSoft.Domain.Billing;
using BillSoft.Domain.Cashiering;
using BillSoft.Domain.Restaurants;
using BillSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillSoft.Infrastructure.Reports;

public sealed class CashReconciliationReportService : ICashReconciliationReportService
{
    private const decimal MinorVarianceThreshold = 100m;

    private readonly BillSoftDbContext _context;

    public CashReconciliationReportService(BillSoftDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<CashReconciliationReportResponse> GetCashReconciliationReportAsync(
        AuthUserContext currentUser,
        DateTime? businessDate,
        Guid? branchId,
        CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var branchScope = await ResolveBranchScopeAsync(currentUser, restaurantId, branchId, cancellationToken);
        var timeZone = ResolveTimeZone(branchScope?.TimeZoneId ?? restaurant.TimeZoneId);
        var reportDate = ResolveBusinessDate(businessDate, timeZone, DateTimeOffset.UtcNow);
        var reportBusinessDate = DateTime.SpecifyKind(reportDate.Date, DateTimeKind.Utc);

        var shiftsQuery = _context.CashierShifts
            .AsNoTracking()
            .Where(entity =>
                entity.RestaurantId == restaurantId &&
                entity.BusinessDate == reportBusinessDate);

        if (branchScope is not null)
        {
            shiftsQuery = shiftsQuery.Where(entity => entity.BranchId == branchScope.BranchId);
        }

        var shifts = (await shiftsQuery.ToArrayAsync(cancellationToken))
            .OrderByDescending(entity => entity.OpenedAt)
            .ThenByDescending(entity => entity.CashierShiftId)
            .ToArray();

        var shiftIds = shifts.Select(entity => entity.CashierShiftId).ToArray();
        var branchIds = shifts.Select(entity => entity.BranchId).Distinct().ToArray();
        var cashierUserIds = shifts.Select(entity => entity.OpenedByUserId).Distinct().ToArray();

        var branchLookup = branchIds.Length == 0
            ? new Dictionary<Guid, BranchSnapshot>()
            : await _context.Branches
                .AsNoTracking()
                .Where(entity => entity.RestaurantId == restaurantId && branchIds.Contains(entity.BranchId))
                .Select(entity => new BranchSnapshot(entity.BranchId, entity.Name, entity.CurrencyCode, entity.TimeZoneId))
                .ToDictionaryAsync(entity => entity.BranchId, cancellationToken);

        var cashierLookup = cashierUserIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await _context.Users
                .AsNoTracking()
                .Where(entity => entity.RestaurantId == restaurantId && cashierUserIds.Contains(entity.UserId))
                .ToDictionaryAsync(entity => entity.UserId, entity => entity.FullName, cancellationToken);

        var cashPayments = shiftIds.Length == 0
            ? []
            : await (
                from payment in _context.Payments.AsNoTracking()
                join bill in _context.Bills.AsNoTracking() on payment.BillId equals bill.BillId
                join shift in _context.CashierShifts.AsNoTracking() on payment.CashierShiftId equals shift.CashierShiftId
                where payment.RestaurantId == restaurantId &&
                      bill.RestaurantId == restaurantId &&
                      shift.RestaurantId == restaurantId &&
                      shiftIds.Contains(shift.CashierShiftId) &&
                      payment.CashierShiftId.HasValue &&
                      payment.PaymentMode == PaymentMode.Cash &&
                      payment.Status == PaymentStatus.Recorded &&
                      payment.BranchId == shift.BranchId &&
                      payment.BranchId == bill.BranchId
                select new CashPaymentSnapshot(payment.CashierShiftId!.Value, payment.Amount))
                .ToArrayAsync(cancellationToken);

        var cashMovements = shiftIds.Length == 0
            ? []
            : await _context.CashDrawerMovements
                .AsNoTracking()
                .Where(entity =>
                    entity.RestaurantId == restaurantId &&
                    shiftIds.Contains(entity.CashierShiftId))
                .Select(entity => new CashMovementSnapshot(
                    entity.CashierShiftId,
                    entity.MovementType,
                    entity.Amount))
                .ToArrayAsync(cancellationToken);

        var paymentTotals = cashPayments
            .GroupBy(entity => entity.CashierShiftId)
            .ToDictionary(
                group => group.Key,
                group => new ShiftPaymentTotals(
                    RoundMoney(group.Sum(entity => entity.Amount)),
                    group.Count()));

        var movementTotals = cashMovements
            .GroupBy(entity => entity.CashierShiftId)
            .ToDictionary(group => group.Key, group => BuildMovementTotals(group));

        var rows = shifts
            .Select(entity =>
            {
                branchLookup.TryGetValue(entity.BranchId, out var branch);
                cashierLookup.TryGetValue(entity.OpenedByUserId, out var cashierName);

                var shiftPayments = paymentTotals.TryGetValue(entity.CashierShiftId, out var paymentTotalsForShift)
                    ? paymentTotalsForShift
                    : ShiftPaymentTotals.Empty;
                var shiftMovements = movementTotals.TryGetValue(entity.CashierShiftId, out var movementTotalsForShift)
                    ? movementTotalsForShift
                    : ShiftMovementTotals.Empty;

                var movementEffect = RoundMoney(shiftMovements.CashInTotal - shiftMovements.CashOutTotal + shiftMovements.AdjustmentTotal);
                var expectedCashAmount = RoundMoney(entity.OpeningCashAmount + shiftPayments.TotalAmount + movementEffect);

                decimal? declaredCashAmount = entity.Status == CashierShiftStatus.Closed
                    ? entity.CountedCashAmount
                    : null;

                decimal? varianceAmount = entity.Status == CashierShiftStatus.Closed && declaredCashAmount.HasValue
                    ? RoundMoney(declaredCashAmount.Value - expectedCashAmount)
                    : null;

                var varianceStatus = ResolveVarianceStatus(entity.Status, varianceAmount);
                var resolvedCashierName = string.IsNullOrWhiteSpace(cashierName) ? entity.OpenedByUserId.ToString() : cashierName;
                var resolvedBranchName = branch?.Name ?? entity.BranchId.ToString();

                return new CashReconciliationShiftRow(
                    entity.CashierShiftId,
                    entity.BranchId,
                    resolvedBranchName,
                    entity.OpenedByUserId,
                    resolvedCashierName,
                    entity.Status.ToString(),
                    entity.OpenedAt,
                    entity.ClosedAt,
                    entity.OpeningCashAmount,
                    shiftPayments.TotalAmount,
                    shiftMovements.CashInTotal,
                    shiftMovements.CashOutTotal,
                    shiftMovements.AdjustmentTotal,
                    expectedCashAmount,
                    declaredCashAmount,
                    varianceAmount,
                    varianceStatus,
                    shiftPayments.Count,
                    shiftMovements.Count,
                    entity.ClosingNote);
            })
            .ToArray();

        var totals = new CashReconciliationReportTotals(
            ShiftCount: rows.Length,
            OpenShiftCount: rows.Count(row => string.Equals(row.Status, CashierShiftStatus.Open.ToString(), StringComparison.Ordinal)),
            ClosedShiftCount: rows.Count(row => string.Equals(row.Status, CashierShiftStatus.Closed.ToString(), StringComparison.Ordinal)),
            OpeningCashTotal: RoundMoney(rows.Sum(row => row.OpeningCashAmount)),
            CashPaymentTotal: RoundMoney(rows.Sum(row => row.CashPaymentTotal)),
            CashInTotal: RoundMoney(rows.Sum(row => row.CashInTotal)),
            CashOutTotal: RoundMoney(rows.Sum(row => row.CashOutTotal)),
            AdjustmentTotal: RoundMoney(rows.Sum(row => row.AdjustmentTotal)),
            ExpectedCashTotal: RoundMoney(rows.Sum(row => row.ExpectedCashAmount)),
            DeclaredCashTotal: RoundMoney(rows.Where(row => row.DeclaredClosingCashAmount.HasValue).Sum(row => row.DeclaredClosingCashAmount ?? 0m)),
            VarianceTotal: RoundMoney(rows.Where(row => row.VarianceAmount.HasValue).Sum(row => row.VarianceAmount ?? 0m)),
            MajorVarianceCount: rows.Count(row => string.Equals(row.VarianceStatus, "MajorVariance", StringComparison.Ordinal)),
            MinorVarianceCount: rows.Count(row => string.Equals(row.VarianceStatus, "MinorVariance", StringComparison.Ordinal)),
            BalancedShiftCount: rows.Count(row => string.Equals(row.VarianceStatus, "Balanced", StringComparison.Ordinal)));

        return new CashReconciliationReportResponse(
            restaurant.RestaurantId,
            restaurant.Name,
            branchScope?.BranchId,
            branchScope?.Name,
            reportDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTimeOffset.UtcNow,
            branchScope?.CurrencyCode ?? restaurant.CurrencyCode,
            totals,
            rows);
    }

    private static ShiftMovementTotals BuildMovementTotals(IEnumerable<CashMovementSnapshot> movements)
    {
        decimal cashIn = 0m;
        decimal cashOut = 0m;
        decimal adjustment = 0m;
        var count = 0;

        foreach (var movement in movements)
        {
            count++;
            switch (movement.MovementType)
            {
                case CashDrawerMovementType.CashIn:
                    cashIn += movement.Amount;
                    break;
                case CashDrawerMovementType.CashOut:
                case CashDrawerMovementType.SafeDrop:
                    cashOut += movement.Amount;
                    break;
                case CashDrawerMovementType.Adjustment:
                    adjustment += movement.Amount;
                    break;
                default:
                    throw new InvalidOperationException("Unsupported movement type.");
            }
        }

        return new ShiftMovementTotals(
            RoundMoney(cashIn),
            RoundMoney(cashOut),
            RoundMoney(adjustment),
            count);
    }

    private static string ResolveVarianceStatus(CashierShiftStatus status, decimal? varianceAmount)
    {
        if (status == CashierShiftStatus.Open)
        {
            return "OpenShift";
        }

        if (!varianceAmount.HasValue)
        {
            return "MajorVariance";
        }

        if (varianceAmount.Value == 0m)
        {
            return "Balanced";
        }

        return Math.Abs(varianceAmount.Value) <= MinorVarianceThreshold
            ? "MinorVariance"
            : "MajorVariance";
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
            restaurant.CurrencyCode,
            restaurant.TimeZoneId);
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

    private static decimal RoundMoney(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private sealed record RestaurantSnapshot(
        Guid RestaurantId,
        string Name,
        string CurrencyCode,
        string TimeZoneId);

    private sealed record BranchSnapshot(
        Guid BranchId,
        string Name,
        string CurrencyCode,
        string TimeZoneId);

    private sealed record CashPaymentSnapshot(Guid CashierShiftId, decimal Amount);

    private sealed record CashMovementSnapshot(Guid CashierShiftId, CashDrawerMovementType MovementType, decimal Amount);

    private sealed record ShiftPaymentTotals(decimal TotalAmount, int Count)
    {
        public static ShiftPaymentTotals Empty { get; } = new(0m, 0);
    }

    private sealed record ShiftMovementTotals(decimal CashInTotal, decimal CashOutTotal, decimal AdjustmentTotal, int Count)
    {
        public static ShiftMovementTotals Empty { get; } = new(0m, 0m, 0m, 0);
    }
}
