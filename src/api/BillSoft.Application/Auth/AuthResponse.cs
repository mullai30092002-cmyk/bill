namespace BillSoft.Application.Auth;

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    Guid UserId,
    Guid RestaurantId,
    string RestaurantCode,
    string CountryCode,
    string CurrencyCode,
    string TimeZoneId,
    Guid? BranchId,
    string FullName,
    string MobileNumber,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    string ActiveRole);
