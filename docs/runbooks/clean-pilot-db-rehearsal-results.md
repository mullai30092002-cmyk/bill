# Clean Pilot Database Rehearsal Results

## 1. Metadata

| Field | Value |
|---|---|
| Date/time | `2026-06-15 01:15 Asia/Singapore` |
| Commit SHA tested | `fc98c0cd721026512f1b15a249d79cfd852323dd` |
| Database provider | `SQL Server LocalDB` |
| Database name/path | `BillSoftCleanPilotRehearsal` via `Server=(localdb)\MSSQLLocalDB` |
| API URL | `http://localhost:5000` |
| Web URL | `http://localhost:3000` |
| Operator | `Codex` |
| Disposable database | `Yes` |

## 2. Executive Verdict

`PASS - clean DB rehearsal passed`

## 3. Clean Database Setup

| Item | Result | Notes |
|---|---|---|
| Clean database creation | PASS | Dropped the disposable LocalDB database `BillSoftCleanPilotRehearsal` and recreated it only for this rehearsal. |
| Disposable confirmation | PASS | The rehearsal used a clearly disposable LocalDB name and did not touch any non-disposable database. |
| Migration command used | PASS | `dotnet ef database update --project src/api/BillSoft.Infrastructure --startup-project src/api/BillSoft.Api` |
| Migration result | PASS | Applied from zero successfully after the blocker fix. The full migration chain ended with `20260615000000_DropLegacyBranchCurrencyTimezoneColumns`. |
| Seed foundation result | PASS | `dotnet run --project src/api/BillSoft.Api/BillSoft.Api.csproj -- --seed-foundation` completed successfully. |
| Seed demo login result | PASS | `dotnet run --project src/api/BillSoft.Api/BillSoft.Api.csproj -- --seed-demo-login` completed successfully after the schema blocker was removed. |
| API startup result | PASS | API started successfully at `http://localhost:5000`. |
| Web startup result | PASS | Web started successfully at `http://localhost:3000`. |

### Setup Notes

- LocalDB database name used: `BillSoftCleanPilotRehearsal`
- Connection string used for the rehearsal:

```text
Server=(localdb)\MSSQLLocalDB;Database=BillSoftCleanPilotRehearsal;Trusted_Connection=True;TrustServerCertificate=True;
```

- The clean DB was created by dropping the disposable LocalDB name, applying migrations from zero, then running the foundation and demo-login seed commands.
- The initial demo-login seed failure exposed a legacy branch-column migration issue; that blocker was fixed before the rehearsal continued.

## 4. Validation Commands

| Command | Result | Notes |
|---|---|---|
| `dotnet restore BillSoft.sln` | PASS | Restored successfully. |
| `dotnet build BillSoft.sln` | PASS | Built successfully. |
| `dotnet test BillSoft.sln` | PASS | Passed after the migration fix; 358 tests passed. |
| `pnpm install` | PASS | Ran in `src/web` successfully. |
| `pnpm run test` | PASS | Frontend test suite passed. |
| `pnpm run typecheck` | PASS | Frontend typecheck passed. |
| `pnpm run build` | PASS | Frontend build passed. |
| `dotnet ef database update --project src/api/BillSoft.Infrastructure --startup-project src/api/BillSoft.Api` | PASS | Applied from zero on the disposable LocalDB database. |
| `dotnet run --project src/api/BillSoft.Api/BillSoft.Api.csproj -- --seed-foundation` | PASS | Foundation seed completed with permissions, roles, and role-permissions inserted. |
| `dotnet run --project src/api/BillSoft.Api/BillSoft.Api.csproj -- --seed-demo-login` | PASS | Demo restaurant, branch, user, and role assignment were created. |
| Targeted regression test: `SqlServer_Migration_Script_Removes_Deprecated_Branch_Timezone_Columns` | PASS | Verified the generated SQL drops the deprecated `Currency` and `Timezone` branch columns. |

## 5. Smoke Checklist Result

