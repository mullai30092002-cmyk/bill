using System.Data;
using System.Text.Json;
using BillSoft.Application.Auth;
using BillSoft.Application.Vendors;
using BillSoft.Domain.Auditing;
using BillSoft.Domain.Inventory;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Users;
using BillSoft.Domain.Vendors;
using BillSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillSoft.Infrastructure.Vendors;

public sealed class VendorService : IVendorService
{
    private readonly BillSoftDbContext _context;

    public VendorService(BillSoftDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<VendorListResponse> ListVendorsAsync(AuthUserContext currentUser, VendorListQuery query, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var branchFilter = ResolveBranchFilter(currentUser, query.BranchId);

        var vendorsQuery = _context.Vendors
            .AsNoTracking()
            .Where(entity => entity.RestaurantId == restaurantId);

        if (branchFilter.HasValue)
        {
            vendorsQuery = vendorsQuery.Where(entity => entity.BranchId == branchFilter.Value || entity.BranchId == null);
        }
        else if (currentUser.BranchId.HasValue)
        {
            vendorsQuery = vendorsQuery.Where(entity => entity.BranchId == currentUser.BranchId.Value || entity.BranchId == null);
        }

        var vendors = await vendorsQuery
            .OrderBy(entity => entity.BranchId)
            .ThenBy(entity => entity.Name)
            .ToListAsync(cancellationToken);

        return new VendorListResponse(vendors.Select(ToListItem).ToArray());
    }

    public async Task<VendorDetail> CreateVendorAsync(AuthUserContext currentUser, CreateVendorRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        var restaurantId = RequireRestaurantScope(currentUser);
        var branchId = ResolveVendorBranchScope(currentUser, request.BranchId);
        Branch? branch = branchId.HasValue
            ? await LoadBranchAsync(restaurantId, branchId.Value, requireActive: true, cancellationToken)
            : null;
        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var vendorType = ResolveVendorType(request.VendorType);
        var normalizedName = Vendor.NormalizeKey(request.Name!);
        var mobileNumber = NormalizeOptionalText(request.MobileNumber, 32)
            ?? throw new InvalidOperationException("Mobile number is required.");
        var normalizedMobileNumber = NormalizeLookupKey(mobileNumber);

        await EnsureUniqueVendorNameAsync(restaurantId, branchId, null, normalizedName, cancellationToken);
        await EnsureUniqueVendorMobileAsync(restaurantId, null, normalizedMobileNumber, cancellationToken);

        var vendor = new Vendor
        {
            RestaurantId = restaurantId,
            BranchId = branchId,
            Name = NormalizeRequiredText(request.Name!),
            NormalizedName = normalizedName,
            VendorType = vendorType,
            ContactName = NormalizeOptionalText(request.ContactName, 160),
            MobileNumber = mobileNumber,
            NormalizedMobileNumber = normalizedMobileNumber,
            Address = NormalizeOptionalText(request.Address, 512),
            Notes = NormalizeOptionalText(request.Notes, 500),
            IsActive = request.IsActive,
            CreatedAtUtc = now
        };

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        _context.Vendors.Add(vendor);
        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: branch,
            action: "Vendor.Created",
            reason: "Vendor created.",
            entityType: "Vendor",
            entityId: vendor.VendorId.ToString(),
            oldValueJson: null,
            newValueJson: Serialize(ToDetail(vendor)),
            createdAt: now);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToDetail(vendor);
    }

