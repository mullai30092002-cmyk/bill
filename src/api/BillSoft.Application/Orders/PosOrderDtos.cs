using BillSoft.Domain.Orders;

namespace BillSoft.Application.Orders;

public sealed record PosOrderListQuery(
    Guid? BranchId,
    string? Status,
    string? OrderType,
    DateTime? From,
    DateTime? To,
    string? Search);

public sealed record PosOrderListResponse(IReadOnlyCollection<PosOrderListItem> Items);

public sealed record PosOrderListItem(
    Guid PosOrderId,
    Guid BranchId,
    string OrderNumber,
    string OrderType,
    string Status,
    string? TableName,
    string? CustomerName,
    decimal GrandTotal,
    int LineCount,
    DateTimeOffset CreatedAt);

public sealed record PosOrderDetail(
    Guid PosOrderId,
    Guid RestaurantId,
    Guid BranchId,
    string OrderNumber,
    string OrderType,
    string Status,
    string? TableName,
    string? CustomerName,
    string? CustomerMobile,
    string? Notes,
    decimal Subtotal,
    decimal TaxTotal,
    decimal GrandTotal,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? CancelledAt,
    string? CancelReason,
    Guid? CreatedByUserId,
    Guid? ConfirmedByUserId,
    Guid? CancelledByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyCollection<PosOrderLineDetail> Lines,
    Guid? KitchenTicketId = null,
    string? KitchenTicketNumber = null,
    string? KitchenTicketStatus = null);

public sealed record PosOrderLineDetail(
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
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record PosOrderLineRequest(
    Guid MenuItemId,
    decimal Quantity,
    string? Notes);

public sealed record CreatePosOrderRequest(
    Guid BranchId,
    string? OrderType,
    string? TableName,
    string? CustomerName,
    string? CustomerMobile,
    string? Notes,
    IReadOnlyCollection<PosOrderLineRequest>? Lines);

public sealed record UpdatePosOrderRequest(
    string? OrderType,
    string? TableName,
    string? CustomerName,
    string? CustomerMobile,
    string? Notes,
    IReadOnlyCollection<PosOrderLineRequest>? Lines);

public sealed record CancelPosOrderRequest(string? Reason);
