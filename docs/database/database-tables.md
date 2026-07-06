# BillSoft Database Tables

## Purpose

This document defines the initial database table structure for BillSoft.

BillSoft is a restaurant billing, kitchen display, inventory, vendor bill, cash-control, and leakage-prevention system. The database must support traceability, auditability, and strict control over billing, stock, vendor payments, and staff activity.

## Core Design Rules

1. Do not hard-delete business records such as orders, bills, payments, vendor bills, stock movements, or expenses.
2. Use status fields such as `Cancelled`, `Voided`, `Inactive`, or `Rejected`.
3. Every financial and inventory change must be traceable.
4. Stock must be updated through stock movement records, not direct manual edits.
5. Bill numbers and vendor bill references must be immutable after confirmation.
6. OCR-scanned vendor bill values must be stored separately from user-confirmed values.
7. Any manual override must require a reason and must be audited.
8. Every important action must capture user, timestamp, branch, device, and reason where applicable.
9. Store uploaded bill files in object storage, not directly inside the database.
10. Use `BusinessDate` separately from `CreatedAt` because restaurants may close after midnight.

---

# 1. Restaurant and Branch Setup

## Restaurants

Stores restaurant/company-level information.

| Column       | Description                          |
| ------------ | ------------------------------------ |
| RestaurantId | Primary key                          |
| RestaurantCode | Stable restaurant code             |
| NormalizedRestaurantCode | Normalized restaurant code |
| CountryCode  | ISO 3166 alpha-2 restaurant country  |
| CurrencyCode | ISO 4217 restaurant currency         |
| TimeZoneId   | IANA timezone ID                     |
| Name         | Restaurant/company name              |
| BusinessType | Business profile such as Restaurant, JuiceShop, Bakery, DessertShop, or CafeTakeaway |
| LegalName    | Registered legal name, if applicable |
| Phone        | Contact number                       |
| Email        | Contact email                        |
| Address      | Main address                         |
| Status       | Active, Inactive, Suspended          |
| CreatedAt    | Created timestamp                    |
| UpdatedAt    | Updated timestamp                    |

## Branches

Stores restaurant outlets or branches.

| Column       | Description           |
| ------------ | --------------------- |
| BranchId     | Primary key           |
| RestaurantId | Linked restaurant     |
| CountryCode  | ISO 3166 alpha-2      |
| Name         | Branch name           |
| Address      | Branch address        |
| Phone        | Branch contact number |
| Timezone     | Branch timezone       |
| Currency     | Billing currency      |
| TimeZoneId   | IANA timezone ID      |
| CurrencyCode | ISO 4217 currency     |
| Status       | Active, Inactive      |
| CreatedAt    | Created timestamp     |
| UpdatedAt    | Updated timestamp     |

Branch records are soft-managed through `Status` transitions. Branch names are treated as unique within a restaurant, and branch deactivation should be blocked while active users are still assigned to the branch.

Branch phone numbers are optional, but when present they must be unique within the restaurant.

## RestaurantSettings

Stores restaurant-specific configuration.

| Column          | Description                      |
| --------------- | -------------------------------- |
| SettingId       | Primary key                      |
| RestaurantId    | Linked restaurant                |
| BranchId        | Optional branch-specific setting |
| SettingKey      | Setting name                     |
| SettingValue    | Setting value                    |
| UpdatedByUserId | Last updated by                  |
| UpdatedAt       | Last updated timestamp           |

Examples:

* Tax enabled
* Service charge enabled
* Report times
* Default language
* Discount approval threshold
* Cash drawer required

---

# 2. Users, Roles, and Security

## Users

Stores all users including owner, cashier, waiter, kitchen user, inventory user, and super admin.

| Column       | Description                              |
| ------------ | ---------------------------------------- |
| UserId       | Primary key                              |
| RestaurantId | Linked restaurant                        |
| BranchId     | Default branch                           |
| FullName     | User full name                           |
| MobileNumber | Login/contact number                     |
| MobileCountryCode | ISO 3166 alpha-2 mobile country    |
| MobileDialCode | Dialing prefix such as `+65` or `+91` |
| MobileNationalNumber | National mobile number         |
| MobileE164   | Canonical login key in E.164 format      |
| Email        | Optional email                           |
| NormalizedEmail | Normalized optional email             |
| PinHash      | PIN hash for quick login                 |
| PasswordHash | Password hash, if password login is used |
| Status       | Active, Inactive, Locked                 |
| CreatedAt    | Created timestamp                        |
| UpdatedAt    | Updated timestamp                        |

`Email` remains optional and unique per restaurant when present. `NormalizedEmail` is used for filtered uniqueness checks.

`MobileE164` is the canonical user login key. `RestaurantId + MobileE164` is the uniqueness boundary for user mobile numbers. `MobileNumber` remains available for display and compatibility, but it is no longer the canonical lookup field.

## Roles

Stores roles.

| Column       | Description                                           |
| ------------ | ----------------------------------------------------- |
| RoleId       | Primary key                                           |
| RestaurantId | Null for system roles, populated for restaurant roles |
| Name         | Role name                                             |
| Description  | Role description                                      |
| IsSystemRole | Whether role is system-defined                        |
| CreatedAt    | Created timestamp                                     |

System roles are seeded idempotently at runtime using deterministic catalog IDs.

Example roles:

* SuperAdmin
* RestaurantOwner
* Admin
* Cashier
* Waiter
* KitchenUser
* InventoryUser
* AccountsUser

## Permissions

Stores permission definitions.

| Column       | Description            |
| ------------ | ---------------------- |
| PermissionId | Primary key            |
| Code         | Permission code        |
| Description  | Permission description |
| Module       | Related module         |

System permissions are seeded idempotently at runtime using deterministic catalog IDs.

Example permissions:

* `Order.Create`
* `Order.Cancel`
* `Bill.Create`
* `Bill.CollectPayment`
* `Bill.ApplyDiscount`
* `Bill.Void`
* `Inventory.Adjust`
* `VendorBill.Confirm`
* `VendorBill.OverrideOcr`
* `Report.View`
* `Settings.Manage`

## RefreshTokens

Stores hashed refresh token records for session rotation and revocation.

| Column               | Description                         |
| -------------------- | ----------------------------------- |
| RefreshTokenId       | Primary key                         |
| RestaurantId         | Linked restaurant                   |
| BranchId             | Optional linked branch              |
| UserId               | Linked user                         |
| TokenHash            | SHA-256 hash of the refresh token   |
| ExpiresAt            | Refresh token expiry                |
| RevokedAt            | Revoked timestamp                   |
| RevokedByIp          | IP address that revoked the token   |
| CreatedByIp          | IP address that created the token    |
| ReplacedByTokenHash  | Hash of replacement refresh token    |
| SessionId            | Session correlation identifier      |
| ActiveRole           | Role active when token was issued    |
| CreatedAt            | Created timestamp                   |
| LastActivityAt       | Last activity timestamp             |

