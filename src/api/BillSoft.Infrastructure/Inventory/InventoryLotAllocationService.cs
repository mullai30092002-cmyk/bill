using System.Data;
using BillSoft.Domain.Inventory;
using BillSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillSoft.Infrastructure.Inventory;

public sealed class InventoryLotAllocationService
{
    private const string OpeningLotBatchReference = "Opening lot";
    private readonly BillSoftDbContext _context;

    public InventoryLotAllocationService(BillSoftDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AllocateAsync(
        Guid restaurantId,
        Guid branchId,
        Guid inventoryItemId,
        Guid inventoryMovementId,
        decimal quantity,
        string allocationReason,
        bool allowExpiredLots,
        decimal currentStockBeforeMovement,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        if (restaurantId == Guid.Empty)
        {
            throw new InvalidOperationException("Restaurant scope is required for lot allocation.");
        }

        if (branchId == Guid.Empty)
        {
            throw new InvalidOperationException("Branch scope is required for lot allocation.");
        }

        if (inventoryItemId == Guid.Empty)
        {
            throw new InvalidOperationException("Inventory item is required for lot allocation.");
        }

        if (inventoryMovementId == Guid.Empty)
        {
            throw new InvalidOperationException("Inventory movement is required for lot allocation.");
        }

        if (quantity <= 0m)
        {
            throw new InvalidOperationException("Allocation quantity must be greater than zero.");
        }

        var inventoryItem = await _context.InventoryItems
            .AsNoTracking()
            .SingleOrDefaultAsync(
                entity => entity.RestaurantId == restaurantId &&
                          entity.BranchId == branchId &&
                          entity.InventoryItemId == inventoryItemId,
                cancellationToken);

        if (inventoryItem is null)
        {
            throw new KeyNotFoundException("Inventory item not found.");
        }

        var lots = await _context.InventoryLots
            .Where(entity =>
                entity.RestaurantId == restaurantId &&
                entity.BranchId == branchId &&
                entity.InventoryItemId == inventoryItemId &&
                entity.RemainingQuantity > 0m)
            .ToListAsync(cancellationToken);

        var totalLotRemaining = lots.Sum(entity => entity.RemainingQuantity);
        var openingLotQuantity = currentStockBeforeMovement - totalLotRemaining;
        if (openingLotQuantity > 0m)
        {
            var openingLot = new InventoryLot
            {
                RestaurantId = restaurantId,
                BranchId = branchId,
                InventoryItemId = inventoryItemId,
                SourceMovementId = null,
                SourceBatchProductionId = null,
                BatchReference = OpeningLotBatchReference,
                ReceivedAtUtc = createdAtUtc,
                ExpiresAtUtc = null,
                InitialQuantity = openingLotQuantity,
                RemainingQuantity = openingLotQuantity,
                UnitOfMeasure = inventoryItem.UnitOfMeasure,
                CreatedAtUtc = createdAtUtc
            };

            _context.InventoryLots.Add(openingLot);
            lots.Add(openingLot);
        }

        var eligibleLots = allowExpiredLots
            ? lots
            : lots.Where(entity => !IsExpired(entity, createdAtUtc)).ToList();

        if (eligibleLots.Count == 0)
        {
            if (!allowExpiredLots && lots.Count > 0)
            {
                throw new InvalidOperationException("Available stock is expired. Record wastage before serving or add fresh stock.");
            }

            throw new InvalidOperationException("Insufficient stock available for allocation.");
        }

        var orderedLots = eligibleLots
            .OrderBy(entity => GetAllocationGroup(entity, allowExpiredLots, createdAtUtc))
            .ThenBy(entity => entity.ExpiresAtUtc ?? DateTimeOffset.MaxValue)
            .ThenBy(entity => entity.ReceivedAtUtc)
            .ThenBy(entity => entity.InventoryLotId)
            .ToList();

        var eligibleQuantity = orderedLots.Sum(entity => entity.RemainingQuantity);
        if (eligibleQuantity < quantity)
        {
            if (!allowExpiredLots && orderedLots.Count == 0 && lots.Count > 0)
            {
                throw new InvalidOperationException("Available stock is expired. Record wastage before serving or add fresh stock.");
            }

            throw new InvalidOperationException("Insufficient stock available for allocation.");
        }

        var remainingToAllocate = quantity;
        foreach (var lot in orderedLots)
        {
            if (remainingToAllocate <= 0m)
            {
                break;
            }

            var allocatedQuantity = Math.Min(lot.RemainingQuantity, remainingToAllocate);
            if (allocatedQuantity <= 0m)
            {
                continue;
            }

            lot.RemainingQuantity = lot.RemainingQuantity - allocatedQuantity;
            lot.UpdatedAtUtc = createdAtUtc;
            lot.ClosedAtUtc = lot.RemainingQuantity == 0m ? createdAtUtc : null;

            _context.InventoryLotAllocations.Add(new InventoryLotAllocation
            {
                RestaurantId = restaurantId,
                BranchId = branchId,
                InventoryItemId = inventoryItemId,
                InventoryLotId = lot.InventoryLotId,
                InventoryMovementId = inventoryMovementId,
                QuantityAllocated = allocatedQuantity,
                AllocationReason = allocationReason.Trim(),
                CreatedAtUtc = createdAtUtc
            });

            remainingToAllocate -= allocatedQuantity;
        }

        if (remainingToAllocate > 0m)
        {
            throw new InvalidOperationException("Insufficient stock available for allocation.");
        }
    }

    private static bool IsExpired(InventoryLot lot, DateTimeOffset nowUtc)
    {
        return lot.ExpiresAtUtc.HasValue && lot.ExpiresAtUtc.Value <= nowUtc;
    }

    private static int GetAllocationGroup(InventoryLot lot, bool allowExpiredLots, DateTimeOffset nowUtc)
    {
        if (allowExpiredLots)
        {
            if (IsExpired(lot, nowUtc))
            {
                return 0;
            }

            if (lot.ExpiresAtUtc.HasValue)
            {
                return 1;
            }

            return 2;
        }

        return lot.ExpiresAtUtc.HasValue ? 0 : 1;
    }
}
