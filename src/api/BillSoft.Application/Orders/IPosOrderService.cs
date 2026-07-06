using BillSoft.Application.Auth;

namespace BillSoft.Application.Orders;

public interface IPosOrderService
{
    Task<PosOrderListResponse> ListAsync(AuthUserContext currentUser, PosOrderListQuery query, CancellationToken cancellationToken);

    Task<PosOrderDetail> GetAsync(AuthUserContext currentUser, Guid orderId, CancellationToken cancellationToken);

    Task<PosOrderDetail> CreateAsync(AuthUserContext currentUser, CreatePosOrderRequest request, CancellationToken cancellationToken);

    Task<PosOrderDetail> UpdateAsync(AuthUserContext currentUser, Guid orderId, UpdatePosOrderRequest request, CancellationToken cancellationToken);

    Task<PosOrderDetail> ConfirmAsync(AuthUserContext currentUser, Guid orderId, CancellationToken cancellationToken);

    Task<PosOrderDetail> CancelAsync(AuthUserContext currentUser, Guid orderId, CancelPosOrderRequest request, CancellationToken cancellationToken);
}
