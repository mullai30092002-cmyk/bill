using BillSoft.Application.Auth;

namespace BillSoft.Application.Reports;

public interface IExpiryStockReportService
{
    Task<ExpiryStockReportResponse> GetExpiryStockReportAsync(
        AuthUserContext currentUser,
        DateTime? asOfDate,
        Guid? branchId,
        CancellationToken cancellationToken);
}
