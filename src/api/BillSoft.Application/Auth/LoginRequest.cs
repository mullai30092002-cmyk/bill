namespace BillSoft.Application.Auth;

public sealed record LoginRequest(
    string RestaurantCode,
    string MobileNumber,
    string Password);
