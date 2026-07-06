# BillSoft Pilot Smoke Results

## 1. Smoke Run Metadata

- Date/time: `2026-06-15 00:22-00:25 Asia/Singapore`
- Commit SHA tested: `fc11d508dc4c3f0fc1a29cf750e64111646d3f44`
- Environment: Windows PowerShell, local localhost smoke run, Chromium headless via Playwright
- Database provider: SQL Server
- API URL: `http://localhost:5000`
- Web URL: `http://localhost:3000`
- Operator: `Codex`
- Demo account used: `DEMO / 90000001 / DemoOwner123!`
- Disposable staff account used for reset check: `Pilot Smoke Cashier / 90009991 / PilotSmoke123!`

## 2. Executive Verdict

- PASS - pilot smoke passed

## 3. Setup Verification

| Check | Result | Notes |
|---|---|---|
| Backend restore | PASS | `dotnet restore BillSoft.sln` completed successfully. |
| Backend build | PASS | `dotnet build BillSoft.sln` completed successfully. |
| Backend test | PASS | `dotnet test BillSoft.sln` passed. |
| Frontend install | PASS | `pnpm install` in `src/web` completed successfully. |
| Frontend test | PASS | `pnpm run test` passed. |
| Frontend typecheck | PASS | `pnpm run typecheck` passed. |
| Frontend build | PASS | `pnpm run build` passed. |
| Database update | PASS | `dotnet ef database update` reported the database was already up to date. |
| Seed foundation | PASS | `--seed-foundation` ran successfully. |
| Seed demo login | PASS | `--seed-demo-login` ran successfully. |
| API startup | PASS | API started at `http://localhost:5000`. |
| Web startup | PASS | Web started at `http://localhost:3000`. |

## 4. Smoke Checklist Results

| Step | Route / Area | Expected | Actual | Result | Evidence / notes |
|---|---|---|---|---|---|
| 1. Login and role landing | `/login` -> `/owner/dashboard` | Demo owner signs in and lands on the correct role route | Login succeeded, session stored, and landing route resolved to `Owner dashboard` | PASS | Demo owner session had `RestaurantOwner` and `User.Manage`. |
| 2. Explicit `/` dashboard | `/` | BillSoft dashboard loads after auth | `BillSoft dashboard` heading loaded | PASS | No `Create`, `Save`, `Delete`, or `Edit` controls were present. |
| 3. Owner dashboard | `/owner/dashboard` | Owner dashboard loads read-only | `Owner dashboard` heading loaded | PASS | Read-only control surface only. |
| 4. Daily report | `/reports/daily-cash-sales` | Daily cash sales report loads read-only | `Daily cash sales report` heading loaded | PASS | Read-only controls only. |
| 5. Branch admin | `/admin/branches` | Branch management workspace loads | `Branch management` heading loaded | PASS | Restaurant-scoped admin workspace loaded. |
| 6. User admin | `/admin/users` | Users and roles workspace loads | `Users and roles` heading loaded | PASS | Admin users workspace loaded. |
| 7. Staff password reset | `/admin/users` | Reset another same-restaurant staff user; block self reset | Self row had `0` reset buttons; target row had `1`; reset completed successfully; new password login returned `200`, old password login returned `401` | PASS | Target user: `Pilot Smoke Cashier / 90009991`. Reset used `NewStrongPassword123!`. |
| 8. Menu admin | `/admin/menu` | Menu management workspace loads | `Menu management` heading loaded | PASS | Restaurant-scoped menu admin loaded. |
| 9. POS order create/edit/confirm | `/pos/orders` | Create, edit, and confirm order | Created `ORD-20260614-0006`, edited it, then confirmed it | PASS | Menu item used: `Masala Dosa`. |
| 10. POS-to-kitchen handoff | `/kitchen/tickets` | Confirmed POS order auto-creates a kitchen ticket | Kitchen ticket `70de92dc-3d8a-4440-a96f-22beafa78ad6` created in `Pending` state | PASS | Confirm audit written automatically. |
| 11. Kitchen ticket detail/status | `/kitchen/tickets` | Ticket detail loads and status changes work | Status moved `Pending -> Preparing -> Ready` | PASS | Ticket detail and status endpoints both responded successfully. |
| 12. Billing create bill | `/billing` | Create a bill from the confirmed order | Bill `BILL-20260614-0005` created from the confirmed POS order | PASS | Grand total `2.50`. |
| 13. Payment record/cancel | `/billing` | Record a disposable payment and cancel it before shift close | Fresh disposable bill `BILL-20260614-0006` recorded `PAY-20260614-0004`, then cancelled with a safe reason | PASS | Cancellation was verified before closing the fresh shift. |
| 14. Receipt preview/print/reprint audit | `/billing` | Receipt preview loads and print/reprint auditing works | Receipt preview loaded; print count advanced from `1` to `2`; reprint flag changed to `true` on the second print | PASS | `Bill.ReceiptPrinted` audit rows were written without exposing secrets. |
| 15. Cashier shift open/movement/close | `/cashier/shifts` | Open shift, record movement, and close shift | Shift `2cbebaef-67d1-4038-8e77-5029fad72a74` opened, movement recorded, and closed with zero variance | PASS | The shift close returned expected and counted cash that matched. |
| 16. End-of-day report/control check | `/reports/daily-cash-sales` | Daily report reflects the day’s activity | Explicit `date=2026-06-14` returned totals with billed, cancelled, paid, and receipt activity | PASS | Summary showed `totalBills=6`, `paidBills=2`, `cancelledBills=1`, `receiptPrints=5`, `receiptReprints=2`. |
| 17. Read-only/no mutation checks for report/dashboard | `/` and `/reports/daily-cash-sales` | No mutation controls on read-only surfaces | No `Create`, `Save`, `Delete`, or `Edit` buttons were present on the checked read-only pages | PASS | Dashboard and report stayed read-only. |
| 18. Anti-fraud checks | Admin, billing, report, and SQL audit | Anti-fraud control surfaces behave correctly | Password reset audited, role-based access worked, cross-restaurant reset returned safe `404`, receipt prints audited, and cancelled payments/bills appeared in the report | PASS | No raw password or password hash appeared in audit payloads. |

