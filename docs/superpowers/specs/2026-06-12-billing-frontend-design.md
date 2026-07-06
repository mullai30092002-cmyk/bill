# Billing Frontend Design

## Goal

Build the `/billing` frontend workspace on top of the existing billing and payment backend APIs.

The screen is for cashier-style billing operations:

- list bills
- inspect bill detail, lines, and payments
- create a bill from a confirmed POS order
- record partial or full payments
- cancel recorded payments
- cancel unpaid bills
- support read-only billing access

## Scope

This design is frontend only.

It does not add:

- backend APIs
- database changes
- migrations
- payment gateway integration
- cash drawer or shift closing
- kitchen tickets or kitchen display
- inventory deduction
- vendor or OCR workflows
- delete actions

## Product Principles

Billing is money movement, so the UI must bias toward the correct bill first.

The workspace should be list-first:

1. Bill list and payment status
2. Selected bill detail
3. Create bill from confirmed POS order

Confirmed POS order candidates must be available for bill creation, but they should not dominate the screen once bills exist.

## Route And Navigation

- Route: `/billing`
- Navigation label: `Billing`
- Show navigation when the session has any of:
  - `Billing.View`
  - `Billing.Manage`
  - `Payment.Record`

## Permission Behavior

- No billing permissions:
  - show not-authorized state
  - do not call billing or POS APIs
- `Billing.View`:
  - can list bills
  - can inspect bill detail, lines, and payments
  - cannot create bills or mutate payments/bills
- `Billing.Manage`:
  - can create bills from confirmed POS orders
  - can cancel unpaid bills when allowed by backend rules
- `Payment.Record`:
  - can record payments
- `Payment.Cancel`:
  - can cancel recorded payments

## Screen Structure

Use a single coordinator page and child panels.

Recommended files:

- `src/web/src/features/billing/BillingPage.tsx`
- `src/web/src/features/billing/BillListPanel.tsx`
- `src/web/src/features/billing/BillDetailPanel.tsx`
- `src/web/src/features/billing/CreateBillPanel.tsx`
- `src/web/src/features/billing/PaymentPanel.tsx`
- `src/web/src/features/billing/BillStatusActions.tsx`
- `src/web/src/features/billing/billingApi.ts`
- `src/web/src/features/billing/billingTypes.ts`
- `src/web/src/features/billing/billingDisplay.ts`
- `src/web/src/features/billing/billingValidation.ts`
- `src/web/src/features/billing/billingErrorDisplay.ts`
- `src/web/src/features/billing/BillingPage.test.tsx`

The coordinator page owns:

- auth and permission checks
- loading bill list
- loading confirmed POS order candidates
- loading selected bill detail
- create bill
- record payment
- cancel payment
- cancel bill
- notice and error state

Child panels render data and controls only.

## Default Workflow

On authorized load:

- load bill list
- load confirmed POS order candidates
- do not auto-select any bill
- show empty detail state:
  - `Select a bill to view details or create a bill from a confirmed order.`

When a bill is created:

- select the newly created bill
- refresh the bill list
- refresh the candidate list if needed

When a payment or cancel action succeeds:

- keep the same selected bill
- replace it with the backend response
- refresh the bill list

Mobile follows the same selection rules.

## Layout

Use the existing BillSoft shell and local layout primitives.

Desktop and laptop:

- primary left area: bill list and status filters
- main/right area: selected bill detail, lines, payments, and actions
- confirmed POS order candidates appear as a secondary create-bill section

Tablet:

- two-column layout where practical
- keep the bill list visible above or alongside the detail panel

Mobile:

- stacked sections in this order:
  1. summary and filters
  2. bill list
  3. selected bill detail
  4. confirmed POS order candidates
  5. payment and bill actions

The important change from a purely creation-first screen is that the cashier’s attention stays on locating the correct bill and seeing payment status before creation shortcuts.

## Data Flow

Frontend API functions:

- `listBills(query?)`
- `getBill(billId)`
- `createBill(request)`
- `cancelBill(billId, request)`
- `recordPayment(billId, request)`
- `cancelPayment(paymentId, request)`

Requests must not include:

- `restaurantId`
- `billNumber`
- `paymentNumber`
- bill line prices or totals
- `amountPaid`
- `balanceDue`

The backend remains the source of truth for:

- bill totals
- payment totals
- bill status
- payment status
- validation

## Confirmed POS Order Candidates

The create-bill panel should load confirmed POS orders and present them as candidates.

Recommended filtering:

- include confirmed orders only
- exclude cancelled orders
- prefer excluding already-billed orders if the data is available from the current bill list

The create-bill panel should be visible, but it should not visually overpower the billing list and detail panels.

## Actions

### Create bill

- Requires `Billing.Manage`
- Select a confirmed POS order
- Send only `posOrderId`
- On success:
  - show the new bill number
  - select the created bill
  - refresh the bill list

### Record payment

- Requires `Payment.Record`
- Requires a selected bill
- Hidden or disabled for cancelled or paid bills
- Amount must be greater than `0`
- Amount must not exceed the selected bill `balanceDue`
- Frontend validation is only a usability guard; backend remains source of truth
- Send only:
  - payment mode
  - amount
  - reference number
  - notes
- On success:
  - replace the selected bill with backend response
  - refresh the bill list

### Cancel payment

- Requires `Payment.Cancel`
- Only for recorded payments
- Requires a reason
- On success:
  - replace the selected bill with backend response
  - refresh the bill list

### Cancel bill

- Requires `Billing.Manage`
- Only for unpaid bills with no recorded payments
- Requires a reason
- On success:
  - replace the selected bill with backend response
  - refresh the bill list

## Error Handling

Billing staff must not see raw backend or SQL exception details.

Use short safe fallback messages such as:

- `Unable to load bills. Please refresh or try again.`
- `Unable to create bill. Please check the selected order and try again.`
- `Unable to record payment. Please check the amount and try again.`
- `Unable to cancel payment. Please refresh and try again.`
- `Unable to cancel bill. Please refresh and try again.`

Allow short, clean validation messages when they are safe.

Sanitize or suppress:

- SQL exception text
- stack traces
- HTML payloads
- JWT or token blobs
- long raw backend dumps

## Testing

Use Vitest and React Testing Library.

Required coverage:

- `/billing` route renders for `Billing.View`
- `/billing` route renders for `Billing.Manage`
- users without billing permissions see not-authorized state
- unauthorized state does not call billing or POS APIs
- navigation shows Billing only for billing/payment permissions
- bill list loads and displays bill number, status, and totals
- selecting a bill loads detail
- bill detail shows line snapshots and payments
- `Billing.View` users do not see mutate controls
- `Billing.Manage` users can create bills from confirmed POS orders
- create request contains only `posOrderId`
- payment record request contains only payment fields
- paid bills do not show the record-payment form
- payment amount above balance due is blocked locally
- payment cancellation updates the selected bill from backend response
- unpaid bill cancellation works with a reason
- cancel controls are hidden or disabled when backend rules say they are not allowed
- safe validation messages are shown
- SQL or exception-like error text is sanitized
- no delete action exists
- no payment gateway, cash drawer, kitchen, or inventory UI appears
- import-boundary test still passes

## Documentation

Update if needed:

- `docs/architecture/frontend-layouts.md`
- `docs/architecture/authentication-plan.md`
- `README.md`

Document:

- `/billing` route
- permission behavior
- bill creation from confirmed POS orders
- payment recording and cancellation
- unpaid bill cancellation
- backend totals as source of truth
- no gateway, cash drawer, kitchen, or inventory behavior yet
