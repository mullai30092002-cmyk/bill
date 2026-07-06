using BillSoft.Application.Auth;

namespace BillSoft.Application.Vendors;

public interface IVendorService
{
    Task<VendorListResponse> ListVendorsAsync(AuthUserContext currentUser, VendorListQuery query, CancellationToken cancellationToken);

    Task<VendorDetail> CreateVendorAsync(AuthUserContext currentUser, CreateVendorRequest request, CancellationToken cancellationToken);

    Task<VendorDetail> UpdateVendorAsync(AuthUserContext currentUser, Guid vendorId, UpdateVendorRequest request, CancellationToken cancellationToken);

    Task<VendorBillListResponse> ListVendorBillsAsync(AuthUserContext currentUser, VendorBillListQuery query, CancellationToken cancellationToken);

    Task<VendorBillDetail> GetVendorBillAsync(AuthUserContext currentUser, Guid vendorBillId, CancellationToken cancellationToken);

    Task<VendorBillDetail> CreateVendorBillAsync(AuthUserContext currentUser, CreateVendorBillRequest request, CancellationToken cancellationToken);

    Task<VendorBillDetail> RecordSettlementAsync(AuthUserContext currentUser, Guid vendorBillId, RecordVendorSettlementRequest request, CancellationToken cancellationToken);

    Task<VendorStatementResponse> GetVendorStatementAsync(AuthUserContext currentUser, VendorStatementQuery query, CancellationToken cancellationToken);

    Task<VendorBillDetail> CancelVendorBillAsync(AuthUserContext currentUser, Guid vendorBillId, CancelVendorBillRequest request, CancellationToken cancellationToken);
}
