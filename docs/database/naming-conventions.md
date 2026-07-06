# Database Naming Conventions

## Purpose

This document defines database naming standards for BillSoft.

Database naming must be consistent before migrations are created. Inconsistent naming creates long-term reporting and maintenance issues.

---

# Table Names

Use plural PascalCase table names.

Examples:

```text
Restaurants
Branches
Users
Orders
OrderItems
Bills
BillLines
InventoryItems
StockMovements
VendorBills
AuditLogs
```

---

# Primary Keys

Use singular entity name followed by `Id`.

Examples:

```text
RestaurantId
BranchId
UserId
OrderId
BillId
VendorBillId
```

---

# Foreign Keys

Foreign key column names must match the referenced primary key name.

Examples:

```text
Orders.RestaurantId -> Restaurants.RestaurantId
Orders.BranchId -> Branches.BranchId
OrderItems.OrderId -> Orders.OrderId
BillLines.BillId -> Bills.BillId
```

---

# Date and Time Columns

Use UTC timestamp columns for exact time and `BusinessDate` for restaurant operating date.

Examples:

```text
CreatedAt
UpdatedAt
ConfirmedAt
PaidAt
ClosedAt
BusinessDate
```

Rules:

1. `CreatedAt` and similar timestamp fields should be UTC.
2. `BusinessDate` represents the restaurant operating day.
3. Do not infer business date from timestamp in reporting queries.

---

# Status Columns

Use `Status` for lifecycle state.

Examples:

```text
Orders.Status
Bills.Status
VendorBills.Status
Expenses.Status
```

Status values must be documented in `docs/architecture/status-transitions.md`.

---

# Money Columns

Use explicit amount names.

Examples:

```text
GrossAmount
DiscountAmount
TaxAmount
NetAmount
PaidAmount
BalanceAmount
ExpectedCash
ClosingCash
CashDifference
```

Rules:

1. Use decimal types for money.
2. Do not use floating point types for money.
3. Store currency at branch or transaction level where required.

---

# Quantity Columns

Use explicit quantity names.

Examples:

```text
Quantity
OpeningQuantity
PurchasedQuantity
ClosingQuantity
CalculatedUsage
VarianceQuantity
BeforeQuantity
AfterQuantity
```

Rules:

1. Use decimal type for inventory quantity.
2. Always store unit reference where quantity depends on unit.

---

# Boolean Columns

Use `Is` prefix.

Examples:

```text
IsActive
IsSystemRole
IsTaxable
IsTrusted
```

---

# Audit Columns

Common audit columns:

```text
CreatedByUserId
CreatedAt
UpdatedByUserId
UpdatedAt
ChangedByUserId
ChangedAt
ApprovedByUserId
ApprovedAt
```

Use specific names when the business action matters:

```text
CollectedByUserId
UploadedByUserId
PaidByUserId
OpenedByUserId
ClosedByUserId
```

---

# JSON Columns

Use `Json` suffix for JSON payloads.

Examples:

```text
OldValueJson
NewValueJson
ExtractedJson
```

---

# Index Naming

Use this pattern:

```text
IX_<TableName>_<ColumnName>
```

Examples:

```text
IX_Orders_RestaurantId_BranchId_BusinessDate
IX_Bills_BillNumber
IX_VendorBills_VendorId_BillDate
```

---

# Unique Constraint Naming

Use this pattern:

```text
UX_<TableName>_<ColumnName>
```

Examples:

```text
UX_Bills_BranchId_BillNumber
UX_Orders_BranchId_OrderNumber
UX_VendorBills_VendorId_BillNumber_BillDate
```

---

# Foreign Key Naming

Use this pattern:

```text
FK_<ChildTable>_<ParentTable>_<ColumnName>
```

Example:

```text
FK_OrderItems_Orders_OrderId
FK_BillLines_Bills_BillId
```

---

# Do Not Use

Avoid:

```text
TblOrder
order_tbl
strName
intCount
CreatedDateTimeUTCValue
IsDeleted for business deletion flows
```

Use clear business names instead.
