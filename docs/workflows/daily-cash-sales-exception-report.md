# Daily Cash Sales Exception Report

## Purpose

The daily cash sales exception report is a read-only owner/admin control report for spotting leakage patterns after billing, payment collection, receipt printing, and cashier shift reconciliation.

It surfaces daily sales totals and exception buckets without changing any transaction workflow.

## Route and Permission

- Frontend route: `/reports/daily-cash-sales`
- Permission: `Report.View`

## Data Sources

The report uses persisted records only:

- `Bills`
- `Payments`
- `BillPrintEvents`
- `CashierShifts`
- `CashDrawerMovements`

It does not read the current menu catalog or recalculate historical totals from current prices.

## Date Basis

- The `date` query parameter is treated as the business date.
- When no date is supplied, the report defaults to today in the restaurant or selected branch timezone.
- When `branchId` is supplied, the branch must belong to the current restaurant.
- The report is read-only and uses timestamp filtering on persisted event records.
- The owner dashboard at `/owner/dashboard` reuses the same persisted data path and shows a compact summary of these metrics and exceptions.

## Included Metrics

- total bills
- paid bills
- partially paid bills
- unpaid bills
- cancelled bills
- gross sales
- cancelled bill amount
- net sales
- total amount paid
- total balance due
- cash payments
- non-cash payments
- receipt prints
- receipt reprints
- cash variance total

## Exception Buckets

- unpaid bills
- cancelled bills
- cancelled payments
- repeated receipt prints
- cash variance shifts
- open shifts

## Non-Goals

- export or PDF generation
- accounting exports
- printer hardware or ESC/POS commands
- payment gateway work
- tax/legal receipt compliance
- inventory deduction
- kitchen workflow changes
- transaction behavior changes
