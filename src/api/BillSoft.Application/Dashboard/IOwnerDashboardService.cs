using BillSoft.Application.Auth;

namespace BillSoft.Application.Dashboard;

public interface IOwnerDashboardService
{
    Task<OwnerDashboardResponse> GetOwnerDashboardAsync(
        AuthUserContext currentUser,
        DateTime? businessDate,
        Guid? branchId,
        CancellationToken cancellationToken);
}
