# BillSoft Pilot RC 003 Checkpoint

## 1. Metadata

| Field | Value |
|---|---|
| RC name | `Pilot RC 003` |
| Verification date | `2026-06-17` |
| Repository | `c:\MyDrive\repos\billsoft` |
| Branch | `master` |
| Verified commit SHA | `cb57effb8b96480253b3f8d327439313f654f514` |
| Issue #44 status | `Closed as completed` |
| Verification scope | Documentation and release checkpoint only |

## 2. Executive Verdict

`GO`

Recipe/BOM inventory deduction passed live rehearsal, GitHub issue #44 is closed as completed, the worktree is clean, and no application or infrastructure changes were required for this checkpoint.

## 3. Baseline Check

| Check | Result | Notes |
|---|---|---|
| `git fetch origin` | PASS | Remote refs refreshed before documentation work. |
| `git status --short` | PASS | Worktree was clean at baseline and remained clean before commit. |
| `git log --oneline -10` | PASS | `cb57eff Add recipe inventory deduction foundation` was the latest verified product commit. |
| Latest verified product commit present on `master` | PASS | `cb57effb8b96480253b3f8d327439313f654f514` was already at `HEAD`. |
| Issue #44 | PASS | Closed as completed after the verified recipe/BOM inventory deduction foundation shipped. |

## 4. Validation Results

| Command | Result | Notes |
|---|---|---|
| Markdown / link review | PASS | `npx markdown-link-check docs/runbooks/pilot-release-candidate-checkpoint.md` found no hyperlinks to validate. |
| `pnpm --dir src/web test` | PASS | Passed on a standalone rerun after a transient contention failure in the parallel validation batch. |
| `pnpm --dir src/web run typecheck` | PASS | Passed in the current validation batch. |
| `pnpm --dir src/web run build` | PASS | Passed in the current validation batch. |
| `dotnet test BillSoft.sln` | PASS | Passed in the current validation batch. |

## 5. Tested Capabilities

| Capability | Result | Notes |
|---|---|---|
| Login | PASS | Demo owner login worked and landed on the expected authenticated surface. |
| Role landing | PASS | Authenticated sessions landed on the correct role routes. |
| Admin users | PASS | `/admin/users` loaded cleanly. |
| Admin staff password reset | PASS | Admin-only staff password reset was verified in the live rehearsal. |
| Branches | PASS | `/admin/branches` loaded cleanly. |
| Menu | PASS | `/admin/menu` loaded cleanly. |
| Inventory item / stock movement | PASS | Inventory item creation and stock movement recording were verified. |
| Recipe / stock usage mapping | PASS | Recipe mapping was edited and saved for the menu item used in rehearsal. |
| POS order | PASS | POS order creation and confirmation were verified. |
| Kitchen ticket | PASS | Kitchen ticket status transitions and completion were verified. |
| Recipe deduction on kitchen completion | PASS | Deduction happened once on completion and did not repeat on retry. |
| Idempotent retry behavior | PASS | Repeating completion did not create a second deduction or stock movement. |
| Insufficient-stock blocking | PASS | Completion was blocked when required stock exceeded availability. |
| No-recipe completion | PASS | Completion was allowed and no inventory movement was created. |
| Bill generation | PASS | Bill creation from a confirmed order was verified. |
| Payment recording | PASS | Cash payment recording was verified. |
| Receipt / audit | PASS | Receipt preview loaded and the print event was recorded. |
| Cashier shift | PASS | Shift open and close were verified through the live workflow. |
| Daily cash sales | PASS | `/reports/daily-cash-sales` loaded and reflected the session activity. |
| Owner dashboard | PASS | `/owner/dashboard` loaded with sensible metrics after the rehearsal. |
| CI / infra / CD | PASS | Existing pipeline definitions were reviewed; no infra/CI/CD changes were needed for this checkpoint. |

## 6. Live Smoke Results

| Area | Result | Notes |
|---|---|---|
| Recipe deduction idempotence | PASS | Kitchen completion deducted stock exactly once; replaying completion did not double-deduct. |
| Insufficient-stock smoke | PASS | Preview showed insufficient stock and completion was blocked without partial deduction. |
| No-recipe smoke | PASS | Preview showed `NoRecipe`; completion succeeded with no stock movement. |
| Billing / payment / receipt / cashier / report loop | PASS | Bill creation, cash payment, receipt preview, receipt print event, shift close, daily report, and owner dashboard were all verified live. |
| Inventory ledger traceability | PASS | Inventory stock remained ledger-derived and movement history linked back to the kitchen deduction. |
| Auditability | PASS | Recipe update and kitchen deduction audit records were present. |

## 7. Known Limitations

- OCR not implemented.
- Vendor settlement not implemented.
- Accounting export not implemented.
- Production monitoring is still minimal and optional.
- Self-service password recovery not implemented.
- Automatic purchase ordering not implemented.

## 8. Deferred Items

- OCR, vendor settlement, accounting, and export scope remain out of this checkpoint.
- Printer hardware integration remains outside this checkpoint.
- Any production observability hardening remains a later operational decision, not an RC blocker.

## 9. Go / No-Go Recommendation

`GO`

The RC is ready to advance because the verified product commit passed the live rehearsal, the critical kitchen-to-inventory path is idempotent, the money loop still works, and no unresolved blocker was found.

## 10. Change Control

- This checkpoint is documentation only.
- No application behavior changed.
- No infra, CI/CD, or migration work was performed for this checkpoint.
- If any product behavior changes after this commit, create a new RC checkpoint and rerun the live rehearsal evidence.
