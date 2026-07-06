using System.Globalization;
using BillSoft.Application.Auth;
using BillSoft.Application.Reports;
using BillSoft.Domain.Inventory;
using BillSoft.Domain.Restaurants;
using BillSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillSoft.Infrastructure.Reports;

public sealed class ExpiryStockReportService : IExpiryStockReportService
{
    private const double PreparedStockNearExpiryHours = 6.0;
    private const double PurchasedStockNearExpiryDays = 3.0;

    private readonly BillSoftDbContext _context;

    public ExpiryStockReportService(BillSoftDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<ExpiryStockReportResponse> GetExpiryStockReportAsync(
        AuthUserContext currentUser,
        DateTime? asOfDate,
        Guid? branchId,
        CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var branchScope = ResolveBranchScope(currentUser, branchId);

        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var branch = await LoadBranchAsync(restaurantId, branchScope, cancellationToken);
        var timeZone = ResolveTimeZone(branch.TimeZoneId ?? restaurant.TimeZoneId);
        var reportDate = ResolveAsOfDate(asOfDate, timeZone, DateTimeOffset.UtcNow);
        var asOfUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(reportDate.Date.AddDays(1), DateTimeKind.Unspecified), timeZone));

        var lots = await _context.InventoryLots
            .AsNoTracking()
            .Where(entity =>
                entity.RestaurantId == restaurantId &&
                entity.BranchId == branchScope &&
                entity.RemainingQuantity > 0m)
            .Select(entity => new InventoryLotSnapshot(
                entity.InventoryLotId,
                entity.InventoryItemId,
                entity.SourceMovementId,
                entity.SourceBatchProductionId,
                entity.BatchReference,
                entity.ReceivedAtUtc,
                entity.ExpiresAtUtc,
                entity.RemainingQuantity,
                entity.UnitOfMeasure))
            .ToListAsync(cancellationToken);

        var itemIds = lots.Select(lot => lot.InventoryItemId).Distinct().ToArray();
        var inventoryItems = itemIds.Length == 0
            ? new Dictionary<Guid, InventoryItemSnapshot>()
            : await _context.InventoryItems
                .AsNoTracking()
                .Where(entity =>
                    entity.RestaurantId == restaurantId &&
                    entity.BranchId == branchScope &&
                    itemIds.Contains(entity.InventoryItemId))
                .Select(entity => new InventoryItemSnapshot(entity.InventoryItemId, entity.Name, entity.UnitOfMeasure))
                .ToDictionaryAsync(entity => entity.InventoryItemId, cancellationToken);

        var movementIds = lots
            .Where(entity => entity.SourceMovementId.HasValue)
            .Select(entity => entity.SourceMovementId!.Value)
            .Distinct()
            .ToArray();

        var movementTypes = movementIds.Length == 0
            ? new Dictionary<Guid, InventoryMovementType>()
            : await _context.InventoryMovements
            .AsNoTracking()
            .Where(entity =>
                entity.RestaurantId == restaurantId &&
                entity.BranchId == branchScope &&
                movementIds.Contains(entity.InventoryMovementId))
            .Select(entity => new
            {
                entity.InventoryMovementId,
                entity.MovementType,
            })
            .ToDictionaryAsync(entity => entity.InventoryMovementId, entity => entity.MovementType, cancellationToken);

        var rows = lots
            .Select(lot =>
            {
                inventoryItems.TryGetValue(lot.InventoryItemId, out var inventoryItem);
                var sourceType = ResolveSourceType(lot, movementTypes);
                var status = ResolveExpiryStatus(
                    lot.ExpiresAtUtc,
                    asOfUtc,
                    ResolveNearExpiryWindowHours(sourceType));

                return new ExpiryStockReportRowProjection(
                    lot.InventoryLotId,
                    lot.InventoryItemId,
                    inventoryItem?.Name ?? lot.InventoryItemId.ToString(),
                    !string.IsNullOrWhiteSpace(lot.UnitOfMeasure) ? lot.UnitOfMeasure : inventoryItem?.UnitOfMeasure ?? string.Empty,
                    sourceType,
                    lot.BatchReference,
                    lot.RemainingQuantity,
                    lot.ReceivedAtUtc,
                    lot.ExpiresAtUtc,
                    status,
                    ResolveWarningReason(status),
                    ResolveSourceReference(lot, sourceType));
            })
            .OrderBy(row => ResolveExpiryStatusSortOrder(row.ExpiryStatus))
            .ThenBy(row => row.ExpiresAtUtc.HasValue ? 0 : 1)
            .ThenBy(row => row.ExpiresAtUtc)
            .ThenBy(row => row.InventoryItemName, StringComparer.Ordinal)
            .ThenBy(row => row.ProducedOrReceivedAt)
            .ThenBy(row => row.InventoryLotId)
            .Select(row => new ExpiryStockReportRow(
                row.InventoryItemId,
                row.InventoryItemName,
                row.UnitOfMeasure,
                row.SourceType,
                row.BatchReference,
                row.Quantity,
                row.ProducedOrReceivedAt,
                row.ExpiresAtUtc,
                row.ExpiryStatus,
                row.WarningReason,
                row.SourceReference))
            .ToArray();

        var totals = new ExpiryStockReportTotals(
            FreshCount: rows.Count(r => r.ExpiryStatus == "Fresh"),
            NearExpiryCount: rows.Count(r => r.ExpiryStatus == "NearExpiry"),
            ExpiredCount: rows.Count(r => r.ExpiryStatus == "Expired"),
            NoExpiryCount: rows.Count(r => r.ExpiryStatus == "NoExpiry"),
            TotalTrackedItems: rows.Length);

