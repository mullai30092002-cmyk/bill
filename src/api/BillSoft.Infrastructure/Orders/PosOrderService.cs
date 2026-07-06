using System.Text.Json;
using System.Data;
using BillSoft.Application.Auth;
using BillSoft.Application.Orders;
using BillSoft.Domain.Auditing;
using BillSoft.Domain.Kitchen;
using BillSoft.Domain.Menu;
using BillSoft.Domain.Orders;
using BillSoft.Domain.Restaurants;
using BillSoft.Infrastructure.Persistence;
using BillSoft.Infrastructure.Kitchen;
using Microsoft.EntityFrameworkCore;

namespace BillSoft.Infrastructure.Orders;

public sealed class PosOrderService : IPosOrderService
{
    private readonly BillSoftDbContext _context;

    public PosOrderService(BillSoftDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<PosOrderListResponse> ListAsync(AuthUserContext currentUser, PosOrderListQuery query, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var status = ResolveStatus(query.Status);
        var orderType = ResolveOrderType(query.OrderType);
        var search = NormalizeSearch(query.Search);

        var ordersQuery = _context.PosOrders
            .AsNoTracking()
            .Where(order => order.RestaurantId == restaurantId);

        if (query.BranchId.HasValue)
        {
            _ = await LoadBranchAsync(restaurantId, query.BranchId.Value, requireActive: false, cancellationToken);
            ordersQuery = ordersQuery.Where(order => order.BranchId == query.BranchId.Value);
        }

        if (status.HasValue)
        {
            ordersQuery = ordersQuery.Where(order => order.Status == status.Value);
        }

        if (orderType.HasValue)
        {
            ordersQuery = ordersQuery.Where(order => order.OrderType == orderType.Value);
        }

        if (query.From.HasValue)
        {
            var start = ResolveStartOfDay(query.From.Value);
            ordersQuery = ordersQuery.Where(order => order.CreatedAt >= start);
        }

        if (query.To.HasValue)
        {
            var endExclusive = ResolveExclusiveEndOfDay(query.To.Value);
            ordersQuery = ordersQuery.Where(order => order.CreatedAt < endExclusive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchPattern = EscapeLikePattern(search);
            ordersQuery = ordersQuery.Where(order =>
                EF.Functions.Like(order.OrderNumber, $"%{searchPattern}%", "\\") ||
                (order.CustomerName != null && EF.Functions.Like(order.CustomerName, $"%{searchPattern}%", "\\")) ||
                (order.TableName != null && EF.Functions.Like(order.TableName, $"%{searchPattern}%", "\\")));
        }

        var items = await ordersQuery
            .Select(order => new PosOrderListItem(
                order.PosOrderId,
                order.BranchId,
                order.OrderNumber,
                order.OrderType.ToString(),
                order.Status.ToString(),
                order.TableName,
                order.CustomerName,
                order.GrandTotal,
                order.PosOrderLines.Count,
                order.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PosOrderListResponse(items
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.OrderNumber)
            .ToArray());
    }

    public async Task<PosOrderDetail> GetAsync(AuthUserContext currentUser, Guid orderId, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var order = await LoadOrderAsync(restaurantId, orderId, cancellationToken);
        return ToDetail(order);
    }

    public async Task<PosOrderDetail> CreateAsync(AuthUserContext currentUser, CreatePosOrderRequest request, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        ValidateRequest(request);

        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var branch = await LoadBranchAsync(restaurantId, request.BranchId, requireActive: true, cancellationToken);
        var orderType = ResolveOrderType(request.OrderType)
            ?? throw new InvalidOperationException(BuildAllowedValuesMessage<PosOrderType>("Order type"));
        var now = DateTimeOffset.UtcNow;
        var orderDate = ResolveOrderDate(now);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            try
            {
                var order = new PosOrder
                {
                    RestaurantId = restaurantId,
                    BranchId = branch.BranchId,
                    OrderType = orderType,
                    Status = PosOrderStatus.Draft,
                    TableName = NormalizeOptionalText(request.TableName, 80),
                    CustomerName = NormalizeOptionalText(request.CustomerName, 160),
                    CustomerMobile = NormalizeOptionalText(request.CustomerMobile, 32),
                    Notes = NormalizeOptionalText(request.Notes, 500),
                    CreatedByUserId = currentUser.UserId,
                    CreatedAt = now
                };

                var lineDetails = await BuildLineEntitiesAsync(restaurantId, orderType, request.Lines!, order.PosOrderLines, now, cancellationToken);
                CalculateTotals(order, lineDetails);
                order.OrderNumber = await AllocateOrderNumberAsync(restaurantId, branch.BranchId, orderDate, now, cancellationToken);

                var detail = ToDetail(order, lineDetails);

                _context.PosOrders.Add(order);
                AddAudit(
                    actor: currentUser,
                    restaurant: restaurant,
                    branch: branch,
                    action: "PosOrder.Created",
                    reason: "Pos order created.",
                    entityId: order.PosOrderId.ToString(),
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

        throw new InvalidOperationException("Unable to allocate a POS order number safely. Please retry.");
    }

    public async Task<PosOrderDetail> UpdateAsync(AuthUserContext currentUser, Guid orderId, UpdatePosOrderRequest request, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        ValidateRequest(request);

        var order = await LoadTrackedOrderAsync(restaurantId, orderId, cancellationToken);
        if (order.Status != PosOrderStatus.Draft)
        {
            throw new InvalidOperationException("Only draft orders can be updated.");
        }

        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var branch = await LoadBranchAsync(restaurantId, order.BranchId, requireActive: false, cancellationToken);
        var before = ToDetail(order);
        var orderType = ResolveOrderType(request.OrderType)
            ?? throw new InvalidOperationException(BuildAllowedValuesMessage<PosOrderType>("Order type"));
        var now = DateTimeOffset.UtcNow;

        order.OrderType = orderType;
        order.TableName = NormalizeOptionalText(request.TableName, 80);
        order.CustomerName = NormalizeOptionalText(request.CustomerName, 160);
        order.CustomerMobile = NormalizeOptionalText(request.CustomerMobile, 32);
        order.Notes = NormalizeOptionalText(request.Notes, 500);
        order.UpdatedAt = now;

        order.PosOrderLines.Clear();
        var lineDetails = await BuildLineEntitiesAsync(restaurantId, orderType, request.Lines!, order.PosOrderLines, now, cancellationToken);
        CalculateTotals(order, lineDetails);

        var after = ToDetail(order, lineDetails);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: branch,
            action: "PosOrder.Updated",
            reason: "Pos order updated.",
            entityId: order.PosOrderId.ToString(),
            oldValueJson: Serialize(before),
            newValueJson: Serialize(after),
            createdAt: now);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return after;
    }

    public async Task<PosOrderDetail> ConfirmAsync(AuthUserContext currentUser, Guid orderId, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var order = await LoadTrackedOrderAsync(restaurantId, orderId, cancellationToken);
        if (order.Status == PosOrderStatus.Cancelled)
        {
            throw new InvalidOperationException("Cancelled orders cannot be confirmed.");
        }

        if (order.Status != PosOrderStatus.Draft)
        {
            throw new InvalidOperationException("Only draft orders can be confirmed.");
        }

        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var branch = await LoadBranchAsync(restaurantId, order.BranchId, requireActive: false, cancellationToken);
        var before = ToDetail(order);

        order.Status = PosOrderStatus.Confirmed;
        order.ConfirmedAt = now;
        order.ConfirmedByUserId = currentUser.UserId;
        order.UpdatedAt = now;

        var ticket = await KitchenTicketWorkflow.CreateAsync(_context, currentUser, order, now, cancellationToken);
        var after = ToDetail(
            order,
            order.PosOrderLines
                .OrderBy(line => line.DisplayOrder)
                .Select(line => new PosOrderLineDetail(
                    line.PosOrderLineId,
                    line.MenuItemId,
                    line.MenuCategoryId,
                    line.MenuItemNameSnapshot,
                    line.MenuCategoryNameSnapshot,
                    line.SkuSnapshot,
                    line.UnitPrice,
                    line.TaxRate,
                    line.Quantity,
                    line.LineSubtotal,
                    line.LineTax,
                    line.LineTotal,
                    line.Notes,
                    line.DisplayOrder,
                    line.CreatedAt,
                    line.UpdatedAt))
                .ToArray(),
            ticket);
        var ticketDetail = KitchenTicketWorkflow.ToDetail(ticket);

        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: branch,
            action: "PosOrder.Confirmed",
            reason: "Pos order confirmed.",
            entityId: order.PosOrderId.ToString(),
            oldValueJson: Serialize(before),
            newValueJson: Serialize(after),
            createdAt: now);

        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: branch,
            action: "KitchenTicket.Created",
            reason: "Kitchen ticket created from confirmed POS order.",
            entityId: ticket.KitchenTicketId.ToString(),
            oldValueJson: null,
            newValueJson: Serialize(ticketDetail),
            createdAt: now,
            entityType: "KitchenTicket");

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return after;
    }

    public async Task<PosOrderDetail> CancelAsync(AuthUserContext currentUser, Guid orderId, CancelPosOrderRequest request, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var reason = NormalizeRequiredText(request?.Reason, "Cancel reason is required.");
        var order = await LoadTrackedOrderAsync(restaurantId, orderId, cancellationToken);

        if (order.Status == PosOrderStatus.Cancelled)
        {
            throw new InvalidOperationException("Order is already cancelled.");
        }

        if (order.Status != PosOrderStatus.Draft && order.Status != PosOrderStatus.Confirmed)
        {
            throw new InvalidOperationException("Only draft or confirmed orders can be cancelled.");
        }

        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var branch = await LoadBranchAsync(restaurantId, order.BranchId, requireActive: false, cancellationToken);
        var before = ToDetail(order);
        var now = DateTimeOffset.UtcNow;

        order.Status = PosOrderStatus.Cancelled;
        order.CancelledAt = now;
        order.CancelledByUserId = currentUser.UserId;
        order.CancelReason = reason;
        order.UpdatedAt = now;

        var after = ToDetail(order);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: branch,
            action: "PosOrder.Cancelled",
            reason: reason,
            entityId: order.PosOrderId.ToString(),
            oldValueJson: Serialize(before),
            newValueJson: Serialize(after),
            createdAt: now);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return after;
    }

    private async Task<IReadOnlyCollection<PosOrderLineDetail>> BuildLineEntitiesAsync(
        Guid restaurantId,
        PosOrderType orderType,
        IReadOnlyCollection<PosOrderLineRequest> requests,
        ICollection<PosOrderLine> targetLines,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (requests.Count == 0)
        {
            throw new InvalidOperationException("At least one line is required.");
        }

        var lineDetails = new List<PosOrderLineDetail>(requests.Count);

        foreach (var (request, index) in requests.Select((line, lineIndex) => (line, lineIndex)))
        {
            if (request.Quantity <= 0)
            {
                throw new InvalidOperationException("Quantity must be greater than zero.");
            }

            var snapshot = await LoadMenuItemSnapshotAsync(restaurantId, request.MenuItemId, orderType, cancellationToken);
            var unitPrice = snapshot.BasePrice;
            var taxRate = snapshot.TaxRate;
            var lineSubtotal = RoundMoney(unitPrice * request.Quantity);
            var lineTax = RoundMoney(lineSubtotal * taxRate / 100m);
            var lineTotal = RoundMoney(lineSubtotal + lineTax);
            var notes = NormalizeOptionalText(request.Notes, 300);

            var line = new PosOrderLine
            {
                RestaurantId = restaurantId,
                MenuItemId = snapshot.MenuItem.MenuItemId,
                MenuCategoryId = snapshot.Category.MenuCategoryId,
                MenuItemNameSnapshot = snapshot.MenuItem.Name,
                MenuCategoryNameSnapshot = snapshot.Category.Name,
                SkuSnapshot = snapshot.MenuItem.Sku,
                UnitPrice = unitPrice,
                TaxRate = taxRate,
                Quantity = request.Quantity,
                LineSubtotal = lineSubtotal,
                LineTax = lineTax,
                LineTotal = lineTotal,
                Notes = notes,
                DisplayOrder = index + 1,
                CreatedAt = now
            };

            targetLines.Add(line);

            lineDetails.Add(new PosOrderLineDetail(
                line.PosOrderLineId,
                line.MenuItemId,
                line.MenuCategoryId,
                line.MenuItemNameSnapshot,
                line.MenuCategoryNameSnapshot,
                line.SkuSnapshot,
                line.UnitPrice,
                line.TaxRate,
                line.Quantity,
                line.LineSubtotal,
                line.LineTax,
                line.LineTotal,
                line.Notes,
                line.DisplayOrder,
                line.CreatedAt,
                line.UpdatedAt));
        }

        return lineDetails;
    }

    private async Task<MenuItemSnapshot> LoadMenuItemSnapshotAsync(
        Guid restaurantId,
        Guid menuItemId,
        PosOrderType orderType,
        CancellationToken cancellationToken)
    {
        var snapshot = await (
                from item in _context.MenuItems.AsNoTracking()
                join category in _context.MenuCategories.AsNoTracking() on item.MenuCategoryId equals category.MenuCategoryId
                where item.MenuItemId == menuItemId &&
                      item.RestaurantId == restaurantId &&
                      category.RestaurantId == restaurantId
                select new MenuItemSnapshot(item, category))
            .SingleOrDefaultAsync(cancellationToken);

        if (snapshot is null)
        {
            throw new KeyNotFoundException("Menu item not found.");
        }

        if (snapshot.Category.Status != MenuCategoryStatus.Active)
        {
            throw new InvalidOperationException("Menu category must be active within the current restaurant.");
        }

        if (snapshot.MenuItem.Status != MenuItemStatus.Active)
        {
            throw new InvalidOperationException("Menu item must be active within the current restaurant.");
        }

        if (orderType == PosOrderType.EatIn && !snapshot.MenuItem.IsAvailableForEatIn)
        {
            throw new InvalidOperationException("Menu item is not available for eat-in orders.");
        }

        if (orderType == PosOrderType.Parcel && !snapshot.MenuItem.IsAvailableForParcel)
        {
            throw new InvalidOperationException("Menu item is not available for parcel orders.");
        }

        return snapshot;
    }

    private async Task<PosOrder> LoadOrderAsync(Guid restaurantId, Guid orderId, CancellationToken cancellationToken)
    {
        var order = await _context.PosOrders
            .AsNoTracking()
            .Include(entity => entity.PosOrderLines.OrderBy(line => line.DisplayOrder))
            .SingleOrDefaultAsync(entity => entity.PosOrderId == orderId && entity.RestaurantId == restaurantId, cancellationToken);

        if (order is null)
        {
            throw new KeyNotFoundException("Order not found.");
        }

        return order;
    }

    private async Task<PosOrder> LoadTrackedOrderAsync(Guid restaurantId, Guid orderId, CancellationToken cancellationToken)
    {
        var order = await _context.PosOrders
            .Include(entity => entity.PosOrderLines)
            .SingleOrDefaultAsync(entity => entity.PosOrderId == orderId && entity.RestaurantId == restaurantId, cancellationToken);

        if (order is null)
        {
            throw new KeyNotFoundException("Order not found.");
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

    private async Task<Branch> LoadBranchAsync(Guid restaurantId, Guid branchId, bool requireActive, CancellationToken cancellationToken)
    {
        var branch = await _context.Branches
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.BranchId == branchId && entity.RestaurantId == restaurantId, cancellationToken);

        if (branch is null)
        {
            throw new KeyNotFoundException("Branch not found.");
        }

        if (requireActive && branch.Status != BranchStatus.Active)
        {
            throw new InvalidOperationException("Branch must be active within the current restaurant.");
        }

        return branch;
    }

    private async Task<string> AllocateOrderNumberAsync(
        Guid restaurantId,
        Guid branchId,
        DateTime orderDate,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var sequence = await _context.PosOrderNumberSequences
            .AsNoTracking()
            .SingleOrDefaultAsync(entity =>
                entity.RestaurantId == restaurantId &&
                entity.BranchId == branchId &&
                entity.OrderDate == orderDate,
                cancellationToken);

        if (sequence is null)
        {
            sequence = new PosOrderNumberSequence
            {
                RestaurantId = restaurantId,
                BranchId = branchId,
                OrderDate = orderDate,
                LastSequence = 1,
                CreatedAt = now
            };

            _context.PosOrderNumberSequences.Add(sequence);
        }
        else
        {
            _context.Attach(sequence);
            sequence.LastSequence += 1;
            sequence.UpdatedAt = now;
        }

        return $"ORD-{orderDate:yyyyMMdd}-{sequence.LastSequence:0000}";
    }

    private static void CalculateTotals(PosOrder order, IReadOnlyCollection<PosOrderLineDetail> lineDetails)
    {
        order.Subtotal = RoundMoney(lineDetails.Sum(line => line.LineSubtotal));
        order.TaxTotal = RoundMoney(lineDetails.Sum(line => line.LineTax));
        order.GrandTotal = RoundMoney(lineDetails.Sum(line => line.LineTotal));
    }

    private static PosOrderDetail ToDetail(PosOrder order)
    {
        return ToDetail(order, order.PosOrderLines
            .OrderBy(line => line.DisplayOrder)
            .Select(line => new PosOrderLineDetail(
                line.PosOrderLineId,
                line.MenuItemId,
                line.MenuCategoryId,
                line.MenuItemNameSnapshot,
                line.MenuCategoryNameSnapshot,
                line.SkuSnapshot,
                line.UnitPrice,
                line.TaxRate,
                line.Quantity,
                line.LineSubtotal,
                line.LineTax,
                line.LineTotal,
                line.Notes,
                line.DisplayOrder,
                line.CreatedAt,
                line.UpdatedAt))
            .ToArray());
    }

    private static PosOrderDetail ToDetail(
        PosOrder order,
        IReadOnlyCollection<PosOrderLineDetail> lineDetails,
        KitchenTicket? kitchenTicket = null)
    {
        return new PosOrderDetail(
            order.PosOrderId,
            order.RestaurantId,
            order.BranchId,
            order.OrderNumber,
            order.OrderType.ToString(),
            order.Status.ToString(),
            order.TableName,
            order.CustomerName,
            order.CustomerMobile,
            order.Notes,
            order.Subtotal,
            order.TaxTotal,
            order.GrandTotal,
            order.ConfirmedAt,
            order.CancelledAt,
            order.CancelReason,
            order.CreatedByUserId,
            order.ConfirmedByUserId,
            order.CancelledByUserId,
            order.CreatedAt,
            order.UpdatedAt,
            lineDetails,
            kitchenTicket?.KitchenTicketId,
            kitchenTicket?.TicketNumber,
            kitchenTicket?.Status.ToString());
    }

    private static Guid RequireRestaurantScope(AuthUserContext currentUser)
    {
        if (currentUser.RestaurantId == Guid.Empty)
        {
            throw new InvalidOperationException("Authentication context is missing the restaurant scope.");
        }

        return currentUser.RestaurantId;
    }

    private static void ValidateRequest(CreatePosOrderRequest request)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Request is required.");
        }

        if (request.BranchId == Guid.Empty)
        {
            throw new InvalidOperationException("Branch is required.");
        }
    }

