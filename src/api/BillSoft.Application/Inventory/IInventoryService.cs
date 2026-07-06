using BillSoft.Application.Auth;

namespace BillSoft.Application.Inventory;

public interface IInventoryService
{
    Task<InventoryItemListResponse> ListItemsAsync(AuthUserContext currentUser, InventoryItemListQuery query, CancellationToken cancellationToken);

    Task<InventorySummaryResponse> GetSummaryAsync(AuthUserContext currentUser, InventorySummaryQuery query, CancellationToken cancellationToken);

    Task<InventoryItemListItem> CreateItemAsync(AuthUserContext currentUser, CreateInventoryItemRequest request, CancellationToken cancellationToken);

    Task<InventoryItemListItem> UpdateItemAsync(AuthUserContext currentUser, Guid itemId, UpdateInventoryItemRequest request, CancellationToken cancellationToken);

    Task<InventoryMovementListResponse> ListMovementsAsync(AuthUserContext currentUser, Guid itemId, CancellationToken cancellationToken);

    Task<InventoryMovementItem> RecordMovementAsync(AuthUserContext currentUser, Guid itemId, CreateInventoryMovementRequest request, CancellationToken cancellationToken);

    Task<BatchProductionListResponse> ListBatchProductionsAsync(AuthUserContext currentUser, BatchProductionListQuery query, CancellationToken cancellationToken);

    Task<BatchProductionDetail> CreateBatchProductionAsync(AuthUserContext currentUser, CreateBatchProductionRequest request, CancellationToken cancellationToken);

    Task<InventoryMovementItem> RecordPreparedStockWastageAsync(AuthUserContext currentUser, RecordPreparedStockWastageRequest request, CancellationToken cancellationToken);
}
