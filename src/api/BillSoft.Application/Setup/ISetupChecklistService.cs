using BillSoft.Application.Auth;

namespace BillSoft.Application.Setup;

public interface ISetupChecklistService
{
    Task<SetupChecklistResponse> GetSetupChecklistAsync(
        AuthUserContext currentUser,
        Guid? branchId,
        CancellationToken cancellationToken);
}
