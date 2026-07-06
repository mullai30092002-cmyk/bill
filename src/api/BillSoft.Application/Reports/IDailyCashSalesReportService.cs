using BillSoft.Application.Auth;

namespace BillSoft.Application.Reports;

public interface IDailyCashSalesReportService
{
    Task<DailyCashSalesReportResponse> GetDailyCashSalesReportAsync(
        AuthUserContext currentUser,
        DateTime? businessDate,
        Guid? branchId,
        CancellationToken cancellationToken);
}