Refresh tokens are persisted as hashes only. Plaintext refresh tokens are never stored.

## UserRoles

Maps users to roles.

| Column           | Description        |
| ---------------- | ------------------ |
| UserRoleId       | Primary key        |
| UserId           | Linked user        |
| RoleId           | Linked role        |
| AssignedByUserId | Assigned by        |
| AssignedAt       | Assigned timestamp |

## RolePermissions

Maps roles to permissions.

| Column           | Description       |
| ---------------- | ----------------- |
| RolePermissionId | Primary key       |
| RoleId           | Linked role       |
| PermissionId     | Linked permission |
| CreatedAt        | Created timestamp |

## Devices

Tracks devices used in the restaurant.

| Column       | Description                                  |
| ------------ | -------------------------------------------- |
| DeviceId     | Primary key                                  |
| RestaurantId | Linked restaurant                            |
| BranchId     | Linked branch                                |
| DeviceName   | Counter tablet, kitchen screen, owner mobile |
| DeviceType   | POS, KitchenDisplay, Mobile, Tablet, Web     |
| IsTrusted    | Whether device is trusted                    |
| LastSeenAt   | Last active timestamp                        |
| Status       | Active, Inactive, Blocked                    |

## UserSessions

Tracks user login sessions.

| Column        | Description                |
| ------------- | -------------------------- |
| UserSessionId | Primary key                |
| UserId        | Linked user                |
| DeviceId      | Linked device              |
| LoginAt       | Login timestamp            |
| LogoutAt      | Logout timestamp           |
| IpAddress     | IP address                 |
| Status        | Active, LoggedOut, Expired |

---

# 3. Menu and Pricing

## MenuCategories

Stores menu categories.

| Column         | Description         |
| -------------- | ------------------- |
| MenuCategoryId | Primary key         |
| RestaurantId   | Linked restaurant   |
| Name           | Category name       |
| DisplayOrder   | Sort order          |
| Status         | Active, Inactive    |
| CreatedAt      | Created timestamp   |
| UpdatedAt      | Updated timestamp   |

Examples:

* Meals
* Biryani
* Fried Rice
* Snacks
* Tea/Coffee
* Juice
* Cold Drinks

## MenuItems

Stores food and drink items.

| Column           | Description            |
| ---------------- | ---------------------- |
| MenuItemId       | Primary key            |
| RestaurantId     | Linked restaurant      |
| MenuCategoryId   | Linked category        |
| Name             | Item name              |
| Description      | Optional description   |
| Sku              | Optional SKU           |
| BasePrice        | Base selling price     |
| TaxRate          | Optional tax rate      |
| IsVegetarian     | Vegetarian flag        |
| IsAvailableForEatIn | Eat-in availability |
| IsAvailableForParcel | Parcel availability |
| Status           | Active, Inactive       |
| CreatedAt        | Created timestamp      |
| UpdatedAt        | Updated timestamp      |

## MenuItemPriceHistory

Tracks menu price changes for anti-fraud auditing.

| Column               | Description                |
| -------------------- | -------------------------- |
| MenuItemPriceHistoryId | Primary key              |
| MenuItemId           | Linked menu item           |
| RestaurantId         | Linked restaurant          |
| OldPrice             | Previous base price        |
| NewPrice             | New base price             |
| ChangedByUserId      | Changed by                 |
| ChangedAt            | Changed timestamp          |
| Reason               | Optional reason for change  |

Price history rows are append-only. Base price changes must write a history row. Non-price edits must not.

## MenuItemRecipeIngredients

Stores branch-scoped ingredient mappings for menu items.

| Column               | Description                                  |
| -------------------- | -------------------------------------------- |
| MenuItemRecipeIngredientId | Primary key                            |
| RestaurantId         | Linked restaurant                            |
| BranchId             | Linked branch                                |
| MenuItemId           | Linked menu item                             |
| InventoryItemId      | Linked inventory item                        |
| QuantityRequired     | Ingredient quantity required per menu item   |
| CreatedAtUtc         | Created timestamp                            |
| UpdatedAtUtc         | Updated timestamp                            |

Rules:

* Recipe rows are scoped by restaurant and branch.
* Duplicate menu item + inventory item mappings are rejected.
* Quantity required must be greater than zero.
* Recipe changes are auditable and do not rewrite historical ticket snapshots.

## KitchenTicketInventoryDeductions

Stores the link from kitchen ticket completion to inventory movements.

| Column               | Description                                  |
| -------------------- | -------------------------------------------- |
| KitchenTicketInventoryDeductionId | Primary key                      |
| RestaurantId         | Linked restaurant                            |
| BranchId             | Linked branch                                |
| KitchenTicketId      | Linked kitchen ticket                        |
| InventoryItemId      | Linked inventory item                        |
| InventoryMovementId  | Linked inventory movement                    |
| QuantityDeducted     | Quantity deducted from stock                 |
| CreatedAtUtc         | Created timestamp                            |

Rules:

* One deduction row is stored per kitchen ticket and inventory item.
* Deduction rows provide traceability from ticket completion to ledger movements.
* The unique mapping prevents duplicate deductions on retry.

## KitchenStations

Stores kitchen preparation areas.

| Column           | Description       |
| ---------------- | ----------------- |
| KitchenStationId | Primary key       |
| RestaurantId     | Linked restaurant |
| BranchId         | Linked branch     |
| Name             | Station name      |
| Description      | Description       |
| IsActive         | Active flag       |

Examples:

* Main Kitchen
* Tea Counter
* Juice Counter
* Parcel Packing

---

# 4. Tables and Orders

## POS Order Foundation

This foundation slice adds restaurant-scoped POS order capture without billing, payment collection, kitchen ticket foundation, or inventory deduction.

## PosOrders

Stores eat-in and parcel POS order headers.

| Column | Description |
|---|---|
| PosOrderId | Primary key |
| RestaurantId | Linked restaurant |
| BranchId | Linked branch |
| OrderNumber | Auto-generated order number, unique per restaurant + branch |
| OrderType | EatIn, Parcel |
| Status | Draft, Confirmed, Cancelled |
| TableName | Optional table label |
| CustomerName | Optional customer name |
| CustomerMobile | Optional customer mobile |
| Notes | Optional order notes |
| Subtotal | Snapshot subtotal |
| TaxTotal | Snapshot tax total |
| GrandTotal | Snapshot grand total |
| ConfirmedAt | Confirmation timestamp |
| CancelledAt | Cancellation timestamp |
| CancelReason | Cancellation reason |
| CreatedByUserId | Created by |
| ConfirmedByUserId | Confirmed by |
| CancelledByUserId | Cancelled by |
| CreatedAt | Created timestamp |
| UpdatedAt | Updated timestamp |