    public async Task<VendorDetail> UpdateVendorAsync(AuthUserContext currentUser, Guid vendorId, UpdateVendorRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        var restaurantId = RequireRestaurantScope(currentUser);
        var vendor = await LoadTrackedVendorAsync(restaurantId, vendorId, cancellationToken);
        var branchId = ResolveVendorBranchScope(currentUser, request.BranchId ?? vendor.BranchId);
        if (currentUser.BranchId.HasValue && branchId.HasValue && branchId.Value != currentUser.BranchId.Value)
        {
            throw new UnauthorizedAccessException("Branch access is restricted to the current branch.");
        }

        Branch? branch = branchId.HasValue
            ? await LoadBranchAsync(restaurantId, branchId.Value, requireActive: true, cancellationToken)
            : null;
        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var before = ToDetail(vendor);
        var now = DateTimeOffset.UtcNow;
        var vendorType = ResolveVendorType(request.VendorType);
        var normalizedName = Vendor.NormalizeKey(request.Name!);
        var mobileNumber = NormalizeOptionalText(request.MobileNumber, 32)
            ?? throw new InvalidOperationException("Mobile number is required.");
        var normalizedMobileNumber = NormalizeLookupKey(mobileNumber);

        await EnsureUniqueVendorNameAsync(restaurantId, branchId, vendor.VendorId, normalizedName, cancellationToken);
        await EnsureUniqueVendorMobileAsync(restaurantId, vendor.VendorId, normalizedMobileNumber, cancellationToken);

        vendor.BranchId = branchId;
        vendor.UpdateProfile(
            request.Name!,
            vendorType,
            request.ContactName,
            mobileNumber,
            request.Address,
            request.Notes,
            request.IsActive,
            now);
        vendor.NormalizedMobileNumber = normalizedMobileNumber;

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: branch,
            action: "Vendor.Updated",
            reason: "Vendor updated.",
            entityType: "Vendor",
            entityId: vendor.VendorId.ToString(),
            oldValueJson: Serialize(before),
            newValueJson: Serialize(ToDetail(vendor)),
            createdAt: now);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToDetail(vendor);
    }

    public async Task<VendorBillListResponse> ListVendorBillsAsync(AuthUserContext currentUser, VendorBillListQuery query, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var branchFilter = ResolveBranchFilter(currentUser, query.BranchId);
        var status = ResolveVendorBillStatus(query.Status);

        var billsQuery = _context.VendorBills
            .AsNoTracking()
            .Where(entity => entity.RestaurantId == restaurantId);

        if (branchFilter.HasValue)
        {
            billsQuery = billsQuery.Where(entity => entity.BranchId == branchFilter.Value);
        }
        else if (currentUser.BranchId.HasValue)
        {
            billsQuery = billsQuery.Where(entity => entity.BranchId == currentUser.BranchId.Value);
        }

        if (query.FromDate.HasValue)
        {
            billsQuery = billsQuery.Where(entity => entity.BillDate >= query.FromDate.Value.Date);
        }

        if (query.ToDate.HasValue)
        {
            billsQuery = billsQuery.Where(entity => entity.BillDate <= query.ToDate.Value.Date);
        }

        if (status.HasValue)
        {
            billsQuery = billsQuery.Where(entity => entity.Status == status.Value);
        }

        var bills = (await billsQuery.ToListAsync(cancellationToken))
            .OrderByDescending(entity => entity.BillDate)
            .ThenByDescending(entity => entity.CreatedAtUtc)
            .ToList();

        var vendorIds = bills.Select(entity => entity.VendorId).Distinct().ToArray();
        var vendorLookup = vendorIds.Length == 0
            ? new Dictionary<Guid, Vendor>()
            : await _context.Vendors
                .AsNoTracking()
                .Where(entity => entity.RestaurantId == restaurantId && vendorIds.Contains(entity.VendorId))
                .ToDictionaryAsync(entity => entity.VendorId, cancellationToken);

        return new VendorBillListResponse(bills.Select(bill => ToListItem(bill, vendorLookup)).ToArray());
    }

    public async Task<VendorBillDetail> GetVendorBillAsync(AuthUserContext currentUser, Guid vendorBillId, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var bill = await LoadVendorBillAsync(restaurantId, vendorBillId, cancellationToken);
        EnsureBranchAccess(currentUser, bill.BranchId);
        return await ToDetailAsync(bill, cancellationToken);
    }

    public async Task<VendorBillDetail> CreateVendorBillAsync(AuthUserContext currentUser, CreateVendorBillRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        var restaurantId = RequireRestaurantScope(currentUser);
        var vendor = await LoadTrackedVendorAsync(restaurantId, request.VendorId, cancellationToken);
        var branchId = ResolveVendorBillBranchScope(currentUser, request.BranchId);
        var branch = await LoadBranchAsync(restaurantId, branchId, requireActive: true, cancellationToken);
        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var recordedByUserId = RequireUserId(currentUser);
        var billDate = DateTime.SpecifyKind(request.BillDate.Date, DateTimeKind.Utc);
        DateTime? dueDate = request.DueDate.HasValue ? DateTime.SpecifyKind(request.DueDate.Value.Date, DateTimeKind.Utc) : null;
        var billNumber = NormalizeOptionalText(request.BillNumber, 40);
        var normalizedBillNumber = NormalizeBillNumber(billNumber);
        var lines = request.Lines?.ToArray() ?? [];

        if (lines.Length == 0)
        {
            throw new InvalidOperationException("At least one bill line is required.");
        }

        if (!vendor.IsActive)
        {
            throw new InvalidOperationException("Inactive vendors cannot receive new bills.");
        }

        if (vendor.BranchId.HasValue && vendor.BranchId.Value != branch.BranchId)
        {
            throw new InvalidOperationException("Vendor is not available for the selected branch.");
        }

        await EnsureUniqueVendorBillNumberAsync(restaurantId, vendor.VendorId, normalizedBillNumber, cancellationToken);

        var normalizedLines = new List<NormalizedVendorBillLine>(lines.Length);
        foreach (var line in lines)
        {
            ValidateLine(line);
            var inventoryItem = line.InventoryItemId.HasValue
                ? await LoadBranchInventoryItemAsync(restaurantId, branch.BranchId, line.InventoryItemId.Value, cancellationToken)
                : null;

            normalizedLines.Add(new NormalizedVendorBillLine(
                line.InventoryItemId,
                inventoryItem?.Name,
                NormalizeRequiredText(line.Description!),
                line.Quantity,
                line.UnitCost,
                RoundMoney(line.Quantity * line.UnitCost)));
        }

        var bill = new VendorBill
        {
            RestaurantId = restaurantId,
            BranchId = branch.BranchId,
            VendorId = vendor.VendorId,
            BillNumber = billNumber,
            NormalizedBillNumber = normalizedBillNumber,
            BillDate = billDate,
            DueDate = dueDate,
            Status = VendorBillStatus.Unpaid,
            TotalAmount = normalizedLines.Sum(line => line.LineTotal),
            PaidAmount = 0m,
            BalanceAmount = normalizedLines.Sum(line => line.LineTotal),
            Notes = NormalizeOptionalText(request.Notes, 500),
            CreatedAtUtc = now
        };

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        _context.VendorBills.Add(bill);
        await _context.SaveChangesAsync(cancellationToken);

        foreach (var line in normalizedLines)
        {
            var billLine = new VendorBillLine
            {
                RestaurantId = restaurantId,
                BranchId = branch.BranchId,
                VendorBillId = bill.VendorBillId,
                InventoryItemId = line.InventoryItemId,
                Description = line.Description,
                Quantity = line.Quantity,
                UnitCost = line.UnitCost,
                LineTotal = line.LineTotal,
                CreatedAtUtc = now
            };

            if (line.InventoryItemId.HasValue)
            {
                var inventoryItem = await LoadBranchInventoryItemAsync(restaurantId, branch.BranchId, line.InventoryItemId.Value, cancellationToken);
                var movement = new InventoryMovement
                {
                    RestaurantId = restaurantId,
                    BranchId = branch.BranchId,
                    InventoryItemId = line.InventoryItemId.Value,
                    MovementType = InventoryMovementType.StockIn,
                    Quantity = line.Quantity,
                    UnitCost = line.UnitCost,
                    ReferenceNumber = bill.BillNumber ?? bill.VendorBillId.ToString(),
                    Notes = line.Description,
                    MovementDate = now,
                    RecordedByUserId = recordedByUserId,
                    CreatedAtUtc = now
                };

                _context.InventoryMovements.Add(movement);
                _context.InventoryLots.Add(CreateInventoryLot(
                    restaurantId,
                    branch.BranchId,
                    line.InventoryItemId.Value,
                    movement.InventoryMovementId,
                    sourceBatchProductionId: null,
                    batchReference: null,
                    receivedAtUtc: movement.MovementDate,
                    expiresAtUtc: null,
                    quantity: line.Quantity,
                    unitOfMeasure: inventoryItem.UnitOfMeasure,
                    createdAtUtc: now));
                await _context.SaveChangesAsync(cancellationToken);
                billLine.InventoryMovementId = movement.InventoryMovementId;
            }

            _context.VendorBillLines.Add(billLine);
        }

        await _context.SaveChangesAsync(cancellationToken);

        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: branch,
            action: "VendorBill.Created",
            reason: "Vendor bill created.",
            entityType: "VendorBill",
            entityId: bill.VendorBillId.ToString(),
            oldValueJson: null,
            newValueJson: Serialize(await ToDetailAsync(bill, cancellationToken)),
            createdAt: now);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await ToDetailAsync(bill, cancellationToken);
    }

    public async Task<VendorBillDetail> RecordSettlementAsync(AuthUserContext currentUser, Guid vendorBillId, RecordVendorSettlementRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        var restaurantId = RequireRestaurantScope(currentUser);
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var bill = await LoadTrackedVendorBillAsync(restaurantId, vendorBillId, cancellationToken);

        if (bill.Status == VendorBillStatus.Cancelled)
        {
            throw new InvalidOperationException("Cancelled vendor bills cannot accept settlements.");
        }

        var paymentMode = ResolveVendorSettlementPaymentMode(request.PaymentMode);
        if (request.Amount <= 0m)
        {
            throw new InvalidOperationException("Settlement amount must be greater than zero.");
        }

        if ((paymentMode == VendorSettlementPaymentMode.UPI ||
             paymentMode == VendorSettlementPaymentMode.Card ||
             paymentMode == VendorSettlementPaymentMode.BankTransfer) &&
            string.IsNullOrWhiteSpace(request.ReferenceNumber))
        {
            throw new InvalidOperationException("Reference number is required for UPI, Card, and BankTransfer settlements.");
        }

        var now = request.PaidAtUtc ?? DateTimeOffset.UtcNow;
        var activeSettlements = bill.Settlements.Where(entity => entity.Status == VendorSettlementStatus.Active).ToArray();
        var paidAmount = RoundMoney(activeSettlements.Sum(entity => entity.Amount));
        var balanceAmount = RoundMoney(bill.TotalAmount - paidAmount);

        if (request.Amount > balanceAmount)
        {
            throw new InvalidOperationException("Settlement amount cannot exceed the current vendor bill balance.");
        }

        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var branch = await LoadBranchAsync(restaurantId, bill.BranchId, requireActive: false, cancellationToken);
        var previousOutstandingAmount = balanceAmount;
        var newOutstandingAmount = RoundMoney(previousOutstandingAmount - request.Amount);
        var settlement = new VendorSettlement
        {
            RestaurantId = restaurantId,
            BranchId = bill.BranchId,
            VendorBillId = bill.VendorBillId,
            PaymentMode = paymentMode,
            Amount = request.Amount,
            ReferenceNumber = NormalizeOptionalText(request.ReferenceNumber, 120),
            Notes = NormalizeOptionalText(request.Notes, 500),
            PaidAtUtc = now,
            RecordedByUserId = RequireUserId(currentUser),
            Status = VendorSettlementStatus.Active,
            PreviousOutstandingAmount = previousOutstandingAmount,
            NewOutstandingAmount = newOutstandingAmount,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        bill.Settlements.Add(settlement);
        RecalculateBillTotals(bill);
        bill.UpdatedAtUtc = DateTimeOffset.UtcNow;

        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: branch,
            action: "VendorSettlement.Created",
            reason: "Vendor settlement recorded.",
            entityType: "VendorSettlement",
            entityId: settlement.VendorSettlementId.ToString(),
            oldValueJson: null,
            newValueJson: Serialize(ToSettlementDetail(settlement)),
            createdAt: settlement.CreatedAtUtc);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await ToDetailAsync(bill, cancellationToken);
    }

    public async Task<VendorStatementResponse> GetVendorStatementAsync(AuthUserContext currentUser, VendorStatementQuery query, CancellationToken cancellationToken)
    {
        ValidateRequest(query);

        var restaurantId = RequireRestaurantScope(currentUser);
        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var vendor = await LoadTrackedVendorAsync(restaurantId, query.VendorId, cancellationToken);
        var branchFilter = ResolveBranchFilter(currentUser, query.BranchId);

        if (branchFilter.HasValue && vendor.BranchId.HasValue && vendor.BranchId.Value != branchFilter.Value)
        {
            throw new UnauthorizedAccessException("Branch access is restricted to the current branch.");
        }

        var branch = branchFilter.HasValue
            ? await LoadBranchAsync(restaurantId, branchFilter.Value, requireActive: false, cancellationToken)
            : null;

        var fromDate = ResolveDate(query.FromDate) ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var toDate = ResolveDate(query.ToDate) ?? DateTime.UtcNow.Date;

        if (fromDate > toDate)
        {
            throw new InvalidOperationException("From date cannot be after to date.");
        }

        var fromDateExclusive = new DateTimeOffset(fromDate.Date, TimeSpan.Zero);
        var toDateExclusive = new DateTimeOffset(toDate.Date.AddDays(1), TimeSpan.Zero);

        var billsQuery = _context.VendorBills
            .AsNoTracking()
            .Where(entity =>
                entity.RestaurantId == restaurantId &&
                entity.VendorId == vendor.VendorId &&
                entity.Status != VendorBillStatus.Cancelled &&
                entity.BillDate <= toDate.Date);

        if (branchFilter.HasValue)
        {
            billsQuery = billsQuery.Where(entity => entity.BranchId == branchFilter.Value);
        }
        else if (currentUser.BranchId.HasValue)
        {
            billsQuery = billsQuery.Where(entity => entity.BranchId == currentUser.BranchId.Value);
        }

        var bills = (await billsQuery
            .ToArrayAsync(cancellationToken))
            .OrderBy(entity => entity.BillDate)
            .ThenBy(entity => entity.CreatedAtUtc.UtcDateTime)
            .ToArray();

        var billIds = bills.Select(entity => entity.VendorBillId).ToArray();
        var branchIds = bills.Select(entity => entity.BranchId).Distinct().ToArray();

        var branchLookup = branchIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await _context.Branches
                .AsNoTracking()
                .Where(entity => entity.RestaurantId == restaurantId && branchIds.Contains(entity.BranchId))
                .Select(entity => new { entity.BranchId, entity.Name })
                .ToDictionaryAsync(entity => entity.BranchId, entity => entity.Name, cancellationToken);

        var settlements = billIds.Length == 0
            ? Array.Empty<VendorSettlement>()
            : (await _context.VendorSettlements
                .AsNoTracking()
                .Where(entity =>
                    entity.RestaurantId == restaurantId &&
                    entity.Status == VendorSettlementStatus.Active &&
                    billIds.Contains(entity.VendorBillId))
                .ToArrayAsync(cancellationToken))
                .Where(entity => entity.PaidAtUtc < toDateExclusive)
                .OrderBy(entity => entity.PaidAtUtc.UtcDateTime)
                .ThenBy(entity => entity.CreatedAtUtc.UtcDateTime)
                .ToArray();

        var settlementsByBill = settlements
            .GroupBy(entity => entity.VendorBillId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(entity => entity.PaidAtUtc).ThenBy(entity => entity.CreatedAtUtc).ToArray());

        decimal GetPaidAmount(Guid billId, DateTimeOffset exclusiveUpperBound)
        {
            if (!settlementsByBill.TryGetValue(billId, out var items))
            {
                return 0m;
            }

            return RoundMoney(items.Where(entity => entity.PaidAtUtc < exclusiveUpperBound).Sum(entity => entity.Amount));
        }

        var billSnapshots = bills
            .Select(bill =>
            {
                branchLookup.TryGetValue(bill.BranchId, out var branchName);
                var paidAmount = GetPaidAmount(bill.VendorBillId, toDateExclusive);
                var outstandingAmount = RoundMoney(Math.Max(0m, bill.TotalAmount - paidAmount));
                return new
                {
                    bill.VendorBillId,
                    bill.BranchId,
                    BranchName = branchName,
                    bill.BillNumber,
                    bill.BillDate,
                    bill.DueDate,
                    bill.Status,
                    bill.TotalAmount,
                    PaidAmount = paidAmount,
                    OutstandingAmount = outstandingAmount,
                    bill.Notes,
                    bill.CreatedAtUtc
                };
            })
            .ToArray();

        var openingOutstandingAmount = RoundMoney(billSnapshots
            .Where(item => item.BillDate < fromDate.Date)
            .Sum(item =>
            {
                var paidBeforeFromDate = GetPaidAmount(item.VendorBillId, fromDateExclusive);
                return Math.Max(0m, item.TotalAmount - paidBeforeFromDate);
            }));

        var currentOutstandingAmount = RoundMoney(billSnapshots.Sum(item => item.OutstandingAmount));
        var totalBillAmount = RoundMoney(billSnapshots.Sum(item => item.TotalAmount));
        var totalSettlementAmount = RoundMoney(settlements.Sum(entity => entity.Amount));

        var payableBills = billSnapshots
            .Where(item => item.OutstandingAmount > 0m)
            .Select(item => new VendorStatementBillItem(
                item.VendorBillId,
                item.BranchId,
                item.BranchName,
                item.BillNumber,
                item.BillDate,
                item.DueDate,
                item.Status.ToString(),
                item.TotalAmount,
                item.PaidAmount,
                item.OutstandingAmount,
                item.Notes,
                item.CreatedAtUtc))
            .OrderByDescending(item => item.BillDate)
            .ThenByDescending(item => item.CreatedAtUtc)
            .ToArray();

        var statementSettlements = settlements
            .Where(entity => entity.PaidAtUtc >= fromDateExclusive)
            .Select(entity =>
            {
                var bill = billSnapshots.Single(snapshot => snapshot.VendorBillId == entity.VendorBillId);
                return new VendorStatementSettlementItem(
                    entity.VendorSettlementId,
                    entity.VendorBillId,
                    entity.BranchId,
                    bill.BranchName,
                    bill.BillNumber,
                    entity.PaidAtUtc,
                    entity.PaymentMode.ToString(),
                    entity.Amount,
                    MaskReferenceNumber(entity.ReferenceNumber),
                    entity.Notes,
                    entity.PreviousOutstandingAmount,
                    entity.NewOutstandingAmount,
                    entity.Status.ToString());
            })
            .OrderByDescending(item => item.PaidAtUtc)
            .ThenByDescending(item => item.VendorSettlementId)
            .ToArray();

        var transactions = new List<VendorStatementTimelineItem>(payableBills.Length + statementSettlements.Length);
        transactions.AddRange(
            billSnapshots
                .Where(item => item.CreatedAtUtc >= fromDateExclusive && item.CreatedAtUtc < toDateExclusive)
                .Select(item => new VendorStatementTimelineItem(
                    "Bill",
                    item.CreatedAtUtc,
                    item.BillNumber,
                    item.BillNumber,
                    item.Notes ?? "Vendor bill recorded.",
                    item.TotalAmount,
                    0m,
                    0m,
                    null,
                    item.Status.ToString())));
        transactions.AddRange(
            statementSettlements.Select(item => new VendorStatementTimelineItem(
                "Settlement",
                item.PaidAtUtc,
                item.BillNumber,
                item.ReferenceNumberMasked,
                item.Notes ?? "Vendor settlement recorded.",
                0m,
                item.Amount,
                0m,
                item.PaymentMode,
                item.Status)));

        var runningBalance = openingOutstandingAmount;
        var orderedTransactions = transactions
            .OrderBy(item => item.TimestampUtc)
            .ThenBy(item => item.EntryType)
            .Select(item =>
            {
                runningBalance = item.EntryType == "Bill"
                    ? RoundMoney(runningBalance + item.DebitAmount)
                    : RoundMoney(Math.Max(0m, runningBalance - item.CreditAmount));

                return item with { RunningBalance = runningBalance };
            })
            .ToArray();

        var overdueBillCount = payableBills.Count(item => item.DueDate.HasValue && item.DueDate.Value.Date < toDate.Date);

        return new VendorStatementResponse(
            restaurantId,
            branch?.BranchId ?? branchFilter,
            branch?.Name,
            vendor.VendorId,
            vendor.Name,
            vendor.VendorType.ToString(),
            branch?.CurrencyCode ?? restaurant.CurrencyCode,
            fromDate.Date,
            toDate.Date,
            DateTimeOffset.UtcNow,
            openingOutstandingAmount,
            currentOutstandingAmount,
            new VendorStatementSummary(
                totalBillAmount,
                totalSettlementAmount,
                payableBills.Length,
                statementSettlements.Length,
                overdueBillCount),
            payableBills,
            statementSettlements,
            orderedTransactions);
    }

    public async Task<VendorBillDetail> CancelVendorBillAsync(AuthUserContext currentUser, Guid vendorBillId, CancelVendorBillRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        var restaurantId = RequireRestaurantScope(currentUser);
        var bill = await LoadTrackedVendorBillAsync(restaurantId, vendorBillId, cancellationToken);

        if (bill.Status == VendorBillStatus.Cancelled)
        {
            throw new InvalidOperationException("Vendor bill is already cancelled.");
        }

        if (bill.Settlements.Any(entity => entity.Status == VendorSettlementStatus.Active))
        {
            throw new InvalidOperationException("Active settlements must be reversed before cancelling a vendor bill.");
        }

        if (bill.Lines.Any(entity => entity.InventoryMovementId.HasValue))
        {
            throw new InvalidOperationException("Vendor bills with stock-in movements cannot be cancelled in this slice.");
        }

        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var branch = await LoadBranchAsync(restaurantId, bill.BranchId, requireActive: false, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var before = await ToDetailAsync(bill, cancellationToken);

        bill.Status = VendorBillStatus.Cancelled;
        bill.CancelledAtUtc = now;
        bill.CancelledByUserId = RequireUserId(currentUser);
        bill.CancellationReason = NormalizeRequiredText(request.Reason!);
        bill.UpdatedAtUtc = now;

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: branch,
            action: "VendorBill.Cancelled",
            reason: bill.CancellationReason,
            entityType: "VendorBill",
            entityId: bill.VendorBillId.ToString(),
            oldValueJson: Serialize(before),
            newValueJson: Serialize(await ToDetailAsync(bill, cancellationToken)),
            createdAt: now);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await ToDetailAsync(bill, cancellationToken);
    }

    private async Task EnsureUniqueVendorNameAsync(
        Guid restaurantId,
        Guid? branchId,
        Guid? vendorId,
        string normalizedName,
        CancellationToken cancellationToken)
    {
        var candidateQuery = _context.Vendors
            .AsNoTracking()
            .Where(entity => entity.RestaurantId == restaurantId && entity.VendorId != vendorId);

        if (branchId.HasValue)
        {
            candidateQuery = candidateQuery.Where(entity => entity.BranchId == branchId.Value);
        }
        else
        {
            candidateQuery = candidateQuery.Where(entity => entity.BranchId == null);
        }

        var exists = await candidateQuery.AnyAsync(entity => entity.NormalizedName == normalizedName, cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException("Vendor name already exists in this scope.");
        }
    }

    private async Task EnsureUniqueVendorMobileAsync(
        Guid restaurantId,
        Guid? vendorId,
        string normalizedMobileNumber,
        CancellationToken cancellationToken)
    {
        var exists = await _context.Vendors
            .AsNoTracking()
            .Where(entity =>
                entity.RestaurantId == restaurantId &&
                entity.VendorId != vendorId)
            .Select(entity => new
            {
                entity.MobileNumber,
                entity.NormalizedMobileNumber
            })
            .ToListAsync(cancellationToken);

        var duplicateExists = exists.Any(candidate =>
            MatchesNormalizedValue(candidate.NormalizedMobileNumber, candidate.MobileNumber, normalizedMobileNumber));

        if (duplicateExists)
        {
            throw new InvalidOperationException("Vendor mobile number already exists.");
        }
    }

    private async Task EnsureUniqueVendorBillNumberAsync(
        Guid restaurantId,
        Guid vendorId,
        string? normalizedBillNumber,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(normalizedBillNumber))
        {
            return;
        }

        var candidateBills = await _context.VendorBills
            .AsNoTracking()
            .Where(entity =>
                entity.RestaurantId == restaurantId &&
                entity.VendorId == vendorId)
            .Select(entity => new
            {
                entity.BillNumber,
                entity.NormalizedBillNumber
            })
            .ToListAsync(cancellationToken);

        var exists = candidateBills.Any(candidate =>
            MatchesNormalizedValue(candidate.NormalizedBillNumber, candidate.BillNumber, normalizedBillNumber));

        if (exists)
        {
            throw new InvalidOperationException("This bill number already exists for the selected vendor.");
        }
    }

    private async Task<Vendor> LoadTrackedVendorAsync(Guid restaurantId, Guid vendorId, CancellationToken cancellationToken)
    {
        var vendor = await _context.Vendors
            .SingleOrDefaultAsync(entity => entity.RestaurantId == restaurantId && entity.VendorId == vendorId, cancellationToken);

        if (vendor is null)
        {
            throw new KeyNotFoundException("Vendor not found.");
        }

        return vendor;
    }

    private async Task<VendorBill> LoadVendorBillAsync(Guid restaurantId, Guid vendorBillId, CancellationToken cancellationToken)
    {
        var bill = await _context.VendorBills
            .AsNoTracking()
            .Include(entity => entity.Lines)
            .Include(entity => entity.Settlements)
            .SingleOrDefaultAsync(entity => entity.RestaurantId == restaurantId && entity.VendorBillId == vendorBillId, cancellationToken);

        if (bill is null)
        {
            throw new KeyNotFoundException("Vendor bill not found.");
        }

        return bill;
    }

    private async Task<VendorBill> LoadTrackedVendorBillAsync(Guid restaurantId, Guid vendorBillId, CancellationToken cancellationToken)
    {
        var bill = await _context.VendorBills
            .Include(entity => entity.Lines)
            .Include(entity => entity.Settlements)
            .SingleOrDefaultAsync(entity => entity.RestaurantId == restaurantId && entity.VendorBillId == vendorBillId, cancellationToken);

        if (bill is null)
        {
            throw new KeyNotFoundException("Vendor bill not found.");
        }

        return bill;
    }

    private async Task<VendorBillDetail> ToDetailAsync(VendorBill bill, CancellationToken cancellationToken)
    {
        var inventoryItemIds = bill.Lines
            .Where(entity => entity.InventoryItemId.HasValue)
            .Select(entity => entity.InventoryItemId!.Value)
            .Distinct()
            .ToArray();

        var inventoryItemNames = inventoryItemIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await _context.InventoryItems
                .AsNoTracking()
                .Where(entity => inventoryItemIds.Contains(entity.InventoryItemId))
                .Select(entity => new { entity.InventoryItemId, entity.Name })
                .ToDictionaryAsync(entity => entity.InventoryItemId, entity => entity.Name, cancellationToken);

        var vendor = await _context.Vendors
            .AsNoTracking()
            .SingleAsync(entity => entity.RestaurantId == bill.RestaurantId && entity.VendorId == bill.VendorId, cancellationToken);

        return new VendorBillDetail(
            bill.VendorBillId,
            bill.RestaurantId,
            bill.BranchId,
            bill.VendorId,
            vendor.Name,
            vendor.VendorType.ToString(),
            bill.BillNumber,
            bill.BillDate,
            bill.DueDate,
            bill.Status.ToString(),
            bill.TotalAmount,
            bill.PaidAmount,
            bill.BalanceAmount,
            bill.Notes,
            bill.CancelledAtUtc,
            bill.CancelledByUserId,
            bill.CancellationReason,
            bill.CreatedAtUtc,
            bill.UpdatedAtUtc,
            bill.Lines
                .OrderBy(entity => entity.CreatedAtUtc)
                .Select(entity => new VendorBillLineDetail(
                    entity.VendorBillLineId,
                    entity.InventoryItemId,
                    entity.InventoryItemId.HasValue && inventoryItemNames.TryGetValue(entity.InventoryItemId.Value, out var inventoryName) ? inventoryName : null,
                    entity.InventoryMovementId,
                    entity.Description,
                    entity.Quantity,
                    entity.UnitCost,
                    entity.LineTotal,
                    entity.CreatedAtUtc,
                    entity.UpdatedAtUtc))
                .ToArray(),
            bill.Settlements
                .OrderBy(entity => entity.CreatedAtUtc)
                .Select(ToSettlementDetail)
                .ToArray());
    }

    private static VendorListItem ToListItem(Vendor vendor)
    {
        return new VendorListItem(
            vendor.VendorId,
            vendor.RestaurantId,
            vendor.BranchId,
            vendor.Name,
            vendor.NormalizedName,
            vendor.VendorType.ToString(),
            vendor.ContactName,
            vendor.MobileNumber,
            vendor.Address,
            vendor.Notes,
            vendor.IsActive,
            vendor.CreatedAtUtc,
            vendor.UpdatedAtUtc);
    }

    private static VendorDetail ToDetail(Vendor vendor)
    {
        return new VendorDetail(
            vendor.VendorId,
            vendor.RestaurantId,
            vendor.BranchId,
            vendor.Name,
            vendor.NormalizedName,
            vendor.VendorType.ToString(),
            vendor.ContactName,
            vendor.MobileNumber,
            vendor.Address,
            vendor.Notes,
            vendor.IsActive,
            vendor.CreatedAtUtc,
            vendor.UpdatedAtUtc);
    }

    private static VendorBillListItem ToListItem(VendorBill bill, IReadOnlyDictionary<Guid, Vendor> vendors)
    {
        vendors.TryGetValue(bill.VendorId, out var vendor);

        return new VendorBillListItem(
            bill.VendorBillId,
            bill.VendorId,
            bill.BranchId,
            vendor?.Name ?? string.Empty,
            vendor?.VendorType.ToString() ?? string.Empty,
            bill.BillNumber,
            bill.BillDate,
            bill.DueDate,
            bill.Status.ToString(),
            bill.TotalAmount,
            bill.PaidAmount,
            bill.BalanceAmount,
            bill.CreatedAtUtc);
    }

    private static VendorSettlementDetail ToSettlementDetail(VendorSettlement settlement)
    {
        return new VendorSettlementDetail(
            settlement.VendorSettlementId,
            settlement.PaymentMode.ToString(),
            settlement.Status.ToString(),
            settlement.Amount,
            settlement.ReferenceNumber,
            settlement.PaidAtUtc,
            settlement.RecordedByUserId,
            settlement.CreatedAtUtc,
            settlement.UpdatedAtUtc,
            settlement.CancelledAtUtc,
            settlement.CancelledByUserId,
            settlement.CancellationReason,
            settlement.Notes,
            settlement.PreviousOutstandingAmount,
            settlement.NewOutstandingAmount);
    }

    private static void RecalculateBillTotals(VendorBill bill)
    {
        bill.PaidAmount = RoundMoney(bill.Settlements.Where(entity => entity.Status == VendorSettlementStatus.Active).Sum(entity => entity.Amount));
        bill.BalanceAmount = RoundMoney(Math.Max(0m, bill.TotalAmount - bill.PaidAmount));
        bill.Status = bill.BalanceAmount <= 0m
            ? VendorBillStatus.Paid
            : bill.PaidAmount > 0m
                ? VendorBillStatus.PartiallyPaid
                : VendorBillStatus.Unpaid;
    }

    private async Task<InventoryItem> LoadBranchInventoryItemAsync(
        Guid restaurantId,
        Guid branchId,
        Guid inventoryItemId,
        CancellationToken cancellationToken)
    {
        var inventoryItem = await _context.InventoryItems
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.RestaurantId == restaurantId && entity.InventoryItemId == inventoryItemId, cancellationToken);

        if (inventoryItem is null)
        {
            throw new KeyNotFoundException("Inventory item not found.");
        }

        if (inventoryItem.BranchId != branchId)
        {
            throw new InvalidOperationException("Inventory item must belong to the selected branch.");
        }

        return inventoryItem;
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
            UnitOfMeasure = string.IsNullOrWhiteSpace(unitOfMeasure) ? string.Empty : unitOfMeasure.Trim(),
            CreatedAtUtc = createdAtUtc
        };
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

    private async Task<Restaurant> LoadRestaurantAsync(Guid restaurantId, CancellationToken cancellationToken)
    {
        var restaurant = await _context.Restaurants
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.RestaurantId == restaurantId, cancellationToken);

        if (restaurant is null)
        {
            throw new KeyNotFoundException("Restaurant not found.");
        }

        return restaurant;
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

    private static Guid? ResolveBranchFilter(AuthUserContext currentUser, Guid? requestedBranchId)
    {
        if (currentUser.BranchId.HasValue)
        {
            if (requestedBranchId.HasValue && requestedBranchId.Value != currentUser.BranchId.Value)
            {
                throw new UnauthorizedAccessException("Branch access is restricted to the current branch.");
            }

            return currentUser.BranchId.Value;
        }

        return requestedBranchId;
    }

    private static Guid? ResolveVendorBranchScope(AuthUserContext currentUser, Guid? requestedBranchId)
    {
        if (currentUser.BranchId.HasValue)
        {
            if (requestedBranchId.HasValue && requestedBranchId.Value != currentUser.BranchId.Value)
            {
                throw new UnauthorizedAccessException("Branch access is restricted to the current branch.");
            }

            return requestedBranchId ?? currentUser.BranchId.Value;
        }

        return requestedBranchId;
    }

    private static Guid ResolveVendorBillBranchScope(AuthUserContext currentUser, Guid requestedBranchId)
    {
        if (currentUser.BranchId.HasValue && requestedBranchId != currentUser.BranchId.Value)
        {
            throw new UnauthorizedAccessException("Branch access is restricted to the current branch.");
        }

        return requestedBranchId;
    }

    private static void EnsureBranchAccess(AuthUserContext currentUser, Guid branchId)
    {
        if (currentUser.BranchId.HasValue && currentUser.BranchId.Value != branchId)
        {
            throw new UnauthorizedAccessException("Branch access is restricted to the current branch.");
        }
    }

    private static VendorType ResolveVendorType(string? vendorType)
    {
        if (string.IsNullOrWhiteSpace(vendorType) || !Enum.TryParse<VendorType>(vendorType, true, out var parsed))
        {
            throw new InvalidOperationException(BuildAllowedValuesMessage<VendorType>("Vendor type"));
        }

        return parsed;
    }

    private static VendorBillStatus? ResolveVendorBillStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        if (!Enum.TryParse<VendorBillStatus>(status, true, out var parsed))
        {
            throw new InvalidOperationException(BuildAllowedValuesMessage<VendorBillStatus>("Status filter"));
        }

        return parsed;
    }

    private static VendorSettlementPaymentMode ResolveVendorSettlementPaymentMode(string? paymentMode)
    {
        if (string.IsNullOrWhiteSpace(paymentMode) || !Enum.TryParse<VendorSettlementPaymentMode>(paymentMode, true, out var parsed))
        {
            throw new InvalidOperationException(BuildAllowedValuesMessage<VendorSettlementPaymentMode>("Payment mode"));
        }

        return parsed;
    }

    private static void ValidateRequest(CreateVendorRequest request)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Request is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Vendor name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.VendorType))
        {
            throw new InvalidOperationException("Vendor type is required.");
        }

        if (string.IsNullOrWhiteSpace(request.MobileNumber))
        {
            throw new InvalidOperationException("Mobile number is required.");
        }
    }

    private static void ValidateRequest(UpdateVendorRequest request)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Request is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Vendor name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.VendorType))
        {
            throw new InvalidOperationException("Vendor type is required.");
        }

        if (string.IsNullOrWhiteSpace(request.MobileNumber))
        {
            throw new InvalidOperationException("Mobile number is required.");
        }
    }

    private static void ValidateRequest(CreateVendorBillRequest request)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Request is required.");
        }
    }

    private static void ValidateRequest(RecordVendorSettlementRequest request)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Request is required.");
        }
    }

    private static void ValidateRequest(VendorStatementQuery query)
    {
        if (query is null)
        {
            throw new InvalidOperationException("Request is required.");
        }

        if (query.VendorId == Guid.Empty)
        {
            throw new InvalidOperationException("Vendor is required.");
        }
    }

    private static void ValidateRequest(CancelVendorBillRequest request)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Request is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new InvalidOperationException("Cancellation reason is required.");
        }
    }

    private static void ValidateLine(CreateVendorBillLineRequest line)
    {
        if (line is null)
        {
            throw new InvalidOperationException("Vendor bill line is required.");
        }

        if (string.IsNullOrWhiteSpace(line.Description))
        {
            throw new InvalidOperationException("Vendor bill line description is required.");
        }

        if (line.Quantity <= 0m)
        {
            throw new InvalidOperationException("Vendor bill line quantity must be greater than zero.");
        }

        if (line.UnitCost < 0m)
        {
            throw new InvalidOperationException("Vendor bill line unit cost must be zero or greater.");
        }
    }

    private static string NormalizeRequiredText(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string? NormalizeOptionalText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }

    private static string NormalizeLookupKey(string value) =>
        NormalizeRequiredText(value).ToUpperInvariant();

    private static string? NormalizeBillNumber(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static bool MatchesNormalizedValue(string? candidateNormalizedValue, string? candidateRawValue, string requestedNormalizedValue)
    {
        if (!string.IsNullOrWhiteSpace(candidateNormalizedValue) &&
            string.Equals(candidateNormalizedValue, requestedNormalizedValue, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(candidateRawValue))
        {
            return false;
        }

        return string.Equals(candidateRawValue.Trim().ToUpperInvariant(), requestedNormalizedValue, StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime? ResolveDate(DateTime? date)
    {
        if (!date.HasValue)
        {
            return null;
        }

        return DateTime.SpecifyKind(date.Value.Date, DateTimeKind.Utc);
    }

    private static decimal RoundMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

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

    private void AddAudit(
        AuthUserContext actor,
        Restaurant restaurant,
        Branch? branch,
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
            BranchId = branch?.BranchId,
            UserId = actor.UserId == Guid.Empty ? null : actor.UserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Reason = reason,
            OldValueJson = oldValueJson,
            NewValueJson = newValueJson,
            RestaurantNameSnapshot = restaurant.Name,
            BranchNameSnapshot = branch?.Name,
            UserNameSnapshot = actor.FullName,
            UserMobileSnapshot = actor.MobileNumber,
            CreatedAt = createdAt
        });
    }

    private static string Serialize(object? value) => value is null ? string.Empty : JsonSerializer.Serialize(value);

    private static string BuildAllowedValuesMessage<TEnum>(string label)
        where TEnum : struct, Enum
    {
        return $"{label} must be one of: {string.Join(", ", Enum.GetNames<TEnum>())}.";
    }

    private sealed record NormalizedVendorBillLine(
        Guid? InventoryItemId,
        string? InventoryItemName,
        string Description,
        decimal Quantity,
        decimal UnitCost,
        decimal LineTotal);
}
