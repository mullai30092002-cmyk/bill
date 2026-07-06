using BillSoft.Application.Auth;

namespace BillSoft.Application.Reports;

public interface ICashReconciliationReportService
{
    Task<CashReconciliationReportResponse> GetCashReconciliationReportAsync(
        AuthUserContext currentUser,
        DateTime? businessDate,
        Guid? branchId,
        CancellationToken cancellationToken);
}
