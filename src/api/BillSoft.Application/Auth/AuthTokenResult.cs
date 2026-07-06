namespace BillSoft.Application.Auth;

public sealed record AuthTokenResult(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc);