Rules:

* RestaurantId comes from the JWT restaurant scope.
* BranchId must belong to the current restaurant and must be active on create.
* Draft orders can be updated.
* Confirmed orders cannot be updated in this foundation slice.
* Cancelled orders cannot be updated or confirmed.
* Order numbers are generated server-side in the format `ORD-YYYYMMDD-0001`.
* The number is backed by a persisted daily sequence per restaurant, branch, and day.
* The public format remains unchanged.
* Concurrency risk is reduced by the sequence table and the unique order-number index, but the numbering path still relies on transactional ordering and should be monitored under very high write contention.

## PosOrderNumberSequences

Stores the last allocated order number sequence per restaurant, branch, and day.

| Column | Description |
|---|---|
| PosOrderNumberSequenceId | Primary key |
| RestaurantId | Linked restaurant |
| BranchId | Linked branch |
| OrderDate | UTC day for the sequence |
| LastSequence | Last allocated sequence value |
| CreatedAt | Created timestamp |
| UpdatedAt | Updated timestamp |

Rules:

* One row exists per restaurant + branch + day.
* `LastSequence` starts at 1 for the first order on that day.
* Order numbers still render as `ORD-YYYYMMDD-0001`.
* This slice only improves numbering safety; it does not add billing, payment, kitchen, or inventory behavior.

## PosOrderLines

Stores line-level snapshots for POS orders.

| Column | Description |
|---|---|
| PosOrderLineId | Primary key |
| PosOrderId | Linked POS order |
| RestaurantId | Linked restaurant |
| MenuItemId | Linked menu item |
| MenuCategoryId | Linked menu category |
| MenuItemNameSnapshot | Item name captured at order time |
| MenuCategoryNameSnapshot | Category name captured at order time |
| SkuSnapshot | SKU captured at order time |
| UnitPrice | Item price captured at order time |
| TaxRate | Item tax rate captured at order time |
| Quantity | Ordered quantity |
| LineSubtotal | Snapshot line subtotal |
| LineTax | Snapshot line tax |
| LineTotal | Snapshot line total |
| Notes | Optional line notes |
| DisplayOrder | Stable line ordering on the order |
| CreatedAt | Created timestamp |
| UpdatedAt | Updated timestamp |

Rules:

* Menu item, category, price, SKU, and tax values are snapshotted from the current menu item when the line is created or updated.
* Later menu price changes must not alter historical order totals.
* Duplicate menu items are allowed as separate lines so line-specific notes remain auditable.
* Line totals are recalculated server-side.
* No billing, payment, kitchen ticket, or inventory movement is created in this slice.

## Billing Foundation

This foundation slice adds restaurant-scoped bill and payment records for confirmed POS orders only. It does not add payment gateways, kitchen flow, inventory deduction, or frontend billing screens.

## Bills

Stores bill headers generated from confirmed POS orders.

| Column | Description |
|---|---|
| BillId | Primary key |
| RestaurantId | Linked restaurant |
| BranchId | Linked branch |
| PosOrderId | Linked confirmed POS order |
| BillNumber | Auto-generated bill number, unique per restaurant + branch |
| BusinessDate | Restaurant business date |
| Status | Unpaid, PartiallyPaid, Paid, Cancelled |
| Subtotal | Snapshot subtotal copied from POS order |
| TaxTotal | Snapshot tax total copied from POS order |
| GrandTotal | Snapshot grand total copied from POS order |
| AmountPaid | Recorded payment total |
| BalanceDue | Remaining unpaid balance |
| CreatedByUserId | Created by |
| CancelledByUserId | Cancelled by |
| CancelledAt | Cancellation timestamp |
| CancelReason | Cancellation reason |
| CreatedAt | Created timestamp |
| UpdatedAt | Updated timestamp |

Rules:

* Bills can only be created from confirmed POS orders.
* Bill totals are copied from POS order snapshots and must not be recalculated from current menu prices.
* Non-cancelled bills are unique per POS order.
* Bill numbers are generated server-side in the format `BILL-YYYYMMDD-0001`.
* The number is backed by a persisted daily sequence per restaurant, branch, and day.
* The public format remains unchanged.
* BusinessDate is stored separately from CreatedAt so late-night orders remain on the correct restaurant day.
* Concurrency risk is reduced by the sequence table and the unique bill-number index, but the numbering path still relies on transactional ordering and should be monitored under very high write contention.

## BillNumberSequences

Stores the last allocated bill number sequence per restaurant, branch, and day.

| Column | Description |
|---|---|
| BillNumberSequenceId | Primary key |
| RestaurantId | Linked restaurant |
| BranchId | Linked branch |
| BillDate | UTC day for the sequence |
| LastSequence | Last allocated sequence value |
| CreatedAt | Created timestamp |
| UpdatedAt | Updated timestamp |

Rules:

* One row exists per restaurant + branch + day.
* `LastSequence` starts at 1 for the first bill on that day.

## BillLines

Stores bill line snapshots copied from POS order lines.

| Column | Description |
|---|---|
| BillLineId | Primary key |
| BillId | Linked bill |
| RestaurantId | Linked restaurant |
| PosOrderLineId | Linked source POS order line |
| MenuItemId | Linked menu item snapshot |
| MenuCategoryId | Linked menu category snapshot |
| MenuItemNameSnapshot | Item name captured at bill time |
| MenuCategoryNameSnapshot | Category name captured at bill time |
| SkuSnapshot | SKU captured at bill time |
| UnitPrice | Item price captured at bill time |
| TaxRate | Item tax rate captured at bill time |
| Quantity | Billed quantity |
| LineSubtotal | Snapshot line subtotal |
| LineTax | Snapshot line tax |
| LineTotal | Snapshot line total |
| Notes | Optional line notes |
| DisplayOrder | Stable line ordering on the bill |
| CreatedAt | Created timestamp |

Rules:

* Bill lines are copied from POS order line snapshots.
* Current menu prices must not be consulted when creating bill lines.

## Payments

Stores bill payment records.

