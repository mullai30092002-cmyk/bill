using BillSoft.Application.Auth;

namespace BillSoft.Application.Billing;

public interface IBillingService
{
    Task<BillListResponse> ListBillsAsync(AuthUserContext currentUser, BillListQuery query, CancellationToken cancellationToken);

    Task<BillDetail> GetBillAsync(AuthUserContext currentUser, Guid billId, CancellationToken cancellationToken);

    Task<BillReceiptResponse> GetBillReceiptAsync(AuthUserContext currentUser, Guid billId, CancellationToken cancellationToken);

    Task<BillDetail> CreateBillAsync(AuthUserContext currentUser, CreateBillRequest request, CancellationToken cancellationToken);

    Task<BillReceiptResponse> RecordBillReceiptPrintEventAsync(
        AuthUserContext currentUser,
        Guid billId,
        RecordBillReceiptPrintEventRequest? request,
        CancellationToken cancellationToken);

    Task<BillDetail> CancelBillAsync(AuthUserContext currentUser, Guid billId, CancelBillRequest request, CancellationToken cancellationToken);

    Task<BillDetail> RecordPaymentAsync(AuthUserContext currentUser, Guid billId, RecordPaymentRequest request, CancellationToken cancellationToken);

    Task<BillDetail> CancelPaymentAsync(AuthUserContext currentUser, Guid paymentId, CancelPaymentRequest request, CancellationToken cancellationToken);
}
