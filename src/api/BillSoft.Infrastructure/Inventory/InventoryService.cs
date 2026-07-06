using System.Data;
using System.Text.Json;
using BillSoft.Application.Auth;
using BillSoft.Application.Inventory;
using BillSoft.Domain.Auditing;
using BillSoft.Domain.Inventory;
using BillSoft.Domain.Menu;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Users;
using BillSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillSoft.Infrastructure.Inventory;

public sealed class InventoryService : IInventoryService
{
    private static readonly string[] AllowedAdjustmentReasons =
    [
        "Opening stock correction",
        "Damaged/wastage",
        "Physical count correction",
        "Manual purchase entry",
        "Other"
    ];

    private readonly BillSoftDbContext _context;
    private readonly InventoryLotAllocationService _lotAllocationService;

    public InventoryService(BillSoftDbContext context, InventoryLotAllocationService lotAllocationService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _lotAllocationService = lotAllocationService ?? throw new ArgumentNullException(nameof(lotAllocationService));
    }

    public async Task<InventoryItemListResponse> ListItemsAsync(AuthUserContext currentUser, InventoryItemListQuery query, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var branchId = ResolveBranchScope(currentUser, query.BranchId);

        var items = await _context.InventoryItems
            .AsNoTracking()
            .Where(entity => entity.RestaurantId == restaurantId && entity.BranchId == branchId)
            .OrderBy(entity => entity.Category)
            .ThenBy(entity => entity.Name)
            .ToListAsync(cancellationToken);

        var stockMap = await LoadCurrentStockMapAsync(restaurantId, branchId, items.Select(item => item.InventoryItemId).ToArray(), cancellationToken);

        return new InventoryItemListResponse(items.Select(item => ToItemListItem(item, stockMap)).ToArray());
    }

    public async Task<InventorySummaryResponse> GetSummaryAsync(AuthUserContext currentUser, InventorySummaryQuery query, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var branchId = ResolveBranchScope(currentUser, query.BranchId);

        var items = await _context.InventoryItems
            .AsNoTracking()
            .Where(entity => entity.RestaurantId == restaurantId && entity.BranchId == branchId)
            .OrderBy(entity => entity.Category)
            .ThenBy(entity => entity.Name)
            .ToListAsync(cancellationToken);

        var stockMap = await LoadCurrentStockMapAsync(restaurantId, branchId, items.Select(item => item.InventoryItemId).ToArray(), cancellationToken);
        var itemSnapshots = items.Select(item => ToItemSnapshot(item, stockMap)).ToArray();
        var recentlyAdjustedCutoff = DateTimeOffset.UtcNow.AddDays(-7);
        var recentlyAdjustedCount = (await _context.InventoryMovements
                .AsNoTracking()
                .Where(entity =>
                    entity.RestaurantId == restaurantId &&
                    entity.BranchId == branchId &&
                    (entity.MovementType == InventoryMovementType.AdjustmentIncrease || entity.MovementType == InventoryMovementType.AdjustmentDecrease))
                .Select(entity => new
                {
                    entity.InventoryItemId,
                    entity.CreatedAtUtc
                })
                .ToListAsync(cancellationToken))
            .Where(entity => entity.CreatedAtUtc >= recentlyAdjustedCutoff)
            .Select(entity => entity.InventoryItemId)
            .Distinct()
            .Count();

        var lowStockItems = itemSnapshots
            .Where(item => item.Status == "Low stock")
            .Select(ToAlertItem)
            .ToArray();

        var outOfStockItems = itemSnapshots
            .Where(item => item.Status == "Out of stock")
            .Select(ToAlertItem)
            .ToArray();

        return new InventorySummaryResponse(
            restaurantId,
            branchId,
            itemSnapshots.Length,
            itemSnapshots.Count(item => item.IsActive),
            itemSnapshots.Count(item => !item.IsActive),
            lowStockItems.Length,
            outOfStockItems.Length,
            itemSnapshots.Sum(item => item.CurrentStock),
            recentlyAdjustedCount,
            lowStockItems,
            outOfStockItems);
    }

