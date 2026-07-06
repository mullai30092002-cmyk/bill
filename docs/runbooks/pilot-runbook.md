# BillSoft Pilot Runbook and Smoke Checklist

## 1. Purpose

This runbook is the repeatable pilot setup and verification path for BillSoft.

Use it when preparing a restaurant pilot, rehearsing the pilot locally, or checking that a deployment still covers the critical operational flows.

This is an operational document, not a feature spec. It is intentionally one executable artifact so a new operator can follow it end to end without jumping between files.

## 2. Pilot Readiness Status

BillSoft is currently ready for a pilot smoke run on the verified core flows listed below.

| Area | Status | Notes |
|---|---|---|
| Role-based landing | Ready | Authenticated users land by role and permission. |
| Owner dashboard | Ready | Read-only control surface at `/owner/dashboard`. |
| Daily cash sales report | Ready | Read-only control report at `/reports/daily-cash-sales`. |
| POS order capture | Ready | Create, edit, confirm, and cancel are available. |
| POS to kitchen handoff | Ready | POS confirm auto-creates kitchen tickets. |
| Kitchen workflow | Ready | Queue, detail, status changes, and cancellation are available. |
| Billing and payment | Ready | Bill creation, payment recording, cancellation, and receipt preview are available. |
| Receipt print audit | Ready | Print/reprint writes audit events before browser printing. |
| Cashier shifts | Ready | Open, movement recording, close, and variance are available. |
| Admin workspace | Ready | Users, branches, and menu management are available. |
| Staff password reset | Ready | Admin-only reset/reissue exists for support use. |
| Local bootstrap | Ready | SQLite local-dev bootstrap is hardened. |

Deferred items are not part of pilot readiness and must not be treated as required for a go decision:

| Deferred area | Pilot status |
|---|---|
| Public forgot-password / self-service recovery | Deferred |
| Email / SMS / OTP recovery | Deferred |
| Payment gateway | Deferred |
| Printer hardware / ESC-POS | Deferred |
| Tax / legal reporting | Deferred |
| Inventory deduction | Deferred |
| OCR / vendor expansion | Deferred |
| Export / accounting integration | Deferred |

## 3. Prerequisites

Use these prerequisites before a pilot smoke run:

1. A Windows workstation with access to the BillSoft repository.
2. .NET 8 SDK installed.
3. Node.js 20.19+ or 22.12+ installed.
4. pnpm `10.18.3` activated through Corepack.
5. Access to the pilot database and any required local configuration.
6. A browser for the operator smoke pass.
7. A seeded demo restaurant and at least one admin-support user with `User.Manage`.
8. A target staff user account for the password reset smoke check.

For a local rehearsal, use the setup guidance in [Local Setup](../development/local-setup.md). For a pilot deployment, keep the same verification order but point the app at the pilot environment instead of local developer services.

## 4. Setup Commands

Run the setup sequence in this order.

From the repository root:

```bash
dotnet restore BillSoft.sln
dotnet build BillSoft.sln
dotnet test BillSoft.sln
```

From `src/web`:

```bash
pnpm install
pnpm run test
pnpm run typecheck
pnpm run build
```

To run the app locally for rehearsal:

```bash
dotnet run --project src/api/BillSoft.Api/BillSoft.Api.csproj --urls http://localhost:5000
pnpm run dev
```

To seed local demo data and start the local app together, use the VS Code task `app: setup seed and run` or run the explicit seed commands first:

```bash
dotnet run --project src/api/BillSoft.Api/BillSoft.Api.csproj -- --seed-foundation
dotnet run --project src/api/BillSoft.Api/BillSoft.Api.csproj -- --seed-demo-login
```

Local endpoints:

- API: `http://localhost:5000`
- Web: `http://localhost:3010`

## 5. Demo Credentials

Use these demo credentials for local rehearsal and pilot smoke checks when the demo seed is active:

| Field | Value |
|---|---|
| Restaurant code | `DEMO` |
| Mobile number | `90000001` |
| Password | `DemoOwner123!` |
| Role | `RestaurantOwner` |

The demo owner mobile is stored canonically as `+6590000001`.

## 6. Smoke Checklist Overview

Use this high-level checklist before the detailed steps:

| Check | What to confirm | Expected result |
|---|---|---|
| Login | Demo owner signs in with the demo credentials | Role-based landing loads without errors. |
| Dashboard | Open `/owner/dashboard` | Read-only owner dashboard loads. |
| Daily report | Open `/reports/daily-cash-sales` | Daily totals and exceptions load read-only. |
| Admin setup | Open `/admin/users`, `/admin/branches`, and `/admin/menu` | Admin workspaces load with the expected actions. |
| Password reset | Reset another staff user from `/admin/users` | New password works, old password fails, and the action is audited. |
| POS order | Create and confirm a test order in `/pos/orders` | A kitchen ticket is auto-created. |
| Kitchen ticket | Open `/kitchen/tickets` | The ticket appears and status changes work. |
| Billing | Create a bill and record payment in `/billing` | Bill state updates correctly. |
| Receipt audit | Print or reprint a receipt | Print-event audit is written before printing. |
| Cash shift | Open, move, and close a shift in `/cashier/shifts` | Expected cash and variance update correctly. |

## 7. Detailed Smoke Steps

1. Sign in with the demo owner account.
   Expected result: the authenticated route loads, the operator label appears, and role-based landing is correct.

