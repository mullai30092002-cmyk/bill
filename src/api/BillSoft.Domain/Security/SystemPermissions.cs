using BillSoft.Domain.Common;

namespace BillSoft.Domain.Security;

public sealed record PermissionDefinition(Guid PermissionId, string Code, string Description, string Module);

public static class SystemPermissions
{
    public const string BranchManage = "Branch.Manage";
    public const string UserManage = "User.Manage";
    public const string RoleManage = "Role.Manage";
    public const string PermissionView = "Permission.View";
    public const string MenuCategoryManage = "MenuCategory.Manage";
    public const string MenuItemManage = "MenuItem.Manage";
    public const string MenuItemView = "MenuItem.View";
    public const string MenuItemChangePrice = "MenuItem.ChangePrice";
    public const string OrderCreate = "Order.Create";
    public const string OrderView = "Order.View";
    public const string OrderCancel = "Order.Cancel";
    public const string BillingView = "Billing.View";
    public const string BillingManage = "Billing.Manage";
    public const string PaymentRecord = "Payment.Record";
    public const string PaymentCancel = "Payment.Cancel";
    public const string CashShiftView = "CashShift.View";
    public const string CashShiftManage = "CashShift.Manage";
    public const string CashMovementRecord = "CashMovement.Record";
    public const string KitchenTicketView = "KitchenTicket.View";
    public const string KitchenTicketManage = "KitchenTicket.Manage";
    public const string KitchenTicketUpdateStatus = "KitchenTicket.UpdateStatus";
    public const string InventoryView = "Inventory.View";
    public const string InventoryAdjust = "Inventory.Adjust";
    public const string VendorBillUpload = "VendorBill.Upload";
    public const string VendorBillReviewOcr = "VendorBill.ReviewOcr";
    public const string VendorBillOverrideOcr = "VendorBill.OverrideOcr";
    public const string VendorBillConfirm = "VendorBill.Confirm";
    public const string VendorPaymentCreate = "VendorPayment.Create";
    public const string ReportView = "Report.View";