    private static void ValidateRequest(UpdatePosOrderRequest request)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Request is required.");
        }
    }

    private static string NormalizeRequiredText(string? value, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(errorMessage);
        }

        return value.Trim();
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

    private static PosOrderStatus? ResolveStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        if (!Enum.TryParse<PosOrderStatus>(status, true, out var parsed))
        {
            throw new InvalidOperationException(BuildAllowedValuesMessage<PosOrderStatus>("Status filter"));
        }

        return parsed;
    }

    private static PosOrderType? ResolveOrderType(string? orderType)
    {
        if (string.IsNullOrWhiteSpace(orderType))
        {
            return null;
        }

        if (!Enum.TryParse<PosOrderType>(orderType, true, out var parsed))
        {
            throw new InvalidOperationException(BuildAllowedValuesMessage<PosOrderType>("Order type filter"));
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

    private static DateTime ResolveOrderDate(DateTimeOffset dateTimeOffset)
    {
        return DateTime.SpecifyKind(dateTimeOffset.UtcDateTime.Date, DateTimeKind.Utc);
    }

    private static string? NormalizeSearch(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        return search.Trim();
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
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

    private static decimal RoundMoney(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
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
        DateTimeOffset createdAt,
        string entityType = "PosOrder")
    {
        _context.AuditLogs.Add(new AuditLog
        {
            RestaurantId = restaurant.RestaurantId,
            BranchId = branch.BranchId,
            UserId = actor.UserId == Guid.Empty ? null : actor.UserId,
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

    private sealed record MenuItemSnapshot(MenuItem MenuItem, MenuCategory Category)
    {
        public decimal BasePrice => MenuItem.BasePrice;

        public decimal TaxRate => MenuItem.TaxRate;
    }

    private sealed record RestaurantSnapshot(Guid RestaurantId, string Name);
}