## 5. Anti-Fraud Control Results

| Check | Result | Evidence / notes |
|---|---|---|
| No hard delete observed | PASS | Smoke used status transitions and cancel flows only; no delete controls were present on the read-only surfaces. |
| POS confirm auto-created kitchen ticket | PASS | `PosOrder.Confirmed` was immediately followed by `KitchenTicket.Created` for the same order. |
| Receipt print audit worked | PASS | `Bill.ReceiptPrinted` rows were written for the receipt sequence 1 and 2 events. |
| Cancelled bills/payments visible in report | PASS | `date=2026-06-14` report showed cancelled payment `PAY-20260614-0004` and historical cancelled items. |
| Cash variance visible | PASS | The report surfaced cash variance history in the summary and exception section. |
| Password reset audited | PASS | `User.PasswordReset` audit row captured `TargetUserId`, `TargetUserName`, `TargetMobileSnapshot`, and `Reason`. |
| No plaintext password or hash in audit | PASS | Audit row did not include `NewStrongPassword123!` or any password hash field. |
| Role-based access behaved correctly | PASS | Self reset was hidden, target reset was visible, and cross-restaurant reset returned a safe `404`. |
| Raw errors not shown to staff | PASS | Blocked UI and API paths returned safe messages, not stack traces or raw SQL errors. |

## 6. Defects Found

### P0 defects

- None.

### P1 findings

- None.

### Deferred / non-pilot observations

- Public forgot-password and self-service recovery remain deferred by design.
- The shared local rehearsal database already contains prior pilot data, so report totals reflect both historical smoke data and the current run.
- No schema changes, migrations, or scope-expanding product work were needed for this smoke run.

## 7. Fixes Applied

- None. No code changes were required for the smoke run itself.

## 8. Go / No-Go Decision

- GO

Rationale: the admin-only staff password reset worked and was audited, the target user could log in with the new password while the old password failed, cross-restaurant reset was blocked safely, POS-to-kitchen, billing, receipt audit, and cashier-shift flows all passed, and no P0 blocker was found.

## 9. Follow-Up Issues

- None.
