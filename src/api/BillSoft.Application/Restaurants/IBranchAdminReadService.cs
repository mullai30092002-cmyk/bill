using BillSoft.Application.Auth;

namespace BillSoft.Application.Restaurants;

public interface IBranchAdminReadService
{
    Task<BranchListResponse> ListAsync(AuthUserContext currentUser, BranchListQuery query, CancellationToken cancellationToken);

    Task<BranchDetail> GetAsync(AuthUserContext currentUser, Guid branchId, CancellationToken cancellationToken);
}
