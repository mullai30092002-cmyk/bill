# Cashier Shift Workflow

## Purpose

This workflow defines the cashier-shift foundation used to reconcile cash at branch level and reduce staff fraud.

The feature has a frontend operational screen at `/cashier/shifts`. There is no drawer hardware integration.

---

# Actors

| Actor | Responsibility |
|---|---|
| Cashier | Open shift, record cash movements, close shift |
| Admin | Operational oversight and exception handling |
| Owner | Reviews cash variance and suspicious patterns |

---

# Shift Open

## 1. Open Shift

Cashier opens a shift for a branch.

System creates:

- `CashierShifts`

Rules:

1. Restaurant scope comes from the authenticated JWT.
2. Branch must belong to the current restaurant.
3. Branch must be active to open a shift.
4. Only one open shift is allowed per branch at a time.
5. Opening cash must be greater than or equal to zero.
6. Opening cash becomes the initial expected cash.
7. The frontend sends only `branchId`, `openingCashAmount`, and `openingNote`.

---

# Cash Movements

## 2. Record Cash Movement

Cashier records a manual cash movement within the open shift.

System creates:

- `CashDrawerMovements`

Supported movement types:

- `CashIn`
- `CashOut`
- `SafeDrop`
- `Adjustment`

Rules:

1. Movement belongs to an open shift.
2. Reason is required.
3. `CashIn`, `CashOut`, and `SafeDrop` require a positive amount.
4. `Adjustment` may be positive or negative and is applied as entered.
5. Cash in increases expected cash.
6. Cash out and safe drop decrease expected cash.
7. Adjustment changes expected cash by the signed amount entered.
8. The frontend sends only `movementType`, `amount`, and `reason`.

---

# Cash Payments

## 3. Record Cash Payment

When a bill payment is recorded as Cash, the system links it to the current open shift for the bill branch.

Rules:

1. If no open shift exists for the bill branch, the payment is rejected.
2. Cash payment amount is added to expected cash on the linked shift.
3. Card, UPI, and Other payments do not affect the shift balance.
4. The payment record stores the optional `CashierShiftId` link for traceability.
5. The frontend billing flow does not send restaurant scope when linking the shift.

## 4. Cancel Cash Payment

If a recorded cash payment is cancelled:

1. The linked shift must still be open.
2. The payment amount is subtracted from expected cash on that shift.
3. If the linked shift is closed, cancellation is rejected.
4. Non-cash payment cancellations do not affect shift cash.

---

# Shift Close

## 5. Close Shift

Cashier closes the shift by entering counted cash.

Rules:

1. Shift must be open.
2. Counted cash must be greater than or equal to zero.
3. Expected cash is the running total after opening cash, cash movements, and cash payments.
4. Variance is `CountedCashAmount - ExpectedCashAmount`.
5. Closed shifts cannot be modified.
6. The frontend sends only `countedCashAmount` and `closingNote`.

---

# Audit Events

Audit these actions:

- Shift opened
- Cash movement recorded
- Shift closed

Cash payment recording and cancellation are already audited in the billing flow and now include the shift link in the stored payment record.

---

# Acceptance Criteria

1. Shift can be opened for an active branch.
2. Only one open shift is allowed per branch.
3. Cash movements update expected cash.
4. Cash payments require an open shift and link to it.
5. Cash payment cancellation adjusts expected cash when the shift is still open.
6. Shift close calculates variance from counted cash and expected cash.
7. Audit records exist for open, movement, and close actions.

---

# Frontend Workflow

## `/cashier/shifts`

The cashier-shift screen is an operational workspace, not an admin data grid.

Layout:

- Desktop and tablet: current shift and actions on the left, recent shifts and movement history on the right
- Mobile: branch selector, current shift summary, open or movement or close actions, movement history, then recent shifts

Primary behavior:

1. Load active branches first.
2. Preselect the only active branch when one exists.
3. Load the current open shift for the selected branch.
4. Load recent shifts for the selected branch.
5. Allow cash movement and close actions only when the user has the required permissions.
6. Show expected cash and variance from the shift detail.
7. Keep current shift actions visible above history and recent-shift verification content.