2. Open `/owner/dashboard`.
   Expected result: the dashboard loads as a read-only page, with no mutation controls.

3. Open `/reports/daily-cash-sales`.
   Expected result: the report loads read-only and shows current-day cash-control metrics and exceptions.

4. Open `/admin/users`.
   Expected result: the users workspace loads, and only users with `User.Manage` can see admin actions.

5. Select a staff user who is not the current signed-in user and open the reset-password action.
   Expected result: the reset form appears and the current signed-in user cannot be reset from this screen.

6. Reset the selected staff password.
   Expected result: the new password is accepted only when it meets the current password rules, and the reset completes without exposing the password again.

7. Sign in as the reset staff user with the new password.
   Expected result: login succeeds with the new password.

8. Attempt the old password.
   Expected result: login fails.

9. Open `/admin/branches` and `/admin/menu`.
   Expected result: the admin workspaces load and remain restaurant-scoped.

10. Open `/pos/orders`, create a test order, and confirm it.
    Expected result: confirmation succeeds and the kitchen ticket is auto-created from the POS snapshot.

11. Open `/kitchen/tickets`.
    Expected result: the new ticket appears, and its status can move through the supported lifecycle.

12. Open `/billing`, create a bill from the confirmed order, and record a payment.
    Expected result: the bill transitions correctly and the payment is recorded against the bill.

13. Print or reprint the receipt from the billing workspace.
    Expected result: a receipt print-event is audited before browser printing starts.

14. Open `/cashier/shifts`, open a shift, record a movement, and close the shift.
    Expected result: the shift balance and variance calculate correctly.

15. Return to `/reports/daily-cash-sales`.
    Expected result: the report reflects the created billing, payment, print, and shift activity.

## 8. Anti-Fraud Verification Checklist

Use this checklist to verify the control surfaces that matter during a pilot:

| Check | Expected result |
|---|---|
| No hard delete behavior | Business records remain status-based, not physically deleted. |
| Bill and payment traceability | Every money movement is auditable. |
| Receipt audit | Print/reprint writes an audit event before printing. |
| Cash movement reasons | Cash movements require a reason. |
| Shift variance visibility | Cash shift close shows expected cash and variance. |
| Kitchen traceability | Confirmed POS orders create kitchen tickets automatically. |
| Password reset audit | Admin password resets are audited without plaintext passwords. |
| Self-reset block | The current signed-in user cannot reset their own password from the admin users screen. |
| Cross-restaurant protection | Admin reads and resets stay scoped to the current restaurant. |
| Read-only control surfaces | Owner dashboard and daily report remain read-only. |

## 9. Failure Triage

Use this triage order when a smoke check fails:

1. Stop the run at the first unexpected result.
2. Record the exact route, role, timestamp, and expected vs actual behavior.
3. Capture the API response or browser error text without editing the evidence.
4. Check whether the failure is a permissions issue, a missing seed record, or a data-scope issue.
5. Re-run only the failed step after the cause is understood.
6. If the failure affects money, kitchen handoff, or audit logging, treat the pilot as not ready until it is fixed.

Common symptoms and likely causes:

| Symptom | Likely cause | Next step |
|---|---|---|
| Login fails for the demo owner | Seed data missing or wrong credentials | Re-run the demo seed and verify the demo login details. |
| `/admin/users` is hidden or forbidden | Missing `User.Manage` | Use the admin-support account or fix the role assignment. |
| Reset password cannot submit | Password rules were not satisfied | Enter a valid new password and try again. |
| Old password still works | The reset did not persist | Re-check the user record and audit log. |
| POS confirm does not create a kitchen ticket | Handoff flow regressed | Stop the pilot and investigate the POS confirm backend path. |
| Receipt printing does not audit | Print-event path regressed | Stop the pilot and verify the billing print-event call. |
| Shift variance is unexpected | Open/close sequence or movement data is wrong | Verify the shift sequence and movement reasons. |
| Daily report does not reflect activity | Wrong business date or branch scope | Check the report date and restaurant scope. |

## 10. Pilot Go/No-Go Checklist

Do not start the pilot unless every item below is true:

- Demo credentials are available and verified.
- A staff account exists for support actions.
- Owner dashboard loads.
- Daily cash sales report loads.
- Admin users, branches, and menu workspaces load.
- Admin password reset works for another same-restaurant staff account.
- POS confirm creates a kitchen ticket.
- Kitchen ticket status changes work.
- Billing and payment work.
- Receipt print audit is visible.
- Cashier shift open, movement, and close work.
- No critical scope leak, audit gap, or permission leak was observed.

If any item is false, the pilot is a no-go until the gap is resolved and the smoke checklist passes again.

## 11. Deferred / Non-Pilot Items

These items are intentionally outside the pilot checklist:

- Public forgot-password or self-service recovery
- Email, SMS, or OTP-based recovery
- Payment gateway integration
- Printer hardware or ESC/POS integration
- Tax or legal reporting
- Inventory deduction
- OCR and vendor bill expansion
- Export or accounting integration
- Offline mode

## 12. Change Control

Keep this runbook current with the shipped pilot surface.

If the setup command sequence, role/permission model, route map, audit behavior, or smoke order changes, update this file in the same change set and re-run the smoke checklist.

Do not add hidden operator steps outside this document. If a step is required to start or verify the pilot, it belongs here.