        return new ExpiryStockReportResponse(
            branch.BranchId,
            branch.Name,
            reportDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            totals,
            rows);
    }

    private static string ResolveExpiryStatus(DateTimeOffset? expiresAtUtc, DateTimeOffset asOfUtc, double nearExpiryWindowHours)
    {
        if (!expiresAtUtc.HasValue)
        {
            return "NoExpiry";
        }

        if (expiresAtUtc.Value <= asOfUtc)
        {
            return "Expired";
        }

        if (expiresAtUtc.Value <= asOfUtc.AddHours(nearExpiryWindowHours))
        {
            return "NearExpiry";
        }

        return "Fresh";
    }

    private static string? ResolveWarningReason(string status)
    {
        return status switch
        {
            "Expired" => "Expired stock should be reviewed.",
            "NearExpiry" => "Stock is near expiry.",
            _ => null
        };
    }

    private static DateTime ResolveAsOfDate(DateTime? requestedDate, TimeZoneInfo timeZone, DateTimeOffset nowUtc)
    {
        if (requestedDate.HasValue)
        {
            return DateTime.SpecifyKind(requestedDate.Value.Date, DateTimeKind.Unspecified);
        }

        return TimeZoneInfo.ConvertTime(nowUtc.UtcDateTime, timeZone).Date;
    }

    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch
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
    }

    private static int ResolveExpiryStatusSortOrder(string expiryStatus)
    {
        return expiryStatus switch
        {
            "Expired" => 0,
            "NearExpiry" => 1,
            "Fresh" => 2,
            _ => 3
        };
    }

    private static double ResolveNearExpiryWindowHours(string sourceType)
    {
        return sourceType == "BatchProduction"
            ? PreparedStockNearExpiryHours
            : PurchasedStockNearExpiryDays * 24.0;
    }

    private static string ResolveSourceType(
        InventoryLotSnapshot lot,
        IReadOnlyDictionary<Guid, InventoryMovementType> movementTypes)
    {
        if (lot.SourceBatchProductionId is not null)
        {
            return "BatchProduction";
        }

        if (lot.SourceMovementId is Guid movementId &&
            movementTypes.TryGetValue(movementId, out var movementType))
        {
            return movementType switch
            {
                InventoryMovementType.StockIn => "StockIn",
                InventoryMovementType.AdjustmentIncrease => "Adjustment",
                _ => "Unknown"
            };
        }

        if (lot.SourceMovementId is null &&
            lot.SourceBatchProductionId is null &&
            string.Equals(lot.BatchReference, "Opening lot", StringComparison.OrdinalIgnoreCase))
        {
            return "OpeningLot";
        }

        return "Unknown";
    }

    private static string ResolveSourceReference(InventoryLotSnapshot lot, string sourceType)
    {
        return sourceType switch
        {
            "BatchProduction" when lot.SourceBatchProductionId is Guid sourceBatchProductionId => $"BATCH-{sourceBatchProductionId:N}",
            "StockIn" when lot.SourceMovementId is Guid sourceMovementId => $"MOV-{sourceMovementId:N}",
            "Adjustment" when lot.SourceMovementId is Guid sourceMovementId => $"MOV-{sourceMovementId:N}",
            _ => $"LOT-{lot.InventoryLotId:N}"
        };
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

        return new RestaurantSnapshot(restaurant.RestaurantId, restaurant.Name, restaurant.TimeZoneId);
    }

    private async Task<Branch> LoadBranchAsync(Guid restaurantId, Guid branchId, CancellationToken cancellationToken)
    {
        var branch = await _context.Branches
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.BranchId == branchId && entity.RestaurantId == restaurantId, cancellationToken);

        if (branch is null)
        {
            throw new KeyNotFoundException("Branch not found.");
        }

        return branch;
    }

    private static Guid RequireRestaurantScope(AuthUserContext currentUser)
    {
        if (currentUser.RestaurantId == Guid.Empty)
        {
            throw new InvalidOperationException("Authentication context is missing the restaurant scope.");
        }

        return currentUser.RestaurantId;
    }

    private static Guid ResolveBranchScope(AuthUserContext currentUser, Guid? requestedBranchId)
    {
        if (currentUser.BranchId.HasValue)
        {
            if (requestedBranchId.HasValue && requestedBranchId.Value != currentUser.BranchId.Value)
            {
                throw new InvalidOperationException("Branch access is restricted to the current branch.");
            }

            return currentUser.BranchId.Value;
        }

        if (requestedBranchId.HasValue)
        {
            return requestedBranchId.Value;
        }

        throw new InvalidOperationException("Branch is required.");
    }

    private sealed record RestaurantSnapshot(Guid RestaurantId, string Name, string TimeZoneId);

    private sealed record InventoryItemSnapshot(Guid InventoryItemId, string Name, string UnitOfMeasure);

    private sealed record InventoryLotSnapshot(
        Guid InventoryLotId,
        Guid InventoryItemId,
        Guid? SourceMovementId,
        Guid? SourceBatchProductionId,
        string? BatchReference,
        DateTimeOffset ReceivedAtUtc,
        DateTimeOffset? ExpiresAtUtc,
        decimal RemainingQuantity,
        string UnitOfMeasure);

    private sealed record ExpiryStockReportRowProjection(
        Guid InventoryLotId,
        Guid InventoryItemId,
        string InventoryItemName,
        string UnitOfMeasure,
        string SourceType,
        string? BatchReference,
        decimal Quantity,
        DateTimeOffset ProducedOrReceivedAt,
        DateTimeOffset? ExpiresAtUtc,
        string ExpiryStatus,
        string? WarningReason,
        string SourceReference);
}