| Column | Description |
|---|---|
| PaymentId | Primary key |
| RestaurantId | Linked restaurant |
| BranchId | Linked branch |
| BillId | Linked bill |
| CashierShiftId | Optional linked cashier shift |
| PaymentNumber | Auto-generated payment number, unique per restaurant + branch + day |
| PaymentMode | Cash, Card, Upi, Other |
| Status | Recorded, Cancelled |
| Amount | Payment amount |
| ReferenceNumber | Optional payment reference |
| Notes | Optional payment notes |
| RecordedByUserId | Recorded by |
| CancelledByUserId | Cancelled by |
| CancelledAt | Cancellation timestamp |
| CancelReason | Cancellation reason |
| CreatedAt | Created timestamp |
| UpdatedAt | Updated timestamp |

Rules:

* Payment amounts must be positive and cannot exceed the current bill balance.
* Recorded payments update the parent bill status to `Unpaid`, `PartiallyPaid`, or `Paid`.
* Cash payments must link to the active cashier shift for the same restaurant, branch, and cashier.
* Billing/payment does not mutate the cashier shift expected cash amount directly.
* `Other` remains supported in the backend for report and compatibility reasons, but cashier-facing entry screens should not expose it.

## BillPrintEvents

Stores audited print and reprint events for customer bills.

| Column | Description |
|---|---|
| BillPrintEventId | Primary key |
| RestaurantId | Linked restaurant |
| BranchId | Linked branch |
| BillId | Linked bill |
| PrintedByUserId | Optional user who triggered the print |
| PrintSequence | Per-bill print sequence |
| PrintReason | Optional reason or context supplied by the caller |
| CreatedAt | Created timestamp |

Rules:

* GET receipt responses are read-only and do not create a print event.
* Each print or reprint from the billing screen creates a new `BillPrintEvents` row.
* Print sequence starts at 1 and increments per bill.
* Receipt previews are built from persisted bill, bill line, and payment snapshots only.
* Browser printing happens only after the print-event write succeeds.
* Cash payments must link to an open cashier shift for the bill branch and cashier.
* Cancelling a linked cash payment must not mutate expected shift cash in this foundation slice.

## CashierShifts

Stores branch-scoped cashier shift sessions for cash reconciliation.

| Column | Description |
|---|---|
| CashierShiftId | Primary key |
| RestaurantId | Linked restaurant |
| BranchId | Linked branch |
| OpenedByUserId | User who opened the shift |
| ClosedByUserId | User who closed the shift |
| Status | Open, Closed |
| OpeningCashAmount | Opening cash amount |
| ExpectedCashAmount | Expected cash after openings, movements, and cash payments |
| CountedCashAmount | Counted cash entered at close |
| CashVarianceAmount | Counted minus expected cash |
| OpenedAt | Opening timestamp |
| ClosedAt | Closing timestamp |
| OpeningNote | Optional opening note |
| ClosingNote | Optional closing note |
| CreatedAt | Created timestamp |
| UpdatedAt | Updated timestamp |

Rules:

* Branch must belong to the current restaurant.
* Branch must be active to open a shift.
* Only one open shift is allowed per branch at a time.
* Opening cash starts the expected cash amount.
* Closed shifts are not modified.

## CashDrawerMovements

Stores manual cash movements within a cashier shift.

| Column | Description |
|---|---|
| CashDrawerMovementId | Primary key |
| RestaurantId | Linked restaurant |
| BranchId | Linked branch |
| CashierShiftId | Linked cashier shift |
| MovementType | CashIn, CashOut, SafeDrop, Adjustment |
| Amount | Movement amount entered by user |
| Reason | Reason for the movement |
| CreatedByUserId | User who created the movement |
| CreatedAt | Created timestamp |

Rules:

* Movements belong to an open shift.
* Reason is required.
* `CashIn`, `CashOut`, and `SafeDrop` require positive amounts.
* `Adjustment` may be positive or negative.
* Movements update expected cash using signed effects.
* Payment cancellations are auditable and recalculate the parent bill balance.
* Payment numbers are generated server-side in the format `PAY-YYYYMMDD-0001`.

## PaymentNumberSequences

Stores the last allocated payment number sequence per restaurant, branch, and day.

| Column | Description |
|---|---|
| PaymentNumberSequenceId | Primary key |
| RestaurantId | Linked restaurant |
| BranchId | Linked branch |
| PaymentDate | UTC day for the sequence |
| LastSequence | Last allocated sequence value |
| CreatedAt | Created timestamp |
| UpdatedAt | Updated timestamp |

Rules:

* One row exists per restaurant + branch + day.
* `LastSequence` starts at 1 for the first payment on that day.

## RestaurantTables

Stores restaurant dining tables.

| Column       | Description                             |
| ------------ | --------------------------------------- |
| TableId      | Primary key                             |
| RestaurantId | Linked restaurant                       |
| BranchId     | Linked branch                           |
| TableNumber  | Table number/name                       |
| Capacity     | Seating capacity                        |
| Status       | Available, Occupied, Reserved, Inactive |

## TableSessions

Tracks customer usage of a table.

| Column         | Description                     |
| -------------- | ------------------------------- |
| TableSessionId | Primary key                     |
| TableId        | Linked table                    |
| BusinessDate   | Restaurant business date        |
| OpenedByUserId | User who opened session         |
| OpenedAt       | Open timestamp                  |
| ClosedByUserId | User who closed session         |
| ClosedAt       | Close timestamp                 |
| Status         | Open, Billed, Closed, Cancelled |

## Orders

Stores eat-in and parcel orders.

| Column          | Description                                                       |
| --------------- | ----------------------------------------------------------------- |
| OrderId         | Primary key                                                       |
| RestaurantId    | Linked restaurant                                                 |
| BranchId        | Linked branch                                                     |
| OrderNumber     | Auto-generated order number                                       |
| OrderType       | EatIn, Parcel, Delivery                                           |
| TableSessionId  | Linked table session, if eat-in                                   |
| TokenNumber     | Parcel token number                                               |
| BusinessDate    | Restaurant business date                                          |
| Status          | Draft, SentToKitchen, Preparing, Ready, Served, Billed, Cancelled |
| CreatedByUserId | Created by                                                        |
| CreatedAt       | Created timestamp                                                 |

## OrderItems

Stores items inside an order.

| Column       | Description                                             |
| ------------ | ------------------------------------------------------- |
| OrderItemId  | Primary key                                             |
| OrderId      | Linked order                                            |
| MenuItemId   | Linked menu item                                        |
| Quantity     | Ordered quantity                                        |
| UnitPrice    | Price captured at order time                            |
| LineTotal    | Quantity × unit price                                   |
| Status       | New, SentToKitchen, Preparing, Ready, Served, Cancelled |
| CancelReason | Reason if cancelled                                     |
| CreatedAt    | Created timestamp                                       |

## OrderItemStatusHistory

Tracks order item status changes.

