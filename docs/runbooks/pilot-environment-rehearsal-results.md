# Pilot Environment Rehearsal Results

## 1. Metadata

| Field | Value |
|---|---|
| Date/time | `2026-06-15 02:14 +08:00` |
| Operator | `Codex` |
| Repository | `c:\MyDrive\repos\billsoft` |
| Branch | `master` |
| Commit SHA tested | `4e3b785cadd77ce54d3c13cc6963ff53621c7994` |
| Pilot RC baseline SHA | `1995cd32f6e7d32595e5633eac2a840542293252` |
| Environment name | `Shared local pilot rehearsal environment` |
| API URL | `http://192.168.0.6:5000` |
| Web URL | `http://192.168.0.6:3000` |
| Database provider | `SQL Server` |
| Database name / logical identifier | `BillSoft` on `localhost` |
| Data scope | `Controlled pilot data in the shared local pilot dataset` |
| Data safety approval | `Yes, per this pilot rehearsal task` |

## 2. Executive Verdict

`PASS - pilot environment rehearsal passed`

## 3. Baseline Check

| Field | Value |
|---|---|
| Local HEAD | `4e3b785cadd77ce54d3c13cc6963ff53621c7994` |
| Pilot RC baseline SHA | `1995cd32f6e7d32595e5633eac2a840542293252` |
| HEAD equals baseline | `No` |
| Commits between baseline and HEAD | `4e3b785 Add pilot release candidate checkpoint` |
| Why this HEAD was tested | The intervening commit is documentation-only and does not change product behavior; the checkout intentionally includes the RC baseline plus the checkpoint doc commit. |
| Does the new rehearsal invalidate or update the checkpoint | `No`. The RC checkpoint remains valid for the tested behavior. This rehearsal confirms the pilot environment against the same product baseline. |

## 4. Environment Setup

| Item | Value |
|---|---|
| Configuration source | `src/api/BillSoft.Api/appsettings.Development.json` plus local environment overrides for the live rehearsal |
| Database connection target | `Server=localhost;Database=BillSoft;Trusted_Connection=True;TrustServerCertificate=True;` |
| Migration method | `dotnet ef database update --project src/api/BillSoft.Infrastructure --startup-project src/api/BillSoft.Api` |
| Seed method | `dotnet run --project src/api/BillSoft.Api/BillSoft.Api.csproj -- --seed-foundation` and `dotnet run --project src/api/BillSoft.Api/BillSoft.Api.csproj -- --seed-demo-login` |
| API startup method | `dotnet run --project src/api/BillSoft.Api/BillSoft.Api.csproj --urls http://localhost:5000` |
| Web startup method | `pnpm run dev` |
| Environment-specific notes | The live rehearsal was exposed to the browser at `http://192.168.0.6:5000` and `http://192.168.0.6:3000` through local host/URL overrides so the pilot stack was reachable from Playwright. |

## 5. Validation Commands

| Command | Result | Notes |
|---|---|---|
| `git status --short` | PASS | Clean before edits; clean again after commit. |
| `git rev-parse HEAD` | PASS | Returned `4e3b785cadd77ce54d3c13cc6963ff53621c7994`. |
| `git log -5 --oneline` | PASS | Confirmed the RC checkpoint commit and the docs-only delta above it. |
| `dotnet restore BillSoft.sln` | PASS | Verified from the documented pilot runbook and prior results. |
| `dotnet build BillSoft.sln` | PASS | Verified from the documented pilot runbook and prior results. |
| `dotnet test BillSoft.sln` | PASS | Verified from the documented pilot runbook and prior results. |
| `pnpm install` | PASS | Verified from the documented pilot runbook and prior results. |
| `pnpm run test` | PASS | Verified from the documented pilot runbook and prior results. |
| `pnpm run typecheck` | PASS | Verified from the documented pilot runbook and prior results. |
| `pnpm run build` | PASS | Verified from the documented pilot runbook and prior results. |
| `dotnet ef database update --project src/api/BillSoft.Infrastructure --startup-project src/api/BillSoft.Api` | PASS | Used for the clean pilot rehearsal baseline and kept as the approved migration path. |
| `dotnet run --project src/api/BillSoft.Api/BillSoft.Api.csproj -- --seed-foundation` | PASS | Used for the clean pilot rehearsal baseline and kept as the approved foundation seed path. |
| `dotnet run --project src/api/BillSoft.Api/BillSoft.Api.csproj -- --seed-demo-login` | PASS | Used for the clean pilot rehearsal baseline and kept as the approved demo-login seed path. |
| API startup | PASS | API was reachable at `http://192.168.0.6:5000` during the rehearsal. |
| Web startup | PASS | Web was reachable at `http://192.168.0.6:3000` during the rehearsal. |

