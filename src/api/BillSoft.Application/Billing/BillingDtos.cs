namespace BillSoft.Application.Billing;

public sealed record BillListQuery(
    Guid? BranchId,
    DateTime? BusinessDate,
    string? Status,
    DateTime? From,
    DateTime? To,
    string? Search);

public sealed record BillListResponse(IReadOnlyCollection<BillListItem> Items);

public sealed record BillListItem(
    Guid BillId,
    Guid BranchId,
    Guid PosOrderId,
    string BillNumber,
    DateTime BusinessDate,
    string Status,
    decimal GrandTotal,
    decimal AmountPaid,
    decimal BalanceDue,
    DateTimeOffset CreatedAt);

public sealed record BillDetail(
    Guid BillId,
    Guid RestaurantId,
    Guid BranchId,
    Guid PosOrderId,
    string BillNumber,
    DateTime BusinessDate,
    string Status,
    decimal Subtotal,
    decimal TaxTotal,
    decimal GrandTotal,
    decimal AmountPaid,
    decimal BalanceDue,
    Guid? CreatedByUserId,
    Guid? CancelledByUserId,
    DateTimeOffset? CancelledAt,
    string? CancelReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyCollection<BillLineDetail> Lines,
    IReadOnlyCollection<PaymentDetail> Payments);

public sealed record BillReceiptResponse(
    Guid BillId,
    Guid RestaurantId,
    Guid BranchId,
    string RestaurantCode,
    string CountryCode,
    string CurrencyCode,
    string TimeZoneId,
    string RestaurantName,
    string BranchName,
    string? BranchAddress,
    Guid PosOrderId,
    DateTime BusinessDate,
    string? OrderNumberSnapshot,
    string? OrderTypeSnapshot,
    string? OrderTableNameSnapshot,
    string? OrderCustomerNameSnapshot,
    string? OrderCustomerMobileSnapshot,
    string BillNumber,
    string Status,
    Guid? CreatedByUserId,
    string CreatedByUserLabel,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset PrintedAt,
    Guid? CancelledByUserId,
    DateTimeOffset? CancelledAt,
    string? CancelReason,
    decimal Subtotal,
    decimal TaxTotal,
    decimal GrandTotal,
    decimal AmountPaid,
    decimal BalanceDue,
    int PrintCount,
    bool IsReprint,
    IReadOnlyCollection<BillReceiptLine> Lines,
    IReadOnlyCollection<BillReceiptPayment> Payments);

public sealed record BillReceiptLine(
    int DisplayOrder,
    string MenuItemNameSnapshot,
    string MenuCategoryNameSnapshot,
    string? SkuSnapshot,
    decimal Quantity,
    string? Notes,
    decimal UnitPrice,
    decimal LineSubtotal,
    decimal LineTax,
    decimal LineTotal);

public sealed record BillReceiptPayment(
    string PaymentNumber,
    string PaymentMode,
    string Status,
    decimal Amount,
    string? ReferenceNumber,
    string? Notes,
    Guid? RecordedByUserId,
    string RecordedByUserLabel,
    DateTimeOffset CreatedAt);

public sealed record BillLineDetail(
    Guid BillLineId,
    Guid PosOrderLineId,
    Guid MenuItemId,
    Guid MenuCategoryId,
    string MenuItemNameSnapshot,
    string MenuCategoryNameSnapshot,
    string? SkuSnapshot,
    decimal UnitPrice,
    decimal TaxRate,
    decimal Quantity,
    decimal LineSubtotal,
    decimal LineTax,
    decimal LineTotal,
    string? Notes,
    int DisplayOrder,
    DateTimeOffset CreatedAt);

public sealed record PaymentDetail(
    Guid PaymentId,
    Guid BillId,
    Guid BranchId,
    string PaymentNumber,
    string PaymentMode,
    string Status,
    decimal Amount,
    string? ReferenceNumber,
    string? Notes,
    Guid? RecordedByUserId,
    Guid? CancelledByUserId,
    DateTimeOffset? CancelledAt,
    string? CancelReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CreateBillRequest(Guid PosOrderId);

public sealed record RecordBillReceiptPrintEventRequest(string? Reason);

public sealed record CancelBillRequest(string? Reason);

public sealed record RecordPaymentRequest(
    string? PaymentMode,
    decimal Amount,
    string? ReferenceNumber,
    string? Notes);

public sealed record CancelPaymentRequest(string? Reason);
