# Pre-MVP Gap Review

## Executive Verdict

**Ready after remaining non-P0 fixes**

BillSoft has the core pilot spine in place, and the original POS-to-kitchen blocker has now been addressed by auto-creating kitchen tickets during POS confirmation. The remaining pilot work is now focused on non-P0 hardening.

## Current Verified Capabilities

- Role-based login landing is in place, with protected routes and preserved return-path handling.
- Owner/admin, cashier, waiter, and kitchen surfaces are wired through the shell.
- Owner dashboard exists as a read-only control surface.
- Daily cash sales exception report exists as a read-only control report.
- POS order capture supports create, edit, confirm, cancel, and recent-order inspection.
- Kitchen ticket workspace supports queue review, detail inspection, status changes, and cancellation.
- Billing supports bill creation from confirmed orders, payment recording, bill cancellation, payment cancellation, receipt preview, and receipt print audit.
- Cashier shifts support branch selection, open, movement recording, close, and variance calculation.
- Admin setup exists for users, branches, and menu categories/items, including activate/deactivate flows.
- Admin users now include an owner/admin-only staff password reset or credential reissue action for same-restaurant accounts, with self-reset blocked and audit logging preserved.
- Audit-oriented rules are already documented: no hard delete, status transitions, immutable bill numbers, price history, and receipt print auditing.
- Local setup and demo seed guidance exist in docs.

## P0 Pilot Blockers

1. **POS confirm now generates a kitchen ticket in-browser**
   - `PosOrderService.ConfirmAsync` now confirms the order and creates the kitchen ticket in the same workflow.
   - `KitchenTicketsPage` still focuses on viewing and mutating tickets, which is the correct division of responsibility.
   - The separate backend `POST /api/v1/kitchen/tickets` endpoint can remain available for explicit admin/API workflows, but the normal browser confirm flow now completes the handoff.
   - This closes the original order-to-kitchen pilot blocker.

## P1 Important Gaps

1. **Branch filtering is not exposed in the daily report or owner dashboard UI**
   - The APIs already accept `branchId`.
   - `/reports/daily-cash-sales` and `/owner/dashboard` only expose the date selector in the browser.
   - For a single-branch pilot this is acceptable; for a multi-branch pilot it adds unnecessary URL editing and slows control review.

2. **Kitchen creation remains backend-authoritative instead of a normal operator action**
   - Even after the P0 fix, the ticket flow should stay explicit and tested so staff do not fall back to paper slips or manual workarounds.
   - This is mostly a workflow-hardening concern once the browser path exists.

## Deferred / Not Required For Pilot

- Tax or legal receipt compliance
- GST / VAT / GSTIN
- Printer hardware / ESC-POS
- Payment gateway integration
- Inventory deduction
- Vendor OCR
- Accounting export
- Advanced reports / charts
- Multilingual UI
- Offline mode

## Recommended Next Implementation Sequence

1. Add branch filtering UI to the daily report and owner dashboard if the pilot is multi-branch.
2. Keep the rest of the work focused on polish, not new product expansion.

## Proposed GitHub Issues

1. `#33` - **Auto-create kitchen tickets from confirmed POS orders**
   - Priority: `P0`
   - Scope: make the POS confirm flow create exactly one active kitchen ticket from the confirmed order snapshot, reusing the existing kitchen ticket model, audit trail, and uniqueness rules.
   - Why it matters: the pilot cannot complete the POS-to-kitchen handoff in the browser today.

## Evidence

Reviewed docs:

- `AGENTS.md`
- `README.md`
- `docs/architecture/authentication-plan.md`
- `docs/architecture/frontend-layouts.md`
- `docs/requirements/permission-matrix.md`
- `docs/database/database-tables.md`
- `docs/development/local-setup.md`
- `docs/workflows/order-to-billing.md`
- `docs/workflows/kitchen-ticket.md`
- `docs/workflows/cashier-shift.md`
- `docs/workflows/daily-cash-sales-exception-report.md`
- `docs/workflows/owner-dashboard.md`

Reviewed routes and shell wiring:

- `src/web/src/App.tsx`
- `src/web/src/components/layout/navigation.ts`
- `src/api/BillSoft.Api/Program.cs`

Reviewed browser surfaces:

- `src/web/src/features/auth/LoginPage.tsx`
- `src/web/src/features/auth/landingRoute.ts`
- `src/web/src/features/pos/PosOrderCapturePage.tsx`
- `src/web/src/features/kitchen/KitchenTicketsPage.tsx`
- `src/web/src/features/billing/BillingPage.tsx`
- `src/web/src/features/cashiering/CashierShiftPage.tsx`
- `src/web/src/features/admin/AdminUsersPage.tsx`
- `src/web/src/features/admin/branches/BranchManagementPage.tsx`
- `src/web/src/features/admin/menu/MenuManagementPage.tsx`
- `src/web/src/features/reports/DailyCashSalesReportPage.tsx`
- `src/web/src/features/dashboard/OwnerDashboardPage.tsx`

Reviewed backend endpoints:

- `src/api/BillSoft.Api/Auth/AuthEndpoints.cs`
- `src/api/BillSoft.Api/Pos/PosOrderEndpoints.cs`
- `src/api/BillSoft.Api/Kitchen/KitchenTicketEndpoints.cs`
- `src/api/BillSoft.Api/Billing/BillingEndpoints.cs`
- `src/api/BillSoft.Api/Cashiering/CashierShiftEndpoints.cs`
- `src/api/BillSoft.Api/Admin/UserAdminEndpoints.cs`
- `src/api/BillSoft.Api/Admin/BranchAdminEndpoints.cs`
- `src/api/BillSoft.Api/Admin/MenuAdminEndpoints.cs`
- `src/api/BillSoft.Api/Reports/DailyCashSalesReportEndpoints.cs`
- `src/api/BillSoft.Api/Dashboard/OwnerDashboardEndpoints.cs`

Reviewed tests and coverage map:

- `src/web/src/App.routes.test.tsx`
- `src/web/src/features/auth/LoginPage.test.tsx`
- `src/web/src/features/auth/landingRoute.test.ts`
- `src/web/src/components/layout/navigation.test.tsx`
- `src/web/src/features/pos/PosOrderCapturePage.test.tsx`
- `src/web/src/features/kitchen/KitchenTicketsPage.test.tsx`
- `src/web/src/features/billing/BillingPage.test.tsx`
- `src/web/src/features/cashiering/CashierShiftPage.test.tsx`
- `src/web/src/features/dashboard/OwnerDashboardPage.test.tsx`
- `tests/BillSoft.Tests/*.cs`

## Risks And Assumptions

- This review is based on docs, code, and tests only; I did not run a live browser/API pilot proof in this pass.
- The kitchen-ticket blocker is inferred from the POS confirm service and the absence of any browser call path to ticket creation.
- The report/dashboard branch selector gap is treated as P1 because the API already supports `branchId`, so the missing piece is UI convenience rather than missing backend capability.
- Password reset is treated as P1 because the pilot can still start with created accounts, but support will be brittle if a staff member loses access.
