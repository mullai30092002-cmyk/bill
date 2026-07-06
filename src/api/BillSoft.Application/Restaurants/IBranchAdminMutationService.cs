using BillSoft.Application.Auth;

namespace BillSoft.Application.Restaurants;

public interface IBranchAdminMutationService
{
    Task<BranchDetail> CreateAsync(AuthUserContext currentUser, CreateBranchRequest request, CancellationToken cancellationToken);

    Task<BranchDetail> UpdateAsync(AuthUserContext currentUser, Guid branchId, UpdateBranchRequest request, CancellationToken cancellationToken);

    Task<BranchDetail> ActivateAsync(AuthUserContext currentUser, Guid branchId, CancellationToken cancellationToken);

    Task<BranchDetail> DeactivateAsync(AuthUserContext currentUser, Guid branchId, CancellationToken cancellationToken);
}