| Column          | Description           |
| --------------- | --------------------- |
| StatusHistoryId | Primary key           |
| OrderItemId     | Linked order item     |
| OldStatus       | Previous status       |
| NewStatus       | New status            |
| ChangedByUserId | Changed by            |
| ChangedAt       | Changed timestamp     |
| Reason          | Reason, if applicable |

---

# 5. Kitchen Display

## KitchenTickets

Stores kitchen preparation tickets generated from confirmed POS orders.

| Column                  | Description                                               |
| ----------------------- | --------------------------------------------------------- |
| KitchenTicketId         | Primary key                                               |
| RestaurantId            | Linked restaurant                                         |
| BranchId                | Linked branch                                             |
| PosOrderId              | Linked confirmed POS order                                |
| TicketNumber            | Kitchen ticket number, immutable after creation           |
| Status                  | Pending, Preparing, Ready, Served, Cancelled             |
| OrderNumberSnapshot     | POS order number snapshot                                 |
| OrderTypeSnapshot       | POS order type snapshot                                   |
| CreatedByUserId         | Created by                                                |
| LastStatusChangedByUserId | Last status changed by                                  |
| CancelledByUserId       | Cancelled by                                              |
| CancelledAt             | Cancellation timestamp                                    |
| CancelReason            | Cancellation reason                                       |
| CreatedAt               | Created timestamp                                         |
| UpdatedAt               | Updated timestamp                                         |
| PreparingAt             | First entered Preparing                                   |
| ReadyAt                 | First entered Ready                                       |
| ServedAt                | First entered Served                                      |

Rules:

* Kitchen tickets are created only from confirmed POS order snapshots.
* Draft and cancelled POS orders cannot create kitchen tickets.
* Only one non-cancelled kitchen ticket can exist for a POS order.
* Ticket numbers are generated server-side with a persisted daily sequence.
* No printer, display-device, or kitchen hardware assignment is included in this foundation.

## KitchenTicketLines

Stores ticket lines copied from POS order line snapshots.

| Column                   | Description                           |
| ------------------------ | ------------------------------------- |
| KitchenTicketLineId      | Primary key                           |
| KitchenTicketId          | Linked kitchen ticket                 |
| RestaurantId             | Linked restaurant                     |
| PosOrderLineId           | Linked source POS order line          |
| MenuItemId               | Linked menu item snapshot             |
| MenuCategoryId           | Linked menu category snapshot         |
| MenuItemNameSnapshot     | Item name captured from POS order     |
| MenuCategoryNameSnapshot  | Category name captured from POS order  |
| SkuSnapshot              | SKU captured from POS order           |
| Quantity                 | Quantity to prepare                   |
| Notes                    | Line notes visible to kitchen         |
| DisplayOrder             | Stable line ordering                  |
| CreatedAt                | Created timestamp                     |

Rules:

* Ticket lines are copied from POS order line snapshots.
* Current menu names, SKUs, prices, and availability do not affect historical kitchen tickets.
* Separate POS lines remain separate kitchen lines.
* Price, tax, and totals are not exposed in the kitchen ticket foundation.

## KitchenTicketNumberSequences

Stores the last allocated kitchen ticket number sequence per restaurant, branch, and day.

| Column                        | Description                 |
| ----------------------------- | --------------------------- |
| KitchenTicketNumberSequenceId | Primary key                 |
| RestaurantId                  | Linked restaurant           |
| BranchId                      | Linked branch               |
| TicketDate                    | UTC day for the sequence    |
| LastSequence                  | Last allocated sequence     |
| CreatedAt                     | Created timestamp           |
| UpdatedAt                     | Updated timestamp           |

Rules:

* One row exists per restaurant + branch + day.
* `LastSequence` starts at 1 for the first kitchen ticket on that day.
* The public number format is `KIT-YYYYMMDD-0001`.

---

# 6. Billing and Payments

## Bills

Stores customer bills.

| Column          | Description                                           |
| --------------- | ----------------------------------------------------- |
| BillId          | Primary key                                           |
| RestaurantId    | Linked restaurant                                     |
| BranchId        | Linked branch                                         |
| OrderId         | Linked order                                          |
| BillNumber      | Auto-generated bill number                            |
| BusinessDate    | Restaurant business date                              |
| GrossAmount     | Amount before tax/discount                            |
| DiscountAmount  | Total discount                                        |
| TaxAmount       | Total tax                                             |
| NetAmount       | Final payable amount                                  |
| Status          | Draft, Issued, PartiallyPaid, Paid, Cancelled, Voided |
| CreatedByUserId | Created by                                            |
| CreatedAt       | Created timestamp                                     |
| ClosedAt        | Closed timestamp                                      |

## BillLines

Stores bill line items.

| Column           | Description               |
| ---------------- | ------------------------- |
| BillLineId       | Primary key               |
| BillId           | Linked bill               |
| OrderItemId      | Linked order item         |
| MenuItemId       | Linked menu item          |
| ItemNameSnapshot | Item name at billing time |
| Quantity         | Quantity                  |
| UnitPrice        | Unit price                |
| LineTotal        | Line total                |

## PaymentMethods

Stores payment methods.

| Column          | Description                            |
| --------------- | -------------------------------------- |
| PaymentMethodId | Primary key                            |
| RestaurantId    | Linked restaurant                      |
| Name            | Cash, UPI, Card, Bank Transfer, Credit |
| IsActive        | Active flag                            |

## Payments

Stores customer payments.

| Column            | Description                   |
| ----------------- | ----------------------------- |
| PaymentId         | Primary key                   |
| BillId            | Linked bill                   |
| PaymentMethodId   | Linked payment method         |
| Amount            | Payment amount                |
| ReferenceNumber   | UPI/card/bank reference       |
| CollectedByUserId | Collected by                  |
| CollectedAt       | Collection timestamp          |
| Status            | Collected, Reversed, Refunded |

## CashDrawerSessions

Tracks cashier cash sessions.

| Column              | Description                    |
| ------------------- | ------------------------------ |
| CashDrawerSessionId | Primary key                    |
| RestaurantId        | Linked restaurant              |
| BranchId            | Linked branch                  |
| BusinessDate        | Restaurant business date       |
| OpenedByUserId      | Opened by                      |
| OpeningCash         | Opening cash amount            |
| OpenedAt            | Opening timestamp              |
| ClosedByUserId      | Closed by                      |
| ClosingCash         | Actual closing cash            |
| ExpectedCash        | Expected cash from system      |
| CashDifference      | Difference amount              |
| ClosedAt            | Closing timestamp              |
| Status              | Open, Closed, VarianceReported |

## CashLedger

Stores money-in and money-out ledger records.

