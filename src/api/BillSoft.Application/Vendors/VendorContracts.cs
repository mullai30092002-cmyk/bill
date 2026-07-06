namespace BillSoft.Application.Vendors;

public sealed record VendorListQuery(Guid? BranchId);

public sealed record VendorListItem(
    Guid VendorId,
    Guid RestaurantId,
    Guid? BranchId,
    string Name,
    string NormalizedName,
    string VendorType,
    string? ContactName,
    string? MobileNumber,
    string? Address,
    string? Notes,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

public sealed record VendorListResponse(IReadOnlyCollection<VendorListItem> Items);

public sealed record VendorDetail(
    Guid VendorId,
    Guid RestaurantId,
    Guid? BranchId,
    string Name,
    string NormalizedName,
    string VendorType,
    string? ContactName,
    string? MobileNumber,
    string? Address,
    string? Notes,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

public sealed record CreateVendorRequest(
    Guid? BranchId,
    string? Name,
    string? VendorType,
    string? ContactName,
    string? MobileNumber,
    string? Address,
    string? Notes,
    bool IsActive);

public sealed record UpdateVendorRequest(
    Guid? BranchId,
    string? Name,
    string? VendorType,
    string? ContactName,
    string? MobileNumber,
    string? Address,
    string? Notes,
    bool IsActive);

public sealed record VendorBillListQuery(
    Guid? BranchId,
    DateTime? FromDate,
    DateTime? ToDate,
    string? Status);

public sealed record VendorBillListItem(
    Guid VendorBillId,
    Guid VendorId,
    Guid BranchId,
    string VendorName,
    string VendorType,
    string? BillNumber,
    DateTime BillDate,
    DateTime? DueDate,
    string Status,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal BalanceAmount,
    DateTimeOffset CreatedAtUtc);

public sealed record VendorBillListResponse(IReadOnlyCollection<VendorBillListItem> Items);

public sealed record VendorBillLineDetail(
    Guid VendorBillLineId,
    Guid? InventoryItemId,
    string? InventoryItemName,
    Guid? InventoryMovementId,
    string Description,
    decimal Quantity,
    decimal UnitCost,
    decimal LineTotal,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

public sealed record VendorSettlementDetail(
    Guid VendorSettlementId,
    string PaymentMode,
    string Status,
    decimal Amount,
    string? ReferenceNumber,
    DateTimeOffset PaidAtUtc,
    Guid RecordedByUserId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    DateTimeOffset? CancelledAtUtc,
    Guid? CancelledByUserId,
    string? CancellationReason,
    string? Notes,
    decimal PreviousOutstandingAmount,
    decimal NewOutstandingAmount);

public sealed record VendorBillDetail(
    Guid VendorBillId,
    Guid RestaurantId,
    Guid BranchId,
    Guid VendorId,
    string VendorName,
    string VendorType,
    string? BillNumber,
    DateTime BillDate,
    DateTime? DueDate,
    string Status,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal BalanceAmount,
    string? Notes,
    DateTimeOffset? CancelledAtUtc,
    Guid? CancelledByUserId,
    string? CancellationReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    IReadOnlyCollection<VendorBillLineDetail> Lines,
    IReadOnlyCollection<VendorSettlementDetail> Settlements);

public sealed record CreateVendorBillLineRequest(
    Guid? InventoryItemId,
    string? Description,
    decimal Quantity,
    decimal UnitCost);

public sealed record CreateVendorBillRequest(
    Guid VendorId,
    Guid BranchId,
    string? BillNumber,
    DateTime BillDate,
    DateTime? DueDate,
    string? Notes,
    IReadOnlyCollection<CreateVendorBillLineRequest>? Lines);

public sealed record RecordVendorSettlementRequest(
    string? PaymentMode,
    decimal Amount,
    string? ReferenceNumber,
    DateTimeOffset? PaidAtUtc,
    string? Notes);

public sealed record CancelVendorBillRequest(string? Reason);

public sealed record VendorStatementQuery(
    Guid VendorId,
    Guid? BranchId,
    DateTime? FromDate,
    DateTime? ToDate);

public sealed record VendorStatementResponse(
    Guid RestaurantId,
    Guid? BranchId,
    string? BranchName,
    Guid VendorId,
    string VendorName,
    string VendorType,
    string CurrencyCode,
    DateTime FromDate,
    DateTime ToDate,
    DateTimeOffset GeneratedAt,
    decimal OpeningOutstandingAmount,
    decimal CurrentOutstandingAmount,
    VendorStatementSummary Summary,
    IReadOnlyCollection<VendorStatementBillItem> PayableBills,
    IReadOnlyCollection<VendorStatementSettlementItem> Settlements,
    IReadOnlyCollection<VendorStatementTimelineItem> Timeline);

public sealed record VendorStatementSummary(
    decimal TotalBillAmount,
    decimal TotalSettlementAmount,
    int PayableBillCount,
    int SettlementCount,
    int OverdueBillCount);

public sealed record VendorStatementBillItem(
    Guid VendorBillId,
    Guid BranchId,
    string? BranchName,
    string? BillNumber,
    DateTime BillDate,
    DateTime? DueDate,
    string Status,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    string? Notes,
    DateTimeOffset CreatedAtUtc);

public sealed record VendorStatementSettlementItem(
    Guid VendorSettlementId,
    Guid VendorBillId,
    Guid BranchId,
    string? BranchName,
    string? BillNumber,
    DateTimeOffset PaidAtUtc,
    string PaymentMode,
    decimal Amount,
    string? ReferenceNumberMasked,
    string? Notes,
    decimal PreviousOutstandingAmount,
    decimal NewOutstandingAmount,
    string Status);

public sealed record VendorStatementTimelineItem(
    string EntryType,
    DateTimeOffset TimestampUtc,
    string? BillNumber,
    string? Reference,
    string? Description,
    decimal DebitAmount,
    decimal CreditAmount,
    decimal RunningBalance,
    string? PaymentMode,
    string? Status);
