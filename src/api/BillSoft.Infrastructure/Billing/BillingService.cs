using System.Data;
using System.Text.Json;
using BillSoft.Application.Auth;
using BillSoft.Application.Billing;
using BillSoft.Domain.Auditing;
using BillSoft.Domain.Cashiering;
using BillSoft.Domain.Billing;
using BillSoft.Domain.Orders;
using BillSoft.Domain.Restaurants;
using BillSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillSoft.Infrastructure.Billing;

public sealed class BillingService : IBillingService
{
    private readonly BillSoftDbContext _context;

    public BillingService(BillSoftDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<BillListResponse> ListBillsAsync(AuthUserContext currentUser, BillListQuery query, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var status = ResolveBillStatus(query.Status);
        var search = NormalizeSearch(query.Search);

        var billsQuery =
            from bill in _context.Bills.AsNoTracking()
            join order in _context.PosOrders.AsNoTracking() on bill.PosOrderId equals order.PosOrderId
            where bill.RestaurantId == restaurantId
            select new { bill, order };

        if (query.BranchId.HasValue)
        {
            _ = await LoadBranchAsync(restaurantId, query.BranchId.Value, requireActive: false, cancellationToken);
            billsQuery = billsQuery.Where(item => item.bill.BranchId == query.BranchId.Value);
        }

        if (query.BusinessDate.HasValue)
        {
            var businessDate = ResolveBusinessDate(query.BusinessDate.Value);
            billsQuery = billsQuery.Where(item => item.bill.BusinessDate == businessDate);
        }

        if (status.HasValue)
        {
            billsQuery = billsQuery.Where(item => item.bill.Status == status.Value);
        }

        if (query.From.HasValue)
        {
            billsQuery = billsQuery.Where(item => item.bill.CreatedAt >= ResolveStartOfDay(query.From.Value));
        }

        if (query.To.HasValue)
        {
            billsQuery = billsQuery.Where(item => item.bill.CreatedAt < ResolveExclusiveEndOfDay(query.To.Value));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchPattern = EscapeLikePattern(search);
            billsQuery = billsQuery.Where(item =>
                EF.Functions.Like(item.bill.BillNumber, $"%{searchPattern}%", "\\") ||
                EF.Functions.Like(item.order.OrderNumber, $"%{searchPattern}%", "\\") ||
                (item.order.CustomerName != null && EF.Functions.Like(item.order.CustomerName, $"%{searchPattern}%", "\\")) ||
                (item.order.TableName != null && EF.Functions.Like(item.order.TableName, $"%{searchPattern}%", "\\")));
        }

        var items = await billsQuery
            .Select(item => new BillListItem(
                item.bill.BillId,
                item.bill.BranchId,
                item.bill.PosOrderId,
                item.bill.BillNumber,
                item.bill.BusinessDate,
                item.bill.Status.ToString(),
                item.bill.GrandTotal,
                item.bill.AmountPaid,
                item.bill.BalanceDue,
                item.bill.CreatedAt))
            .ToListAsync(cancellationToken);

        return new BillListResponse(items
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.BillNumber)
            .ToArray());
    }

    public async Task<BillDetail> GetBillAsync(AuthUserContext currentUser, Guid billId, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var bill = await LoadBillAsync(restaurantId, billId, cancellationToken);
        return ToDetail(bill);
    }

    public async Task<BillReceiptResponse> GetBillReceiptAsync(AuthUserContext currentUser, Guid billId, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        return await LoadBillReceiptAsync(restaurantId, billId, DateTimeOffset.UtcNow, cancellationToken);
    }

