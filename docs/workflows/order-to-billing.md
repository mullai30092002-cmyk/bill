# Order to Billing Workflow

## Purpose

This workflow defines how BillSoft handles eat-in orders, parcel orders, kitchen preparation, bill generation, and payment collection.

Every prepared order must end in one of these outcomes:

```text
Billed and paid
Cancelled with reason
Visible as pending/unpaid
```

---

# Actors

| Actor | Responsibility |
|---|---|
| Waiter | Creates eat-in and parcel orders |
| Cashier | Generates bills and records payment |
| KitchenUser | Prepares items and updates kitchen status |
| Admin | Handles exceptions and configuration |
| Owner | Monitors sales, cancellations, discounts, and cash difference |

---

# Eat-In Flow

## 1. Open Table Session

User selects a table and opens a session.

System creates:

- `TableSessions`

Status:

```text
TableSessions.Status = Open
```

## 2. Create Order

User selects menu items and quantities.

System creates:

- `Orders`
- `OrderItems`

Status:

```text
Orders.Status = Draft
OrderItems.Status = New
```

## 3. Send to Kitchen

Confirmed POS orders automatically create kitchen preparation tickets from snapshot data during the normal POS confirm flow.

System creates:

- `KitchenTickets`
- `KitchenTicketLines`
- `KitchenTicketNumberSequences`

Status:

```text
KitchenTickets.Status = Pending
```

## 4. Kitchen Preparation

Kitchen updates ticket progress.

Standard sequence:

```text
Pending -> Preparing -> Ready -> Served
```

System updates:

- `KitchenTickets`
- `KitchenTicketLines`
- `AuditLogs`

Rules:

1. Kitchen ticket lines are copied from confirmed POS order snapshots.
2. Current menu prices, categories, or availability do not change historical kitchen ticket data.
3. Kitchen ticket status changes are audited.
4. Kitchen ticket creation is backend-only in this foundation. No printer, kitchen display, or hardware integration is added yet.
5. Staff do not need a separate paper slip or manual create-ticket action to hand off an order to the kitchen.

## 5. Add Additional Items

Additional items may be added before final billing.

Rules:

1. Additional items must be tied to the same table session.
2. Items already converted into kitchen tickets must be tracked through ticket status or cancellation, not through ad-hoc paper slips.

## 6. Generate Bill

Cashier generates a bill from a confirmed POS order.

System creates:

- `Bills`
- `BillLines`
- `BillPrintEvents` when the bill is printed or reprinted from the billing screen

Status:

```text
Bills.Status = Unpaid
Orders.Status = Confirmed
```

Rules:

1. Bill number is auto-generated server-side.
2. Bill number is immutable after creation.
3. Bill item names, SKUs, prices, and tax values are captured as snapshots from the confirmed POS order lines.
4. Closed bills cannot be edited.
5. `/billing` exposes an inline receipt preview card with bill, business-date, branch, order, customer, item, payment, and totals snapshots, and each browser print/reprint records an audited print event before `window.print()` runs.

## 7. Record Payment

Cashier records payment.

System creates:

- `Payments`

Status:

```text
Bills.Status = Paid
```

If partial payment is enabled:

```text
Bills.Status = PartiallyPaid
```

Payment recording is blocked when the bill is cancelled.

## 8. Close Table Session

After payment, table session is closed.

Status:

```text
TableSessions.Status = Closed
```

---

# Parcel / Takeaway Flow

## 1. Create Parcel Order

System generates:

- Order number
- Token number

Status:

```text
Orders.OrderType = Parcel
Orders.Status = Draft
```

## 2. Send to Kitchen

Kitchen display must show:

- Token number
- Parcel/takeaway marker
- Item names
- Quantities

## 3. Prepare and Pack

Kitchen marks parcel as ready/packed.

## 4. Bill and Payment

Rules:

1. Parcel order must close through billing or cancellation.
2. Unpaid parcel orders must be visible to owner/admin.
3. Parcel token must not disappear from the workflow.

---

# Cancellation Rules

## Before Kitchen Submission

Allowed based on permission.

## After Kitchen Submission

Requires:

- Permission
- Mandatory reason
- Audit record
- Owner/admin visibility

## After Bill Creation

Requires:

- Bill cancellation with reason
- Permission
- Mandatory reason
- Audit record

---

# Discount Rules

Discounts require explicit permission.

System must capture:

- Bill
- Discount amount
- Reason
- User
- Approval user, if required
- Timestamp

Discounts must appear in daily reports.

---

# Ledger Rules

Cash-control and shift reconciliation are covered by `docs/workflows/cashier-shift.md` in the backend foundation.

Daily bill, payment, receipt-print, cancellation, and cash-variance control review is covered by `docs/workflows/daily-cash-sales-exception-report.md` and uses persisted snapshots only.

---

# Audit Events

Audit these actions:

- Table session opened
- Order created
- Order submitted to kitchen
- Kitchen status changed
- Order item cancelled
- Bill created
- Bill cancelled
- Discount applied
- Payment recorded
- Payment cancelled
- Table session closed

---

# Suspicious Activity Signals

Owner/admin reports should highlight:

- Orders sent to kitchen but not billed
- Cancelled items after kitchen submission
- High discount count
- Bill cancellations
- Cash drawer difference
- Parcel tokens not closed

---

# Acceptance Criteria

1. Eat-in order can be created against a table session.
2. Parcel order can be created with token number.
3. Kitchen tickets are created from submitted order items.
4. Kitchen can update preparation status.
5. Bill can be generated from a confirmed POS order.
6. Bill number is immutable after creation.
7. Payment updates the bill balance and status.
8. Cash payment affects expected cash drawer balance.
9. Cancellation after kitchen submission requires reason and audit.
10. Orders sent to kitchen but not billed are visible to owner/admin.
