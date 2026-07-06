using BillSoft.Domain.Common;

namespace BillSoft.Domain.Security;

public sealed record RoleDefinition(Guid RoleId, string Name, string Description, bool IsSystemRole);

public static class SystemRoles
{
    public static readonly RoleDefinition[] Definitions =
    [
        new(DeterministicGuid.FromString("SystemRole:SuperAdmin"), "SuperAdmin", "Platform-level administration", true),
        new(DeterministicGuid.FromString("SystemRole:RestaurantOwner"), "RestaurantOwner", "Owner monitoring and control", true),
        new(DeterministicGuid.FromString("SystemRole:Admin"), "Admin", "Restaurant configuration and operations management", true),
        new(DeterministicGuid.FromString("SystemRole:Cashier"), "Cashier", "Billing counter and payment collection", true),
        new(DeterministicGuid.FromString("SystemRole:Waiter"), "Waiter", "Eat-in and parcel order creation", true),
        new(DeterministicGuid.FromString("SystemRole:KitchenUser"), "KitchenUser", "Kitchen preparation workflow", true),
        new(DeterministicGuid.FromString("SystemRole:InventoryUser"), "InventoryUser", "Stock entry and stock checking", true),
        new(DeterministicGuid.FromString("SystemRole:AccountsUser"), "AccountsUser", "Vendor bills, expenses, and settlements", true)
    ];

    public static IReadOnlyCollection<string> Names => Definitions.Select(definition => definition.Name).ToArray();
}
