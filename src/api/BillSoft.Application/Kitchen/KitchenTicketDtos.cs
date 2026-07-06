namespace BillSoft.Application.Kitchen;

public sealed record KitchenTicketListQuery(
    Guid? BranchId,
    string? Status,
    DateTime? From,
    DateTime? To);

public sealed record KitchenTicketListResponse(IReadOnlyCollection<KitchenTicketListItem> Items);

public sealed record KitchenTicketListItem(
    Guid KitchenTicketId,
    Guid BranchId,
    Guid PosOrderId,
    string TicketNumber,
    string OrderNumberSnapshot,
    string OrderTypeSnapshot,
    string? TableNameSnapshot,
    string? CustomerNameSnapshot,
    string? OrderNotesSnapshot,
    string Status,
    int LineCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? CancelledAt,
    string? CancelReason);

public sealed record KitchenTicketDetail(
    Guid KitchenTicketId,
    Guid RestaurantId,
    Guid BranchId,
    Guid PosOrderId,
    string TicketNumber,
    string OrderNumberSnapshot,
    string OrderTypeSnapshot,
    string? TableNameSnapshot,
    string? CustomerNameSnapshot,
    string? OrderNotesSnapshot,
    string Status,
    Guid? CreatedByUserId,
    Guid? LastStatusChangedByUserId,
    Guid? CancelledByUserId,
    DateTimeOffset? CancelledAt,
    string? CancelReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? PreparingAt,
    DateTimeOffset? ReadyAt,
    DateTimeOffset? ServedAt,
    string InventoryDeductionStatus,
    IReadOnlyCollection<KitchenTicketLineDetail> Lines);

public sealed record KitchenTicketLineDetail(
    Guid KitchenTicketLineId,
    Guid PosOrderLineId,
    Guid MenuItemId,
    Guid MenuCategoryId,
    string MenuItemNameSnapshot,
    string MenuCategoryNameSnapshot,
    string? SkuSnapshot,
    decimal Quantity,
    string? Notes,
    int DisplayOrder,
    DateTimeOffset CreatedAt);

public sealed record CreateKitchenTicketRequest(Guid PosOrderId);

public sealed record UpdateKitchenTicketStatusRequest(string? Status);

public sealed record CancelKitchenTicketRequest(string? Reason);

public sealed record KitchenTicketDeductionPreviewResponse(
    Guid KitchenTicketId,
    bool CanComplete,
    IReadOnlyCollection<KitchenTicketDeductionPreviewLine> Lines);

public sealed record KitchenTicketDeductionPreviewLine(
    string MenuItemName,
    string? InventoryItemName,
    decimal RequiredQuantity,
    decimal AvailableQuantity,
    decimal ResultingQuantity,
    string Status);
