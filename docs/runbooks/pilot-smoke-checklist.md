# BillSoft Pilot Smoke Checklist

## Purpose

Use this checklist to verify the BillSoft pilot after deployment, after database changes, or before a restaurant goes live.

This checklist is intentionally short, repeatable, and focused on the control paths that matter for restaurant billing, receipt audit, cashier reconciliation, and owner review.

Use it together with:

- [Pilot Operations Runbook](./pilot-operations-runbook.md)
- [Database Migration and Backup Runbook](./database-migration-and-backup-runbook.md)
- [Order to Billing Workflow](../workflows/order-to-billing.md)
- [Cashier Shift Workflow](../workflows/cashier-shift.md)
- [Daily Cash Sales Exception Report](../workflows/daily-cash-sales-exception-report.md)
- [Owner Dashboard](../workflows/owner-dashboard.md)

## Checklist

Mark each step as pass or fail and record the route, user role, and timestamp.

### 1. Login Smoke

- [ ] Open `/login`
- [ ] Sign in with the intended pilot user
- [ ] Confirm the landing route matches the user’s role

Expected result:

- authentication succeeds
- no raw stack trace or secret is shown
- the user lands on the expected role-based surface

### 2. Admin Users Smoke

- [ ] Open `/admin/users`
- [ ] Confirm the admin user list loads
- [ ] Confirm branch scoping and permission gating behave correctly

Expected result:

- same-restaurant admin users are visible
- cross-restaurant users are not exposed
- only authorized users can see admin actions

### 3. Password Reset Smoke

- [ ] Select another staff user
- [ ] Trigger the reset-password action
- [ ] Set a valid new password
- [ ] Sign in with the new password
- [ ] Confirm the old password fails

Expected result:

- password reset succeeds
- the action is audited
- the new password is not displayed again

### 4. Menu Smoke

- [ ] Open the menu category workspace
- [ ] Open the menu item workspace
- [ ] Confirm active categories and items load

Expected result:

- menu categories are restaurant-scoped
- menu items are restaurant-scoped
- current prices and price history are visible where supported

### 5. POS Order Smoke

- [ ] Open `/pos/orders`
- [ ] Create a test order
- [ ] Confirm the order

Expected result:

- the order saves successfully
- the confirmed order uses snapshot lines
- the POS confirm flow produces the expected kitchen handoff

### 6. Kitchen Ticket Smoke

- [ ] Open `/kitchen/tickets`
- [ ] Confirm the ticket appears
- [ ] Verify item names and quantities match the confirmed order

Expected result:

- the kitchen ticket is visible
- the ticket reflects the confirmed snapshot
- status changes remain within the supported workflow

### 7. Billing and Payment Smoke

- [ ] Open `/billing`
- [ ] Generate a bill from the confirmed order
- [ ] Record a payment
- [ ] Verify the bill status and balance

Expected result:

- bill number is assigned
- bill lines are immutable snapshots
- payment is recorded against the bill
- bill total is not rewritten by receipt actions

### 8. Receipt Smoke

- [ ] Open the receipt preview
- [ ] Confirm the receipt shows bill number, business date, generated time, item lines, totals, paid amount, balance, and payment breakdown
- [ ] Trigger print or reprint if the workflow requires it
- [ ] Confirm the receipt uses the stored bill snapshot rather than live menu prices

Expected result:

- receipt preview loads successfully
- print or reprint is auditable
- no internal IDs or secret values appear on the customer receipt
- cancelled bills show a cancelled state rather than a normal paid receipt

### 9. Cashier Shift Smoke

- [ ] Open `/cashier/shifts`
- [ ] Open a shift for the pilot branch
- [ ] Confirm cash payments link to the open shift
- [ ] Close the shift

Expected result:

- one open shift per branch is enforced
- cash payments are rejected when no shift is open
- variance is calculated from expected cash and counted cash

### 10. Daily Cash Sales Smoke

- [ ] Open `/reports/daily-cash-sales`
- [ ] Confirm the selected business date loads
- [ ] Confirm bills, payments, receipt prints, reprints, and cash variance metrics are visible

Expected result:

- the report is read-only
- the report uses persisted data only
- no current menu price recalculation appears in historical totals

### 11. Owner Dashboard Smoke

- [ ] Open `/owner/dashboard`
- [ ] Confirm the summary loads
- [ ] Confirm the dashboard links to detailed control pages

Expected result:

- the dashboard is read-only
- the dashboard shows the top-line control signals
- the dashboard does not mutate any transaction state

## Negative Checks

Run these checks if the environment and data setup make them practical:

- [ ] Cash payment without an open shift is rejected
- [ ] UPI/Card payment without a required reference is rejected
- [ ] Duplicate bill prevention works for the same source order
- [ ] Cancelled bill payment is blocked
- [ ] Cross-restaurant access does not leak data

Expected result:

- each negative case fails safely
- the user sees a clear, non-secret error
- no money, shift, or audit record is silently mutated

## Sign-Off Notes

Record the following after the checklist:

- environment name
- deployed commit SHA
- operator name
- date and time
- pass/fail summary
- any error messages
- any manual recovery steps taken