    public async Task<BillReceiptResponse> RecordBillReceiptPrintEventAsync(
        AuthUserContext currentUser,
        Guid billId,
        RecordBillReceiptPrintEventRequest? request,
        CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var printReason = NormalizeOptionalText(request?.Reason, 300);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            try
            {
                var bill = await _context.Bills
                    .AsNoTracking()
                    .SingleOrDefaultAsync(entity => entity.BillId == billId && entity.RestaurantId == restaurantId, cancellationToken);

                if (bill is null)
                {
                    throw new KeyNotFoundException("Bill not found.");
                }

                var now = DateTimeOffset.UtcNow;
                var nextSequence = await _context.BillPrintEvents
                    .AsNoTracking()
                    .Where(entity => entity.RestaurantId == restaurantId && entity.BillId == billId)
                    .Select(entity => (int?)entity.PrintSequence)
                    .MaxAsync(cancellationToken) ?? 0;

                var billPrintEvent = new BillPrintEvent
                {
                    RestaurantId = restaurantId,
                    BranchId = bill.BranchId,
                    BillId = bill.BillId,
                    PrintedByUserId = currentUser.UserId == Guid.Empty ? null : currentUser.UserId,
                    PrintSequence = nextSequence + 1,
                    PrintReason = printReason,
                    CreatedAt = now
                };

                _context.BillPrintEvents.Add(billPrintEvent);

                var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
                var branch = await LoadBranchAsync(restaurantId, bill.BranchId, requireActive: false, cancellationToken);

                AddAudit(
                    actor: currentUser,
                    restaurant: restaurant,
                    branch: branch,
                    action: "Bill.ReceiptPrinted",
                    reason: BuildReceiptPrintReason(bill.BillNumber, billPrintEvent.PrintSequence),
                    entityType: "BillPrintEvent",
                    entityId: billPrintEvent.BillPrintEventId.ToString(),
                    oldValueJson: null,
                    newValueJson: Serialize(new
                    {
                        bill.BillId,
                        bill.BillNumber,
                        billPrintEvent.PrintSequence,
                        printCount = billPrintEvent.PrintSequence,
                        isReprint = billPrintEvent.PrintSequence > 1
                    }),
                    createdAt: now);

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return await LoadBillReceiptAsync(
                    restaurantId,
                    billId,
                    now,
                    cancellationToken,
                    printCountOverride: nextSequence + 1,
                    isReprintOverride: nextSequence > 0);
            }
            catch (DbUpdateException ex) when (attempt == 0 && IsUniqueConstraintViolation(ex))
            {
                await transaction.RollbackAsync(cancellationToken);
                _context.ChangeTracker.Clear();
            }
        }

