using BillSoft.Application.Auth;

namespace BillSoft.Application.Vendors;

public interface IVendorBillOcrService
{
    Task<VendorBillOcrDraftListResponse> ListDraftsAsync(AuthUserContext currentUser, VendorBillOcrDraftListQuery query, CancellationToken cancellationToken);

    Task<VendorBillOcrDraftDetail> GetDraftAsync(AuthUserContext currentUser, Guid draftId, CancellationToken cancellationToken);

    Task<VendorBillOcrDraftDetail> UploadDraftAsync(
        AuthUserContext currentUser,
        Guid? branchId,
        string originalFileName,
        string contentType,
        Stream fileContent,
        long fileSizeBytes,
        CancellationToken cancellationToken);

    Task<VendorBillOcrDraftDetail> UpdateDraftAsync(
        AuthUserContext currentUser,
        Guid draftId,
        UpdateVendorBillOcrDraftRequest request,
        CancellationToken cancellationToken);

    Task<VendorBillDetail> ConfirmDraftAsync(AuthUserContext currentUser, Guid draftId, CancellationToken cancellationToken);

    Task<VendorBillOcrDraftDetail> CancelDraftAsync(
        AuthUserContext currentUser,
        Guid draftId,
        string reason,
        CancellationToken cancellationToken);
}
