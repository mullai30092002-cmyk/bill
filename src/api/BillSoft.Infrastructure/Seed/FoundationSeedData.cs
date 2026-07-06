using BillSoft.Domain.Common;
using BillSoft.Domain.Security;

namespace BillSoft.Infrastructure.Seed;

public sealed record RolePermissionSeedDefinition(Guid RolePermissionId, string RoleName, string PermissionCode);

public static class FoundationSeedData
{
    public static IReadOnlyList<PermissionDefinition> Permissions => SystemPermissions.Definitions;

    public static IReadOnlyList<RoleDefinition> Roles => SystemRoles.Definitions;

    public static IReadOnlyList<RolePermissionSeedDefinition> RolePermissions { get; } = BuildRolePermissions();

    private static IReadOnlyList<RolePermissionSeedDefinition> BuildRolePermissions()
    {
        var allPermissionCodes = SystemPermissions.Definitions.Select(definition => definition.Code).ToArray();
        var restaurantOwnerPermissionCodes = allPermissionCodes
            .Where(code => !string.Equals(code, "Restaurant.Manage", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var adminPermissionCodes = new[]
        {
            SystemPermissions.BranchManage,
            SystemPermissions.UserManage,
            "Permission.View",
            "AuditLog.View",
            SystemPermissions.MenuCategoryManage,
            SystemPermissions.MenuItemManage,
            SystemPermissions.MenuItemView,
            SystemPermissions.MenuItemChangePrice,
            SystemPermissions.OrderCreate,
            SystemPermissions.OrderView,
            SystemPermissions.OrderCancel,
            SystemPermissions.KitchenTicketView,
            SystemPermissions.KitchenTicketManage,
            SystemPermissions.KitchenTicketUpdateStatus,
            SystemPermissions.BillingView,
            SystemPermissions.BillingManage,
            SystemPermissions.PaymentRecord,
            SystemPermissions.PaymentCancel,
            SystemPermissions.CashShiftView,
            SystemPermissions.CashShiftManage,
            SystemPermissions.CashMovementRecord,
            "Bill.Create",
            "Bill.CollectPayment",
            "Bill.ApplyDiscount",
            "Bill.Cancel",
            "Bill.Reprint",
            "Inventory.View",
            "Inventory.Adjust",
            "VendorBill.Upload",
            "VendorBill.ReviewOcr",
            "VendorBill.OverrideOcr",
            "VendorBill.Confirm",
            "Expense.Create",
            SystemPermissions.ReportView
        };
        var cashierPermissionCodes = new[]
        {
            SystemPermissions.MenuItemView,
            SystemPermissions.OrderCreate,
            SystemPermissions.OrderView,
            SystemPermissions.OrderCancel,
            SystemPermissions.KitchenTicketView,
            SystemPermissions.KitchenTicketManage,
            SystemPermissions.BillingView,
            SystemPermissions.BillingManage,
            SystemPermissions.PaymentRecord,
            SystemPermissions.PaymentCancel,
            SystemPermissions.CashShiftView,
            SystemPermissions.CashShiftManage,
            SystemPermissions.CashMovementRecord
        };
        var waiterPermissionCodes = new[]
        {
            SystemPermissions.MenuItemView,
            SystemPermissions.OrderCreate,
            SystemPermissions.OrderView,
            SystemPermissions.OrderCancel,
            SystemPermissions.KitchenTicketView,
            SystemPermissions.BillingView
        };
        var kitchenPermissionCodes = new[]
        {
            SystemPermissions.MenuItemView,
            SystemPermissions.OrderView,
            SystemPermissions.KitchenTicketView,
            SystemPermissions.KitchenTicketUpdateStatus
        };
        var inventoryPermissionCodes = new[]
        {
            SystemPermissions.MenuItemView,
            "Inventory.View",
            "VendorBill.Upload",
            "VendorBill.ReviewOcr",
            "VendorBill.OverrideOcr",
            "VendorBill.Confirm"
        };
        var accountsPermissionCodes = new[]
        {
            SystemPermissions.BillingView,
            SystemPermissions.CashShiftView,
            "VendorPayment.Create",
            "Expense.Create",
            SystemPermissions.ReportView
        };

        var rolePermissions = new List<RolePermissionSeedDefinition>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddRolePermissions("SuperAdmin", allPermissionCodes);
        AddRolePermissions("RestaurantOwner", restaurantOwnerPermissionCodes);
        AddRolePermissions("Admin", adminPermissionCodes);
        AddRolePermissions("Cashier", cashierPermissionCodes);
        AddRolePermissions("Waiter", waiterPermissionCodes);
        AddRolePermissions("KitchenUser", kitchenPermissionCodes);
        AddRolePermissions("InventoryUser", inventoryPermissionCodes);
        AddRolePermissions("AccountsUser", accountsPermissionCodes);

        return rolePermissions;

        void AddRolePermissions(string roleName, IEnumerable<string> permissionCodes)
        {
            foreach (var permissionCode in permissionCodes)
            {
                var normalizedRoleName = NormalizeKey(roleName);
                var normalizedPermissionCode = NormalizeKey(permissionCode);
                var pairKey = $"{normalizedRoleName}|{normalizedPermissionCode}";

                if (!seen.Add(pairKey))
                {
                    throw new InvalidOperationException(
                        $"Duplicate role-permission seed mapping detected for role '{roleName}' and permission '{permissionCode}'.");
                }

                rolePermissions.Add(new RolePermissionSeedDefinition(
                    DeterministicGuid.FromString($"RolePermission:{normalizedRoleName}:{normalizedPermissionCode}"),
                    roleName,
                    permissionCode));
            }
        }
    }

    private static string NormalizeKey(string value)
    {
        return value.Trim().ToUpperInvariant();
    }
}