    public async Task<InventoryItemListItem> CreateItemAsync(AuthUserContext currentUser, CreateInventoryItemRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        var restaurantId = RequireRestaurantScope(currentUser);
        var branchId = ResolveBranchScope(currentUser, request.BranchId);
        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var branch = await LoadBranchAsync(restaurantId, branchId, cancellationToken);
        var normalizedName = InventoryItem.NormalizeKey(request.Name!);
        var now = DateTimeOffset.UtcNow;

        await EnsureUniqueNameAsync(restaurantId, branchId, null, normalizedName, cancellationToken);

        var item = new InventoryItem
        {
            RestaurantId = restaurantId,
            BranchId = branchId,
            Name = NormalizeRequiredText(request.Name!),
            NormalizedName = normalizedName,
            Category = NormalizeRequiredText(request.Category!),
            UnitOfMeasure = NormalizeRequiredText(request.UnitOfMeasure!),
            LowStockThreshold = request.LowStockThreshold,
            IsActive = request.IsActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = null
        };

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        _context.InventoryItems.Add(item);
        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: branch,
            action: "InventoryItem.Created",
            reason: "Inventory item created.",
            entityType: "InventoryItem",
            entityId: item.InventoryItemId.ToString(),
            oldValueJson: null,
            newValueJson: Serialize(ToItemListItem(item, 0m)),
            createdAt: now);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToItemListItem(item, 0m);
    }

    public async Task<InventoryItemListItem> UpdateItemAsync(AuthUserContext currentUser, Guid itemId, UpdateInventoryItemRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        var restaurantId = RequireRestaurantScope(currentUser);
        var item = await LoadTrackedItemAsync(restaurantId, itemId, currentUser.BranchId, cancellationToken);
        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var branch = await LoadBranchAsync(restaurantId, item.BranchId, cancellationToken);
        var before = ToItemListItem(item, await GetCurrentStockAsync(item.InventoryItemId, cancellationToken));
        var normalizedName = InventoryItem.NormalizeKey(request.Name!);

        await EnsureUniqueNameAsync(restaurantId, item.BranchId, item.InventoryItemId, normalizedName, cancellationToken);

        item.UpdateProfile(
            request.Name!,
            request.Category!,
            request.UnitOfMeasure!,
            request.LowStockThreshold,
            request.IsActive,
            DateTimeOffset.UtcNow);

        var after = ToItemListItem(item, await GetCurrentStockAsync(item.InventoryItemId, cancellationToken));
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: branch,
            action: "InventoryItem.Updated",
            reason: "Inventory item updated.",
            entityType: "InventoryItem",
            entityId: item.InventoryItemId.ToString(),
            oldValueJson: Serialize(before),
            newValueJson: Serialize(after),
            createdAt: DateTimeOffset.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return after;
    }

    public async Task<InventoryMovementListResponse> ListMovementsAsync(AuthUserContext currentUser, Guid itemId, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var item = await LoadItemAsync(restaurantId, itemId, currentUser.BranchId, cancellationToken);

        var movements = await _context.InventoryMovements
            .AsNoTracking()
            .Where(entity => entity.RestaurantId == restaurantId && entity.InventoryItemId == item.InventoryItemId)
            .ToListAsync(cancellationToken);

        var userIds = movements.Select(entity => entity.RecordedByUserId).Distinct().ToArray();
        var users = userIds.Length == 0
            ? new Dictionary<Guid, (string Name, string Mobile)>()
            : await _context.Users
                .AsNoTracking()
                .Where(entity => userIds.Contains(entity.UserId))
                .Select(entity => new
                {
                    entity.UserId,
                    entity.FullName,
                    entity.MobileNumber
                })
                .ToDictionaryAsync(entity => entity.UserId, entity => (Name: entity.FullName, Mobile: entity.MobileNumber), cancellationToken);

        movements = movements
            .OrderBy(entity => entity.MovementDate)
            .ThenBy(entity => entity.CreatedAtUtc)
            .ToList();

        var runningBalance = 0m;
        var items = new List<InventoryMovementItem>(movements.Count);

        foreach (var movement in movements)
        {
            var delta = GetMovementDelta(movement.MovementType, movement.Quantity);
            var previousStock = runningBalance;
            runningBalance = RoundMoney(previousStock + delta);
            users.TryGetValue(movement.RecordedByUserId, out var user);
            items.Add(ToMovementItem(
                movement,
                previousStock,
                delta,
                runningBalance,
                item,
                string.IsNullOrWhiteSpace(user.Name) ? movement.RecordedByUserId.ToString() : user.Name,
                string.IsNullOrWhiteSpace(user.Mobile) ? string.Empty : user.Mobile));
        }

        return new InventoryMovementListResponse(
            items
                .OrderByDescending(item => item.MovementDate)
                .ThenByDescending(item => item.CreatedAtUtc)
                .ToArray());
    }

    public async Task<InventoryMovementItem> RecordMovementAsync(AuthUserContext currentUser, Guid itemId, CreateInventoryMovementRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        var movementType = ResolveAdjustmentMovementType(request.MovementType);
        if (request.Quantity <= 0)
        {
            throw new InvalidOperationException("Quantity must be greater than zero.");
        }

        var reason = ResolveAdjustmentReason(request.Reason);
        var restaurantId = RequireRestaurantScope(currentUser);
        var item = await LoadTrackedItemAsync(restaurantId, itemId, currentUser.BranchId, cancellationToken);
        if (!item.IsActive)
        {
            throw new InvalidOperationException("Inactive inventory items cannot receive new movements.");
        }

        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var branch = await LoadBranchAsync(restaurantId, item.BranchId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var currentStock = await GetCurrentStockAsync(item.InventoryItemId, cancellationToken);
        var resultingStock = RoundMoney(currentStock + GetMovementDelta(movementType, request.Quantity));

        if (resultingStock < 0m)
        {
            throw new InvalidOperationException("Inventory stock cannot go below zero.");
        }

        ValidateExpiryForMovement(request.ExpiresAt, request.MovementDate ?? now, movementType);

        var movement = new InventoryMovement
        {
            RestaurantId = restaurantId,
            BranchId = item.BranchId,
            InventoryItemId = item.InventoryItemId,
            MovementType = movementType,
            Quantity = request.Quantity,
            UnitCost = request.UnitCost,
            ReferenceNumber = NormalizeOptionalText(request.ReferenceNumber, 128),
            Reason = reason,
            Notes = NormalizeOptionalText(request.Notes, 500),
            MovementDate = request.MovementDate ?? now,
            RecordedByUserId = RequireUserId(currentUser),
            CreatedAtUtc = now,
            ExpiresAtUtc = request.ExpiresAt,
            BatchReference = NormalizeOptionalText(request.BatchReference, 128)
        };
        var createLot = movementType is InventoryMovementType.StockIn or InventoryMovementType.AdjustmentIncrease;

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        _context.InventoryMovements.Add(movement);
        if (createLot)
        {
            _context.InventoryLots.Add(CreateInventoryLot(
                restaurantId,
                item.BranchId,
                item.InventoryItemId,
                movement.InventoryMovementId,
                sourceBatchProductionId: null,
                batchReference: movement.BatchReference,
                receivedAtUtc: movement.MovementDate,
                expiresAtUtc: movement.ExpiresAtUtc,
                quantity: movement.Quantity,
                unitOfMeasure: item.UnitOfMeasure,
                createdAtUtc: now));
        }
        else if (movementType == InventoryMovementType.AdjustmentDecrease)
        {
            await _lotAllocationService.AllocateAsync(
                restaurantId,
                item.BranchId,
                item.InventoryItemId,
                movement.InventoryMovementId,
                request.Quantity,
                "Inventory adjustment decrease",
                allowExpiredLots: false,
                currentStockBeforeMovement: currentStock,
                createdAtUtc: now,
                cancellationToken: cancellationToken);
        }

        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: branch,
            action: "InventoryMovement.Recorded",
            reason: reason,
            entityType: "InventoryMovement",
            entityId: movement.InventoryMovementId.ToString(),
            oldValueJson: null,
            newValueJson: Serialize(new
            {
                movement.InventoryMovementId,
                movement.InventoryItemId,
                movement.MovementType,
                movement.Quantity,
                movement.UnitCost,
                movement.ReferenceNumber,
                movement.Reason,
                movement.Notes,
                movement.MovementDate,
                previousStock = currentStock,
                delta = GetMovementDelta(movementType, request.Quantity),
                resultingStock
            }),
            createdAt: now);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToMovementItem(
            movement,
            currentStock,
            GetMovementDelta(movementType, request.Quantity),
            resultingStock,
            item,
            currentUser.FullName,
            currentUser.MobileNumber);
    }

    public async Task<BatchProductionListResponse> ListBatchProductionsAsync(AuthUserContext currentUser, BatchProductionListQuery query, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var branchId = ResolveBranchScope(currentUser, query.BranchId);
        _ = await LoadBranchAsync(restaurantId, branchId, cancellationToken);

        var productionsQuery =
            from batch in _context.BatchProductions.AsNoTracking()
            join menuItem in _context.MenuItems.AsNoTracking() on batch.MenuItemId equals menuItem.MenuItemId
            join preparedItem in _context.InventoryItems.AsNoTracking() on batch.PreparedInventoryItemId equals preparedItem.InventoryItemId
            join user in _context.Users.AsNoTracking() on batch.ProducedByUserId equals user.UserId
            where batch.RestaurantId == restaurantId &&
                  batch.BranchId == branchId &&
                  menuItem.RestaurantId == restaurantId &&
                  preparedItem.RestaurantId == restaurantId &&
                  preparedItem.BranchId == branchId &&
                  user.RestaurantId == restaurantId
            select new
            {
                batch,
                MenuItemName = menuItem.Name,
                PreparedInventoryItemName = preparedItem.Name,
                ProducedByUserName = user.FullName
            };

        if (query.FromBusinessDate.HasValue)
        {
            var fromDate = query.FromBusinessDate.Value.Date;
            productionsQuery = productionsQuery.Where(entry => entry.batch.BusinessDate >= fromDate);
        }

        if (query.ToBusinessDate.HasValue)
        {
            var toExclusive = query.ToBusinessDate.Value.Date.AddDays(1);
            productionsQuery = productionsQuery.Where(entry => entry.batch.BusinessDate < toExclusive);
        }

        var batchRows = await productionsQuery
            .OrderByDescending(entry => entry.batch.BusinessDate)
            .ThenByDescending(entry => entry.batch.ProducedAtUtc)
            .ToListAsync(cancellationToken);

        var batchIds = batchRows.Select(entry => entry.batch.BatchProductionId).ToArray();
        var rawConsumptionTotals = batchIds.Length == 0
            ? new Dictionary<Guid, decimal>()
            : await _context.BatchProductionIngredientConsumptions
                .AsNoTracking()
                .Where(entity => entity.RestaurantId == restaurantId && entity.BranchId == branchId && batchIds.Contains(entity.BatchProductionId))
                .GroupBy(entity => entity.BatchProductionId)
                .Select(group => new
                {
                    BatchProductionId = group.Key,
                    QuantityConsumed = group.Sum(entity => entity.QuantityConsumed)
                })
                .ToDictionaryAsync(entry => entry.BatchProductionId, entry => entry.QuantityConsumed, cancellationToken);

        var items = batchRows.Select(entry => new BatchProductionListItem(
            entry.batch.BatchProductionId,
            entry.batch.RestaurantId,
            entry.batch.BranchId,
            entry.batch.MenuItemId,
            entry.MenuItemName,
            entry.batch.PreparedInventoryItemId,
            entry.PreparedInventoryItemName,
            entry.batch.QuantityProduced,
            entry.batch.BusinessDate,
            entry.batch.ProducedAtUtc,
            entry.batch.ProducedByUserId,
            entry.ProducedByUserName,
            entry.batch.Notes,
            rawConsumptionTotals.TryGetValue(entry.batch.BatchProductionId, out var quantityConsumed) ? quantityConsumed : 0m,
            entry.batch.CreatedAtUtc,
            entry.batch.ShelfLifeHours,
            entry.batch.ExpiresAtUtc,
            entry.batch.StorageNote,
            entry.batch.BatchReference)).ToArray();

        return new BatchProductionListResponse(items);
    }

    public async Task<BatchProductionDetail> CreateBatchProductionAsync(AuthUserContext currentUser, CreateBatchProductionRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        if (request.QuantityProduced <= 0m)
        {
            throw new InvalidOperationException("Quantity produced must be greater than zero.");
        }

        var restaurantId = RequireRestaurantScope(currentUser);
        var branchId = ResolveBranchScope(currentUser, request.BranchId);
        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var branch = await LoadBranchAsync(restaurantId, branchId, cancellationToken);
        var menuItem = await LoadMenuItemAsync(restaurantId, request.MenuItemId, cancellationToken);

        if (menuItem.InventoryDeductionMode != MenuItemInventoryDeductionMode.BatchPrepared)
        {
            throw new InvalidOperationException("Batch production is only allowed for BatchPrepared menu items.");
        }

        var stockMapping = await LoadPreparedStockMappingAsync(restaurantId, branchId, menuItem.MenuItemId, cancellationToken);
        if (stockMapping is null)
        {
            throw new InvalidOperationException("BatchPrepared menu items must have a prepared stock item mapping.");
        }

        var recipeIngredients = await LoadRecipeIngredientSnapshotsAsync(restaurantId, branchId, [menuItem.MenuItemId], cancellationToken);
        if (recipeIngredients.Count == 0)
        {
            throw new InvalidOperationException("Batch production requires a recipe for the current branch.");
        }

        var producedAtUtc = request.ProducedAtUtc ?? DateTimeOffset.UtcNow;
        var businessDate = (request.BusinessDate ?? producedAtUtc.UtcDateTime.Date).Date;
        var producedByUserId = RequireUserId(currentUser);
        var now = DateTimeOffset.UtcNow;

        var resolvedExpiresAt = ResolveExpiresAt(producedAtUtc, request.ShelfLifeHours, request.ExpiresAt);
        ValidateBatchExpiry(producedAtUtc, request.ShelfLifeHours, request.ExpiresAt, resolvedExpiresAt);

        var rawRequirements = recipeIngredients
            .GroupBy(entry => new { entry.InventoryItemId, entry.InventoryItemName })
            .Select(group => new BatchIngredientPlan(
                group.Key.InventoryItemId,
                group.Key.InventoryItemName,
                RoundMoney(group.Sum(entry => entry.QuantityRequired) * request.QuantityProduced)))
            .ToArray();

        var stockMap = await LoadCurrentStockMapAsync(
            restaurantId,
            branchId,
            rawRequirements.Select(entry => entry.InventoryItemId).Append(stockMapping.InventoryItemId).Distinct().ToArray(),
            cancellationToken);

        var insufficientRaw = rawRequirements
            .Where(entry => (stockMap.TryGetValue(entry.InventoryItemId, out var stock) ? stock : 0m) < entry.QuantityRequired)
            .ToArray();

        if (insufficientRaw.Length > 0)
        {
            throw new InvalidOperationException(BuildInsufficientBatchStockMessage(insufficientRaw));
        }

        var preparedStockCurrent = stockMap.TryGetValue(stockMapping.InventoryItemId, out var preparedStock) ? preparedStock : 0m;
        var preparedStockResulting = RoundMoney(preparedStockCurrent + request.QuantityProduced);
        if (preparedStockResulting < 0m)
        {
            throw new InvalidOperationException("Prepared stock cannot go below zero.");
        }

        var batch = new BatchProduction
        {
            RestaurantId = restaurantId,
            BranchId = branchId,
            MenuItemId = menuItem.MenuItemId,
            PreparedInventoryItemId = stockMapping.InventoryItemId,
            QuantityProduced = request.QuantityProduced,
            BusinessDate = businessDate,
            ProducedAtUtc = producedAtUtc,
            ProducedByUserId = producedByUserId,
            Notes = NormalizeOptionalText(request.Notes, 500),
            ShelfLifeHours = request.ShelfLifeHours,
            ExpiresAtUtc = resolvedExpiresAt,
            StorageNote = NormalizeOptionalText(request.StorageNote, 500),
            BatchReference = NormalizeOptionalText(request.BatchReference, 128),
            CreatedAtUtc = now
        };

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        _context.BatchProductions.Add(batch);

        foreach (var ingredient in rawRequirements)
        {
            var currentStockBeforeMovement = stockMap.TryGetValue(ingredient.InventoryItemId, out var rawStock) ? rawStock : 0m;
            var movement = new InventoryMovement
            {
                RestaurantId = restaurantId,
                BranchId = branchId,
                InventoryItemId = ingredient.InventoryItemId,
                MovementType = InventoryMovementType.Consumption,
                Quantity = ingredient.QuantityRequired,
                ReferenceNumber = $"BATCH-{batch.BatchProductionId:N}",
                Reason = "Batch production raw ingredient consumption",
                Notes = $"Batch production for {menuItem.Name}.",
                MovementDate = producedAtUtc,
                RecordedByUserId = producedByUserId,
                CreatedAtUtc = now
            };

            _context.InventoryMovements.Add(movement);
            _context.BatchProductionIngredientConsumptions.Add(new BatchProductionIngredientConsumption
            {
                RestaurantId = restaurantId,
                BranchId = branchId,
                BatchProductionId = batch.BatchProductionId,
                InventoryItemId = ingredient.InventoryItemId,
                InventoryMovementId = movement.InventoryMovementId,
                InventoryItemNameSnapshot = ingredient.InventoryItemName,
                QuantityConsumed = ingredient.QuantityRequired,
                CreatedAtUtc = now
            });

            await _lotAllocationService.AllocateAsync(
                restaurantId,
                branchId,
                ingredient.InventoryItemId,
                movement.InventoryMovementId,
                ingredient.QuantityRequired,
                "Batch production raw ingredient consumption",
                allowExpiredLots: false,
                currentStockBeforeMovement: currentStockBeforeMovement,
                createdAtUtc: now,
                cancellationToken: cancellationToken);

            AddAudit(
                actor: currentUser,
                restaurant: restaurant,
                branch: branch,
                action: "InventoryMovement.Recorded",
                reason: "Batch production raw ingredient consumption.",
                entityType: "InventoryMovement",
                entityId: movement.InventoryMovementId.ToString(),
                oldValueJson: null,
                newValueJson: Serialize(new
                {
                    movement.InventoryMovementId,
                    movement.InventoryItemId,
                    movement.MovementType,
                    movement.Quantity,
                    movement.ReferenceNumber,
                    movement.Reason,
                    movement.Notes,
                    movement.MovementDate
                }),
                createdAt: now);
        }

        var preparedMovement = new InventoryMovement
        {
            RestaurantId = restaurantId,
            BranchId = branchId,
            InventoryItemId = stockMapping.InventoryItemId,
            MovementType = InventoryMovementType.StockIn,
            Quantity = request.QuantityProduced,
            ReferenceNumber = $"BATCH-{batch.BatchProductionId:N}",
            Reason = "Batch production prepared stock increase",
            Notes = NormalizeOptionalText(request.Notes, 500),
            MovementDate = producedAtUtc,
            RecordedByUserId = producedByUserId,
            CreatedAtUtc = now
        };
        var preparedStockItem = await LoadTrackedItemAsync(restaurantId, stockMapping.InventoryItemId, branchId, cancellationToken);

        _context.InventoryMovements.Add(preparedMovement);
        batch.PreparedInventoryMovementId = preparedMovement.InventoryMovementId;
        batch.UpdatedAtUtc = now;
        _context.InventoryLots.Add(CreateInventoryLot(
            restaurantId,
            branchId,
            stockMapping.InventoryItemId,
            preparedMovement.InventoryMovementId,
            sourceBatchProductionId: batch.BatchProductionId,
            batchReference: batch.BatchReference,
            receivedAtUtc: batch.ProducedAtUtc,
            expiresAtUtc: batch.ExpiresAtUtc,
            quantity: batch.QuantityProduced,
            unitOfMeasure: preparedStockItem.UnitOfMeasure,
            createdAtUtc: now));

        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: branch,
            action: "InventoryMovement.Recorded",
            reason: "Batch production prepared stock increase.",
            entityType: "InventoryMovement",
            entityId: preparedMovement.InventoryMovementId.ToString(),
            oldValueJson: null,
            newValueJson: Serialize(new
            {
                preparedMovement.InventoryMovementId,
                preparedMovement.InventoryItemId,
                preparedMovement.MovementType,
                preparedMovement.Quantity,
                preparedMovement.ReferenceNumber,
                preparedMovement.Reason,
                preparedMovement.Notes,
                preparedMovement.MovementDate
            }),
            createdAt: now);

        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: branch,
            action: "BatchProduction.Created",
            reason: "Batch production recorded.",
            entityType: "BatchProduction",
            entityId: batch.BatchProductionId.ToString(),
            oldValueJson: null,
            newValueJson: Serialize(new
            {
                batch.BatchProductionId,
                batch.MenuItemId,
                MenuItemName = menuItem.Name,
                batch.PreparedInventoryItemId,
                PreparedInventoryItemName = stockMapping.InventoryItemName,
                batch.QuantityProduced,
                batch.BusinessDate,
                batch.ProducedAtUtc,
                batch.Notes,
                batch.ShelfLifeHours,
                batch.ExpiresAtUtc,
                batch.StorageNote,
                batch.BatchReference
            }),
            createdAt: now);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await LoadBatchProductionDetailAsync(restaurantId, branchId, batch.BatchProductionId, cancellationToken);
    }

    public async Task<InventoryMovementItem> RecordPreparedStockWastageAsync(AuthUserContext currentUser, RecordPreparedStockWastageRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        if (request.Quantity <= 0m)
        {
            throw new InvalidOperationException("Quantity must be greater than zero.");
        }

        var restaurantId = RequireRestaurantScope(currentUser);
        var branchId = ResolveBranchScope(currentUser, request.BranchId);
        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var branch = await LoadBranchAsync(restaurantId, branchId, cancellationToken);
        var menuItem = await LoadMenuItemAsync(restaurantId, request.MenuItemId, cancellationToken);

        if (menuItem.InventoryDeductionMode is not MenuItemInventoryDeductionMode.BatchPrepared and not MenuItemInventoryDeductionMode.DirectStockItem)
        {
            throw new InvalidOperationException("Prepared stock wastage is only available for prepared or direct stock menu items.");
        }

        var stockMapping = await LoadPreparedStockMappingAsync(restaurantId, branchId, menuItem.MenuItemId, cancellationToken);
        if (stockMapping is null)
        {
            throw new InvalidOperationException("Prepared stock wastage requires a prepared stock item mapping.");
        }

        var inventoryItem = await _context.InventoryItems
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.RestaurantId == restaurantId && entity.BranchId == branchId && entity.InventoryItemId == stockMapping.InventoryItemId, cancellationToken);

        if (inventoryItem is null)
        {
            throw new KeyNotFoundException("Inventory item not found.");
        }

        var currentStock = await GetCurrentStockAsync(restaurantId, branchId, inventoryItem.InventoryItemId, cancellationToken);
        var resultingStock = RoundMoney(currentStock - request.Quantity);
        if (resultingStock < 0m)
        {
            throw new InvalidOperationException("Prepared stock cannot go below zero.");
        }

        var movement = new InventoryMovement
        {
            RestaurantId = restaurantId,
            BranchId = branchId,
            InventoryItemId = inventoryItem.InventoryItemId,
            MovementType = InventoryMovementType.Waste,
            Quantity = request.Quantity,
            ReferenceNumber = $"WASTE-{menuItem.MenuItemId:N}",
            Reason = request.Reason!.Trim(),
            Notes = NormalizeOptionalText(request.Notes, 500),
            MovementDate = request.WastedAtUtc ?? DateTimeOffset.UtcNow,
            RecordedByUserId = RequireUserId(currentUser),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        _context.InventoryMovements.Add(movement);
        await _lotAllocationService.AllocateAsync(
            restaurantId,
            branchId,
            inventoryItem.InventoryItemId,
            movement.InventoryMovementId,
            request.Quantity,
            "Prepared stock wastage",
            allowExpiredLots: true,
            currentStockBeforeMovement: currentStock,
            createdAtUtc: movement.MovementDate,
            cancellationToken: cancellationToken);
        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: branch,
            action: "InventoryMovement.Recorded",
            reason: "Prepared stock wastage recorded.",
            entityType: "InventoryMovement",
            entityId: movement.InventoryMovementId.ToString(),
            oldValueJson: null,
            newValueJson: Serialize(new
            {
                movement.InventoryMovementId,
                movement.InventoryItemId,
                movement.MovementType,
                movement.Quantity,
                movement.ReferenceNumber,
                movement.Reason,
                movement.Notes,
                movement.MovementDate,
                previousStock = currentStock,
                resultingStock
            }),
            createdAt: movement.MovementDate);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToMovementItem(
            movement,
            currentStock,
            GetMovementDelta(movement.MovementType, movement.Quantity),
            resultingStock,
            inventoryItem,
            currentUser.FullName,
            currentUser.MobileNumber);
    }

    private async Task<InventoryItem> LoadItemAsync(Guid restaurantId, Guid itemId, Guid? branchId, CancellationToken cancellationToken)
    {
        var query = _context.InventoryItems.AsNoTracking().Where(entity => entity.RestaurantId == restaurantId && entity.InventoryItemId == itemId);

        if (branchId.HasValue)
        {
            query = query.Where(entity => entity.BranchId == branchId.Value);
        }

        var item = await query.SingleOrDefaultAsync(cancellationToken);
        if (item is null)
        {
            throw new KeyNotFoundException("Inventory item not found.");
        }

        return item;
    }

    private async Task<InventoryItem> LoadTrackedItemAsync(Guid restaurantId, Guid itemId, Guid? branchId, CancellationToken cancellationToken)
    {
        var query = _context.InventoryItems.Where(entity => entity.RestaurantId == restaurantId && entity.InventoryItemId == itemId);

        if (branchId.HasValue)
        {
            query = query.Where(entity => entity.BranchId == branchId.Value);
        }

        var item = await query.SingleOrDefaultAsync(cancellationToken);
        if (item is null)
        {
            throw new KeyNotFoundException("Inventory item not found.");
        }

        return item;
    }

    private async Task EnsureUniqueNameAsync(
        Guid restaurantId,
        Guid branchId,
        Guid? itemId,
        string normalizedName,
        CancellationToken cancellationToken)
    {
        var candidateNames = await _context.InventoryItems
            .AsNoTracking()
            .Where(entity =>
                entity.RestaurantId == restaurantId &&
                entity.BranchId == branchId &&
                entity.InventoryItemId != itemId)
            .Select(entity => entity.NormalizedName)
            .ToListAsync(cancellationToken);

        if (candidateNames.Any(name => string.Equals(name.Trim(), normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Inventory item name already exists in this branch.");
        }
    }

    private async Task<Dictionary<Guid, decimal>> LoadCurrentStockMapAsync(
        Guid restaurantId,
        Guid branchId,
        IReadOnlyCollection<Guid> itemIds,
        CancellationToken cancellationToken)
    {
        if (itemIds.Count == 0)
        {
            return new Dictionary<Guid, decimal>();
        }

        var movements = await _context.InventoryMovements
            .AsNoTracking()
            .Where(entity =>
                entity.RestaurantId == restaurantId &&
                entity.BranchId == branchId &&
                itemIds.Contains(entity.InventoryItemId))
            .Select(entity => new
            {
                entity.InventoryItemId,
                entity.MovementType,
                entity.Quantity
            })
            .ToListAsync(cancellationToken);

        return movements
            .GroupBy(entity => entity.InventoryItemId)
            .ToDictionary(
                group => group.Key,
                group => RoundMoney(group.Sum(entity => GetMovementDelta(entity.MovementType, entity.Quantity))));
    }

    private async Task<decimal> GetCurrentStockAsync(Guid restaurantId, Guid branchId, Guid itemId, CancellationToken cancellationToken)
    {
        var stockMap = await LoadCurrentStockMapAsync(restaurantId, branchId, [itemId], cancellationToken);
        return stockMap.TryGetValue(itemId, out var stock) ? stock : 0m;
    }

    private async Task<decimal> GetCurrentStockAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var result = await _context.InventoryMovements
            .AsNoTracking()
            .Where(entity => entity.InventoryItemId == itemId)
            .Select(entity => new
            {
                entity.MovementType,
                entity.Quantity
            })
            .ToListAsync(cancellationToken);

        return RoundMoney(result.Sum(entity => GetMovementDelta(entity.MovementType, entity.Quantity)));
    }

    private async Task<MenuItem> LoadMenuItemAsync(Guid restaurantId, Guid menuItemId, CancellationToken cancellationToken)
    {
        var menuItem = await _context.MenuItems
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.RestaurantId == restaurantId && entity.MenuItemId == menuItemId, cancellationToken);

        if (menuItem is null)
        {
            throw new KeyNotFoundException("Menu item not found.");
        }

        return menuItem;
    }

    private async Task<MenuItemStockItemSnapshot?> LoadPreparedStockMappingAsync(Guid restaurantId, Guid branchId, Guid menuItemId, CancellationToken cancellationToken)
    {
        var mapping = await (
            from stockItem in _context.MenuItemStockItems.AsNoTracking()
            join inventory in _context.InventoryItems.AsNoTracking() on stockItem.InventoryItemId equals inventory.InventoryItemId
            where stockItem.RestaurantId == restaurantId &&
                  stockItem.BranchId == branchId &&
                  stockItem.MenuItemId == menuItemId &&
                  inventory.RestaurantId == restaurantId &&
                  inventory.BranchId == branchId
            select new MenuItemStockItemSnapshot(
                stockItem.MenuItemStockItemId,
                stockItem.InventoryItemId,
                inventory.Name,
                stockItem.CreatedAtUtc,
                stockItem.UpdatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

        return mapping;
    }

    private async Task<IReadOnlyCollection<RecipeIngredientSnapshot>> LoadRecipeIngredientSnapshotsAsync(
        Guid restaurantId,
        Guid branchId,
        IReadOnlyCollection<Guid> menuItemIds,
        CancellationToken cancellationToken)
    {
        if (menuItemIds.Count == 0)
        {
            return Array.Empty<RecipeIngredientSnapshot>();
        }

        return await (
            from recipe in _context.MenuItemRecipeIngredients.AsNoTracking()
            join inventory in _context.InventoryItems.AsNoTracking() on recipe.InventoryItemId equals inventory.InventoryItemId
            where recipe.RestaurantId == restaurantId &&
                  recipe.BranchId == branchId &&
                  menuItemIds.Contains(recipe.MenuItemId) &&
                  inventory.RestaurantId == restaurantId &&
                  inventory.BranchId == branchId
            select new RecipeIngredientSnapshot(
                recipe.MenuItemId,
                recipe.InventoryItemId,
                inventory.Name,
                recipe.QuantityRequired))
            .ToListAsync(cancellationToken);
    }

    private async Task<BatchProductionDetail> LoadBatchProductionDetailAsync(Guid restaurantId, Guid branchId, Guid batchProductionId, CancellationToken cancellationToken)
    {
        var batch = await _context.BatchProductions
            .AsNoTracking()
            .SingleAsync(entity => entity.RestaurantId == restaurantId && entity.BranchId == branchId && entity.BatchProductionId == batchProductionId, cancellationToken);

        var menuItem = await LoadMenuItemAsync(restaurantId, batch.MenuItemId, cancellationToken);
        var preparedStockItem = await _context.InventoryItems
            .AsNoTracking()
            .SingleAsync(entity => entity.InventoryItemId == batch.PreparedInventoryItemId && entity.RestaurantId == restaurantId && entity.BranchId == branchId, cancellationToken);

        var user = await _context.Users
            .AsNoTracking()
            .SingleAsync(entity => entity.UserId == batch.ProducedByUserId && entity.RestaurantId == restaurantId, cancellationToken);

        var consumptions = await _context.BatchProductionIngredientConsumptions
            .AsNoTracking()
            .Where(entity => entity.RestaurantId == restaurantId && entity.BranchId == branchId && entity.BatchProductionId == batchProductionId)
            .OrderBy(entity => entity.InventoryItemNameSnapshot)
            .Select(entity => new BatchProductionIngredientConsumptionItem(
                entity.BatchProductionIngredientConsumptionId,
                entity.InventoryItemId,
                entity.InventoryItemNameSnapshot,
                entity.QuantityConsumed,
                entity.InventoryMovementId,
                entity.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);

        return new BatchProductionDetail(
            batch.BatchProductionId,
            batch.RestaurantId,
            batch.BranchId,
            batch.MenuItemId,
            menuItem.Name,
            batch.PreparedInventoryItemId,
            preparedStockItem.Name,
            batch.QuantityProduced,
            batch.BusinessDate,
            batch.ProducedAtUtc,
            batch.ProducedByUserId,
            user.FullName,
            batch.Notes,
            batch.PreparedInventoryMovementId,
            batch.CreatedAtUtc,
            batch.UpdatedAtUtc,
            consumptions,
            batch.ShelfLifeHours,
            batch.ExpiresAtUtc,
            batch.StorageNote,
            batch.BatchReference);
    }

    private InventoryItemListItem ToItemListItem(InventoryItem item, IReadOnlyDictionary<Guid, decimal> stockMap)
    {
        var currentStock = stockMap.TryGetValue(item.InventoryItemId, out var stock) ? stock : 0m;
        return ToItemListItem(item, currentStock);
    }

    private static InventoryItemListItem ToItemListItem(InventoryItem item, decimal currentStock)
    {
        return new InventoryItemListItem(
            item.InventoryItemId,
            item.RestaurantId,
            item.BranchId,
            item.Name,
            item.NormalizedName,
            item.Category,
            item.UnitOfMeasure,
            item.LowStockThreshold,
            item.IsActive,
            currentStock,
            ResolveStatus(item.IsActive, currentStock, item.LowStockThreshold),
            item.CreatedAtUtc,
            item.UpdatedAtUtc);
    }

    private static InventoryAlertItem ToAlertItem(InventoryItemListItem item)
    {
        return new InventoryAlertItem(
            item.InventoryItemId,
            item.Name,
            item.Category,
            item.UnitOfMeasure,
            item.LowStockThreshold,
            item.CurrentStock,
            item.Status);
    }

    private static InventoryItemListItem ToItemSnapshot(InventoryItem item, IReadOnlyDictionary<Guid, decimal> stockMap)
    {
        var currentStock = stockMap.TryGetValue(item.InventoryItemId, out var stock) ? stock : 0m;
        return ToItemListItem(item, currentStock);
    }

    private static InventoryMovementItem ToMovementItem(
        InventoryMovement movement,
        decimal previousStock,
        decimal delta,
        decimal resultingStock,
        InventoryItem item,
        string recordedByUserName,
        string recordedByUserMobile)
    {
        return new InventoryMovementItem(
            movement.InventoryMovementId,
            movement.InventoryItemId,
            movement.RestaurantId,
            movement.BranchId,
            movement.MovementType.ToString(),
            movement.Quantity,
            movement.UnitCost,
            movement.ReferenceNumber,
            movement.Reason,
            movement.Notes,
            movement.MovementDate,
            movement.RecordedByUserId,
            recordedByUserName,
            recordedByUserMobile,
            movement.CreatedAtUtc,
            previousStock,
            delta,
            resultingStock,
            ResolveStatus(item.IsActive, resultingStock, item.LowStockThreshold),
            movement.ExpiresAtUtc,
            movement.BatchReference);
    }

    private static string ResolveStatus(bool isActive, decimal currentStock, decimal lowStockThreshold)
    {
        if (!isActive)
        {
            return "Inactive";
        }

        if (currentStock <= 0m)
        {
            return "Out of stock";
        }

        if (currentStock <= lowStockThreshold)
        {
            return "Low stock";
        }

        return "In stock";
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

    private static string BuildInsufficientBatchStockMessage(IEnumerable<BatchIngredientPlan> insufficientLines)
    {
        var messageParts = insufficientLines.Select(line =>
            $"{line.InventoryItemName} requires {line.QuantityRequired:0.###}.");

        return $"Insufficient stock for batch production: {string.Join(" ", messageParts)}";
    }

    private static InventoryMovementType ResolveAdjustmentMovementType(string? movementType)
    {
        if (string.IsNullOrWhiteSpace(movementType))
        {
            throw new InvalidOperationException("Movement type must be Increase or Decrease.");
        }

        return NormalizeRequiredText(movementType).ToLowerInvariant() switch
        {
            "increase" or "adjustmentincrease" => InventoryMovementType.AdjustmentIncrease,
            "decrease" or "adjustmentdecrease" => InventoryMovementType.AdjustmentDecrease,
            _ => throw new InvalidOperationException("Movement type must be Increase or Decrease.")
        };
    }

    private static string ResolveAdjustmentReason(string? reason)
    {
        var normalizedReason = NormalizeRequiredText(reason ?? string.Empty);

        if (string.IsNullOrWhiteSpace(normalizedReason))
        {
            throw new InvalidOperationException($"Reason must be one of: {string.Join(", ", AllowedAdjustmentReasons)}.");
        }

        var matchedReason = AllowedAdjustmentReasons.FirstOrDefault(value => string.Equals(value, normalizedReason, StringComparison.OrdinalIgnoreCase));
        if (matchedReason is null)
        {
            throw new InvalidOperationException($"Reason must be one of: {string.Join(", ", AllowedAdjustmentReasons)}.");
        }

        return matchedReason;
    }

    private static Guid RequireRestaurantScope(AuthUserContext currentUser)
    {
        if (currentUser.RestaurantId == Guid.Empty)
        {
            throw new InvalidOperationException("Authentication context is missing the restaurant scope.");
        }

        return currentUser.RestaurantId;
    }

    private static Guid RequireUserId(AuthUserContext currentUser)
    {
        if (currentUser.UserId == Guid.Empty)
        {
            throw new InvalidOperationException("Authentication context is missing the user scope.");
        }

        return currentUser.UserId;
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

    private static void ValidateRequest(CreateInventoryItemRequest request)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Request is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Category))
        {
            throw new InvalidOperationException("Category is required.");
        }

        if (string.IsNullOrWhiteSpace(request.UnitOfMeasure))
        {
            throw new InvalidOperationException("Unit of measure is required.");
        }

        if (request.LowStockThreshold < 0)
        {
            throw new InvalidOperationException("Low stock threshold must be greater than or equal to zero.");
        }
    }

    private static void ValidateRequest(UpdateInventoryItemRequest request)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Request is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Category))
        {
            throw new InvalidOperationException("Category is required.");
        }

        if (string.IsNullOrWhiteSpace(request.UnitOfMeasure))
        {
            throw new InvalidOperationException("Unit of measure is required.");
        }

        if (request.LowStockThreshold < 0)
        {
            throw new InvalidOperationException("Low stock threshold must be greater than or equal to zero.");
        }
    }

    private static void ValidateRequest(CreateInventoryMovementRequest request)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Request is required.");
        }

        if (string.IsNullOrWhiteSpace(request.MovementType))
        {
            throw new InvalidOperationException("Movement type is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new InvalidOperationException("Reason is required.");
        }
    }

    private static void ValidateRequest(CreateBatchProductionRequest request)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Request is required.");
        }

        if (request.MenuItemId == Guid.Empty)
        {
            throw new InvalidOperationException("Menu item is required.");
        }
    }

    private static void ValidateRequest(RecordPreparedStockWastageRequest request)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Request is required.");
        }

        if (request.MenuItemId == Guid.Empty)
        {
            throw new InvalidOperationException("Menu item is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new InvalidOperationException("Reason is required.");
        }
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

        return new RestaurantSnapshot(restaurant.RestaurantId, restaurant.Name);
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

    private void AddAudit(
        AuthUserContext actor,
        RestaurantSnapshot restaurant,
        Branch branch,
        string action,
        string reason,
        string entityType,
        string entityId,
        string? oldValueJson,
        string? newValueJson,
        DateTimeOffset createdAt)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            RestaurantId = restaurant.RestaurantId,
            BranchId = branch.BranchId,
            UserId = actor.UserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Reason = reason,
            OldValueJson = oldValueJson,
            NewValueJson = newValueJson,
            RestaurantNameSnapshot = restaurant.Name,
            BranchNameSnapshot = branch.Name,
            UserNameSnapshot = actor.FullName,
            UserMobileSnapshot = actor.MobileNumber,
            CreatedAt = createdAt
        });
    }

    private static string Serialize(object? value)
    {
        return value is null ? string.Empty : JsonSerializer.Serialize(value);
    }

    private static string NormalizeRequiredText(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string? NormalizeOptionalText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length > maxLength ? normalized[..maxLength] : normalized;
    }

    private static string BuildAllowedValuesMessage<TEnum>(string label)
        where TEnum : struct, Enum
    {
        return $"{label} must be one of: {string.Join(", ", Enum.GetNames<TEnum>())}.";
    }

    private static decimal RoundMoney(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static InventoryLot CreateInventoryLot(
        Guid restaurantId,
        Guid branchId,
        Guid inventoryItemId,
        Guid sourceMovementId,
        Guid? sourceBatchProductionId,
        string? batchReference,
        DateTimeOffset receivedAtUtc,
        DateTimeOffset? expiresAtUtc,
        decimal quantity,
        string unitOfMeasure,
        DateTimeOffset createdAtUtc)
    {
        if (quantity <= 0m)
        {
            throw new InvalidOperationException("Lot quantity must be greater than zero.");
        }

        return new InventoryLot
        {
            RestaurantId = restaurantId,
            BranchId = branchId,
            InventoryItemId = inventoryItemId,
            SourceMovementId = sourceMovementId,
            SourceBatchProductionId = sourceBatchProductionId,
            BatchReference = batchReference,
            ReceivedAtUtc = receivedAtUtc,
            ExpiresAtUtc = expiresAtUtc,
            InitialQuantity = quantity,
            RemainingQuantity = quantity,
            UnitOfMeasure = NormalizeRequiredText(unitOfMeasure),
            CreatedAtUtc = createdAtUtc
        };
    }

    private sealed record MenuItemStockItemSnapshot(
        Guid MenuItemStockItemId,
        Guid InventoryItemId,
        string InventoryItemName,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? UpdatedAtUtc);

    private sealed record RecipeIngredientSnapshot(
        Guid MenuItemId,
        Guid InventoryItemId,
        string InventoryItemName,
        decimal QuantityRequired);

    private sealed record BatchIngredientPlan(
        Guid InventoryItemId,
        string InventoryItemName,
        decimal QuantityRequired);

    private static DateTimeOffset? ResolveExpiresAt(
        DateTimeOffset producedAtUtc,
        decimal? shelfLifeHours,
        DateTimeOffset? explicitExpiresAt)
    {
        if (explicitExpiresAt.HasValue)
        {
            return explicitExpiresAt.Value;
        }

        if (shelfLifeHours.HasValue)
        {
            return producedAtUtc.AddHours((double)shelfLifeHours.Value);
        }

        return null;
    }

    private static void ValidateBatchExpiry(
        DateTimeOffset producedAtUtc,
        decimal? shelfLifeHours,
        DateTimeOffset? explicitExpiresAt,
        DateTimeOffset? resolvedExpiresAt)
    {
        if (shelfLifeHours.HasValue && shelfLifeHours.Value <= 0m)
        {
            throw new InvalidOperationException("Shelf life hours must be greater than zero.");
        }

        if (resolvedExpiresAt.HasValue && resolvedExpiresAt.Value <= producedAtUtc)
        {
            throw new InvalidOperationException("Expiry date must be after the production time.");
        }
    }

    private static void ValidateExpiryForMovement(
        DateTimeOffset? expiresAt,
        DateTimeOffset movementDate,
        InventoryMovementType movementType)
    {
        if (!expiresAt.HasValue)
        {
            return;
        }

        if (movementType != InventoryMovementType.StockIn && movementType != InventoryMovementType.AdjustmentIncrease)
        {
            throw new InvalidOperationException("Expiry date can only be set on stock-in or adjustment-increase movements.");
        }

        if (expiresAt.Value <= movementDate)
        {
            throw new InvalidOperationException("Expiry date must be after the movement date.");
        }
    }

    private sealed record RestaurantSnapshot(Guid RestaurantId, string Name);
}
