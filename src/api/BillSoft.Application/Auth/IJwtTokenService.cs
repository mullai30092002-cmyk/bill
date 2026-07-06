using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Users;

namespace BillSoft.Application.Auth;

public interface IJwtTokenService
{
    AuthTokenResult CreateAccessToken(
        User user,
        Restaurant restaurant,
        Branch? branch,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> permissions,
        string sessionId,
        string? activeRole = null,
        bool mustChangePassword = false);
}
