# BillSoft Product Requirements

## 1. Product Goal

BillSoft is a restaurant billing, kitchen display, inventory, vendor bill OCR, cash-control, and leakage-prevention system.

The product must not be treated as a basic POS application. The primary goal is to help restaurant owners control money, stock, staff actions, vendor purchases, and suspicious activity without needing to sit at the billing counter all day.

BillSoft must answer these questions:

1. What was ordered?
2. What was sent to the kitchen?
3. What was billed?
4. What money was collected?
5. What stock was purchased?
6. What stock was used?
7. What money went out?
8. Who changed, cancelled, discounted, corrected, or overrode anything?

---

## 2. Target Users

## 2.1 Order User / Waiter

The order user creates eat-in and parcel/takeaway orders.

Responsibilities:

- Create eat-in orders.
- Create parcel/takeaway orders.
- Select menu items and quantities.
- Send orders to the kitchen.
- Add additional items before billing.

Restrictions:

- Cannot change item prices.
- Cannot delete completed orders.
- Cannot cancel kitchen-submitted items without reason.
- Cannot apply discounts unless explicitly permitted.

## 2.2 Billing Counter User / Cashier

The cashier generates bills and collects payments.

Responsibilities:

- Generate bills.
- Collect cash, UPI, card, or mixed payment.
- Print or reprint bills.
- Close bills after payment.

Restrictions:

- Cannot edit closed bills.
- Cannot delete bills.
- Cannot apply unauthorized discounts.
- Cannot void or reopen bills without permission.

## 2.3 Kitchen Display User

The kitchen user views food preparation tasks.

Responsibilities:

- View new kitchen tickets.
- Accept orders.
- Mark items as preparing.
- Mark items as ready.
- Mark items as served or packed.

Restrictions:

- Should not see bill totals, revenue, or owner reports.

## 2.4 Restaurant Owner / Admin

The owner/admin monitors and controls the restaurant.

Responsibilities:

- View sales.
- View cash collection.
- View unpaid bills.
- View cancelled bills and discounts.
- View stock status.
- View vendor dues.
- View expenses.
- View suspicious activity.
- Configure menu, users, prices, vendors, inventory, payment methods, and report timings.

## 2.5 Super Admin

The super admin manages the BillSoft platform.

Responsibilities:

- Manage restaurants and branches.
- Manage subscription/license status.
- Support tenant-level configuration.
- Monitor system health and usage.

---

## 3. Core Functional Requirements

## 3.1 Order Management

The system shall support both eat-in and parcel/takeaway orders.

Eat-in order requirements:

1. User shall select a table.
2. User shall add menu items using large, touch-friendly buttons.
3. User shall send selected items to the kitchen.
4. User shall add additional items before final billing.
5. Table shall remain open until the bill is paid and closed.

Parcel/takeaway order requirements:

1. System shall generate a token number.
2. User shall add menu items.
3. User shall send selected items to the kitchen.
4. Parcel orders shall clearly show parcel/takeaway status.
5. Parcel order shall be closed only after billing or cancellation with reason.

## 3.2 Kitchen Display

The system shall display kitchen tickets for items to be prepared.

Kitchen display shall show:

- Token number or table number.
- Parcel/eat-in indicator.
- Item name.
- Quantity.
- Preparation status.
- Order time.

Kitchen ticket statuses:

- New
- Accepted
- Preparing
- Ready
- Served/Packed
- Cancelled

## 3.3 Billing

The system shall generate bills from orders.

Billing requirements:

1. Bill numbers shall be auto-generated.
2. Bill numbers shall be immutable after issue.
3. Bills shall include item, quantity, rate, tax, discount, and net amount.
4. Closed bills shall not be editable.
5. Bills shall not be physically deleted.
6. Bill cancellation shall require a reason.
7. Bill reprint shall be audit logged.

## 3.4 Payment Collection

The system shall support payment collection through:

- Cash
- UPI
- Card
- Bank transfer
- Credit/customer due, if enabled
- Mixed payment

Payment requirements:

1. A bill may have one or more payment records.
2. Every payment shall capture amount, method, user, timestamp, and optional reference number.
3. Reversal/refund shall require permission and audit logging.
4. Cash payments shall update the cash ledger.

## 3.5 Cash Drawer and Cash Control

The system shall support cashier cash sessions.

Cash drawer requirements:

1. Cashier shall open a cash drawer session with opening cash.
2. System shall calculate expected cash using cash payments and money-out entries.
3. Cashier/admin shall enter actual closing cash.
4. System shall calculate cash difference.
5. Cash difference shall be visible to owner/admin.
6. Cash drawer closing shall be audit logged.

---

## 4. Inventory Requirements

The system shall manage stock for restaurant groceries and supplies.

Inventory examples:

