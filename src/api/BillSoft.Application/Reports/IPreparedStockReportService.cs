using BillSoft.Application.Auth;

namespace BillSoft.Application.Reports;

public interface IPreparedStockReportService
{
    Task<PreparedStockReportResponse> GetPreparedStockReportAsync(
        AuthUserContext currentUser,
        DateTime? businessDate,
        Guid? branchId,
        CancellationToken cancellationToken);
}