        throw new InvalidOperationException("Unable to record the receipt print event safely. Please retry.");
    }

    public async Task<BillDetail> CreateBillAsync(AuthUserContext currentUser, CreateBillRequest request, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        ValidateRequest(request);

        var now = DateTimeOffset.UtcNow;
        var billDate = ResolveSequenceDate(now);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            try
            {
                var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
                var order = await LoadConfirmedOrderAsync(restaurantId, request.PosOrderId, cancellationToken);
                var branch = await LoadBranchAsync(restaurantId, order.BranchId, requireActive: false, cancellationToken);

                var existingBill = await _context.Bills
                    .AsNoTracking()
                    .SingleOrDefaultAsync(entity =>
                        entity.RestaurantId == restaurantId &&
                        entity.PosOrderId == request.PosOrderId &&
                        entity.Status != BillStatus.Cancelled,
                        cancellationToken);

                if (existingBill is not null)
                {
                    throw new InvalidOperationException("A bill already exists for this POS order.");
                }

                var bill = new Bill
                {
                    RestaurantId = restaurantId,
                    BranchId = branch.BranchId,
                    PosOrderId = order.PosOrderId,
                    Status = BillStatus.Unpaid,
                    BusinessDate = billDate,
                    Subtotal = order.Subtotal,
                    TaxTotal = order.TaxTotal,
                    GrandTotal = order.GrandTotal,
                    AmountPaid = 0m,
                    BalanceDue = order.GrandTotal,
                    CreatedByUserId = currentUser.UserId,
                    CreatedAt = now
                };

                foreach (var line in order.PosOrderLines.OrderBy(line => line.DisplayOrder))
                {
                    bill.BillLines.Add(new BillLine
                    {
                        RestaurantId = restaurantId,
                        PosOrderLineId = line.PosOrderLineId,
                        MenuItemId = line.MenuItemId,
                        MenuCategoryId = line.MenuCategoryId,
                        MenuItemNameSnapshot = line.MenuItemNameSnapshot,
                        MenuCategoryNameSnapshot = line.MenuCategoryNameSnapshot,
                        SkuSnapshot = line.SkuSnapshot,
                        UnitPrice = line.UnitPrice,
                        TaxRate = line.TaxRate,
                        Quantity = line.Quantity,
                        LineSubtotal = line.LineSubtotal,
                        LineTax = line.LineTax,
                        LineTotal = line.LineTotal,
                        Notes = line.Notes,
                        DisplayOrder = line.DisplayOrder,
                        CreatedAt = now
                    });
                }

                bill.BillNumber = await AllocateBillNumberAsync(restaurantId, branch.BranchId, billDate, now, cancellationToken);

                _context.Bills.Add(bill);

                var detail = ToDetail(bill);
                AddAudit(
                    actor: currentUser,
                    restaurant: restaurant,
                    branch: branch,
                    action: "Bill.Created",
                    reason: "Bill created from confirmed POS order.",
                    entityType: "Bill",
                    entityId: bill.BillId.ToString(),
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

        throw new InvalidOperationException("Unable to allocate a bill number safely. Please retry.");
    }

    public async Task<BillDetail> CancelBillAsync(AuthUserContext currentUser, Guid billId, CancelBillRequest request, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var reason = NormalizeRequiredText(request?.Reason, "Cancel reason is required.");
        var bill = await LoadTrackedBillAsync(restaurantId, billId, cancellationToken);

        if (bill.Status == BillStatus.Cancelled)
        {
            throw new InvalidOperationException("Bill is already cancelled.");
        }

        if (bill.Payments.Any(payment => payment.Status == PaymentStatus.Recorded))
        {
            throw new InvalidOperationException("Recorded payments must be cancelled before cancelling the bill.");
        }

        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var branch = await LoadBranchAsync(restaurantId, bill.BranchId, requireActive: false, cancellationToken);
        var before = ToDetail(bill);
        var now = DateTimeOffset.UtcNow;

        bill.Status = BillStatus.Cancelled;
        bill.CancelledAt = now;
        bill.CancelledByUserId = currentUser.UserId;
        bill.CancelReason = reason;
        bill.UpdatedAt = now;

        var after = ToDetail(bill);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: branch,
            action: "Bill.Cancelled",
            reason: reason,
            entityType: "Bill",
            entityId: bill.BillId.ToString(),
            oldValueJson: Serialize(before),
            newValueJson: Serialize(after),
            createdAt: now);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return after;
    }

    public async Task<BillDetail> RecordPaymentAsync(AuthUserContext currentUser, Guid billId, RecordPaymentRequest request, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        ValidateRequest(request);
        var paymentMode = ResolvePaymentMode(request.PaymentMode);
        if (request.Amount <= 0)
        {
            throw new InvalidOperationException("Payment amount must be greater than zero.");
        }

        if ((paymentMode == PaymentMode.Upi || paymentMode == PaymentMode.Card) && string.IsNullOrWhiteSpace(request.ReferenceNumber))
        {
            throw new InvalidOperationException("Reference number is required for UPI and Card payments.");
        }

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            try
            {
                var bill = await LoadTrackedBillAsync(restaurantId, billId, cancellationToken);
                if (bill.Status == BillStatus.Cancelled)
                {
                    throw new InvalidOperationException("Cancelled bills cannot accept payments.");
                }

                var now = DateTimeOffset.UtcNow;
                var existingPaid = RoundMoney(bill.Payments.Where(payment => payment.Status == PaymentStatus.Recorded).Sum(payment => payment.Amount));
                var balanceDue = RoundMoney(bill.GrandTotal - existingPaid);

                if (request.Amount > balanceDue)
                {
                    throw new InvalidOperationException("Payment amount cannot exceed current bill balance due.");
                }

                var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
                var branch = await LoadBranchAsync(restaurantId, bill.BranchId, requireActive: false, cancellationToken);
                var billDate = ResolveSequenceDate(now);

                CashierShift? cashierShift = null;
                if (paymentMode == PaymentMode.Cash)
                {
                    cashierShift = await LoadOpenCashierShiftAsync(restaurantId, bill.BranchId, RequireUserId(currentUser), cancellationToken);
                }

                var payment = new Payment
                {
                    RestaurantId = restaurantId,
                    BranchId = bill.BranchId,
                    BillId = bill.BillId,
                    CashierShiftId = cashierShift?.CashierShiftId,
                    PaymentMode = paymentMode,
                    Status = PaymentStatus.Recorded,
                    Amount = request.Amount,
                    ReferenceNumber = NormalizeOptionalText(request.ReferenceNumber, 120),
                    Notes = NormalizeOptionalText(request.Notes, 500),
                    RecordedByUserId = currentUser.UserId,
                    CreatedAt = now
                };

                payment.PaymentNumber = await AllocatePaymentNumberAsync(restaurantId, bill.BranchId, billDate, now, cancellationToken);

                bill.Payments.Add(payment);
                RecalculateBillTotals(bill);
                bill.UpdatedAt = now;

                var detail = ToDetail(bill);

                AddAudit(
                    actor: currentUser,
                    restaurant: restaurant,
                    branch: branch,
                    action: "Payment.Recorded",
                    reason: "Payment recorded.",
                    entityType: "Payment",
                    entityId: payment.PaymentId.ToString(),
                    oldValueJson: null,
                    newValueJson: Serialize(ToPaymentDetail(payment)),
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

        throw new InvalidOperationException("Unable to allocate a payment number safely. Please retry.");
    }

    public async Task<BillDetail> CancelPaymentAsync(AuthUserContext currentUser, Guid paymentId, CancelPaymentRequest request, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var reason = NormalizeRequiredText(request?.Reason, "Cancel reason is required.");

        var paymentSummary = await _context.Payments
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.PaymentId == paymentId && entity.RestaurantId == restaurantId, cancellationToken);

        if (paymentSummary is null)
        {
            throw new KeyNotFoundException("Payment not found.");
        }

        var bill = await LoadTrackedBillAsync(restaurantId, paymentSummary.BillId, cancellationToken);
        if (bill.Status == BillStatus.Cancelled)
        {
            throw new InvalidOperationException("Payments cannot be cancelled when the bill is cancelled.");
        }

        var payment = bill.Payments.SingleOrDefault(entity => entity.PaymentId == paymentId);
        if (payment is null)
        {
            throw new KeyNotFoundException("Payment not found.");
        }

        if (payment.Status == PaymentStatus.Cancelled)
        {
            throw new InvalidOperationException("Payment is already cancelled.");
        }

        if (payment.Status != PaymentStatus.Recorded)
        {
            throw new InvalidOperationException("Only recorded payments can be cancelled.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
            var branch = await LoadBranchAsync(restaurantId, bill.BranchId, requireActive: false, cancellationToken);
            var before = ToDetail(bill);
            var beforePayment = ToPaymentDetail(payment);
            var now = DateTimeOffset.UtcNow;

            if (payment.PaymentMode == PaymentMode.Cash && payment.CashierShiftId.HasValue)
            {
                var cashierShift = await _context.CashierShifts
                    .SingleOrDefaultAsync(entity => entity.CashierShiftId == payment.CashierShiftId.Value && entity.RestaurantId == restaurantId, cancellationToken);

                if (cashierShift is null)
                {
                    throw new KeyNotFoundException("Cashier shift not found.");
                }

                if (cashierShift.Status != CashierShiftStatus.Open)
                {
                    throw new InvalidOperationException("Cash payment cannot be cancelled after the cashier shift is closed.");
                }
            }

            payment.Status = PaymentStatus.Cancelled;
            payment.CancelledAt = now;
            payment.CancelledByUserId = currentUser.UserId;
            payment.CancelReason = reason;
            payment.UpdatedAt = now;

            RecalculateBillTotals(bill);
            bill.UpdatedAt = now;

            var after = ToDetail(bill);

            AddAudit(
                actor: currentUser,
                restaurant: restaurant,
                branch: branch,
                action: "Payment.Cancelled",
                reason: reason,
                entityType: "Payment",
                entityId: payment.PaymentId.ToString(),
                oldValueJson: Serialize(beforePayment),
                newValueJson: Serialize(ToPaymentDetail(payment)),
                createdAt: now);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return after;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static Guid RequireUserId(AuthUserContext currentUser)
    {
        if (currentUser.UserId == Guid.Empty)
        {
            throw new InvalidOperationException("Authentication context is missing the user id.");
        }

        return currentUser.UserId;
    }

    private async Task<Bill> LoadBillAsync(Guid restaurantId, Guid billId, CancellationToken cancellationToken)
    {
        var bill = await _context.Bills
            .AsNoTracking()
            .Include(entity => entity.BillLines)
            .Include(entity => entity.Payments)
            .SingleOrDefaultAsync(entity => entity.BillId == billId && entity.RestaurantId == restaurantId, cancellationToken);

        if (bill is null)
        {
            throw new KeyNotFoundException("Bill not found.");
        }

        return bill;
    }

    private async Task<Bill> LoadTrackedBillAsync(Guid restaurantId, Guid billId, CancellationToken cancellationToken)
    {
        var bill = await _context.Bills
            .Include(entity => entity.BillLines)
            .Include(entity => entity.Payments)
            .SingleOrDefaultAsync(entity => entity.BillId == billId && entity.RestaurantId == restaurantId, cancellationToken);

        if (bill is null)
        {
            throw new KeyNotFoundException("Bill not found.");
        }

        return bill;
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

        if (order.Status == PosOrderStatus.Cancelled)
        {
            throw new InvalidOperationException("Cancelled POS orders cannot be billed.");
        }

        if (order.Status != PosOrderStatus.Confirmed)
        {
            throw new InvalidOperationException("Only confirmed POS orders can be billed.");
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

        return new RestaurantSnapshot(
            restaurant.RestaurantId,
            restaurant.Name,
            restaurant.RestaurantCode,
            restaurant.CountryCode,
            restaurant.CurrencyCode,
            restaurant.TimeZoneId);
    }

    private async Task<BillReceiptResponse> LoadBillReceiptAsync(
        Guid restaurantId,
        Guid billId,
        DateTimeOffset printedAt,
        CancellationToken cancellationToken,
        int? printCountOverride = null,
        bool? isReprintOverride = null)
    {
        var bill = await _context.Bills
            .AsNoTracking()
            .Include(entity => entity.BillLines)
            .Include(entity => entity.Payments)
            .SingleOrDefaultAsync(entity => entity.BillId == billId && entity.RestaurantId == restaurantId, cancellationToken);

        if (bill is null)
        {
            throw new KeyNotFoundException("Bill not found.");
        }

        var order = await _context.PosOrders
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.PosOrderId == bill.PosOrderId && entity.RestaurantId == restaurantId, cancellationToken);

        if (order is null)
        {
            throw new KeyNotFoundException("POS order not found.");
        }

        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var branch = await LoadBranchAsync(restaurantId, bill.BranchId, requireActive: false, cancellationToken);
        var userLabels = await LoadReceiptUserLabelsAsync(restaurantId, bill, cancellationToken);
        var printCount = printCountOverride ?? await _context.BillPrintEvents
            .AsNoTracking()
            .CountAsync(entity => entity.RestaurantId == restaurantId && entity.BillId == billId, cancellationToken);

        var createdByUserLabel = BuildReceiptUserLabel(bill.CreatedByUserId, userLabels);

        return new BillReceiptResponse(
            bill.BillId,
            bill.RestaurantId,
            bill.BranchId,
            restaurant.RestaurantCode,
            branch.CountryCode,
            branch.CurrencyCode,
            branch.TimeZoneId,
            restaurant.Name,
            branch.Name,
            branch.Address,
            bill.PosOrderId,
            bill.BusinessDate,
            order.OrderNumber,
            order.OrderType.ToString(),
            order.TableName,
            order.CustomerName,
            order.CustomerMobile,
            bill.BillNumber,
            bill.Status.ToString(),
            bill.CreatedByUserId,
            createdByUserLabel,
            bill.CreatedAt,
            bill.UpdatedAt,
            printedAt,
            bill.CancelledByUserId,
            bill.CancelledAt,
            bill.CancelReason,
            bill.Subtotal,
            bill.TaxTotal,
            bill.GrandTotal,
            bill.AmountPaid,
            bill.BalanceDue,
            printCount,
            isReprintOverride ?? printCount > 0,
            bill.BillLines.OrderBy(line => line.DisplayOrder).Select(ToReceiptBillLine).ToArray(),
            bill.Payments.OrderBy(payment => payment.CreatedAt).ThenBy(payment => payment.PaymentNumber).Select(payment => ToReceiptPayment(payment, userLabels)).ToArray());
    }

    private async Task<Dictionary<Guid, string>> LoadReceiptUserLabelsAsync(Guid restaurantId, Bill bill, CancellationToken cancellationToken)
    {
        var userIds = new HashSet<Guid>();

        if (bill.CreatedByUserId.HasValue && bill.CreatedByUserId.Value != Guid.Empty)
        {
            _ = userIds.Add(bill.CreatedByUserId.Value);
        }

        foreach (var payment in bill.Payments)
        {
            if (payment.RecordedByUserId.HasValue && payment.RecordedByUserId.Value != Guid.Empty)
            {
                _ = userIds.Add(payment.RecordedByUserId.Value);
            }
        }

        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var users = await _context.Users
            .AsNoTracking()
            .Where(entity => entity.RestaurantId == restaurantId && userIds.Contains(entity.UserId))
            .Select(entity => new { entity.UserId, entity.FullName })
            .ToListAsync(cancellationToken);

        return users.ToDictionary(
            entity => entity.UserId,
            entity => BuildReceiptUserLabel(entity.UserId, string.IsNullOrWhiteSpace(entity.FullName) ? null : entity.FullName));
    }

    private static string BuildReceiptUserLabel(Guid? userId, IReadOnlyDictionary<Guid, string> userLabels)
    {
        if (userId.HasValue && userLabels.TryGetValue(userId.Value, out var label))
        {
            return label;
        }

        return userId.HasValue && userId.Value != Guid.Empty
            ? userId.Value.ToString()
            : "Recorded by system";
    }

    private static string BuildReceiptUserLabel(Guid userId, string? displayName)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName.Trim();
        }

        return userId != Guid.Empty ? userId.ToString() : "Recorded by system";
    }

    private static BillReceiptLine ToReceiptBillLine(BillLine line)
    {
        return new BillReceiptLine(
            line.DisplayOrder,
            line.MenuItemNameSnapshot,
            line.MenuCategoryNameSnapshot,
            line.SkuSnapshot,
            line.Quantity,
            line.Notes,
            line.UnitPrice,
            line.LineSubtotal,
            line.LineTax,
            line.LineTotal);
    }

    private static BillReceiptPayment ToReceiptPayment(Payment payment, IReadOnlyDictionary<Guid, string> userLabels)
    {
        return new BillReceiptPayment(
            payment.PaymentNumber,
            payment.PaymentMode.ToString(),
            payment.Status.ToString(),
            payment.Amount,
            payment.ReferenceNumber,
            payment.Notes,
            payment.RecordedByUserId,
            BuildReceiptUserLabel(payment.RecordedByUserId, userLabels),
            payment.CreatedAt);
    }

    private static string BuildReceiptPrintReason(string billNumber, int printSequence)
    {
        return $"Receipt printed for {billNumber} (sequence {printSequence}).";
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

        if (requireActive && branch.Status != Domain.Restaurants.BranchStatus.Active)
        {
            throw new InvalidOperationException("Branch must be active within the current restaurant.");
        }

        return branch;
    }

    private async Task<string> AllocateBillNumberAsync(
        Guid restaurantId,
        Guid branchId,
        DateTime billDate,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var sequence = await _context.BillNumberSequences
            .AsNoTracking()
            .SingleOrDefaultAsync(entity =>
                entity.RestaurantId == restaurantId &&
                entity.BranchId == branchId &&
                entity.BillDate == billDate,
                cancellationToken);

        if (sequence is null)
        {
            sequence = new BillNumberSequence
            {
                RestaurantId = restaurantId,
                BranchId = branchId,
                BillDate = billDate,
                LastSequence = 1,
                CreatedAt = now
            };

            _context.BillNumberSequences.Add(sequence);
        }
        else
        {
            _context.Attach(sequence);
            sequence.LastSequence += 1;
            sequence.UpdatedAt = now;
        }

        return $"BILL-{billDate:yyyyMMdd}-{sequence.LastSequence:0000}";
    }

    private async Task<string> AllocatePaymentNumberAsync(
        Guid restaurantId,
        Guid branchId,
        DateTime paymentDate,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var sequence = await _context.PaymentNumberSequences
            .AsNoTracking()
            .SingleOrDefaultAsync(entity =>
                entity.RestaurantId == restaurantId &&
                entity.BranchId == branchId &&
                entity.PaymentDate == paymentDate,
                cancellationToken);

        if (sequence is null)
        {
            sequence = new PaymentNumberSequence
            {
                RestaurantId = restaurantId,
                BranchId = branchId,
                PaymentDate = paymentDate,
                LastSequence = 1,
                CreatedAt = now
            };

            _context.PaymentNumberSequences.Add(sequence);
        }
        else
        {
            _context.Attach(sequence);
            sequence.LastSequence += 1;
            sequence.UpdatedAt = now;
        }

        return $"PAY-{paymentDate:yyyyMMdd}-{sequence.LastSequence:0000}";
    }

    private async Task<CashierShift> LoadOpenCashierShiftAsync(Guid restaurantId, Guid branchId, Guid cashierUserId, CancellationToken cancellationToken)
    {
        var cashierShift = await _context.CashierShifts
            .SingleOrDefaultAsync(entity =>
                entity.RestaurantId == restaurantId &&
                entity.BranchId == branchId &&
                entity.OpenedByUserId == cashierUserId &&
                entity.Status == CashierShiftStatus.Open,
                cancellationToken);

        if (cashierShift is null)
        {
            throw new InvalidOperationException("Open cashier shift is required for cash payments.");
        }

        return cashierShift;
    }

    private static void RecalculateBillTotals(Bill bill)
    {
        bill.AmountPaid = RoundMoney(bill.Payments.Where(payment => payment.Status == PaymentStatus.Recorded).Sum(payment => payment.Amount));
        bill.BalanceDue = RoundMoney(bill.GrandTotal - bill.AmountPaid);
        bill.Status = bill.AmountPaid <= 0m
            ? BillStatus.Unpaid
            : bill.AmountPaid < bill.GrandTotal
                ? BillStatus.PartiallyPaid
                : BillStatus.Paid;
    }

    private static BillDetail ToDetail(Bill bill)
    {
        return new BillDetail(
            bill.BillId,
            bill.RestaurantId,
            bill.BranchId,
            bill.PosOrderId,
            bill.BillNumber,
            bill.BusinessDate,
            bill.Status.ToString(),
            bill.Subtotal,
            bill.TaxTotal,
            bill.GrandTotal,
            bill.AmountPaid,
            bill.BalanceDue,
            bill.CreatedByUserId,
            bill.CancelledByUserId,
            bill.CancelledAt,
            bill.CancelReason,
            bill.CreatedAt,
            bill.UpdatedAt,
            bill.BillLines.OrderBy(line => line.DisplayOrder).Select(ToBillLineDetail).ToArray(),
            bill.Payments.OrderBy(payment => payment.CreatedAt).ThenBy(payment => payment.PaymentNumber).Select(ToPaymentDetail).ToArray());
    }

    private static BillLineDetail ToBillLineDetail(BillLine line)
    {
        return new BillLineDetail(
            line.BillLineId,
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
            line.CreatedAt);
    }

    private static PaymentDetail ToPaymentDetail(Payment payment)
    {
        return ToPaymentDetail(payment, payment.Status);
    }

    private static PaymentDetail ToPaymentDetail(Payment payment, PaymentStatus status)
    {
        return new PaymentDetail(
            payment.PaymentId,
            payment.BillId,
            payment.BranchId,
            payment.PaymentNumber,
            payment.PaymentMode.ToString(),
            status.ToString(),
            payment.Amount,
            payment.ReferenceNumber,
            payment.Notes,
            payment.RecordedByUserId,
            payment.CancelledByUserId,
            payment.CancelledAt,
            payment.CancelReason,
            payment.CreatedAt,
            payment.UpdatedAt);
    }

    private static Guid RequireRestaurantScope(AuthUserContext currentUser)
    {
        if (currentUser.RestaurantId == Guid.Empty)
        {
            throw new InvalidOperationException("Authentication context is missing the restaurant scope.");
        }

        return currentUser.RestaurantId;
    }

    private static void ValidateRequest(CreateBillRequest request)
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

    private static void ValidateRequest(RecordPaymentRequest request)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Request is required.");
        }
    }

    private static BillStatus? ResolveBillStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        if (!Enum.TryParse<BillStatus>(status, true, out var parsed))
        {
            throw new InvalidOperationException(BuildAllowedValuesMessage<BillStatus>("Status filter"));
        }

        return parsed;
    }

    private static PaymentMode ResolvePaymentMode(string? paymentMode)
    {
        if (string.IsNullOrWhiteSpace(paymentMode))
        {
            throw new InvalidOperationException(BuildAllowedValuesMessage<PaymentMode>("Payment mode"));
        }

        if (!Enum.TryParse<PaymentMode>(paymentMode, true, out var parsed))
        {
            throw new InvalidOperationException(BuildAllowedValuesMessage<PaymentMode>("Payment mode"));
        }

        return parsed;
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

    private static DateTimeOffset ResolveStartOfDay(DateTime date)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(date.Date, DateTimeKind.Utc));
    }

    private static DateTimeOffset ResolveExclusiveEndOfDay(DateTime date)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(date.Date, DateTimeKind.Utc)).AddDays(1);
    }

    private static DateTime ResolveSequenceDate(DateTimeOffset dateTimeOffset)
    {
        return DateTime.SpecifyKind(dateTimeOffset.UtcDateTime.Date, DateTimeKind.Utc);
    }

    private static DateTime ResolveBusinessDate(DateTime date)
    {
        return DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
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

    private static string BuildAllowedValuesMessage<TEnum>(string label)
        where TEnum : struct, Enum
    {
        return $"{label} must be one of: {string.Join(", ", Enum.GetNames<TEnum>())}.";
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

    private sealed record RestaurantSnapshot(
        Guid RestaurantId,
        string Name,
        string RestaurantCode,
        string CountryCode,
        string CurrencyCode,
        string TimeZoneId);
}