- Rice
- Cooking oil
- Chicken
- Mutton
- Eggs
- Vegetables
- Fruits
- Gas cylinder
- Firewood
- Water can
- Snacks
- Juice bottles
- Carbonated drinks

Inventory requirements:

1. System shall store inventory items and units.
2. System shall show current stock.
3. System shall show low-stock items.
4. System shall show out-of-stock items.
5. System shall record purchases.
6. System shall record wastage.
7. System shall record manual adjustments with reason.
8. System shall calculate daily usage.
9. System shall not directly edit stock without stock movement history.

Stock usage formula:

```text
Usage = Opening Stock + Purchases - Closing Stock
```

Stock movement types:

- OpeningStock
- Purchase
- Usage
- Wastage
- Adjustment
- Correction

---

## 5. Vendor Bill and Settlement Requirements

The system shall manage vendors and vendor bills.

Vendor categories may include:

- Grocery
- Gas
- Water
- Firewood
- Snacks
- Juice
- Fruits
- Meat
- Vegetables

Vendor bill requirements:

1. User shall create or upload a vendor bill.
2. Vendor bill shall store bill number, date, vendor, total amount, paid amount, and balance amount.
3. Vendor bill shall support unpaid, partially paid, paid, rejected, and duplicate-suspected statuses.
4. Vendor payments shall update vendor balance.
5. Vendor payment shall create money-out ledger records.
6. Vendor bill changes shall be audited.

---

## 6. Vendor Bill OCR Requirements

The system shall support scanning and extraction of vendor bills.

Supported bill types:

- English printed bills
- Tamil printed bills
- Mixed English/Tamil bills
- Uploaded image bills
- Uploaded PDF bills
- Camera-captured bills

OCR requirements:

1. System shall save the original uploaded bill document.
2. System shall run OCR extraction after upload.
3. System shall detect language where possible.
4. System shall extract vendor name, bill number, bill date, item names, quantity, unit, rate, amount, and total.
5. System shall store raw OCR text.
6. System shall store structured OCR JSON.
7. System shall show confidence score.
8. System shall match OCR item names to inventory items using aliases.
9. System shall show extracted values to user for confirmation.
10. System shall not update inventory directly from OCR without user confirmation.

OCR confirmation flow:

```text
Upload bill document
  → Store original bill copy
  → Run OCR
  → Store OCR result
  → Match items to inventory aliases
  → Show review screen
  → User confirms or corrects values
  → Audit overrides
  → Confirm vendor bill
  → Create stock movements
  → Update vendor payable/payment status
```

## 6.1 OCR Manual Override Requirements

If a scanned value is changed by a user, the system shall:

1. Store the original OCR value.
2. Store the corrected value.
3. Require a reason.
4. Store remarks if reason is `Other`.
5. Capture user, timestamp, vendor bill reference, and affected field.
6. Show override activity in audit reports.

Override reasons:

- OCRIncorrect
- BillUnclear
- WrongItemMapping
- UnitConversion
- VendorMistake
- Other

## 6.2 Inventory Item Alias Requirements

The system shall support aliases for English, Tamil, and vendor-specific item names.

Examples:

| Alias | Inventory Item |
|---|---|
| அரிசி | Rice |
| Ponni Rice | Rice |
| எண்ணெய் | Cooking Oil |
| கோழி | Chicken |
| மரக்கட்டை | Firewood |

Approved aliases shall be reused for future OCR matching.

---

## 7. Document Storage Requirements

The system shall store vendor bill documents outside the database in object storage.

Recommended storage providers:

- Azure Blob Storage
- AWS S3
- Google Cloud Storage
- Local storage only for development

The database shall store only:

- Storage provider
- Storage container/bucket
- Storage path
- Original file name
- Content type
- File size
- File hash
- Upload user
- Upload timestamp
- Document status

Document requirements:

1. Original vendor bill copy shall be preserved.
2. Bill documents shall not be publicly accessible.
3. Duplicate detection shall use bill number, vendor, amount, bill date, file hash, and similar OCR text where possible.
4. Deleting or rejecting bill documents shall be controlled and audited.

---

## 8. Money-In and Money-Out Requirements

The system shall record all money-in and money-out activity.

Money-in examples:

- Customer cash payment
- UPI payment
- Card payment
- Customer credit repayment

Money-out examples:

- Vendor payment
- Expense
- Staff salary advance
- Gas payment
- Water payment
- Firewood payment
- Maintenance
- Miscellaneous expense

Ledger requirements:

1. Every payment and expense shall create ledger entries.
2. Ledger entries shall include business date, branch, source type, source ID, amount, payment method, user, and timestamp.
3. Ledger entries shall support daily reports and cash reconciliation.

---

## 9. Daily Reporting Requirements

