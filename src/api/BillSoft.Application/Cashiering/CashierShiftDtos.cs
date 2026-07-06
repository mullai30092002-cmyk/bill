namespace BillSoft.Application.Cashiering;

public sealed record CashierShiftListQuery(
    DateTime? BusinessDate,
    Guid? BranchId);

public sealed record CashierShiftListResponse(IReadOnlyCollection<CashierShiftListItem> Items);

public sealed record CashierShiftListItem(
    Guid CashierShiftId,
    Guid RestaurantId,
    Guid BranchId,
    Guid CashierUserId,
    string CashierName,
    string BranchName,
    DateTime BusinessDate,
    string Status,
    DateTimeOffset OpenedAtUtc,
    decimal OpeningCashAmount,
    DateTimeOffset? ClosedAtUtc,
    decimal? DeclaredClosingCashAmount,
    decimal ExpectedClosingCashAmount,
    decimal? CashVarianceAmount,
    string? CloseNotes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

public sealed record CashierShiftDetail(
    Guid CashierShiftId,
    Guid RestaurantId,
    Guid BranchId,
    Guid CashierUserId,
    string CashierName,
    string BranchName,
    DateTime BusinessDate,
    string Status,
    DateTimeOffset OpenedAtUtc,
    decimal OpeningCashAmount,
    DateTimeOffset? ClosedAtUtc,
    decimal? DeclaredClosingCashAmount,
    decimal ExpectedClosingCashAmount,
    decimal? CashVarianceAmount,
    string? CloseNotes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

public sealed record OpenCashierShiftRequest(
    Guid BranchId,
    DateTime BusinessDate,
    decimal OpeningCashAmount);

public sealed record CloseCashierShiftRequest(
    decimal DeclaredClosingCashAmount,
    string? CloseNotes);

public sealed record RecordCashDrawerMovementRequest(
    string? MovementType,
    decimal Amount,
    string? Reason);

public sealed record CashDrawerMovementDetail(
    Guid CashDrawerMovementId,
    Guid CashierShiftId,
    string MovementType,
    decimal Amount,
    string Reason,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt);
