# BillSoft Status Transitions

## Purpose

This document defines allowed status transitions for BillSoft core workflows.

Status transitions are part of the control model. Developers must not invent new transitions without updating this document and the relevant tests.

---

# 1. Order Status

## Statuses

```text
Draft
SentToKitchen
Preparing
Ready
Served
Billed
Cancelled
```

## Allowed Transitions

| From | To | Notes |
|---|---|---|
| Draft | SentToKitchen | Order submitted to kitchen |
| Draft | Cancelled | Allowed before kitchen submission |
| SentToKitchen | Preparing | Kitchen accepts/starts order |
| SentToKitchen | Cancelled | Requires authorized cancellation and reason |
| Preparing | Ready | Kitchen marks ready |
| Preparing | Cancelled | Requires authorized cancellation and reason |
| Ready | Served | Eat-in served or parcel packed |
| Ready | Cancelled | Requires authorized cancellation and reason |
| Served | Billed | Bill generated |
| Billed | Cancelled | Only through bill cancellation/void process |

---

# 2. Order Item Status

## Statuses

```text
New
SentToKitchen
Preparing
Ready
Served
Cancelled
```

## Rules

1. Cancelling after `SentToKitchen` requires reason.
2. Cancelled items must remain visible in audit and owner reports.
3. Order item status history must be stored.

---

# 3. Kitchen Ticket Status

## Statuses

```text
New
Accepted
Preparing
Ready
ServedOrPacked
Cancelled
```

## Allowed Transitions

| From | To |
|---|---|
| New | Accepted |
| New | Cancelled |
| Accepted | Preparing |
| Accepted | Cancelled |
| Preparing | Ready |
| Preparing | Cancelled |
| Ready | ServedOrPacked |

## Rules

1. Kitchen users must not see bill totals or revenue.
2. Kitchen cancellation must be tied back to order item cancellation.
3. Kitchen status changes must be timestamped.

---

# 4. Bill Status

## Statuses

```text
Unpaid
PartiallyPaid
Paid
Cancelled
```

## Allowed Transitions

| From | To | Notes |
|---|---|---|
| Unpaid | PartiallyPaid | Partial payment recorded |
| Unpaid | Paid | Full payment recorded |
| PartiallyPaid | Paid | Remaining payment recorded |
| Unpaid | Cancelled | No recorded payments exist |

## Rules

1. Bill numbers are immutable after creation.
2. Bills must not be hard-deleted.
3. Bill cancellation must be audited.
4. Bill snapshot totals must not be recalculated from current menu prices.

---

# 5. Payment Status

## Statuses

```text
Recorded
Cancelled
```

## Rules

1. Payment cancellation requires permission.
2. Payment changes must update the parent bill balance and status.
3. Payment reference changes must be audited.

---

# 6. Vendor Bill Status

## Statuses

```text
Uploaded
OcrPending
ReviewRequired
Confirmed
PartiallyPaid
Paid
Rejected
DuplicateSuspected
```

## Allowed Transitions

| From | To | Notes |
|---|---|---|
| Uploaded | OcrPending | OCR job queued |
| Uploaded | ReviewRequired | Manual entry without OCR |
| OcrPending | ReviewRequired | OCR completed and needs review |
| OcrPending | Rejected | OCR/document invalid |
| ReviewRequired | Confirmed | User confirms bill lines |
| ReviewRequired | Rejected | User rejects bill |
| ReviewRequired | DuplicateSuspected | Possible duplicate detected |
| DuplicateSuspected | ReviewRequired | User continues with reason |
| Confirmed | PartiallyPaid | Partial vendor payment recorded |
| Confirmed | Paid | Full payment recorded |
| PartiallyPaid | Paid | Balance paid |

## Rules

1. Vendor bill confirmation creates stock movements.
2. OCR cannot directly update inventory.
3. Confirmed vendor bill edits require controlled correction and audit.

---

# 7. OCR Result Status

## Statuses

```text
Pending
Success
Failed
ReviewRequired
```

## Rules

1. OCR raw result must be preserved.
2. Confirmed bill values must be stored separately from OCR values.
3. User override requires reason and audit record.

---

# 8. Stock Session Status

## Statuses

```text
Open
Closed
```

## Rules

1. Stock usage is calculated from opening stock, purchases, and closing stock.
2. Stock corrections require reason.
3. Stock must change through `StockMovements`.

---

# 9. Expense Status

## Statuses

```text
Draft
Submitted
Approved
Rejected
Paid
Cancelled
```

## Rules

1. Large or sensitive expenses may require approval.
2. Expense payment creates a money-out ledger entry.
3. Expense rejection or cancellation requires reason.

---

# 10. Alert Status

## Statuses

```text
Open
Acknowledged
Resolved
Dismissed
```

## Rules

1. Owner/admin must be able to see open high-risk alerts.
2. Dismissing an alert requires permission.
3. Alert acknowledgment must be timestamped.
