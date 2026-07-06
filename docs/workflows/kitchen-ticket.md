# Kitchen Ticket Workflow

## Purpose

This workflow defines how BillSoft creates and manages kitchen preparation tickets from confirmed POS orders.

Kitchen tickets are a backend control record. They are not paper slips, verbal slips, printer output, or a kitchen display UI.

---

# Core Rules

1. Kitchen tickets are created automatically from confirmed POS order snapshots.
2. Kitchen ticket lines copy POS order line snapshots.
3. Current menu names, SKUs, prices, and availability do not change historical kitchen ticket data.
4. Only one non-cancelled kitchen ticket can exist for a POS order.
5. Ticket numbers are generated server-side from a persisted daily sequence.
6. Inventory deduction is performed only when a ticket is completed as `Served`.
7. No printer integration or kitchen hardware integration is added in this slice.

---

# Actors

| Actor | Responsibility |
|---|---|
| Admin | Creates tickets, updates status, and cancels tickets |
| Cashier | Creates tickets, views tickets, and cancels tickets |
| KitchenUser | Views tickets and advances status |
| Waiter | Views tickets |

---

# Ticket Creation

## 1. Confirm POS Order

The POS order must be in `Confirmed` status.
The normal browser POS confirm flow creates the kitchen ticket in the same backend workflow, so staff do not need a separate manual create-ticket action.

## 2. Create Kitchen Ticket

Backend creates:

- `KitchenTickets`
- `KitchenTicketLines`
- `KitchenTicketNumberSequences`

Ticket status starts as:

```text
Pending
```

Rules:

1. Ticket `RestaurantId` comes from the JWT/current user context.
2. Ticket `BranchId` comes from the source POS order.
3. Ticket number format is `KIT-YYYYMMDD-0001`.
4. Ticket line data comes from POS order line snapshots only.
5. Ticket lines remain separate when POS lines are separate.
6. The created ticket is the system record used by the kitchen display and audit trail, not a printer slip or manual handoff note.

---

# Status Lifecycle

Allowed transitions:

```text
Pending -> Preparing
Pending -> Ready
Preparing -> Ready
Ready -> Served
```

Blocked transitions:

- `Cancelled` tickets cannot change status
- `Served` tickets cannot change status
- Any transition not listed above is rejected

Timestamp rules:

- `PreparingAt` is set the first time a ticket enters `Preparing`
- `ReadyAt` is set the first time a ticket enters `Ready`
- `ServedAt` is set the first time a ticket enters `Served`
- `UpdatedAt` is refreshed on every transition
- `LastStatusChangedByUserId` is refreshed on every transition

---

# Stock Deduction

When a ticket is completed as `Served`, BillSoft resolves the mapped recipe ingredients for the current branch, calculates the required inventory quantities, validates current stock from the ledger, and creates inventory movement records for the consumption.

Rules:

1. No deduction occurs on POS order confirmation.
2. No deduction occurs on bill payment.
3. Deduction is idempotent for a ticket retry.
4. Insufficient stock blocks completion and rolls back the ticket transition.
5. Menu items without a recipe remain completable and do not deduct stock.
6. Deduction is audited together with the kitchen ticket status change.

---

# Cancellation

Tickets may be cancelled when they are `Pending`, `Preparing`, or `Ready`.

Rules:

1. Cancellation requires a reason.
2. `Cancelled` tickets cannot be cancelled again.
3. `Served` tickets cannot be cancelled in this foundation.
4. Cancellation is audited.

---

# Audit

Audit these actions:

- `KitchenTicket.Created`
- `KitchenTicket.StatusChanged`
- `KitchenTicket.Cancelled`

Audit entries must include the restaurant, branch, user snapshot, timestamps, and reason where applicable.

---

# Deferred

The following remain out of scope for this foundation:

- printer integration
- kitchen hardware assignment
- recipe-based prep estimation
- prep timers

The kitchen display frontend is now implemented at `/kitchen/tickets`. Remaining deferred work stays focused on printer output, kitchen hardware assignment, recipe-based prep estimation, and prep timers.
