using BillSoft.Application.Auth;

namespace BillSoft.Application.Reports;

public interface IVendorPayablesReportService
{
    Task<VendorPayablesReportResponse> GetVendorPayablesReportAsync(
        AuthUserContext currentUser,
        DateTime? fromDate,
        DateTime? toDate,
        Guid? branchId,
        CancellationToken cancellationToken);
}