The system shall generate reports three times per day.

Default report types:

- Morning
- Afternoon
- Closing/Night

Reports shall include:

1. Total sales.
2. Cash sales.
3. UPI sales.
4. Card sales.
5. Parcel sales.
6. Eat-in sales.
7. Number of bills.
8. Average bill value.
9. Money out.
10. Vendor payments.
11. Expenses.
12. Expected cash.
13. Actual cash, if entered.
14. Cash difference.
15. Cancelled bills.
16. Cancelled items.
17. Discounts.
18. Reopened or voided bills.
19. Low-stock items.
20. Out-of-stock items.
21. Vendor dues.
22. Duplicate vendor bill warnings.
23. OCR override count.
24. Suspicious activity summary.

Reports should be stored as snapshots so the owner can compare what was reported at a point in time even if later corrections are made.

---

## 10. Audit Requirements

Audit logging is mandatory.

The system shall audit:

- Login attempts
- Order creation
- Order item cancellation
- Kitchen status changes
- Bill generation
- Bill cancellation
- Bill reprint
- Discount application
- Payment collection
- Payment reversal/refund
- Cash drawer opening
- Cash drawer closing
- Cash difference
- Inventory adjustment
- Stock correction
- Vendor bill upload
- OCR extraction
- OCR override
- Vendor bill confirmation
- Vendor payment
- Expense creation
- Expense approval
- Menu price change
- User and role changes

Audit records shall include:

- Restaurant
- Branch
- User
- Device, where available
- Action
- Entity type
- Entity ID
- Old value, where applicable
- New value, where applicable
- Reason, where applicable
- Timestamp

---

## 11. Anti-Fraud Requirements

BillSoft shall reduce common restaurant leakage patterns.

| Leakage Pattern | Required Control |
|---|---|
| Staff takes order but does not bill | Kitchen order must link to order/bill flow |
| Cashier deletes bill | No hard delete; cancellation with reason only |
| Unauthorized discount | Permission or approval required |
| Price changed secretly | Price history and audit required |
| Parcel prepared but not billed | Token must close through billing or cancellation |
| Cash collected but not reported | Cash drawer and cash ledger reconciliation |
| Fake vendor payment | Vendor bill/payment audit and optional bill proof |
| Stock stolen or wasted | Daily stock usage and stock movement ledger |
| OCR bill value manually changed | Override audit with reason |
| Same vendor bill uploaded twice | Duplicate detection warning |

---

## 12. Usability Requirements

The system shall be usable by restaurant staff with basic education.

Staff-facing screens shall use:

- Large buttons
- Minimal typing
- Clear labels
- Touch-friendly layouts
- Simple categories
- Quantity controls
- Clear parcel/eat-in separation
- Local language support where possible

Owner/admin screens may use dashboards, filters, reports, and tables.

Kitchen screens shall prioritize speed and readability over complex controls.

---

## 13. Role-Based Access Requirements

The system shall support role-based access control.

Minimum roles:

- SuperAdmin
- RestaurantOwner
- Admin
- Cashier
- Waiter
- KitchenUser
- InventoryUser
- AccountsUser

Sensitive actions requiring explicit permission:

- Apply discount
- Cancel bill
- Void bill
- Reverse/refund payment
- Adjust inventory
- Confirm vendor bill
- Override OCR values
- Pay vendor
- Approve expense
- Change menu price
- Manage users/roles
- View reports

---

## 14. MVP Scope

The first production MVP shall include:

1. Restaurant and branch setup.
2. User, role, and permission setup.
3. Menu category and menu item setup.
4. Eat-in order flow.
5. Parcel/takeaway order flow.
6. Kitchen display flow.
7. Bill generation.
8. Payment collection.
9. Cash drawer session.
10. Cash ledger.
11. Inventory item setup.
12. Stock movement ledger.
13. Daily stock count.
14. Vendor setup.
15. Vendor bill entry.
16. Vendor bill document upload.
17. English/Tamil OCR extraction.
18. OCR review and confirmation.
19. OCR override audit.
20. Vendor payment settlement.
21. Expense entry.
22. Daily report snapshots.
23. Alerts for low stock, cash difference, cancellations, and duplicate bills.
24. Audit logs.

---

## 15. Future Phase Scope

Future phases may include:

1. Recipe-based expected stock usage.
2. Expected vs actual stock variance.
3. WhatsApp report delivery.
4. Mobile owner app.
5. Multi-branch owner dashboard.
6. Sales forecasting.
7. Vendor price comparison.
8. Auto reorder suggestion.
9. Staff performance monitoring.
10. Advanced suspicious activity detection.

---

## 16. Database Reference

The database table design is documented in:

```text
docs/database/database-tables.md
```

The database design must remain aligned with this product requirements document.
