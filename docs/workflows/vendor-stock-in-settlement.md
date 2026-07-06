# Vendor Stock-In and Settlement Workflow

## Purpose

This workflow covers the non-OCR vendor foundation for BillSoft:

- vendor master data
- vendor bill entry
- optional inventory-linked stock-in creation
- settlement payments against vendor bills
- payable balance tracking
- audit logging

OCR, accounting export, supplier portal, bank integration, and automatic purchase ordering are deferred from this slice.

---

## Actors

| Actor | Responsibility |
|---|---|
| Owner | Manages vendors, reviews bills, records settlements, and checks payable balances |
| Admin | Manages vendors, bills, and settlements within allowed branch scope |
| System | Validates scope, creates ledger entries, updates bill balances, and writes audit logs |

---

## 1. Maintain Vendors

Owner or admin creates a vendor with:

- name
- vendor type
- optional contact name
- optional mobile number
- optional address
- optional notes
- active flag

Rules:

- vendor names are unique within the configured restaurant or branch scope
- inactive vendors cannot receive new bills
- cross-restaurant and cross-branch access is rejected

---

## 2. Enter Vendor Bill

User creates a vendor bill with:

- vendor
- bill date
- optional bill number
- optional due date
- bill lines

Each line contains:

- description
- quantity
- unit cost
- optional inventory item

Rules:

- quantity must be greater than zero
- unit cost must be zero or greater
- total amount equals the sum of line totals
- cancelled vendors bills cannot be edited into new stock-in activity

---

## 3. Create Stock-In Link

If a bill line references an inventory item, BillSoft creates a ledger-based `StockIn` inventory movement in the same transaction and stores the movement id on the bill line.

Notes:

- inventory stock is never updated directly
- inventory remains movement-derived
- if stock-in creation cannot be completed safely, the bill should not be confirmed

---

## 4. Record Settlement

User records a settlement against an open vendor bill with:

- payment mode
- amount
- optional reference number
- paid at timestamp

Rules:

- settlement amount must be greater than zero
- settlement cannot overpay the current balance
- cancelled vendor bills cannot accept settlements
- UPI, Card, and BankTransfer require a reference number

Active settlements update the bill:

- paid amount = sum of active settlements
- balance amount = total amount minus paid amount
- status becomes `Unpaid`, `PartiallyPaid`, or `Paid`

---

## 5. Cancel Vendor Bill

Vendor bill cancellation is limited in this slice.

Rules:

- cancelled bills cannot accept new settlements
- cancellation is blocked when inventory-linked stock-in movements already exist
- reversal workflows are deferred until a safe stock reversal path exists

---

## 6. Audit and Scope

The system writes audit logs for:

- vendor create and update
- vendor bill create
- settlement create
- cancellation actions when supported

All reads and writes are scoped to the restaurant and branch allowed for the current user.

---

## API Surface

Current endpoints:

- `GET /api/v1/vendors`
- `POST /api/v1/vendors`
- `PUT /api/v1/vendors/{id}`
- `GET /api/v1/vendor-bills`
- `GET /api/v1/vendor-bills/{id}`
- `POST /api/v1/vendor-bills`
- `POST /api/v1/vendor-bills/{id}/settlements`
- `POST /api/v1/vendor-bills/{id}/cancel`

---

## Deferred Items

The following are explicitly out of scope for this foundation:

- OCR vendor bill capture
- accounting export
- supplier portal
- bank integration
- automatic purchase ordering
- barcode scanning
- multi-currency