| Column          | Description                                     |
| --------------- | ----------------------------------------------- |
| CashLedgerId    | Primary key                                     |
| RestaurantId    | Linked restaurant                               |
| BranchId        | Linked branch                                   |
| BusinessDate    | Restaurant business date                        |
| EntryType       | MoneyIn, MoneyOut                               |
| SourceType      | BillPayment, VendorPayment, Expense, Adjustment |
| SourceId        | Related source record                           |
| PaymentMethodId | Linked payment method                           |
| Amount          | Amount                                          |
| CreatedByUserId | Created by                                      |
| CreatedAt       | Created timestamp                               |

---

# 7. Inventory

## InventoryCategories

Stores inventory categories.

| Column              | Description       |
| ------------------- | ----------------- |
| InventoryCategoryId | Primary key       |
| RestaurantId        | Linked restaurant |
| Name                | Category name     |
| IsActive            | Active flag       |

Examples:

* Grocery
* Meat
* Vegetables
* Fruits
* Gas
* Firewood
* Water
* Snacks
* Drinks

## InventoryUnits

Stores inventory units.

| Column          | Description                                |
| --------------- | ------------------------------------------ |
| InventoryUnitId | Primary key                                |
| Name            | Unit name                                  |
| Symbol          | kg, litre, packet, bottle, cylinder, piece |

## InventoryItems

Stores stock items.

| Column              | Description            |
| ------------------- | ---------------------- |
| InventoryItemId     | Primary key            |
| RestaurantId        | Linked restaurant      |
| InventoryCategoryId | Linked category        |
| DefaultUnitId       | Linked unit            |
| Name                | Item name              |
| LocalName           | Local language name    |
| CurrentStock        | Cached stock snapshot (updated only when `StockMovements` is posted; not edited directly) |
| LowStockThreshold   | Low stock threshold    |
| OutOfStockThreshold | Out-of-stock threshold |
| IsActive            | Active flag            |

Examples:

* Rice
* Cooking Oil
* Chicken
* Eggs
* Tomato
* Onion
* Gas Cylinder
* Firewood
* Water Can
* Juice Bottle
* Carbonated Drink

## StockMovements

Stores all stock changes.

| Column          | Description                                                    |
| --------------- | -------------------------------------------------------------- |
| StockMovementId | Primary key                                                    |
| RestaurantId    | Linked restaurant                                              |
| BranchId        | Linked branch                                                  |
| InventoryItemId | Linked inventory item                                          |
| MovementType    | OpeningStock, Purchase, Usage, Wastage, Adjustment, Correction |
| Quantity        | Movement quantity                                              |
| UnitId          | Linked unit                                                    |
| SourceType      | VendorBill, ManualAdjustment, DailyClosing, RecipeConsumption  |
| SourceId        | Source record ID                                               |
| BeforeQuantity  | Stock before movement                                          |
| AfterQuantity   | Stock after movement                                           |
| Reason          | Reason                                                         |
| CreatedByUserId | Created by                                                     |
| CreatedAt       | Created timestamp                                              |

## DailyStockSessions

Stores daily stock checking sessions.

| Column              | Description              |
| ------------------- | ------------------------ |
| DailyStockSessionId | Primary key              |
| RestaurantId        | Linked restaurant        |
| BranchId            | Linked branch            |
| BusinessDate        | Restaurant business date |
| OpenedByUserId      | Opened by                |
| ClosedByUserId      | Closed by                |
| Status              | Open, Closed             |
| CreatedAt           | Created timestamp        |
| ClosedAt            | Closed timestamp         |

## DailyStockCounts

Stores item-level daily stock counts.

| Column              | Description                  |
| ------------------- | ---------------------------- |
| DailyStockCountId   | Primary key                  |
| DailyStockSessionId | Linked stock session         |
| InventoryItemId     | Linked inventory item        |
| OpeningQuantity     | Opening quantity             |
| PurchasedQuantity   | Purchased quantity           |
| ClosingQuantity     | Closing quantity             |
| CalculatedUsage     | Opening + Purchase - Closing |
| WastageQuantity     | Wastage quantity             |
| VarianceQuantity    | Difference quantity          |

---

# 8. Vendors and Vendor Bills

## Vendors

Stores vendor master data.

| Column           | Description            |
| ---------------- | ---------------------- |
| VendorId         | Primary key            |
| RestaurantId     | Linked restaurant      |
| Name             | Vendor name            |
| NormalizedName   | Case-insensitive name  |
| VendorType       | Groceries, Wood, Gas, Water, Snacks, Juice, Fruits, Other |
| BranchId         | Optional linked branch |
| ContactName      | Contact person         |
| MobileNumber     | Mobile number          |
| NormalizedMobileNumber | Normalized mobile number |
| Address          | Vendor address         |
| Notes            | Internal notes         |
| IsActive         | Active flag            |
| CreatedAtUtc     | Created timestamp      |
| UpdatedAtUtc     | Updated timestamp      |

Vendor names are unique within the configured scope. Restaurant-scoped vendors use restaurant-level uniqueness, while branch-specific vendors use restaurant plus branch uniqueness.

Vendor mobile numbers are required and unique within the restaurant when present. Inactive vendors cannot receive new bills.

## VendorBills

Stores purchase bills from vendors.

| Column               | Description                              |
| -------------------- | ---------------------------------------- |
| VendorBillId         | Primary key                              |
| RestaurantId         | Linked restaurant                        |
| BranchId             | Linked branch                            |
| VendorId             | Linked vendor                            |
| BillNumber           | Vendor bill number, optional             |
| NormalizedBillNumber  | Normalized vendor bill number            |
| BillDate             | Vendor bill date                         |
| DueDate              | Optional due date                        |
| Status               | Unpaid, PartiallyPaid, Paid, Cancelled   |
| TotalAmount          | Total bill amount                        |
| PaidAmount           | Active settlement total                  |
| BalanceAmount        | Total minus paid                         |
| Notes                | Internal notes                           |
| CancelledAtUtc       | Cancellation timestamp                   |
| CancelledByUserId    | User who cancelled the bill              |
| CancellationReason   | Cancellation reason                      |
| CreatedAtUtc         | Created timestamp                        |
| UpdatedAtUtc         | Updated timestamp                        |

Vendor bill totals are derived from bill lines. Active settlements update paid and balance amounts. Cancelled bills cannot accept new settlements. Cancellation is blocked when inventory-linked stock-in movements already exist.

Vendor bill numbers are unique per restaurant and vendor when present, after trimming and normalization.

## VendorBillLines

Stores vendor bill line items.

