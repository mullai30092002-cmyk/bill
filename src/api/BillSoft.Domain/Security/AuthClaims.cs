namespace BillSoft.Domain.Security;

public static class AuthClaims
{
    public const string RestaurantId = "restaurant_id";

    public const string RestaurantCode = "restaurant_code";

    public const string BranchId = "branch_id";

    public const string SessionId = "session_id";

    public const string Permission = "permission";

    public const string ActiveRole = "active_role";

    public const string MustChangePassword = "must_change_password";
}
