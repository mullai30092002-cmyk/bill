# BillSoft Permission Matrix

## Purpose

This document defines the initial role-based access model for BillSoft.

Roles must be enforced by the backend. Frontend hiding alone is not sufficient.

---

# Roles

| Role | Purpose |
|---|---|
| SuperAdmin | Platform-level administration |
| RestaurantOwner | Owner monitoring and control |
| Admin | Restaurant configuration and operations management |
| Cashier | Billing counter and payment collection |
| Waiter | Eat-in and parcel order creation |
| KitchenUser | Kitchen preparation workflow |
| InventoryUser | Stock entry and stock checking |
| AccountsUser | Vendor bills, expenses, and settlements |

---

The runtime foundation seed service inserts missing system roles and permissions using deterministic IDs. The matrix below still describes the intended backend enforcement model.

## Seed Baseline

The current foundation seed uses a conservative baseline before authentication exists:

- `SuperAdmin` gets all permissions.
- `RestaurantOwner` gets all non-platform permissions, excluding `Restaurant.Manage`.
- `Admin` gets operational permissions for branches, users, menu categories, menu items, item viewing, orders, kitchen tickets, billing, payments, cash shifts, cash movements, inventory view, vendor bill review/confirm, and reporting, but does not get the highest-risk permissions by default.
- `Cashier` gets `Order.Create`, `Order.View`, `Order.Cancel`, `KitchenTicket.View`, `KitchenTicket.Manage`, `Billing.View`, `Billing.Manage`, `Payment.Record`, `Payment.Cancel`, cash shift view/manage, cash movement record, and menu item view permissions.
- `Waiter` gets `Order.Create`, `Order.View`, `Order.Cancel`, `KitchenTicket.View`, `Billing.View`, and menu item view permissions.
- `KitchenUser` gets `KitchenTicket.View`, `KitchenTicket.UpdateStatus`, and menu item view permissions.
- `InventoryUser` gets inventory view, menu item view, plus vendor bill upload/review/confirm only.
- `AccountsUser` gets cash shift view, vendor payment, expense creation, and reports.

High-risk actions that remain intentionally excluded from lower roles by default:

- `Payment.Refund`
- `Payment.Reverse`
- `Billing.Manage`
- `Payment.Cancel`
- `Payment.Record`
- `Inventory.Adjust`
- `VendorBill.OverrideOcr`
- `Expense.Approve`
- `MenuCategory.Manage`
- `MenuItem.Manage`
- `MenuItem.View`
- `MenuItem.ChangePrice`

# Permission Matrix