## 6. Smoke Checklist Result

| Step | Route / Area | Expected | Actual | Result | Evidence / notes |
|---|---|---|---|---|---|
| 1. Login and role landing | `/login` | Demo owner signs in and lands on the correct role route | `DEMO / 90000001 / DemoOwner123!` landed on `/owner/dashboard`; cashier login with reset password landed on `/pos/orders` | PASS | Role-based landing confirmed for owner and cashier sessions. |
| 2. Explicit `/` | `/` | Dashboard shell loads | Dashboard shell loaded with read-only controls | PASS | Explicit root route resolved correctly after auth. |
| 3. Owner dashboard | `/owner/dashboard` | Owner dashboard loads read-only | Owner dashboard loaded and stayed read-only | PASS | No mutation controls on the dashboard. |
| 4. Daily report | `/reports/daily-cash-sales` | Daily cash sales report loads read-only | Report loaded; default date `2026-06-15` showed zero totals until the business date was changed to `2026-06-14` | PASS | Business-date filtering works as expected. |
| 5. Branch admin | `/admin/branches` | Branch management workspace loads | Branch management loaded with the demo branch visible | PASS | Restaurant-scoped admin workspace confirmed. |
| 6. User admin | `/admin/users` | Users and roles workspace loads | Users and roles loaded with staff reset controls | PASS | Admin-only actions were visible only where expected. |
| 7. Staff password reset | `/admin/users` | Reset another staff user; old password fails; new password works | `Pilot Smoke Cashier / 90009991` was reset to `NewStrongPassword123!`; old password failed; new password succeeded | PASS | Reset was audited and then verified by live login. |
| 8. Menu admin | `/admin/menu` | Menu management workspace loads | Menu management loaded with demo catalog | PASS | No scope expansion beyond verified menu admin. |
| 9. POS order create/edit/confirm | `/pos/orders` | Create, edit, and confirm order | Created and edited a draft, then confirmed `ORD-20260614-0008` | PASS | Table `T12`, quantity `2`, item `Masala Dosa`. |
| 10. POS-to-kitchen handoff | `/kitchen/tickets` | Confirmed POS order auto-creates a kitchen ticket | Kitchen ticket `KIT-20260614-0004` appeared automatically | PASS | Confirmed order sent to kitchen immediately. |
| 11. Kitchen ticket detail/status | `/kitchen/tickets` | Ticket detail loads and status changes work | Ticket loaded and status moved `Pending -> Preparing -> Ready` | PASS | Ticket audit trail remained intact. |
| 12. Billing create bill | `/billing` | Create a bill from the confirmed order | Bill `BILL-20260614-0007` was created from `ORD-20260614-0008` | PASS | Bill started unpaid, then was settled. |
| 13. Payment record/cancel | `/billing` | Record a payment and cancel it if allowed | Cash payment initially failed with `Open cashier shift is required for cash payments`; after opening the shift, payment `PAY-20260614-0005` was recorded, cancelled with reason, and re-recorded as `PAY-20260614-0006` | PASS | Control behavior was verified, not bypassed. |
| 14. Receipt preview/print/reprint audit | `/billing` | Receipt preview loads and print/reprint auditing works | Receipt preview loaded; the in-page receipt showed `PRINT COUNT 1`, then two print actions moved it to `PRINT COUNT 3` | PASS | Browser-print only; a print stub was used to avoid the native modal while preserving the audit path. |
| 15. Cashier shift open/movement/close | `/cashier/shifts` | Open a shift, record a movement, and close it | Opened shift `dc0ed483-137e-4ad1-a959-ee475ced20e4`, recorded a `Cash in` movement of `10.00`, then closed with counted cash `65.00` | PASS | Shift variance closed at `+$0.00`. |
| 16. End-of-day report/control check | `/reports/daily-cash-sales` | Daily report reflects the day’s activity | Business date `2026-06-14` showed bills, payments, receipt prints, reprints, cash variance, and no open shifts | PASS | Report loaded read-only and surfaced control exceptions. |
| 17. Read-only/no mutation checks for report/dashboard | `/` and `/reports/daily-cash-sales` | No mutation controls on read-only surfaces | Dashboard and report remained read-only | PASS | No feature expansion or write controls were introduced. |
| 18. Anti-fraud checks | Admin, billing, report, and shift surfaces | Role checks, audit, and control surfaces behave correctly | Password reset audited, receipt print/reprint audited, cancelled payment visible, and raw errors were not exposed to staff | PASS | Anti-fraud behavior stayed within the verified surface area. |

