using System.Data;
using System.Text.Json;
using BillSoft.Application.Auth;
using BillSoft.Application.Cashiering;
using BillSoft.Domain.Auditing;
using BillSoft.Domain.Billing;
using BillSoft.Domain.Cashiering;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Users;
using BillSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillSoft.Infrastructure.Cashiering;

public sealed class CashierShiftService : ICashierShiftService
{
    private readonly BillSoftDbContext _context;

    public CashierShiftService(BillSoftDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<CashierShiftListResponse> ListShiftsAsync(AuthUserContext currentUser, CashierShiftListQuery query, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var businessDate = ResolveBusinessDate(query.BusinessDate);

        var shiftsQuery = _context.CashierShifts
            .AsNoTracking()
            .Where(entity => entity.RestaurantId == restaurantId && entity.BusinessDate == businessDate);

        if (query.BranchId.HasValue)
        {
            await EnsureBranchAccessAsync(currentUser, restaurantId, query.BranchId.Value, cancellationToken);
            shiftsQuery = shiftsQuery.Where(entity => entity.BranchId == query.BranchId.Value);
        }

        var shifts = await shiftsQuery.ToListAsync(cancellationToken);
        shifts = shifts
            .OrderByDescending(entity => entity.OpenedAt)
            .ThenByDescending(entity => entity.CashierShiftId)
            .ToList();

        var branchLookup = await LoadBranchNamesAsync(restaurantId, shifts.Select(entity => entity.BranchId), cancellationToken);
        var userLookup = await LoadUserNamesAsync(restaurantId, shifts.Select(entity => entity.OpenedByUserId), cancellationToken);

        var items = shifts
            .Select(entity => ToListItem(entity, ResolveBranchName(branchLookup, entity.BranchId), ResolveUserName(userLookup, entity.OpenedByUserId)))
            .ToArray();

        return new CashierShiftListResponse(items);
    }

    public async Task<CashierShiftDetail?> GetCurrentShiftAsync(AuthUserContext currentUser, Guid branchId, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var cashierUserId = RequireUserId(currentUser);
        await EnsureBranchAccessAsync(currentUser, restaurantId, branchId, cancellationToken);
        _ = await LoadBranchAsync(restaurantId, branchId, requireActive: false, cancellationToken);

        var shift = await LoadOpenShiftAsync(restaurantId, branchId, cashierUserId, cancellationToken);
        if (shift is null)
        {
            return null;
        }

        return await LoadShiftDetailAsync(currentUser, shift.CashierShiftId, cancellationToken);
    }

    public async Task<CashierShiftDetail> GetShiftAsync(AuthUserContext currentUser, Guid shiftId, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var shift = await LoadTrackedShiftAsync(restaurantId, shiftId, cancellationToken);
        await EnsureBranchAccessAsync(currentUser, restaurantId, shift.BranchId, cancellationToken);
        return await LoadShiftDetailAsync(currentUser, shift.CashierShiftId, cancellationToken);
    }

    public async Task<CashierShiftDetail> OpenShiftAsync(AuthUserContext currentUser, OpenCashierShiftRequest request, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        ValidateRequest(request);

        if (request.OpeningCashAmount < 0)
        {
            throw new InvalidOperationException("Opening cash amount must be greater than or equal to zero.");
        }

        var businessDate = ResolveBusinessDate(request.BusinessDate);
        var cashierUserId = RequireUserId(currentUser);
        await EnsureBranchAccessAsync(currentUser, restaurantId, request.BranchId, cancellationToken);

        var now = DateTimeOffset.UtcNow;

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
            var branch = await LoadBranchAsync(restaurantId, request.BranchId, requireActive: true, cancellationToken);
            await EnsureNoOpenShiftAsync(restaurantId, request.BranchId, cashierUserId, cancellationToken);

            var shift = new CashierShift
            {
                RestaurantId = restaurantId,
                BranchId = branch.BranchId,
                OpenedByUserId = cashierUserId,
                BusinessDate = businessDate,
                Status = CashierShiftStatus.Open,
                OpeningCashAmount = RoundMoney(request.OpeningCashAmount),
                ExpectedCashAmount = RoundMoney(request.OpeningCashAmount),
                OpenedAt = now,
                CreatedAt = now
            };

            _context.CashierShifts.Add(shift);

            await _context.SaveChangesAsync(cancellationToken);

            var detail = await LoadShiftDetailAsync(currentUser, shift.CashierShiftId, cancellationToken);
            AddAudit(
                actor: currentUser,
                restaurant: restaurant,
                branch: branch,
                action: "CashierShift.Opened",
                reason: "Shift opened.",
                entityType: "CashierShift",
                entityId: shift.CashierShiftId.ToString(),
                oldValueJson: null,
                newValueJson: Serialize(detail),
                createdAt: now);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return detail;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<CashierShiftDetail> RecordMovementAsync(AuthUserContext currentUser, Guid shiftId, RecordCashDrawerMovementRequest request, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        ValidateRequest(request);

        var movementType = ResolveMovementType(request.MovementType);
        var reason = NormalizeRequiredText(request.Reason, "Reason is required.");
        var amount = RoundMoney(request.Amount);

        if (movementType != CashDrawerMovementType.Adjustment && amount <= 0)
        {
            throw new InvalidOperationException("Movement amount must be greater than zero.");
        }

        if (movementType == CashDrawerMovementType.Adjustment && amount == 0)
        {
            throw new InvalidOperationException("Adjustment amount cannot be zero.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            var shift = await LoadTrackedShiftAsync(restaurantId, shiftId, cancellationToken);
            if (shift.Status != CashierShiftStatus.Open)
            {
                throw new InvalidOperationException("Cashier shift is closed.");
            }

            var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
            var branch = await LoadBranchAsync(restaurantId, shift.BranchId, requireActive: false, cancellationToken);
            var before = await LoadShiftDetailAsync(shift.CashierShiftId, cancellationToken);
            var now = DateTimeOffset.UtcNow;

            var effect = ResolveCashEffect(movementType, amount);
            shift.ExpectedCashAmount = RoundMoney(shift.ExpectedCashAmount + effect);
            shift.UpdatedAt = now;

            var movement = new CashDrawerMovement
            {
                RestaurantId = restaurantId,
                BranchId = branch.BranchId,
                CashierShiftId = shift.CashierShiftId,
                MovementType = movementType,
                Amount = movementType == CashDrawerMovementType.Adjustment ? amount : RoundMoney(amount),
                Reason = reason,
                CreatedByUserId = RequireUserId(currentUser),
                CreatedAt = now
            };

            _context.CashDrawerMovements.Add(movement);
            await _context.SaveChangesAsync(cancellationToken);

            var after = await LoadShiftDetailAsync(shift.CashierShiftId, cancellationToken);
            AddAudit(
                actor: currentUser,
                restaurant: restaurant,
                branch: branch,
                action: "CashDrawerMovement.Recorded",
                reason: reason,
                entityType: "CashierShift",
                entityId: shift.CashierShiftId.ToString(),
                oldValueJson: Serialize(before),
                newValueJson: Serialize(after),
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

    public async Task<CashierShiftDetail> CloseShiftAsync(AuthUserContext currentUser, Guid shiftId, CloseCashierShiftRequest request, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        ValidateRequest(request);

        if (request.DeclaredClosingCashAmount < 0)
        {
            throw new InvalidOperationException("Declared closing cash amount must be greater than or equal to zero.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            var shift = await LoadTrackedShiftAsync(restaurantId, shiftId, cancellationToken);
            if (shift.Status != CashierShiftStatus.Open)
            {
                throw new InvalidOperationException("Cashier shift is already closed.");
            }

            await EnsureBranchAccessAsync(currentUser, restaurantId, shift.BranchId, cancellationToken);

            var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
            var branch = await LoadBranchAsync(restaurantId, shift.BranchId, requireActive: false, cancellationToken);
            var before = await LoadShiftDetailAsync(currentUser, shift.CashierShiftId, cancellationToken);
            var now = DateTimeOffset.UtcNow;

            shift.Status = CashierShiftStatus.Closed;
            shift.CountedCashAmount = RoundMoney(request.DeclaredClosingCashAmount);
            shift.CashVarianceAmount = RoundMoney(shift.CountedCashAmount.Value - shift.ExpectedCashAmount);
            shift.ClosingNote = NormalizeOptionalText(request.CloseNotes, 500);
            shift.ClosedByUserId = RequireUserId(currentUser);
            shift.ClosedAt = now;
            shift.UpdatedAt = now;

            await _context.SaveChangesAsync(cancellationToken);

            var after = await LoadShiftDetailAsync(currentUser, shift.CashierShiftId, cancellationToken);
            AddAudit(
                actor: currentUser,
                restaurant: restaurant,
                branch: branch,
                action: "CashierShift.Closed",
                reason: shift.ClosingNote ?? "Shift closed.",
                entityType: "CashierShift",
                entityId: shift.CashierShiftId.ToString(),
                oldValueJson: Serialize(before),
                newValueJson: Serialize(after),
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

    private async Task<CashierShift?> LoadOpenShiftAsync(Guid restaurantId, Guid branchId, Guid cashierUserId, CancellationToken cancellationToken)
    {
        var shift = await _context.CashierShifts
            .AsNoTracking()
            .SingleOrDefaultAsync(entity =>
                entity.RestaurantId == restaurantId &&
                entity.BranchId == branchId &&
                entity.OpenedByUserId == cashierUserId &&
                entity.Status == CashierShiftStatus.Open,
                cancellationToken);

        return shift;
    }

    private async Task EnsureNoOpenShiftAsync(Guid restaurantId, Guid branchId, Guid cashierUserId, CancellationToken cancellationToken)
    {
        var openShiftExists = await _context.CashierShifts
            .AsNoTracking()
            .AnyAsync(entity =>
                entity.RestaurantId == restaurantId &&
                entity.BranchId == branchId &&
                entity.OpenedByUserId == cashierUserId &&
                entity.Status == CashierShiftStatus.Open,
                cancellationToken);

        if (openShiftExists)
        {
            throw new InvalidOperationException("An open cashier shift already exists for this cashier and branch.");
        }
    }

    private async Task<CashierShift> LoadTrackedShiftAsync(Guid restaurantId, Guid shiftId, CancellationToken cancellationToken)
    {
        var shift = await _context.CashierShifts
            .Include(entity => entity.CashDrawerMovements)
            .Include(entity => entity.Payments)
            .SingleOrDefaultAsync(entity => entity.CashierShiftId == shiftId && entity.RestaurantId == restaurantId, cancellationToken);

        if (shift is null)
        {
            throw new KeyNotFoundException("Cashier shift not found.");
        }

        return shift;
    }

    private async Task<CashierShiftDetail> LoadShiftDetailAsync(AuthUserContext currentUser, Guid shiftId, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var shift = await _context.CashierShifts
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.CashierShiftId == shiftId && entity.RestaurantId == restaurantId, cancellationToken);

        if (shift is null)
        {
            throw new KeyNotFoundException("Cashier shift not found.");
        }

        await EnsureBranchAccessAsync(currentUser, restaurantId, shift.BranchId, cancellationToken);
        return await ToDetailAsync(shift, cancellationToken);
    }

    private async Task<CashierShiftDetail> LoadShiftDetailAsync(Guid restaurantId, Guid shiftId, CancellationToken cancellationToken)
    {
        var shift = await _context.CashierShifts
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.CashierShiftId == shiftId && entity.RestaurantId == restaurantId, cancellationToken);

        if (shift is null)
        {
            throw new KeyNotFoundException("Cashier shift not found.");
        }

        return await ToDetailAsync(shift, cancellationToken);
    }

    private async Task<CashierShiftDetail> LoadShiftDetailAsync(Guid shiftId, CancellationToken cancellationToken)
    {
        var shift = await _context.CashierShifts
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.CashierShiftId == shiftId, cancellationToken);

        if (shift is null)
        {
            throw new KeyNotFoundException("Cashier shift not found.");
        }

        return await ToDetailAsync(shift, cancellationToken);
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

    private async Task EnsureBranchAccessAsync(AuthUserContext currentUser, Guid restaurantId, Guid branchId, CancellationToken cancellationToken)
    {
        if (!currentUser.BranchId.HasValue)
        {
            return;
        }

        if (currentUser.BranchId.Value == branchId)
        {
            return;
        }

        var branch = await _context.Branches
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.BranchId == currentUser.BranchId.Value && entity.RestaurantId == restaurantId, cancellationToken);

        if (branch is null)
        {
            throw new UnauthorizedAccessException("Branch access is required.");
        }

        throw new UnauthorizedAccessException("Branch access is required.");
    }

    private async Task<Dictionary<Guid, string>> LoadBranchNamesAsync(Guid restaurantId, IEnumerable<Guid> branchIds, CancellationToken cancellationToken)
    {
        var ids = branchIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await _context.Branches
            .AsNoTracking()
            .Where(entity => entity.RestaurantId == restaurantId && ids.Contains(entity.BranchId))
            .ToDictionaryAsync(entity => entity.BranchId, entity => entity.Name, cancellationToken);
    }

    private async Task<Dictionary<Guid, string>> LoadUserNamesAsync(Guid restaurantId, IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await _context.Users
            .AsNoTracking()
            .Where(entity => entity.RestaurantId == restaurantId && ids.Contains(entity.UserId))
            .ToDictionaryAsync(entity => entity.UserId, entity => entity.FullName, cancellationToken);
    }

    private static string ResolveBranchName(IReadOnlyDictionary<Guid, string> lookup, Guid branchId) =>
        lookup.TryGetValue(branchId, out var name) ? name : branchId.ToString();

    private static string ResolveUserName(IReadOnlyDictionary<Guid, string> lookup, Guid userId) =>
        lookup.TryGetValue(userId, out var name) ? name : userId.ToString();

    private static DateTime ResolveBusinessDate(DateTime? businessDate)
    {
        var resolved = (businessDate ?? DateTime.UtcNow).Date;
        return DateTime.SpecifyKind(resolved, DateTimeKind.Unspecified);
    }

    private static CashierShiftDetail ToDetail(CashierShift shift, string branchName, string cashierName)
    {
        return new CashierShiftDetail(
            shift.CashierShiftId,
            shift.RestaurantId,
            shift.BranchId,
            shift.CashierUserId,
            cashierName,
            branchName,
            shift.BusinessDate,
            shift.Status.ToString(),
            shift.OpenedAtUtc,
            shift.OpeningCashAmount,
            shift.ClosedAtUtc,
            shift.DeclaredClosingCashAmount,
            shift.ExpectedClosingCashAmount,
            shift.CashVarianceAmount,
            shift.CloseNotes,
            shift.CreatedAtUtc,
            shift.UpdatedAtUtc);
    }

    private async Task<CashierShiftDetail> ToDetailAsync(CashierShift shift, CancellationToken cancellationToken)
    {
        var branch = await LoadBranchAsync(shift.RestaurantId, shift.BranchId, requireActive: false, cancellationToken);
        var user = await LoadUserAsync(shift.RestaurantId, shift.CashierUserId, cancellationToken);
        return ToDetail(shift, branch.Name, user.FullName);
    }

    private async Task<User> LoadUserAsync(Guid restaurantId, Guid userId, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.UserId == userId && entity.RestaurantId == restaurantId, cancellationToken);

        if (user is null)
        {
            throw new KeyNotFoundException("User not found.");
        }

        return user;
    }

    private static CashierShiftListItem ToListItem(CashierShift shift, string branchName, string cashierName) =>
        new(
            shift.CashierShiftId,
            shift.RestaurantId,
            shift.BranchId,
            shift.CashierUserId,
            cashierName,
            branchName,
            shift.BusinessDate,
            shift.Status.ToString(),
            shift.OpenedAtUtc,
            shift.OpeningCashAmount,
            shift.ClosedAtUtc,
            shift.DeclaredClosingCashAmount,
            shift.ExpectedClosingCashAmount,
            shift.CashVarianceAmount,
            shift.CloseNotes,
            shift.CreatedAtUtc,
            shift.UpdatedAtUtc);

    private static CashDrawerMovementType ResolveMovementType(string? movementType)
    {
        if (string.IsNullOrWhiteSpace(movementType))
        {
            throw new InvalidOperationException(BuildAllowedValuesMessage<CashDrawerMovementType>("Movement type"));
        }

        if (!Enum.TryParse<CashDrawerMovementType>(movementType, true, out var parsed))
        {
            throw new InvalidOperationException(BuildAllowedValuesMessage<CashDrawerMovementType>("Movement type"));
        }

        return parsed;
    }

    private static CashierShiftStatus? ResolveShiftStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        if (!Enum.TryParse<CashierShiftStatus>(status, true, out var parsed))
        {
            throw new InvalidOperationException(BuildAllowedValuesMessage<CashierShiftStatus>("Status filter"));
        }

        return parsed;
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

    private static void ValidateRequest(OpenCashierShiftRequest request)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Request is required.");
        }
    }

    private static void ValidateRequest(RecordCashDrawerMovementRequest request)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Request is required.");
        }
    }

    private static void ValidateRequest(CloseCashierShiftRequest request)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Request is required.");
        }
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
            throw new InvalidOperationException("Authentication context is missing the user id.");
        }

        return currentUser.UserId;
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

    private static string NormalizeRequiredText(string? value, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(errorMessage);
        }

        return value.Trim();
    }

    private static DateTimeOffset ResolveStartOfDay(DateTime date)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(date.Date, DateTimeKind.Utc));
    }

    private static DateTimeOffset ResolveExclusiveEndOfDay(DateTime date)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(date.Date, DateTimeKind.Utc)).AddDays(1);
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

    private sealed record RestaurantSnapshot(Guid RestaurantId, string Name);
}
