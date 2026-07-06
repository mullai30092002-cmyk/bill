using System.Globalization;
using BillSoft.Application.Auth;
using BillSoft.Application.Reports;
using BillSoft.Domain.Inventory;
using BillSoft.Domain.Kitchen;
using BillSoft.Domain.Menu;
using BillSoft.Domain.Restaurants;
using BillSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillSoft.Infrastructure.Reports;

public sealed class PreparedStockReportService : IPreparedStockReportService
{
    private readonly BillSoftDbContext _context;

    public PreparedStockReportService(BillSoftDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<PreparedStockReportResponse> GetPreparedStockReportAsync(
        AuthUserContext currentUser,
        DateTime? businessDate,
        Guid? branchId,
        CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var branchScope = ResolveBranchScope(currentUser, branchId);

        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var branch = await LoadBranchAsync(restaurantId, branchScope, cancellationToken);
        var timeZone = ResolveTimeZone(branch.TimeZoneId ?? restaurant.TimeZoneId);
        var reportDate = ResolveBusinessDate(businessDate, timeZone, DateTimeOffset.UtcNow);
        var reportBusinessDate = DateTime.SpecifyKind(reportDate.Date, DateTimeKind.Utc);
        var (dayStartUtc, dayEndUtc) = ResolveBusinessDateRange(reportDate, timeZone);

        var batchProductions = await _context.BatchProductions
            .AsNoTracking()
            .Where(entity =>
                entity.RestaurantId == restaurantId &&
                entity.BranchId == branchScope &&
                entity.BusinessDate == reportBusinessDate)
            .Select(entity => new PreparedStockBatchProductionSnapshot(
                entity.BatchProductionId,
                entity.MenuItemId,
                entity.PreparedInventoryItemId,
                entity.QuantityProduced,
                entity.BusinessDate,
                entity.ProducedAtUtc))
            .ToArrayAsync(cancellationToken);

        var rawMenuItemMappings = await _context.MenuItemStockItems
            .AsNoTracking()
            .Where(entity =>
                entity.RestaurantId == restaurantId &&
                entity.BranchId == branchScope)
            .Select(entity => new PreparedStockMappingSnapshot(
                entity.MenuItemId,
                entity.InventoryItemId))
            .ToArrayAsync(cancellationToken);

        var candidateMenuItemIds = batchProductions
            .Select(entity => entity.MenuItemId)
            .Concat(rawMenuItemMappings.Select(entity => entity.MenuItemId))
            .Distinct()
            .ToArray();

        var menuItems = candidateMenuItemIds.Length == 0
            ? new Dictionary<Guid, MenuItemSnapshot>()
            : await _context.MenuItems
                .AsNoTracking()
                .Where(entity => entity.RestaurantId == restaurantId && candidateMenuItemIds.Contains(entity.MenuItemId))
                .Select(entity => new MenuItemSnapshot(
                    entity.MenuItemId,
                    entity.Name,
                    entity.InventoryDeductionMode))
                .ToDictionaryAsync(entity => entity.MenuItemId, cancellationToken);

        var preparedMenuItemIds = rawMenuItemMappings
            .Where(mapping =>
                menuItems.TryGetValue(mapping.MenuItemId, out var menuItem) &&
                menuItem.InventoryDeductionMode == MenuItemInventoryDeductionMode.BatchPrepared)
            .Select(mapping => mapping.MenuItemId)
            .Distinct()
            .ToArray();

        var menuItemMappings = rawMenuItemMappings
            .Where(mapping => preparedMenuItemIds.Contains(mapping.MenuItemId))
            .ToArray();

        var candidatePreparedInventoryItemIds = batchProductions
            .Select(entity => entity.PreparedInventoryItemId)
            .Concat(menuItemMappings.Select(entity => entity.InventoryItemId))
            .Distinct()
            .ToArray();

        var inventoryItems = candidatePreparedInventoryItemIds.Length == 0
            ? new Dictionary<Guid, InventoryItemSnapshot>()
            : await _context.InventoryItems
                .AsNoTracking()
                .Where(entity =>
                    entity.RestaurantId == restaurantId &&
                    entity.BranchId == branchScope &&
                    candidatePreparedInventoryItemIds.Contains(entity.InventoryItemId))
                .Select(entity => new InventoryItemSnapshot(
                    entity.InventoryItemId,
                    entity.Name,
                    entity.UnitOfMeasure))
                .ToDictionaryAsync(entity => entity.InventoryItemId, cancellationToken);

        var servedTotals = candidatePreparedInventoryItemIds.Length == 0
            ? new Dictionary<Guid, decimal>()
            : await LoadServedTotalsAsync(
                restaurantId,
                branchScope,
                candidatePreparedInventoryItemIds,
                dayStartUtc,
                dayEndUtc,
                cancellationToken);

        var wastedTotals = candidatePreparedInventoryItemIds.Length == 0
            ? new Dictionary<Guid, decimal>()
            : (await _context.InventoryMovements
                .AsNoTracking()
                .Where(entity =>
                    entity.RestaurantId == restaurantId &&
                    entity.BranchId == branchScope &&
                    entity.MovementType == InventoryMovementType.Waste &&
                    candidatePreparedInventoryItemIds.Contains(entity.InventoryItemId))
                .Select(entity => new
                {
                    entity.InventoryItemId,
                    entity.Quantity,
                    entity.MovementDate
                })
                .ToListAsync(cancellationToken))
                .Where(entity => entity.MovementDate >= dayStartUtc && entity.MovementDate < dayEndUtc)
                .GroupBy(entity => entity.InventoryItemId)
                .ToDictionary(
                    grouping => grouping.Key,
                    grouping => RoundQuantity(grouping.Sum(entity => entity.Quantity)));

        var preparedMovementTypes = new[]
        {
            InventoryMovementType.StockIn,
            InventoryMovementType.Consumption,
            InventoryMovementType.AdjustmentIncrease,
            InventoryMovementType.AdjustmentDecrease,
            InventoryMovementType.Waste,
            InventoryMovementType.Correction
        };

        var preparedMovementTotals = candidatePreparedInventoryItemIds.Length == 0
            ? new Dictionary<Guid, decimal>()
            : (await _context.InventoryMovements
                .AsNoTracking()
                .Where(entity =>
                    entity.RestaurantId == restaurantId &&
                    entity.BranchId == branchScope &&
                    candidatePreparedInventoryItemIds.Contains(entity.InventoryItemId) &&
                    preparedMovementTypes.Contains(entity.MovementType))
                .Select(entity => new
                {
                    entity.InventoryItemId,
                    entity.Quantity,
                    entity.MovementType,
                    entity.MovementDate
                })
                .ToListAsync(cancellationToken))
                .Where(entity => entity.MovementDate >= dayStartUtc && entity.MovementDate < dayEndUtc)
                .GroupBy(entity => entity.InventoryItemId)
                .ToDictionary(
                    grouping => grouping.Key,
                    grouping => RoundQuantity(grouping.Sum(entity => GetMovementDelta(entity.MovementType, entity.Quantity))));

        var preparedItemToMenuItemId = new Dictionary<Guid, Guid>();
        var builders = new Dictionary<Guid, PreparedStockReportRowBuilder>();

        foreach (var batch in batchProductions)
        {
            var builder = GetOrCreateBuilder(builders, batch.MenuItemId);
            builder.RecordBatchProduction(batch.QuantityProduced);
            builder.SetPreparedItem(batch.PreparedInventoryItemId);

            if (menuItems.TryGetValue(batch.MenuItemId, out var menuItem))
            {
                builder.SetMenuItem(menuItem.Name);
                if (menuItem.InventoryDeductionMode != MenuItemInventoryDeductionMode.BatchPrepared)
                {
                    builder.AddWarning("Menu item is not configured for batch prepared deduction.");
                }
            }
            else
            {
                builder.AddWarning("Menu item could not be resolved.");
            }

            if (inventoryItems.TryGetValue(batch.PreparedInventoryItemId, out var inventoryItem))
            {
                builder.SetPreparedInventoryItem(inventoryItem.Name, inventoryItem.UnitOfMeasure);
            }
            else
            {
                builder.AddWarning("Prepared stock item could not be resolved.");
            }

            if (!menuItemMappings.Any(mapping =>
                    mapping.MenuItemId == batch.MenuItemId &&
                    mapping.InventoryItemId == batch.PreparedInventoryItemId))
            {
                builder.AddWarning("Missing prepared stock mapping.");
            }

            if (!preparedItemToMenuItemId.TryAdd(batch.PreparedInventoryItemId, batch.MenuItemId) &&
                preparedItemToMenuItemId[batch.PreparedInventoryItemId] != batch.MenuItemId)
            {
                builder.AddWarning("Prepared stock mapping is inconsistent.");
            }
        }

        foreach (var mapping in menuItemMappings)
        {
            var builder = GetOrCreateBuilder(builders, mapping.MenuItemId);
            builder.SetPreparedItem(mapping.InventoryItemId);

            if (menuItems.TryGetValue(mapping.MenuItemId, out var menuItem))
            {
                builder.SetMenuItem(menuItem.Name);
            }

            if (inventoryItems.TryGetValue(mapping.InventoryItemId, out var inventoryItem))
            {
                builder.SetPreparedInventoryItem(inventoryItem.Name, inventoryItem.UnitOfMeasure);
            }

            if (!preparedItemToMenuItemId.TryAdd(mapping.InventoryItemId, mapping.MenuItemId) &&
                preparedItemToMenuItemId[mapping.InventoryItemId] != mapping.MenuItemId)
            {
                builder.AddWarning("Prepared stock mapping is inconsistent.");
            }
        }

        foreach (var batch in batchProductions)
        {
            if (preparedItemToMenuItemId.TryGetValue(batch.PreparedInventoryItemId, out var mappedMenuItemId) &&
                builders.TryGetValue(mappedMenuItemId, out var builder))
            {
                builder.SetPreparedItem(batch.PreparedInventoryItemId);
            }
        }

        foreach (var served in servedTotals)
        {
            if (preparedItemToMenuItemId.TryGetValue(served.Key, out var menuItemId) &&
                builders.TryGetValue(menuItemId, out var builder))
            {
                builder.AddServed(served.Value);
            }
        }

        foreach (var wasted in wastedTotals)
        {
            if (preparedItemToMenuItemId.TryGetValue(wasted.Key, out var menuItemId) &&
                builders.TryGetValue(menuItemId, out var builder))
            {
                builder.AddWasted(wasted.Value);
            }
        }

        foreach (var movement in preparedMovementTotals)
        {
            if (preparedItemToMenuItemId.TryGetValue(movement.Key, out var menuItemId) &&
                builders.TryGetValue(menuItemId, out var builder))
            {
                builder.SetRemaining(movement.Value);
            }
        }

        var rows = builders.Values
            .Where(builder => builder.HasVisibleActivity)
            .Select(builder =>
            {
                if (builder.RemainingQuantity < 0m)
                {
                    builder.AddWarning("Negative remaining stock.");
                }

                return builder.ToRow();
            })
            .OrderByDescending(row => row.ProducedQuantity)
            .ThenBy(row => row.MenuItemName ?? row.MenuItemId.ToString())
            .ToArray();

        var totals = new PreparedStockReportTotals(
            RoundQuantity(rows.Sum(row => row.ProducedQuantity)),
            RoundQuantity(rows.Sum(row => row.ServedQuantity)),
            RoundQuantity(rows.Sum(row => row.WastedQuantity)),
            RoundQuantity(rows.Sum(row => row.RemainingQuantity)),
            rows.Length,
            rows.Count(row => row.HasWarning));

        return new PreparedStockReportResponse(
            branch.BranchId,
            branch.Name,
            reportBusinessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            totals,
            rows);
    }

    private static PreparedStockReportRowBuilder GetOrCreateBuilder(
        IDictionary<Guid, PreparedStockReportRowBuilder> builders,
        Guid menuItemId)
    {
        if (!builders.TryGetValue(menuItemId, out var builder))
        {
            builder = new PreparedStockReportRowBuilder(menuItemId);
            builders[menuItemId] = builder;
        }

        return builder;
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

    private static DateTime ResolveBusinessDate(DateTime? requestedDate, TimeZoneInfo timeZone, DateTimeOffset nowUtc)
    {
        if (requestedDate.HasValue)
        {
            return DateTime.SpecifyKind(requestedDate.Value.Date, DateTimeKind.Unspecified);
        }

        return TimeZoneInfo.ConvertTime(nowUtc.UtcDateTime, timeZone).Date;
    }

    private static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) ResolveBusinessDateRange(DateTime businessDate, TimeZoneInfo timeZone)
    {
        var localStart = DateTime.SpecifyKind(businessDate.Date, DateTimeKind.Unspecified);
        var localEnd = localStart.AddDays(1);

        return (
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone)),
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localEnd, timeZone)));
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

    private static decimal GetMovementDelta(InventoryMovementType movementType, decimal quantity)
    {
        return movementType switch
        {
            InventoryMovementType.StockIn => quantity,
            InventoryMovementType.Consumption => -quantity,
            InventoryMovementType.AdjustmentIncrease => quantity,
            InventoryMovementType.AdjustmentDecrease => -quantity,
            InventoryMovementType.Waste => -quantity,
            InventoryMovementType.Correction => -quantity,
            _ => throw new InvalidOperationException($"Unsupported movement type '{movementType}'.")
        };
    }

    private async Task<Dictionary<Guid, decimal>> LoadServedTotalsAsync(
        Guid restaurantId,
        Guid branchScope,
        Guid[] candidatePreparedInventoryItemIds,
        DateTimeOffset dayStartUtc,
        DateTimeOffset dayEndUtc,
        CancellationToken cancellationToken)
    {
        var servedTickets = await _context.KitchenTickets
            .AsNoTracking()
            .Where(ticket =>
                ticket.RestaurantId == restaurantId &&
                ticket.BranchId == branchScope &&
                ticket.Status == KitchenTicketStatus.Served &&
                ticket.ServedAt.HasValue)
            .Select(ticket => new
            {
                ticket.KitchenTicketId,
                ticket.ServedAt
            })
            .ToListAsync(cancellationToken);

        var servedTicketIds = servedTickets
            .Where(ticket =>
                ticket.ServedAt.HasValue &&
                ticket.ServedAt.Value >= dayStartUtc &&
                ticket.ServedAt.Value < dayEndUtc)
            .Select(ticket => ticket.KitchenTicketId)
            .ToArray();

        if (servedTicketIds.Length == 0)
        {
            return new Dictionary<Guid, decimal>();
        }

        var servedDeductionRows = await _context.KitchenTicketInventoryDeductions
            .AsNoTracking()
            .Where(deduction =>
                deduction.RestaurantId == restaurantId &&
                deduction.BranchId == branchScope &&
                servedTicketIds.Contains(deduction.KitchenTicketId) &&
                candidatePreparedInventoryItemIds.Contains(deduction.InventoryItemId))
            .Select(deduction => new
            {
                deduction.InventoryItemId,
                deduction.QuantityDeducted
            })
            .ToListAsync(cancellationToken);

        return servedDeductionRows
            .GroupBy(row => row.InventoryItemId)
            .ToDictionary(
                grouping => grouping.Key,
                grouping => RoundQuantity(grouping.Sum(row => row.QuantityDeducted)));
    }

    private static decimal RoundQuantity(decimal value)
    {
        return Math.Round(value, 3, MidpointRounding.AwayFromZero);
    }

    private sealed record RestaurantSnapshot(Guid RestaurantId, string Name, string TimeZoneId);

    private sealed record MenuItemSnapshot(Guid MenuItemId, string Name, MenuItemInventoryDeductionMode InventoryDeductionMode);

    private sealed record InventoryItemSnapshot(Guid InventoryItemId, string Name, string UnitOfMeasure);

    private sealed record PreparedStockBatchProductionSnapshot(
        Guid BatchProductionId,
        Guid MenuItemId,
        Guid PreparedInventoryItemId,
        decimal QuantityProduced,
        DateTime BusinessDate,
        DateTimeOffset ProducedAtUtc);

    private sealed record PreparedStockMappingSnapshot(
        Guid MenuItemId,
        Guid InventoryItemId);

    private sealed class PreparedStockReportRowBuilder
    {
        private readonly List<string> _warnings = new();

        public PreparedStockReportRowBuilder(Guid menuItemId)
        {
            MenuItemId = menuItemId;
        }

        public Guid MenuItemId { get; }

        public string? MenuItemName { get; private set; }

        public Guid? PreparedInventoryItemId { get; private set; }

        public string? PreparedInventoryItemName { get; private set; }

        public string? UnitOfMeasure { get; private set; }

        public decimal ProducedQuantity { get; private set; }

        public decimal ServedQuantity { get; private set; }

        public decimal WastedQuantity { get; private set; }

        public decimal RemainingQuantity { get; private set; }

        public bool HasVisibleActivity =>
            ProducedQuantity != 0m ||
            ServedQuantity != 0m ||
            WastedQuantity != 0m ||
            RemainingQuantity != 0m ||
            _warnings.Count > 0;

        public void SetMenuItem(string? menuItemName)
        {
            if (string.IsNullOrWhiteSpace(menuItemName))
            {
                return;
            }

            if (MenuItemName is null)
            {
                MenuItemName = menuItemName.Trim();
                return;
            }

            if (!string.Equals(MenuItemName, menuItemName.Trim(), StringComparison.Ordinal))
            {
                AddWarning("Menu item is inconsistent.");
            }
        }

        public void SetPreparedItem(Guid? preparedInventoryItemId)
        {
            if (!preparedInventoryItemId.HasValue)
            {
                return;
            }

            if (!PreparedInventoryItemId.HasValue)
            {
                PreparedInventoryItemId = preparedInventoryItemId;
                return;
            }

            if (PreparedInventoryItemId.Value != preparedInventoryItemId.Value)
            {
                AddWarning("Prepared stock item is inconsistent.");
            }
        }

        public void SetPreparedInventoryItem(string? preparedInventoryItemName, string? unitOfMeasure)
        {
            if (!string.IsNullOrWhiteSpace(preparedInventoryItemName))
            {
                if (PreparedInventoryItemName is null)
                {
                    PreparedInventoryItemName = preparedInventoryItemName.Trim();
                }
                else if (!string.Equals(PreparedInventoryItemName, preparedInventoryItemName.Trim(), StringComparison.Ordinal))
                {
                    AddWarning("Prepared stock item is inconsistent.");
                }
            }

            if (!string.IsNullOrWhiteSpace(unitOfMeasure))
            {
                if (UnitOfMeasure is null)
                {
                    UnitOfMeasure = unitOfMeasure.Trim();
                }
                else if (!string.Equals(UnitOfMeasure, unitOfMeasure.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    AddWarning("Prepared stock unit mismatch.");
                }
            }
        }

        public void RecordBatchProduction(decimal quantityProduced)
        {
            ProducedQuantity += quantityProduced;
        }

        public void AddServed(decimal quantity)
        {
            ServedQuantity += quantity;
        }

        public void AddWasted(decimal quantity)
        {
            WastedQuantity += quantity;
        }

        public void SetRemaining(decimal quantity)
        {
            RemainingQuantity = quantity;
        }

        public void AddWarning(string warning)
        {
            if (string.IsNullOrWhiteSpace(warning))
            {
                return;
            }

            var normalizedWarning = warning.Trim();
            if (!_warnings.Any(existing => string.Equals(existing, normalizedWarning, StringComparison.Ordinal)))
            {
                _warnings.Add(normalizedWarning);
            }
        }

        public PreparedStockReportRow ToRow()
        {
            var warningReason = _warnings.Count == 0
                ? null
                : string.Join("; ", _warnings);

            return new PreparedStockReportRow(
                MenuItemId,
                MenuItemName,
                PreparedInventoryItemId,
                PreparedInventoryItemName,
                UnitOfMeasure,
                RoundQuantity(ProducedQuantity),
                RoundQuantity(ServedQuantity),
                RoundQuantity(WastedQuantity),
                RoundQuantity(RemainingQuantity),
                _warnings.Count > 0,
                warningReason);
        }
    }
}