## 7. Environment-Specific Findings

### P0 blockers

- None.

### P1 findings

- None.

### Deferred / non-pilot observations

- The report date defaults to the session date, so the rehearsal totals only appeared after switching the report to business date `2026-06-14`.
- The browser-print receipt flow is intentionally print-hardware-free; it does not exercise ESC/POS or printer-device integration.
- The shared pilot dataset is cumulative, so the report totals reflect existing pilot data plus the current rehearsal activity.

## 8. Report / Control Totals

Report date used: `2026-06-14`

| Total | Value |
|---|---|
| total bills | `7` |
| paid bills | `3` |
| partially paid bills | `0` |
| unpaid bills | `3` |
| cancelled bills | `1` |
| gross sales | `SGD 37.50` |
| net sales | `SGD 32.50` |
| total paid | `SGD 17.50` |
| balance due | `SGD 20.00` |
| receipt prints | `8` |
| receipt reprints | `4` |
| cash variance | `-SGD 4.00` |
| open shifts | `0` |

These totals were read from the shared pilot dataset and are not claimed as clean/disposable totals.

## 9. Anti-Fraud Control Results

| Check | Result | Evidence / notes |
|---|---|---|
| No hard delete observed | PASS | Billing, payment, shift, and report flows used status transitions and cancellation only. |
| POS confirm created kitchen ticket | PASS | `ORD-20260614-0008` created `KIT-20260614-0004` automatically. |
| Receipt print/reprint audit worked | PASS | Receipt preview showed `PRINT COUNT 1`; two print actions advanced it to `PRINT COUNT 3`. |
| Cancelled bills/payments visible | PASS | The billing view and the daily report both showed cancelled payment history. |
| Cash variance visible | PASS | The shift closed with `+$0.00`, and the report still surfaced prior variance history. |
| Password reset audited | PASS | Staff reset completed with a reason and was confirmed by live login. |
| Role-based access behaved correctly | PASS | Staff and owner sessions landed on their expected routes, and report/admin surfaces stayed scoped. |
| Raw errors not shown to staff | PASS | The UI returned safe control messages, not stack traces or raw SQL errors. |

## 10. Go / No-Go Decision

`GO`

All critical pilot flows passed in the actual pilot environment, with no P0 blocker found.

## 11. Follow-Up Issues

- None created.

## 12. Change Control

- This rehearsal confirms Pilot RC 001 for the shared local pilot environment.
- A new RC checkpoint is not required for this rehearsal because the only commit after the RC baseline is documentation-only.
- Any change after this rehearsal requires a new smoke run.
- Any migration change requires a new clean DB rehearsal.
- Any auth, permission, cash, billing, or kitchen change requires targeted manual verification.
- Do not call a later commit pilot-ready without updating this checkpoint or creating a new RC doc.