| Feature / Action | SuperAdmin | Owner | Admin | Cashier | Waiter | Kitchen | Inventory | Accounts |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Manage restaurants | Yes | No | No | No | No | No | No | No |
| Manage branches | Yes | Yes | Yes | No | No | No | No | No |
| Manage users | Yes | Yes | Yes | No | No | No | No | No |
| Reset staff passwords | Yes | Yes | Yes | No | No | No | No | No |
| Manage roles/permissions | Yes | Yes | No | No | No | No | No | No |
| Configure menu categories | Yes | Yes | Yes | No | No | No | No | No |
| Configure menu items | Yes | Yes | Yes | No | No | No | No | No |
| View menu items | Yes | Yes | Yes | Yes | Yes | Yes | Yes | No |
| Change menu prices | Yes | Yes | Yes | No | No | No | No | No |
| View POS orders | Yes | Yes | Yes | Yes | Yes | Yes | No | No |
| Create eat-in order | Yes | Yes | Yes | Yes | Yes | No | No | No |
| Create parcel order | Yes | Yes | Yes | Yes | Yes | No | No | No |
| Send order to kitchen | Yes | Yes | Yes | Yes | Yes | No | No | No |
| Cancel order before kitchen | Yes | Yes | Yes | Yes | Yes | No | No | No |
| Cancel order after kitchen | Yes | Yes | Yes | No | No | No | No | No |
| View kitchen tickets | Yes | Yes | Yes | Yes | Yes | Yes | No | No |
| Create kitchen tickets from confirmed POS order | Yes | Yes | Yes | Yes | No | No | No | No |
| Update kitchen status | Yes | Yes | Yes | Yes | No | Yes | No | No |
| Cancel kitchen ticket | Yes | Yes | Yes | Yes | No | No | No | No |
| View bills/payments | Yes | Yes | Yes | Yes | Yes | No | No | Yes |
| Create bill from confirmed POS order | Yes | Yes | Yes | Yes | No | No | No | No |
| Record payment | Yes | Yes | Yes | Yes | No | No | No | Yes |
| Cancel bill | Yes | Yes | Yes | Yes | No | No | No | No |
| Cancel payment | Yes | Yes | Yes | Yes | No | No | No | No |
| Apply discount | Yes | Yes | Configurable | No | No | No | No | No |
| Reprint bill | Yes | Yes | Yes | Yes | No | No | No | No |
| View cashier shifts | Yes | Yes | Yes | Yes | No | No | No | Yes |
| Manage cashier shifts (open/close) | Yes | Yes | Yes | Yes | No | No | No | No |
| Record cash movement | Yes | Yes | Yes | Yes | No | No | No | No |
| View cash difference | Yes | Yes | Yes | Limited | No | No | No | No |
| Manage inventory items | Yes | Yes | Yes | No | No | No | Yes | No |
| Record stock movement | Yes | Yes | Yes | No | No | No | Yes | No |
| Adjust stock | Yes | Yes | Configurable | No | No | No | Configurable | No |
| Perform daily stock count | Yes | Yes | Yes | No | No | No | Yes | No |
| Manage vendors | Yes | Yes | Yes | No | No | No | No | Yes |
| Create vendor bill | Yes | Yes | Yes | No | No | No | Yes | Yes |
| Upload vendor bill document | Yes | Yes | Yes | No | No | No | Yes | Yes |
| Run/review OCR | Yes | Yes | Yes | No | No | No | Yes | Yes |
| Override OCR values | Yes | Yes | Configurable | No | No | No | Configurable | Configurable |
| Confirm vendor bill | Yes | Yes | Yes | No | No | No | Configurable | Configurable |
| Pay vendor | Yes | Yes | Configurable | No | No | No | No | Yes |
| Create expense | Yes | Yes | Yes | No | No | No | No | Yes |
| Approve expense | Yes | Yes | Configurable | No | No | No | No | No |
| View owner dashboard | Yes | Yes | Yes | No | No | No | No | Limited |
| View daily reports / cash sales exception report | Yes | Yes | Yes | Limited | No | No | Limited | Yes |
| View audit logs | Yes | Yes | Yes | No | No | No | No | No |

---

# Sensitive Permissions

The following actions must be explicit permissions and must not be granted accidentally:

```text
Bill.ApplyDiscount
Bill.Cancel
Billing.View
Billing.Manage
Payment.Record
Payment.Cancel
CashShift.View
CashShift.Manage
CashMovement.Record
KitchenTicket.View
KitchenTicket.Manage
KitchenTicket.UpdateStatus
Payment.Refund
Payment.Reverse
Inventory.Adjust
VendorBill.OverrideOcr
VendorBill.Confirm
VendorPayment.Create
Expense.Approve
MenuCategory.Manage
MenuItem.Manage
MenuItem.View
MenuItem.ChangePrice
AuditLog.View
User.Manage
Role.Manage
```

---

# Enforcement Rules

1. Backend must enforce all permissions.
2. Frontend may hide unavailable actions, but this is not sufficient.
3. Sensitive actions must create audit records.
4. Configurable permissions must default to restricted.
5. Kitchen users must not access revenue reports.
6. Waiters must not change prices or discounts.
7. Cashiers must not adjust inventory or confirm vendor bills.
8. User administration APIs under `/api/v1/admin/users` require `User.Manage` and are scoped to the current JWT `restaurant_id` claim, including admin-only staff password reset/reissue.
9. Read-only role APIs under `/api/v1/admin/roles` require `Role.Manage` or `User.Manage` and only expose system roles plus the current restaurant's roles.
10. The permission catalog API under `/api/v1/admin/permissions` requires `Permission.View` or `Role.Manage` and reads from the database permission catalog.
11. Read-only branch APIs under `/api/v1/admin/branches` and `/api/v1/admin/branches/{branchId}` require `Branch.Manage` or `User.Manage`, use the current JWT `restaurant_id` claim for scoping, and never expose branches from other restaurants.
12. Branch mutation APIs under `/api/v1/admin/branches` require `Branch.Manage` only; `User.Manage` alone must never allow create, update, activate, or deactivate actions.
13. Branch mutations remain restaurant-scoped to the current JWT `restaurant_id` claim and must never hard-delete branch records.
14. Branch deactivation must be rejected when active users are assigned to the branch.
15. The owner dashboard frontend route is explicit at `/owner/dashboard`, requires `Report.View`, and does not replace the authenticated home route.