| Column             | Description               |
| ------------------ | ------------------------- |
| VendorBillLineId   | Primary key               |
| RestaurantId       | Linked restaurant         |
| BranchId           | Linked branch             |
| VendorBillId       | Linked vendor bill        |
| InventoryItemId    | Optional linked inventory item |
| InventoryMovementId | Optional linked stock-in movement |
| Description        | Line description          |
| Quantity           | Quantity                  |
| UnitCost           | Unit cost                 |
| LineTotal          | Quantity times unit cost  |
| CreatedAtUtc       | Created timestamp         |
| UpdatedAtUtc       | Updated timestamp         |

If a line links to an inventory item, the system creates a ledger-based stock-in movement and stores the movement id on the line. Inventory remains movement-derived and is never edited directly.

## VendorSettlements

Stores vendor payments.

| Column               | Description           |
| -------------------- | --------------------- |
| VendorSettlementId   | Primary key           |
| RestaurantId         | Linked restaurant     |
| BranchId             | Linked branch         |
| VendorBillId         | Linked vendor bill    |
| PaymentMode          | Cash, UPI, Card, BankTransfer, Other |
| Amount               | Settlement amount     |
| ReferenceNumber      | Payment reference     |
| PaidAtUtc            | Payment timestamp     |
| RecordedByUserId     | Recorded by           |
| Status               | Active, Cancelled     |
| CancelledAtUtc       | Cancellation timestamp |
| CancelledByUserId    | Cancelling user       |
| CancellationReason   | Cancellation reason   |
| CreatedAtUtc         | Created timestamp     |
| UpdatedAtUtc         | Updated timestamp     |

Only active settlements count toward paid and balance amounts. Settlement amounts must be greater than zero and cannot exceed the current bill balance.

---

# 9. Vendor Bill OCR Drafts

The OCR slice keeps untrusted draft data separate from trusted vendor bills.

## VendorBillOcrDrafts

Stores uploaded bill metadata, OCR extraction results, reviewed values, and confirmation status.

| Column                 | Description                                         |
| ---------------------- | --------------------------------------------------- |
| VendorBillOcrDraftId   | Primary key                                         |
| RestaurantId           | Linked restaurant                                   |
| BranchId               | Linked branch                                       |
| UploadedByUserId       | Uploaded by                                         |
| OriginalFileName       | Uploaded file name                                  |
| StoredFilePath         | Secure storage path or blob name                    |
| ContentType            | JPEG, PNG, or PDF                                   |
| FileSizeBytes          | File size                                           |
| Status                 | Uploaded, Extracted, ExtractionFailed, Confirmed, Cancelled |
| ExtractedVendorName    | OCR vendor name, if detected                        |
| ExtractedBillNumber    | OCR bill number, if detected                        |
| ExtractedBillDate      | OCR bill date, if detected                          |
| ExtractedTotalAmount   | OCR total amount, if detected                       |
| ExtractedConfidenceScore | Overall OCR confidence                            |
| ReviewedVendorId       | User-selected vendor                                 |
| ReviewedBillNumber     | User-corrected bill number                           |
| ReviewedBillDate       | User-corrected bill date                             |
| ReviewedTotalAmount    | User-corrected total amount                          |
| SafeErrorMessage       | Safe user-facing OCR error message                   |
| ConfirmedVendorBillId   | Linked confirmed vendor bill                        |
| ConfirmedByUserId       | Confirmed by                                         |
| ConfirmedAtUtc         | Confirmation timestamp                               |
| CreatedAtUtc           | Created timestamp                                    |
| UpdatedAtUtc           | Updated timestamp                                    |

Rules:

- draft records are restaurant and branch scoped
- uploaded file content is retained for audit and review
- OCR values are not trusted until user confirmation
- confirmation reuses the existing vendor bill creation flow
- OCR drafts never create settlements or stock-in movements directly

## VendorBillOcrDraftLines

Stores extracted and reviewed line-level values for each OCR draft.

| Column                 | Description                              |
| ---------------------- | ---------------------------------------- |
| VendorBillOcrDraftLineId | Primary key                            |
| RestaurantId           | Linked restaurant                        |
| BranchId               | Linked branch                            |
| VendorBillOcrDraftId   | Linked OCR draft                         |
| LineNumber             | Stable draft line order                  |
| ExtractedDescription   | OCR description                          |
| ExtractedQuantity      | OCR quantity                             |
| ExtractedUnitCost      | OCR unit cost                            |
| ExtractedLineTotal     | OCR line total                           |
| ConfidenceScore        | Line confidence                          |
| SelectedInventoryItemId | Optional inventory item link            |
| ReviewedDescription    | User-corrected description               |
| ReviewedQuantity       | User-corrected quantity                  |
| ReviewedUnitCost       | User-corrected unit cost                 |
| ReviewedLineTotal      | User-corrected line total                |
| IsIgnored              | Ignored line flag                        |
| CreatedAtUtc           | Created timestamp                        |
| UpdatedAtUtc           | Updated timestamp                        |

Rules:

- line quantities and costs are reviewed before confirmation
- inventory item links are optional at draft time
- ignored lines stay in the draft but do not create stock movements on confirmation
- confirmed inventory stock changes still happen only through vendor bill confirmation logic

---

# 10. Expenses

---

# 10. Expenses

## ExpenseCategories

Stores expense categories.

| Column            | Description       |
| ----------------- | ----------------- |
| ExpenseCategoryId | Primary key       |
| RestaurantId      | Linked restaurant |
| Name              | Category name     |
| IsActive          | Active flag       |

Examples:

* Rent
* Electricity
* Salary Advance
* Cleaning
* Maintenance
* Transport
* Miscellaneous

## Expenses

Stores money-out records.

| Column            | Description                                |
| ----------------- | ------------------------------------------ |
| ExpenseId         | Primary key                                |
| RestaurantId      | Linked restaurant                          |
| BranchId          | Linked branch                              |
| BusinessDate      | Restaurant business date                   |
| ExpenseCategoryId | Linked category                            |
| PaymentMethodId   | Linked payment method                      |
| Amount            | Expense amount                             |
| PaidTo            | Person/vendor paid                         |
| Reason            | Expense reason                             |
| ProofDocumentPath | Optional proof document path               |
| CreatedByUserId   | Created by                                 |
| ApprovedByUserId  | Approved by                                |
| Status            | Draft, Submitted, Approved, Rejected, Paid |
| CreatedAt         | Created timestamp                          |

---

# 11. Reports and Alerts

## DailyReportRuns

Stores scheduled report runs.

| Column            | Description                 |
| ----------------- | --------------------------- |
| ReportRunId       | Primary key                 |
| RestaurantId      | Linked restaurant           |
| BranchId          | Linked branch               |
| BusinessDate      | Restaurant business date    |
| ReportType        | Morning, Afternoon, Closing |
| GeneratedAt       | Generated timestamp         |
| GeneratedByUserId | Generated by/system         |
| Status            | Generated, Sent, Failed     |

