# Owner Dashboard

## Purpose

The owner dashboard is a read-only landing surface for restaurant owners and admin users who need fast daily control signals.

It is intentionally smaller than the daily cash sales report and focuses on the top-line operational exceptions that need attention first.

## Route and Permission

- Frontend route: `/owner/dashboard`
- Permission: `Report.View`

This route is explicit only.

- It does not replace `/`.
- It does not change post-login redirect behavior.
- It does not become the default home for `Report.View` users in this slice.

## Data Source

The dashboard reuses the persisted daily cash sales report data path.

It summarizes the same underlying restaurant-scoped records:

- `Bills`
- `Payments`
- `BillPrintEvents`
- `CashierShifts`
- `CashDrawerMovements`

The dashboard does not recalculate historical totals from live menu data and does not change any transaction state.

## Included Metrics

- net sales
- gross sales
- cash payments
- non-cash payments
- total amount paid
- total balance due
- unpaid bills
- cancelled bills
- cancelled payments
- receipt reprints
- cash variance total
- open shifts

## Alerts

Alerts are derived from the daily report exception buckets:

- unpaid bills
- cancelled activity
- receipt reprints
- cash variance
- open shifts

## Quick Links

The dashboard links out to the detailed control pages:

- `/reports/daily-cash-sales`
- `/billing`
- `/cashier/shifts`
- `/kitchen/tickets`
- `/pos/orders`

## Non-Goals

- export or PDF generation
- accounting integration
- printer hardware or ESC/POS
- payment gateway work
- tax/legal reporting
- inventory deduction
- kitchen workflow changes
- mutation actions from the dashboard
