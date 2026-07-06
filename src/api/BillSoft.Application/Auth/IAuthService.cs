using System.Security.Claims;

namespace BillSoft.Application.Auth;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest? request, string ipAddress, CancellationToken cancellationToken);

    Task<AuthResponse> RefreshAsync(RefreshTokenRequest? request, string ipAddress, CancellationToken cancellationToken);

    Task LogoutAsync(RefreshTokenRequest? request, string ipAddress, CancellationToken cancellationToken);

    AuthUserContext GetCurrentUserContext(ClaimsPrincipal principal);
}