    public static readonly PermissionDefinition[] Definitions =
    [
        new(DeterministicGuid.FromString("Permission:Restaurant.Manage"), "Restaurant.Manage", "Manage restaurant records", "Restaurant"),
        new(DeterministicGuid.FromString("Permission:Branch.Manage"), BranchManage, "Manage branch records", "Restaurant"),
        new(DeterministicGuid.FromString("Permission:User.Manage"), UserManage, "Manage users", "Security"),
        new(DeterministicGuid.FromString("Permission:Role.Manage"), RoleManage, "Manage roles", "Security"),
        new(DeterministicGuid.FromString("Permission:Permission.View"), PermissionView, "View permission catalog", "Security"),
        new(DeterministicGuid.FromString("Permission:AuditLog.View"), "AuditLog.View", "View audit logs", "Audit"),
        new(DeterministicGuid.FromString("Permission:MenuCategory.Manage"), MenuCategoryManage, "Manage menu categories", "Menu"),
        new(DeterministicGuid.FromString("Permission:MenuItem.Manage"), MenuItemManage, "Manage menu items", "Menu"),
        new(DeterministicGuid.FromString("Permission:MenuItem.View"), MenuItemView, "View menu items", "Menu"),
        new(DeterministicGuid.FromString("Permission:MenuItem.ChangePrice"), MenuItemChangePrice, "Change menu item prices", "Menu"),
        new(DeterministicGuid.FromString("Permission:Order.Create"), OrderCreate, "Create orders", "Order"),
        new(DeterministicGuid.FromString("Permission:Order.View"), OrderView, "View orders", "Order"),
        new(DeterministicGuid.FromString("Permission:Order.Cancel"), OrderCancel, "Cancel orders", "Order"),
        new(DeterministicGuid.FromString("Permission:Billing.View"), BillingView, "View bills and payments", "Billing"),
        new(DeterministicGuid.FromString("Permission:Billing.Manage"), BillingManage, "Create and cancel bills", "Billing"),
        new(DeterministicGuid.FromString("Permission:Payment.Record"), PaymentRecord, "Record payments", "Billing"),
        new(DeterministicGuid.FromString("Permission:Payment.Cancel"), PaymentCancel, "Cancel payments", "Billing"),
        new(DeterministicGuid.FromString("Permission:CashShift.View"), CashShiftView, "View cashier shifts", "CashControl"),
        new(DeterministicGuid.FromString("Permission:CashShift.Manage"), CashShiftManage, "Open and close cashier shifts", "CashControl"),
        new(DeterministicGuid.FromString("Permission:CashMovement.Record"), CashMovementRecord, "Record cash drawer movements", "CashControl"),
        new(DeterministicGuid.FromString("Permission:KitchenTicket.View"), KitchenTicketView, "View kitchen tickets", "Kitchen"),
        new(DeterministicGuid.FromString("Permission:KitchenTicket.Manage"), KitchenTicketManage, "Create and cancel kitchen tickets", "Kitchen"),
        new(DeterministicGuid.FromString("Permission:KitchenTicket.UpdateStatus"), KitchenTicketUpdateStatus, "Update kitchen ticket status", "Kitchen"),
        new(DeterministicGuid.FromString("Permission:Bill.Create"), "Bill.Create", "Generate bills", "Billing"),
        new(DeterministicGuid.FromString("Permission:Bill.CollectPayment"), "Bill.CollectPayment", "Collect bill payments", "Billing"),
        new(DeterministicGuid.FromString("Permission:Bill.ApplyDiscount"), "Bill.ApplyDiscount", "Apply bill discounts", "Billing"),
        new(DeterministicGuid.FromString("Permission:Bill.Cancel"), "Bill.Cancel", "Cancel bills", "Billing"),
        new(DeterministicGuid.FromString("Permission:Bill.Void"), "Bill.Void", "Void bills", "Billing"),
        new(DeterministicGuid.FromString("Permission:Bill.Reprint"), "Bill.Reprint", "Reprint bills", "Billing"),
        new(DeterministicGuid.FromString("Permission:Payment.Refund"), "Payment.Refund", "Refund payments", "Billing"),
        new(DeterministicGuid.FromString("Permission:Payment.Reverse"), "Payment.Reverse", "Reverse payments", "Billing"),
        new(DeterministicGuid.FromString("Permission:CashDrawer.Open"), "CashDrawer.Open", "Open cash drawer sessions", "CashControl"),
        new(DeterministicGuid.FromString("Permission:CashDrawer.Close"), "CashDrawer.Close", "Close cash drawer sessions", "CashControl"),
        new(DeterministicGuid.FromString("Permission:Inventory.View"), "Inventory.View", "View inventory", "Inventory"),
        new(DeterministicGuid.FromString("Permission:Inventory.Adjust"), "Inventory.Adjust", "Adjust stock levels", "Inventory"),
        new(DeterministicGuid.FromString("Permission:VendorBill.Upload"), "VendorBill.Upload", "Upload vendor bill documents", "VendorBill"),
        new(DeterministicGuid.FromString("Permission:VendorBill.ReviewOcr"), "VendorBill.ReviewOcr", "Review OCR output", "VendorBill"),
        new(DeterministicGuid.FromString("Permission:VendorBill.OverrideOcr"), "VendorBill.OverrideOcr", "Override OCR values", "VendorBill"),
        new(DeterministicGuid.FromString("Permission:VendorBill.Confirm"), "VendorBill.Confirm", "Confirm vendor bills", "VendorBill"),
        new(DeterministicGuid.FromString("Permission:VendorPayment.Create"), "VendorPayment.Create", "Create vendor payments", "VendorBill"),
        new(DeterministicGuid.FromString("Permission:Expense.Create"), "Expense.Create", "Create expenses", "Expense"),
        new(DeterministicGuid.FromString("Permission:Expense.Approve"), "Expense.Approve", "Approve expenses", "Expense"),
        new(DeterministicGuid.FromString("Permission:Report.View"), ReportView, "View reports", "Reports")
    ];

    public static IReadOnlyCollection<string> Codes => Definitions.Select(definition => definition.Code).ToArray();
}
