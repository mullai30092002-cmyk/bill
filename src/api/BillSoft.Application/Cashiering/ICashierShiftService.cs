using BillSoft.Application.Auth;

namespace BillSoft.Application.Cashiering;

public interface ICashierShiftService
{
    Task<CashierShiftListResponse> ListShiftsAsync(AuthUserContext currentUser, CashierShiftListQuery query, CancellationToken cancellationToken);

    Task<CashierShiftDetail?> GetCurrentShiftAsync(AuthUserContext currentUser, Guid branchId, CancellationToken cancellationToken);

    Task<CashierShiftDetail> GetShiftAsync(AuthUserContext currentUser, Guid shiftId, CancellationToken cancellationToken);

    Task<CashierShiftDetail> OpenShiftAsync(AuthUserContext currentUser, OpenCashierShiftRequest request, CancellationToken cancellationToken);

    Task<CashierShiftDetail> CloseShiftAsync(AuthUserContext currentUser, Guid shiftId, CloseCashierShiftRequest request, CancellationToken cancellationToken);
}