| Step | Expected result | Actual result | Result | Notes |
|---|---|---|---|---|
| 1. Login and role landing | Demo owner signs in and lands on the correct role route | Demo owner login succeeded and landed on `/owner/dashboard` | PASS | Demo credentials were `DEMO / 90000001 / DemoOwner123!`. |
| 2. Explicit `/` | `/` loads the dashboard shell | `BillSoft dashboard` loaded and stayed read-only | PASS | No mutation controls were present. |
| 3. Owner dashboard | `/owner/dashboard` loads read-only | `Owner dashboard` loaded | PASS | Read-only control surface only. |
| 4. Daily report | `/reports/daily-cash-sales` loads read-only | `Daily cash sales report` loaded | PASS | Report surface stayed read-only. |
| 5. Branch admin | `/admin/branches` loads | `Branch management` loaded | PASS | Restaurant-scoped admin workspace loaded. |
| 6. User admin | `/admin/users` loads | `Users and roles` loaded | PASS | Admin user workspace loaded. |
| 7. Staff password reset | Reset another staff user; old password fails; new password works | `Pilot Smoke Cashier` was reset successfully; old login returned `401`; new login returned `200` | PASS | The reset request returned `200` and the new token carried the `Cashier` role. |
| 8. Menu admin | `/admin/menu` loads | `Menu management` loaded | PASS | Restaurant-scoped menu admin loaded. |
| 9. POS order | Create, edit, and confirm a test order | Order `ORD-20260614-0002` was created, updated, and confirmed | PASS | POS confirm auto-created a kitchen ticket. |
| 10. POS-to-kitchen handoff | Confirmed POS order auto-creates a kitchen ticket | Kitchen ticket `KIT-20260614-0001` was created in `Pending` state | PASS | Confirmed order `50f542cb-c6a5-426e-8a33-05d47d825172`. |
| 11. Kitchen ticket detail/status | Ticket detail loads and status changes work | Ticket moved `Pending -> Preparing -> Ready` | PASS | Ticket detail remained consistent after each status update. |
| 12. Billing create bill | Create a bill from the confirmed order | Bill `BILL-20260614-0001` was created | PASS | Bill started as `Unpaid`, then was paid. |
| 13. Payment record/cancel if disposable data | Record a payment and cancel it safely | Bill `BILL-20260614-0003` recorded `PAY-20260614-0003`, then the payment was cancelled and the bill returned to `Unpaid` | PASS | Cash payment required the open shift, and cancellation updated the bill correctly. |
| 14. Receipt preview/print/reprint audit | Receipt preview loads and print/reprint auditing works | Receipt preview loaded, `printCount` advanced to `2`, and `isReprint` became `true` on the second print-event call | PASS | The first bill ended as paid and the audit trail was written without exposing secrets. |
| 15. Cashier shift open/movement/close | Open a shift, record a movement, and close it | Shift `f346463b-bfd8-4aeb-a58e-c10c0c42556c` opened, movement was recorded, and the shift closed with zero variance | PASS | Opening cash `50`, movement `20`, cash payments `8`, expected/counted cash `78`. |
| 16. End-of-day report/control check | Report reflects the day’s activity | Daily report for `2026-06-14` showed the clean rehearsal totals | PASS | `2026-06-15` returned all zeros, confirming date isolation. |
| 17. Anti-fraud checks | Audit and control surfaces stay safe | No hard delete controls were present on the checked surfaces, password reset was audited, self-reset was hidden, receipt prints were audited, and cash movement reasons were required | PASS | Cross-restaurant negative probing was not available on this single-restaurant clean DB, but same-restaurant scope stayed intact and safe invalid-target behavior was observed. |

## 6. Clean Report Totals

Report date: `2026-06-14`

| Total | Value |
|---|---|
| total bills | `4` |
| paid bills | `1` |
| partially paid bills | `1` |
| unpaid bills | `1` |
| cancelled bills | `1` |
| gross sales | `17.5` |
| net sales | `15` |
| total paid | `8` |
| balance due | `9.5` |
| receipt prints | `2` |
| receipt reprints | `1` |
| cash variance | `0` |
| open shifts | `0` |

Confirmation:

- The totals above reflect only this clean rehearsal run for business date `2026-06-14`.
- The `2026-06-15` daily report query returned all zeros, so no historical/shared local data polluted the clean rehearsal totals.

## 7. Defects Found

### P0 defects

| Title | Severity | Reproduction | Evidence | Decision | Linked issue |
|---|---|---|---|---|---|
| Legacy branch columns blocked demo seed on the clean database | P0 | Apply migrations from zero on `BillSoftCleanPilotRehearsal`, then run `--seed-demo-login` | SQL Server rejected the insert with `Cannot insert the value NULL into column 'Timezone'` because the initial branch schema still carried legacy `Currency` and `Timezone` columns | Fixed locally before continuing the rehearsal | None |

### P1 findings

- None.

### Deferred / non-pilot observations

- Cross-restaurant negative probing could not be done directly on this clean DB because only the demo restaurant was seeded.
- The rehearsal intentionally stayed inside the pilot surface; no export, accounting, printer hardware, gateway, tax, inventory, OCR, or vendor expansion was added.

## 8. Fixes Applied

| Item | Details |
|---|---|
| Files changed | `tests/BillSoft.Tests/InfrastructureSmokeTests.cs`<br>`src/api/BillSoft.Infrastructure/Migrations/20260615000000_DropLegacyBranchCurrencyTimezoneColumns.cs` |
| Tests added/updated | Added `SqlServer_Migration_Script_Removes_Deprecated_Branch_Timezone_Columns` to verify the generated migration script drops the legacy branch columns. |
| Validation commands | `dotnet test tests/BillSoft.Tests/BillSoft.Tests.csproj --filter SqlServer_Migration_Script_Removes_Deprecated_Branch_Timezone_Columns`<br>`dotnet build BillSoft.sln`<br>`dotnet test BillSoft.sln`<br>`dotnet ef database update --project src/api/BillSoft.Infrastructure --startup-project src/api/BillSoft.Api`<br>`dotnet run --project src/api/BillSoft.Api/BillSoft.Api.csproj -- --seed-foundation`<br>`dotnet run --project src/api/BillSoft.Api/BillSoft.Api.csproj -- --seed-demo-login` |
| Commit hash | `8e8d60b31d6fbafdd97714be12c6de8dbe46e56f` |

## 9. Go / No-Go Decision

`GO`

The clean database rehearsal passed end to end after fixing the one P0 schema blocker. The disposable database was verified, migrations applied from zero, foundation and demo seeding succeeded, the smoke checklist passed, and the clean report totals were isolated from shared local data.

## 10. Follow-Up Issues

- None.
