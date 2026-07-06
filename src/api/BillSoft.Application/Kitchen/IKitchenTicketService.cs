using BillSoft.Application.Auth;

namespace BillSoft.Application.Kitchen;

public interface IKitchenTicketService
{
    Task<KitchenTicketListResponse> ListAsync(AuthUserContext currentUser, KitchenTicketListQuery query, CancellationToken cancellationToken);

    Task<KitchenTicketDetail> GetAsync(AuthUserContext currentUser, Guid ticketId, CancellationToken cancellationToken);

    Task<KitchenTicketDetail> CreateAsync(AuthUserContext currentUser, CreateKitchenTicketRequest request, CancellationToken cancellationToken);

    Task<KitchenTicketDetail> UpdateStatusAsync(AuthUserContext currentUser, Guid ticketId, UpdateKitchenTicketStatusRequest request, CancellationToken cancellationToken);

    Task<KitchenTicketDetail> CancelAsync(AuthUserContext currentUser, Guid ticketId, CancelKitchenTicketRequest request, CancellationToken cancellationToken);

    Task<KitchenTicketDeductionPreviewResponse> GetDeductionPreviewAsync(AuthUserContext currentUser, Guid ticketId, CancellationToken cancellationToken);
}
