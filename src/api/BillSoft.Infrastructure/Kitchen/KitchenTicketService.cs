using System.Data;
using System.Text.Json;
using BillSoft.Application.Auth;
using BillSoft.Application.Kitchen;
using BillSoft.Domain.Auditing;
using BillSoft.Domain.Inventory;
using BillSoft.Domain.Kitchen;
using BillSoft.Domain.Menu;
using BillSoft.Domain.Orders;
using BillSoft.Domain.Restaurants;
using BillSoft.Infrastructure.Inventory;
using BillSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillSoft.Infrastructure.Kitchen;

public sealed class KitchenTicketService : IKitchenTicketService
{
    private readonly BillSoftDbContext _context;
    private readonly InventoryLotAllocationService _lotAllocationService;

    public KitchenTicketService(BillSoftDbContext context, InventoryLotAllocationService lotAllocationService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _lotAllocationService = lotAllocationService ?? throw new ArgumentNullException(nameof(lotAllocationService));
    }

    public async Task<KitchenTicketListResponse> ListAsync(AuthUserContext currentUser, KitchenTicketListQuery query, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var status = ResolveStatusFilter(query.Status);

        var ticketsQuery = _context.KitchenTickets
            .AsNoTracking()
            .Where(ticket => ticket.RestaurantId == restaurantId);

        if (query.BranchId.HasValue)
        {
            _ = await LoadBranchAsync(restaurantId, query.BranchId.Value, cancellationToken);
            ticketsQuery = ticketsQuery.Where(ticket => ticket.BranchId == query.BranchId.Value);
        }

        if (status.HasValue)
        {
            ticketsQuery = ticketsQuery.Where(ticket => ticket.Status == status.Value);
        }

        if (query.From.HasValue)
        {
            ticketsQuery = ticketsQuery.Where(ticket => ticket.CreatedAt >= ResolveStartOfDay(query.From.Value));
        }

        if (query.To.HasValue)
        {
            ticketsQuery = ticketsQuery.Where(ticket => ticket.CreatedAt < ResolveExclusiveEndOfDay(query.To.Value));
        }

        var items = await ticketsQuery
            .Select(ticket => new KitchenTicketListItem(
                ticket.KitchenTicketId,
                ticket.BranchId,
                ticket.PosOrderId,
                ticket.TicketNumber,
                ticket.OrderNumberSnapshot,
                ticket.OrderTypeSnapshot,
                ticket.TableNameSnapshot,
                ticket.CustomerNameSnapshot,
                ticket.OrderNotesSnapshot,
                ticket.Status.ToString(),
                ticket.KitchenTicketLines.Count,
                ticket.CreatedAt,
                ticket.UpdatedAt,
                ticket.CancelledAt,
                ticket.CancelReason))
            .ToListAsync(cancellationToken);

        return new KitchenTicketListResponse(items
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.TicketNumber)
            .ToArray());
    }

    public async Task<KitchenTicketDetail> GetAsync(AuthUserContext currentUser, Guid ticketId, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var ticket = await LoadTicketAsync(restaurantId, ticketId, cancellationToken);
        return KitchenTicketWorkflow.ToDetail(ticket);
    }

    public async Task<KitchenTicketDetail> CreateAsync(AuthUserContext currentUser, CreateKitchenTicketRequest request, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        ValidateRequest(request);

        var now = DateTimeOffset.UtcNow;

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            try
            {
                var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
                var order = await LoadConfirmedOrderAsync(restaurantId, request.PosOrderId, cancellationToken);
                var branch = await LoadBranchAsync(restaurantId, order.BranchId, cancellationToken);
                var ticket = await KitchenTicketWorkflow.CreateAsync(_context, currentUser, order, now, cancellationToken);
                var detail = KitchenTicketWorkflow.ToDetail(ticket);
                AddAudit(
                    actor: currentUser,
                    restaurant: restaurant,
                    branch: branch,
                    action: "KitchenTicket.Created",
                    reason: "Kitchen ticket created from confirmed POS order.",
                    entityId: ticket.KitchenTicketId.ToString(),
                    oldValueJson: null,
                    newValueJson: Serialize(detail),
                    createdAt: now);

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return detail;
            }
            catch (DbUpdateException ex) when (attempt == 0 && IsUniqueConstraintViolation(ex))
            {
                await transaction.RollbackAsync(cancellationToken);
                _context.ChangeTracker.Clear();
            }
        }

        throw new InvalidOperationException("Unable to allocate a kitchen ticket number safely. Please retry.");
    }

    public async Task<KitchenTicketDetail> UpdateStatusAsync(AuthUserContext currentUser, Guid ticketId, UpdateKitchenTicketStatusRequest request, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        ValidateRequest(request);
        var targetStatus = ResolveTransitionStatus(request.Status);

        var ticket = await LoadTrackedTicketAsync(restaurantId, ticketId, cancellationToken);
        if (ticket.Status == KitchenTicketStatus.Served && targetStatus == KitchenTicketStatus.Served)
        {
            return KitchenTicketWorkflow.ToDetail(ticket);
        }

        if (!CanTransition(ticket.Status, targetStatus))
        {
            throw new InvalidOperationException("Kitchen ticket status transition is not allowed.");
        }

        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var branch = await LoadBranchAsync(restaurantId, ticket.BranchId, cancellationToken);
        var before = KitchenTicketWorkflow.ToDetail(ticket);
        var now = DateTimeOffset.UtcNow;

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        if (targetStatus == KitchenTicketStatus.Served)
        {
            var deductionPlan = await BuildDeductionPlanAsync(ticket, cancellationToken);
            var missingConfigurationLines = deductionPlan
                .Where(line => line.Status == "MissingMapping")
                .ToArray();
            var insufficientLines = deductionPlan
                .Where(line => line.Status == "Insufficient")
                .ToArray();

            if (missingConfigurationLines.Length > 0)
            {
                throw new InvalidOperationException(BuildMissingDeductionConfigurationMessage(missingConfigurationLines));
            }

            if (insufficientLines.Length > 0)
            {
                throw new InvalidOperationException(BuildInsufficientStockMessage(insufficientLines));
            }

            ticket.InventoryDeductionStatus = ResolveCompletionDeductionStatus(deductionPlan);
            await ApplyInventoryDeductionsAsync(currentUser, restaurant, branch, ticket, deductionPlan, now, cancellationToken);
        }

        ApplyTransition(ticket, targetStatus, currentUser.UserId, now);

        var after = KitchenTicketWorkflow.ToDetail(ticket);

        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: branch,
            action: "KitchenTicket.StatusChanged",
            reason: $"Kitchen ticket status changed to {targetStatus}.",
            entityId: ticket.KitchenTicketId.ToString(),
            oldValueJson: Serialize(before),
            newValueJson: Serialize(after),
            createdAt: now);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return after;
    }

    public async Task<KitchenTicketDetail> CancelAsync(AuthUserContext currentUser, Guid ticketId, CancelKitchenTicketRequest request, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var reason = NormalizeRequiredText(request?.Reason, "Cancel reason is required.");
        var ticket = await LoadTrackedTicketAsync(restaurantId, ticketId, cancellationToken);

        if (ticket.Status == KitchenTicketStatus.Cancelled)
        {
            throw new InvalidOperationException("Kitchen ticket is already cancelled.");
        }

        if (ticket.Status == KitchenTicketStatus.Served)
        {
            throw new InvalidOperationException("Kitchen ticket cannot be cancelled in its current status.");
        }

        if (ticket.Status != KitchenTicketStatus.Pending &&
            ticket.Status != KitchenTicketStatus.Preparing &&
            ticket.Status != KitchenTicketStatus.Ready)
        {
            throw new InvalidOperationException("Kitchen ticket cannot be cancelled in its current status.");
        }

        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var branch = await LoadBranchAsync(restaurantId, ticket.BranchId, cancellationToken);
        var before = KitchenTicketWorkflow.ToDetail(ticket);
        var now = DateTimeOffset.UtcNow;

        ticket.Status = KitchenTicketStatus.Cancelled;
        ticket.CancelledAt = now;
        ticket.CancelledByUserId = currentUser.UserId;
        ticket.CancelReason = reason;
        ticket.LastStatusChangedByUserId = currentUser.UserId;
        ticket.UpdatedAt = now;

        var after = KitchenTicketWorkflow.ToDetail(ticket);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: branch,
            action: "KitchenTicket.Cancelled",
            reason: reason,
            entityId: ticket.KitchenTicketId.ToString(),
            oldValueJson: Serialize(before),
            newValueJson: Serialize(after),
            createdAt: now);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return after;
    }

    public async Task<KitchenTicketDeductionPreviewResponse> GetDeductionPreviewAsync(AuthUserContext currentUser, Guid ticketId, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var ticket = await LoadTicketAsync(restaurantId, ticketId, cancellationToken);
        _ = await LoadBranchAsync(restaurantId, ticket.BranchId, cancellationToken);

        var lines = await BuildDeductionPlanAsync(ticket, cancellationToken);
        return new KitchenTicketDeductionPreviewResponse(
            ticket.KitchenTicketId,
            lines.All(line => line.Status is "Sufficient" or "NoDeduction" or "NoRecipe"),
            lines
                .Select(line => new KitchenTicketDeductionPreviewLine(
                    line.MenuItemName,
                    line.InventoryItemName,
                    line.RequiredQuantity,
                    line.AvailableQuantity,
                    line.ResultingQuantity,
                    line.Status))
                .ToArray());
    }

    private async Task<KitchenTicket> LoadTicketAsync(Guid restaurantId, Guid ticketId, CancellationToken cancellationToken)
    {
        var ticket = await _context.KitchenTickets
            .AsNoTracking()
            .Include(entity => entity.KitchenTicketLines)
            .SingleOrDefaultAsync(entity => entity.KitchenTicketId == ticketId && entity.RestaurantId == restaurantId, cancellationToken);

        if (ticket is null)
        {
            throw new KeyNotFoundException("Kitchen ticket not found.");
        }

        return ticket;
    }

    private async Task<KitchenTicket> LoadTrackedTicketAsync(Guid restaurantId, Guid ticketId, CancellationToken cancellationToken)
    {
        var ticket = await _context.KitchenTickets
            .Include(entity => entity.KitchenTicketLines)
            .SingleOrDefaultAsync(entity => entity.KitchenTicketId == ticketId && entity.RestaurantId == restaurantId, cancellationToken);

        if (ticket is null)
        {
            throw new KeyNotFoundException("Kitchen ticket not found.");
        }

        return ticket;
    }

    private async Task<PosOrder> LoadConfirmedOrderAsync(Guid restaurantId, Guid posOrderId, CancellationToken cancellationToken)
    {
        var order = await _context.PosOrders
            .AsNoTracking()
            .Include(entity => entity.PosOrderLines.OrderBy(line => line.DisplayOrder))
            .SingleOrDefaultAsync(entity => entity.PosOrderId == posOrderId && entity.RestaurantId == restaurantId, cancellationToken);

        if (order is null)
        {
            throw new KeyNotFoundException("POS order not found.");
        }

        if (order.Status == PosOrderStatus.Draft)
        {
            throw new InvalidOperationException("Draft orders cannot generate kitchen tickets.");
        }

        if (order.Status == PosOrderStatus.Cancelled)
        {
            throw new InvalidOperationException("Cancelled orders cannot generate kitchen tickets.");
        }

        if (order.Status != PosOrderStatus.Confirmed)
        {
            throw new InvalidOperationException("Only confirmed POS orders can generate kitchen tickets.");
        }

        return order;
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

    private static void ApplyTransition(KitchenTicket ticket, KitchenTicketStatus targetStatus, Guid? userId, DateTimeOffset now)
    {
        ticket.Status = targetStatus;
        ticket.LastStatusChangedByUserId = userId;
        ticket.UpdatedAt = now;

        if (targetStatus == KitchenTicketStatus.Preparing && ticket.PreparingAt is null)
        {
            ticket.PreparingAt = now;
        }

        if (targetStatus == KitchenTicketStatus.Ready && ticket.ReadyAt is null)
        {
            ticket.ReadyAt = now;
        }

        if (targetStatus == KitchenTicketStatus.Served && ticket.ServedAt is null)
        {
            ticket.ServedAt = now;
        }
    }

    private static bool CanTransition(KitchenTicketStatus currentStatus, KitchenTicketStatus targetStatus)
    {
        if (currentStatus is KitchenTicketStatus.Cancelled or KitchenTicketStatus.Served)
        {
            return false;
        }

        return currentStatus switch
        {
            KitchenTicketStatus.Pending => targetStatus is KitchenTicketStatus.Preparing or KitchenTicketStatus.Ready,
            KitchenTicketStatus.Preparing => targetStatus == KitchenTicketStatus.Ready,
            KitchenTicketStatus.Ready => targetStatus == KitchenTicketStatus.Served,
            _ => false
        };
    }

    private static Guid RequireRestaurantScope(AuthUserContext currentUser)
    {
        if (currentUser.RestaurantId == Guid.Empty)
        {
            throw new InvalidOperationException("Authentication context is missing the restaurant scope.");
        }

        return currentUser.RestaurantId;
    }

    private static void ValidateRequest(CreateKitchenTicketRequest request)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Request is required.");
        }

        if (request.PosOrderId == Guid.Empty)
        {
            throw new InvalidOperationException("POS order is required.");
        }
    }

    private static void ValidateRequest(UpdateKitchenTicketStatusRequest request)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Request is required.");
        }
    }

    private static KitchenTicketStatus? ResolveStatusFilter(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        if (!Enum.TryParse<KitchenTicketStatus>(status, true, out var parsed))
        {
            throw new InvalidOperationException(BuildAllowedValuesMessage<KitchenTicketStatus>("Status filter"));
        }

        return parsed;
    }

    private static KitchenTicketStatus ResolveTransitionStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new InvalidOperationException(BuildAllowedValuesMessage<KitchenTicketStatus>("Status"));
        }

        if (!Enum.TryParse<KitchenTicketStatus>(status, true, out var parsed))
        {
            throw new InvalidOperationException(BuildAllowedValuesMessage<KitchenTicketStatus>("Status"));
        }

        return parsed;
    }

    private static string BuildAllowedValuesMessage<TEnum>(string label)
        where TEnum : struct, Enum
    {
        return $"{label} must be one of: {string.Join(", ", Enum.GetNames<TEnum>())}.";
    }

    private static DateTimeOffset ResolveStartOfDay(DateTime date)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(date.Date, DateTimeKind.Utc));
    }

    private static DateTimeOffset ResolveExclusiveEndOfDay(DateTime date)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(date.Date, DateTimeKind.Utc)).AddDays(1);
    }

    private static string NormalizeRequiredText(string? value, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(errorMessage);
        }

        return value.Trim();
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        var baseException = exception.GetBaseException();
        var exceptionType = baseException.GetType().FullName ?? baseException.GetType().Name;

        if (string.Equals(exceptionType, "Microsoft.Data.SqlClient.SqlException", StringComparison.Ordinal))
        {
            var numberProperty = baseException.GetType().GetProperty("Number");
            return numberProperty?.GetValue(baseException) is int number && (number == 2601 || number == 2627);
        }

        if (string.Equals(exceptionType, "Microsoft.Data.Sqlite.SqliteException", StringComparison.Ordinal))
        {
            var codeProperty = baseException.GetType().GetProperty("SqliteErrorCode");
            return codeProperty?.GetValue(baseException) is int code && code == 19;
        }

        return baseException.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
            || baseException.Message.Contains("Cannot insert duplicate key row", StringComparison.OrdinalIgnoreCase)
            || baseException.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyCollection<DeductionPlanLine>> BuildDeductionPlanAsync(KitchenTicket ticket, CancellationToken cancellationToken)
    {
        var ticketLines = ticket.KitchenTicketLines
            .OrderBy(line => line.DisplayOrder)
            .ToArray();

        if (ticketLines.Length == 0)
        {
            return Array.Empty<DeductionPlanLine>();
        }

        var ticketLineQuantities = ticketLines
            .GroupBy(line => new { line.MenuItemId, line.MenuItemNameSnapshot })
            .Select(group => new TicketLineQuantity(
                group.Key.MenuItemId,
                group.Key.MenuItemNameSnapshot,
                RoundMoney(group.Sum(line => line.Quantity))))
            .ToArray();

        var menuItemIds = ticketLineQuantities.Select(line => line.MenuItemId).ToArray();
        var menuItems = await LoadMenuItemSnapshotsAsync(ticket.RestaurantId, menuItemIds, cancellationToken);
        var recipeIngredients = await LoadRecipeIngredientSnapshotsAsync(ticket.RestaurantId, ticket.BranchId, menuItemIds, cancellationToken);
        var recipeIngredientsByMenuItem = recipeIngredients
            .GroupBy(entry => entry.MenuItemId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var preparedStockMappings = await LoadPreparedStockMappingsAsync(ticket.RestaurantId, ticket.BranchId, menuItemIds, cancellationToken);

        var stockItemIds = recipeIngredients.Select(entry => entry.InventoryItemId)
            .Concat(preparedStockMappings.Values.Select(entry => entry.InventoryItemId))
            .Distinct()
            .ToArray();

        var stockMap = await LoadCurrentStockMapAsync(ticket.RestaurantId, ticket.BranchId, stockItemIds, cancellationToken);
        var planLines = new List<DeductionPlanLine>();

        foreach (var line in ticketLineQuantities)
        {
            if (!menuItems.TryGetValue(line.MenuItemId, out var menuItem))
            {
                throw new KeyNotFoundException("Menu item not found.");
            }

            switch (menuItem.InventoryDeductionMode)
            {
                case MenuItemInventoryDeductionMode.RecipeOnServe:
                {
                    if (!recipeIngredientsByMenuItem.TryGetValue(line.MenuItemId, out var itemRecipeIngredients) ||
                        itemRecipeIngredients.Length == 0)
                    {
                        planLines.Add(new DeductionPlanLine(
                            line.MenuItemId,
                            line.MenuItemNameSnapshot,
                            menuItem.InventoryDeductionMode,
                            null,
                            null,
                            0m,
                            0m,
                            0m,
                            "NoRecipe"));
                        break;
                    }

                    foreach (var ingredientGroup in itemRecipeIngredients.GroupBy(entry => new { entry.InventoryItemId, entry.InventoryItemName }))
                    {
                        var requiredQuantity = RoundMoney(line.Quantity * ingredientGroup.Sum(entry => entry.QuantityRequired));
                        var availableQuantity = stockMap.TryGetValue(ingredientGroup.Key.InventoryItemId, out var stock) ? stock : 0m;
                        var resultingQuantity = RoundMoney(availableQuantity - requiredQuantity);

                        planLines.Add(new DeductionPlanLine(
                            line.MenuItemId,
                            line.MenuItemNameSnapshot,
                            menuItem.InventoryDeductionMode,
                            ingredientGroup.Key.InventoryItemId,
                            ingredientGroup.Key.InventoryItemName,
                            requiredQuantity,
                            availableQuantity,
                            resultingQuantity,
                            resultingQuantity < 0m ? "Insufficient" : "Sufficient"));
                    }

                    break;
                }
                case MenuItemInventoryDeductionMode.BatchPrepared:
                case MenuItemInventoryDeductionMode.DirectStockItem:
                {
                    if (!preparedStockMappings.TryGetValue(line.MenuItemId, out var preparedMapping))
                    {
                        planLines.Add(new DeductionPlanLine(
                            line.MenuItemId,
                            line.MenuItemNameSnapshot,
                            menuItem.InventoryDeductionMode,
                            null,
                            null,
                            line.Quantity,
                            0m,
                            0m,
                            "MissingMapping"));
                        break;
                    }

                    var availableQuantity = stockMap.TryGetValue(preparedMapping.InventoryItemId, out var preparedStock) ? preparedStock : 0m;
                    var resultingQuantity = RoundMoney(availableQuantity - line.Quantity);

                    planLines.Add(new DeductionPlanLine(
                        line.MenuItemId,
                        line.MenuItemNameSnapshot,
                        menuItem.InventoryDeductionMode,
                        preparedMapping.InventoryItemId,
                        preparedMapping.InventoryItemName,
                        line.Quantity,
                        availableQuantity,
                        resultingQuantity,
                        resultingQuantity < 0m ? "Insufficient" : "Sufficient"));
                    break;
                }
                case MenuItemInventoryDeductionMode.NoDeduction:
                    planLines.Add(new DeductionPlanLine(
                        line.MenuItemId,
                        line.MenuItemNameSnapshot,
                        menuItem.InventoryDeductionMode,
                        null,
                        null,
                        0m,
                        0m,
                        0m,
                        "NoDeduction"));
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported inventory deduction mode '{menuItem.InventoryDeductionMode}'.");
            }
        }

        return planLines
            .OrderBy(line => line.InventoryItemName ?? line.MenuItemName)
            .ToArray();
    }

    private async Task ApplyInventoryDeductionsAsync(
        AuthUserContext currentUser,
        RestaurantSnapshot restaurant,
        Branch branch,
        KitchenTicket ticket,
        IReadOnlyCollection<DeductionPlanLine> deductionPlan,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var deductableLines = deductionPlan
            .Where(line => line.InventoryItemId.HasValue && line.Status == "Sufficient")
            .ToArray();

        if (deductableLines.Length == 0)
        {
            return;
        }

        var existingDeductionItemIds = await _context.KitchenTicketInventoryDeductions
            .AsNoTracking()
            .Where(entity =>
                entity.RestaurantId == ticket.RestaurantId &&
                entity.BranchId == ticket.BranchId &&
                entity.KitchenTicketId == ticket.KitchenTicketId)
            .Select(entity => entity.InventoryItemId)
            .ToListAsync(cancellationToken);
        var existingDeductionItemIdSet = existingDeductionItemIds.ToHashSet();

        var inventoryItemIds = deductableLines.Select(line => line.InventoryItemId!.Value).Distinct().ToArray();
        var inventoryItems = await _context.InventoryItems
            .AsNoTracking()
            .Where(entity =>
                entity.RestaurantId == ticket.RestaurantId &&
                entity.BranchId == ticket.BranchId &&
                inventoryItemIds.Contains(entity.InventoryItemId))
            .ToDictionaryAsync(entity => entity.InventoryItemId, cancellationToken);

        foreach (var group in deductableLines.GroupBy(line => line.InventoryItemId!.Value))
        {
            if (existingDeductionItemIdSet.Contains(group.Key))
            {
                continue;
            }

            if (!inventoryItems.ContainsKey(group.Key))
            {
                throw new KeyNotFoundException("Inventory item not found.");
            }

            var quantity = RoundMoney(group.Sum(line => line.RequiredQuantity));
            var menuItemNames = string.Join(", ", group.Select(line => line.MenuItemName).Distinct());
            var movement = new InventoryMovement
            {
                RestaurantId = ticket.RestaurantId,
                BranchId = ticket.BranchId,
                InventoryItemId = group.Key,
                MovementType = InventoryMovementType.Consumption,
                Quantity = quantity,
                ReferenceNumber = ticket.TicketNumber,
                Reason = "Kitchen ticket completion consumption",
                Notes = $"Kitchen ticket {ticket.TicketNumber} completion consumption for {menuItemNames}.",
                MovementDate = now,
                RecordedByUserId = currentUser.UserId,
                CreatedAtUtc = now
            };

            _context.InventoryMovements.Add(movement);
            _context.KitchenTicketInventoryDeductions.Add(new KitchenTicketInventoryDeduction
            {
                RestaurantId = ticket.RestaurantId,
                BranchId = ticket.BranchId,
                KitchenTicketId = ticket.KitchenTicketId,
                InventoryItemId = group.Key,
                InventoryMovementId = movement.InventoryMovementId,
                QuantityDeducted = quantity,
                CreatedAtUtc = now
            });

            var currentStockBeforeMovement = group.First().AvailableQuantity;
            await _lotAllocationService.AllocateAsync(
                ticket.RestaurantId,
                ticket.BranchId,
                group.Key,
                movement.InventoryMovementId,
                quantity,
                "Kitchen ticket served deduction",
                allowExpiredLots: false,
                currentStockBeforeMovement: currentStockBeforeMovement,
                createdAtUtc: now,
                cancellationToken: cancellationToken);

            AddAudit(
                actor: currentUser,
                restaurant: restaurant,
                branch: branch,
                action: "InventoryMovement.Recorded",
                reason: "Kitchen ticket completed.",
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
                    menuItemNames
                }),
                createdAt: now);
        }

        await _context.SaveChangesAsync(cancellationToken);
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

    private static string BuildInsufficientStockMessage(IEnumerable<DeductionPlanLine> insufficientLines)
    {
        var messageParts = insufficientLines.Select(line =>
            $"{line.InventoryItemName ?? "Inventory item"} requires {line.RequiredQuantity:0.###}, available {line.AvailableQuantity:0.###}.");

        return $"Insufficient stock for kitchen ticket completion: {string.Join(" ", messageParts)}";
    }

    private static string BuildMissingDeductionConfigurationMessage(IEnumerable<DeductionPlanLine> missingLines)
    {
        var messages = missingLines
            .GroupBy(line => line.Status)
            .Select(group =>
            {
                var names = string.Join(", ", group.Select(line => line.MenuItemName).Distinct());
                return group.Key switch
                {
                    "NoRecipe" => $"Recipe is missing for {names}.",
                    "MissingMapping" => $"Prepared stock mapping is missing for {names}.",
                    _ => $"{names} has an unresolved inventory deduction configuration."
                };
            });

        return $"Inventory deduction configuration is missing: {string.Join(" ", messages)}";
    }

    private static decimal RoundMoney(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static KitchenTicketInventoryDeductionStatus ResolveCompletionDeductionStatus(IReadOnlyCollection<DeductionPlanLine> deductionPlan)
    {
        if (deductionPlan.Any(line => line.Status is "Insufficient" or "NoRecipe" or "MissingMapping"))
        {
            return KitchenTicketInventoryDeductionStatus.DeductionWarning;
        }

        return KitchenTicketInventoryDeductionStatus.Deducted;
    }

    private void AddAudit(
        AuthUserContext actor,
        RestaurantSnapshot restaurant,
        Branch branch,
        string action,
        string reason,
        string entityId,
        string? oldValueJson,
        string? newValueJson,
        DateTimeOffset createdAt)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            RestaurantId = restaurant.RestaurantId,
            BranchId = branch.BranchId,
            UserId = actor.UserId == Guid.Empty ? null : actor.UserId,
            Action = action,
            EntityType = "KitchenTicket",
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

    private async Task<IReadOnlyDictionary<Guid, MenuItemSnapshot>> LoadMenuItemSnapshotsAsync(
        Guid restaurantId,
        IReadOnlyCollection<Guid> menuItemIds,
        CancellationToken cancellationToken)
    {
        if (menuItemIds.Count == 0)
        {
            return new Dictionary<Guid, MenuItemSnapshot>();
        }

        return await _context.MenuItems
            .AsNoTracking()
            .Where(entity => entity.RestaurantId == restaurantId && menuItemIds.Contains(entity.MenuItemId))
            .Select(entity => new MenuItemSnapshot(entity.MenuItemId, entity.Name, entity.InventoryDeductionMode))
            .ToDictionaryAsync(entity => entity.MenuItemId, cancellationToken);
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

    private async Task<IReadOnlyDictionary<Guid, MenuItemStockItemSnapshot>> LoadPreparedStockMappingsAsync(
        Guid restaurantId,
        Guid branchId,
        IReadOnlyCollection<Guid> menuItemIds,
        CancellationToken cancellationToken)
    {
        if (menuItemIds.Count == 0)
        {
            return new Dictionary<Guid, MenuItemStockItemSnapshot>();
        }

        return await (
            from stockItem in _context.MenuItemStockItems.AsNoTracking()
            join inventory in _context.InventoryItems.AsNoTracking() on stockItem.InventoryItemId equals inventory.InventoryItemId
            where stockItem.RestaurantId == restaurantId &&
                  stockItem.BranchId == branchId &&
                  menuItemIds.Contains(stockItem.MenuItemId) &&
                  inventory.RestaurantId == restaurantId &&
                  inventory.BranchId == branchId
            select new
            {
                stockItem.MenuItemId,
                Snapshot = new MenuItemStockItemSnapshot(
                    stockItem.MenuItemStockItemId,
                    stockItem.InventoryItemId,
                    inventory.Name,
                    stockItem.CreatedAtUtc,
                    stockItem.UpdatedAtUtc)
            })
            .ToDictionaryAsync(entry => entry.MenuItemId, entry => entry.Snapshot, cancellationToken);
    }

    private sealed record DeductionPlanLine(
        Guid? MenuItemId,
        string MenuItemName,
        MenuItemInventoryDeductionMode InventoryDeductionMode,
        Guid? InventoryItemId,
        string? InventoryItemName,
        decimal RequiredQuantity,
        decimal AvailableQuantity,
        decimal ResultingQuantity,
        string Status);

    private sealed record TicketLineQuantity(Guid MenuItemId, string MenuItemNameSnapshot, decimal Quantity);

    private sealed record MenuItemSnapshot(Guid MenuItemId, string Name, MenuItemInventoryDeductionMode InventoryDeductionMode);

    private sealed record RecipeIngredientSnapshot(
        Guid MenuItemId,
        Guid InventoryItemId,
        string InventoryItemName,
        decimal QuantityRequired);

    private sealed record MenuItemStockItemSnapshot(
        Guid MenuItemStockItemId,
        Guid InventoryItemId,
        string InventoryItemName,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? UpdatedAtUtc);

    private sealed record RestaurantSnapshot(Guid RestaurantId, string Name);
}