## DailyReportMetrics

Stores report snapshot metrics.

| Column         | Description                                        |
| -------------- | -------------------------------------------------- |
| ReportMetricId | Primary key                                        |
| ReportRunId    | Linked report run                                  |
| MetricGroup    | Sales, Cash, Inventory, Vendor, SuspiciousActivity |
| MetricName     | Metric name                                        |
| MetricValue    | Metric value                                       |
| DisplayOrder   | Display order                                      |

## AlertRules

Stores alert rules.

| Column         | Description                                                     |
| -------------- | --------------------------------------------------------------- |
| AlertRuleId    | Primary key                                                     |
| RestaurantId   | Linked restaurant                                               |
| BranchId       | Optional branch                                                 |
| AlertType      | LowStock, CashDifference, HighCancellation, DuplicateVendorBill |
| ThresholdValue | Threshold                                                       |
| IsActive       | Active flag                                                     |

## AlertEvents

Stores triggered alerts.

| Column               | Description                  |
| -------------------- | ---------------------------- |
| AlertEventId         | Primary key                  |
| RestaurantId         | Linked restaurant            |
| BranchId             | Linked branch                |
| AlertRuleId          | Linked alert rule            |
| Severity             | Low, Medium, High, Critical  |
| Message              | Alert message                |
| EntityType           | Related entity type          |
| EntityId             | Related entity ID            |
| Status               | Open, Acknowledged, Resolved |
| CreatedAt            | Created timestamp            |
| AcknowledgedByUserId | Acknowledged by              |
| AcknowledgedAt       | Acknowledged timestamp       |

## Notifications

Stores app/email/WhatsApp notification records.

| Column         | Description                   |
| -------------- | ----------------------------- |
| NotificationId | Primary key                   |
| RestaurantId   | Linked restaurant             |
| BranchId       | Linked branch                 |
| Channel        | App, Email, WhatsApp, SMS     |
| Recipient      | Recipient address/number/user |
| Subject        | Notification subject          |
| Message        | Notification message          |
| Status         | Pending, Sent, Failed         |
| SentAt         | Sent timestamp                |
| CreatedAt      | Created timestamp             |

---

# 12. Audit and Security Logs

## AuditLogs

Stores anti-fraud audit records.

| Column       | Description               |
| ------------ | ------------------------- |
| AuditLogId   | Primary key               |
| RestaurantId | Linked restaurant         |
| BranchId     | Linked branch             |
| UserId       | User who performed action |
| Action       | Action name               |
| EntityType   | Entity/table affected     |
| EntityId     | Entity ID                 |
| OldValueJson | Previous value            |
| NewValueJson | New value                 |
| Reason       | Reason for change         |
| RestaurantNameSnapshot | Restaurant name at action time |
| BranchNameSnapshot | Branch name at action time |
| UserNameSnapshot | User name at action time |
| UserMobileSnapshot | User mobile at action time |
| DeviceId     | Device used               |
| IpAddress    | IP address                |
| CreatedAt    | Created timestamp         |

Audit examples:

* Bill cancelled
* Bill reprinted
* Discount applied
* Payment reversed
* Vendor bill OCR overridden
* Inventory adjusted
* Stock corrected
* Vendor payment created
* Expense approved
* Menu price changed

## LoginAttempts

Stores login success and failure attempts.

| Column         | Description             |
| -------------- | ----------------------- |
| LoginAttemptId | Primary key             |
| RestaurantId   | Optional restaurant     |
| UserId         | Optional matched user   |
| MobileNumber   | Attempted mobile number |
| DeviceId       | Device used             |
| IpAddress      | IP address              |
| IsSuccessful   | Success/failure         |
| FailureReason  | Failure reason          |
| AttemptedAt    | Attempt timestamp       |

---

# 13. Future Phase Tables

The recipe foundation tables are now implemented above as `MenuItemRecipeIngredients` and `KitchenTicketInventoryDeductions`.

The remaining future-phase tables stay reserved for later expansion after the core billing, inventory, and vendor flow is stable.

---

# Recommended MVP Table Set

The first production-ready MVP should include these tables:

```text
Restaurants
Branches
RestaurantSettings

Users
Roles
Permissions
UserRoles
RolePermissions
Devices
UserSessions

MenuCategories
MenuItems
MenuItemPriceHistory
KitchenStations

RestaurantTables
TableSessions
Orders
OrderItems
OrderItemStatusHistory

KitchenTickets
KitchenTicketLines
KitchenTicketNumberSequences

Bills
BillLines
PaymentMethods
Payments
CashDrawerSessions
CashLedger

InventoryCategories
InventoryUnits
InventoryItems
StockMovements
DailyStockSessions
DailyStockCounts

VendorCategories
Vendors
VendorBills
VendorBillLines
VendorPayments

VendorBillDocuments
VendorBillOcrResults
VendorBillOcrLines
VendorBillOverrideAudits
InventoryItemAliases

ExpenseCategories
Expenses

DailyReportRuns
DailyReportMetrics
AlertRules
AlertEvents
Notifications

AuditLogs
LoginAttempts
```

Total: 54 tables.

This is a serious MVP. Do not pretend this is a small billing app. The database exists to prove money flow, stock flow, vendor purchases, and staff actions.

---

# Prototype Cut

For an early working prototype, use this smaller set:

```text
Restaurants
Branches

Users
Roles
UserRoles

MenuCategories
MenuItems

RestaurantTables
Orders
OrderItems

KitchenTickets
KitchenTicketLines
KitchenTicketNumberSequences

Bills
BillLines
PaymentMethods
Payments
CashDrawerSessions
CashLedger

InventoryItems
StockMovements

Vendors
VendorBills
VendorBillLines
VendorPayments
VendorBillDocuments
VendorBillOcrResults
VendorBillOcrLines
VendorBillOverrideAudits
InventoryItemAliases

Expenses
DailyReportRuns
DailyReportMetrics
AuditLogs
```

Total: 34 tables.

Use this only for prototype/demo. For real usage, move to the full MVP structure.

---

# Non-Negotiable Tables

These tables must not be removed:

```text
Orders
OrderItems
Bills
BillLines
Payments
CashDrawerSessions
CashLedger
InventoryItems
StockMovements
VendorBills
VendorBillLines
VendorBillDocuments
VendorBillOcrResults
VendorBillOverrideAudits
AuditLogs
```

Without these, BillSoft becomes a normal POS system and loses the leakage-control purpose.
