# BillSoft Pilot Deployment Smoke Checklist

## Purpose

Use this checklist to verify a deployed BillSoft pilot environment after infrastructure and application deployment.

This is the live-deployment companion to the local pilot smoke checklist. It focuses on proving that the deployed pilot can actually be launched, that the main operational surfaces are reachable, and that live Azure OCR behaves safely on a real deployed runtime.

Use it together with:

- [Pilot Operations Runbook](./pilot-operations-runbook.md)
- [Pilot Smoke Checklist](./pilot-smoke-checklist.md)
- [Azure OCR Live Smoke Results](./azure-ocr-live-smoke-results.md)
- [Pilot Deployment Smoke Results](./pilot-deployment-smoke-results.md)

## Required Environment Variables

Set these before running the live smoke script:

| Variable | Required | Purpose |
|---|---|---|
| `BILLSOFT_BASE_URL` | Yes | Deployed BillSoft web origin, such as `https://app-billsoft-stage.example.com`. |
| `BILLSOFT_RESTAURANT_CODE` | Yes | Restaurant code used by the staff and owner login forms. |
| `BILLSOFT_STAFF_MOBILE` | Yes | Staff account mobile number used for POS, billing, cashier, and operational checks. |
| `BILLSOFT_STAFF_PASSWORD` | Yes | Password for the staff account. |
| `BILLSOFT_OWNER_MOBILE` | Yes | Owner or admin account mobile number used for dashboard, reports, and vendor checks. |
| `BILLSOFT_OWNER_PASSWORD` | Yes | Password for the owner or admin account. |
| `BILLSOFT_OCR_SAMPLE_FILE` | Yes | Path to a sanitized bill sample used for live Azure OCR verification. |
| `BILLSOFT_PLAYWRIGHT_CORE_PATH` | No | Optional resolvable path to `playwright-core` if it is not available from the current Node environment. |
| `BILLSOFT_PLAYWRIGHT_PACKAGE` | No | Optional package name override for Playwright resolution. |
| `BILLSOFT_CHROMIUM_PATH` | No | Optional Chromium executable path when the default browser is not available. |
| `BILLSOFT_SMOKE_SCREENSHOT_DIR` | No | Optional screenshot output directory for evidence. |

Recommended script:

```bash
node scripts/playwright/pilot-deployment-smoke.cjs
```

The script sets the browser language to English for stable selectors and fails fast when any required variable is missing.

## Test Accounts Needed

Use real pilot accounts. Do not use placeholder credentials.

- Staff account:
  - Should be able to sign in with restaurant code + mobile + password.
  - Should have the permissions needed for POS, billing, cashier-shift, and operational checks.
  - A cashier-support or floor-support account is usually the right choice.
- Owner or admin account:
  - Should have `Report.View`.
  - Should have vendor permissions for OCR review and vendor statement access.
  - Should be able to load owner dashboard and daily cash sales.

If the pilot splits these responsibilities across multiple accounts, use the script for the route checks and run the manual operational steps with the appropriate account.

## Data Assumptions

Confirm these before the smoke run:

- At least one active branch exists for the target restaurant.
- At least one active menu category and menu item exist.
- A confirmed POS order can be created from the live menu data.
- An open cashier shift can be created or already exists for the branch.
- At least one vendor and one inventory item exist for vendor review and statement checks.
- The OCR sample file is sanitized and safe to upload.
- The OCR sample file is chosen to produce a review-required or low-confidence draft, not a synthetic secret leak.
- Vendor statement data exists for at least one vendor.

## Smoke Path

### 1. Staff Login

- Open `/login`.
- Sign in with `BILLSOFT_RESTAURANT_CODE`, `BILLSOFT_STAFF_MOBILE`, and `BILLSOFT_STAFF_PASSWORD`.

Expected result:

- login succeeds
- the app navigates away from `/login`
- no raw stack trace or secret is shown

Failure triage:

- If login fails, verify the restaurant code, mobile, password, and deployed auth endpoint.
- If the page stays on `/login`, check for a backend auth outage or wrong environment URL.

### 2. Owner Login

- Open `/login` in a fresh browser context.
- Sign in with `BILLSOFT_RESTAURANT_CODE`, `BILLSOFT_OWNER_MOBILE`, and `BILLSOFT_OWNER_PASSWORD`.

Expected result:

- login succeeds
- the app navigates away from `/login`
- the owner can reach reporting and dashboard surfaces

Failure triage:

- If the owner lands on the wrong route, confirm the role mapping on the deployed auth response.
- If owner access is denied, check `Report.View` and related support permissions.

### 3. Owner Dashboard

- Open `/owner/dashboard`.
- Confirm the dashboard loads.
- Confirm the top-level summary, quick links, and vendor dues controls render.

Expected result:

- `Owner dashboard` loads
- `Open statement` and reporting surfaces are visible
- the page remains read-only

### 4. Daily Cash Sales Report

- Open `/reports/daily-cash-sales`.
- Confirm the report heading loads.
- Confirm the summary cards and control-exception sections render.

Expected result:

- `Daily cash sales report` loads
- the report is read-only
- cash totals, shift summary, and exception sections are visible

### 5. POS Order Capture

- Open `/pos/orders`.
- Create a test order from an active menu item.
- Edit the draft line quantity or notes.
- Confirm the order.
- Cancel a separate disposable draft or a confirmed order only when the kitchen-side impact has been acknowledged.

Expected result:

- draft creation works
- edit/save works
- confirm sends the order to the kitchen
- cancel uses the supported workflow and leaves an auditable status transition

Failure triage:

- If confirm does not create a kitchen ticket, stop the smoke run and inspect the POS confirm backend path.
- If cancel fails, verify the cancellation reason and status transition rules.

### 6. Kitchen Tickets

- Open `/kitchen/tickets`.
- Confirm the kitchen ticket created from the confirmed POS order appears.
- Verify the item lines and status labels match the confirmed snapshot.

Expected result:

- ticket queue loads
- the confirmed order is visible in kitchen
- the ticket status updates through the supported lifecycle

### 7. Billing

- Open `/billing`.
- Create a bill only from a confirmed order.
- Record a cash payment only after a cashier shift is open.
- If the workflow requires it, cancel the payment or bill using the supported cancellation path.

Expected result:

- bill creation only uses confirmed POS orders
- cash payment is blocked until a current cashier shift exists
- bill and payment changes remain auditable

Failure triage:

- If the bill candidate appears before order confirmation, stop and investigate the billing query.
- If cash payment is allowed with no open shift, that is a release blocker.

### 8. Cashier Shift

- Open `/cashier/shifts`.
- Open a shift for the active branch if no current shift exists.
- Close the shift after the payment and billing checks are complete.

Expected result:

- an open shift can be created for the branch
- the open shift is visible while it is active
- closing the shift calculates variance from the recorded values

### 9. Vendor Workspace and OCR

- Open `/vendors`.
- Upload the file referenced by `BILLSOFT_OCR_SAMPLE_FILE`.
- Wait for the extracted draft to appear.
- Confirm that provider warnings or low-confidence review state are shown when the sample is configured for that path.
- Review the draft.
- If the form becomes valid, save the review.
- If the draft can be safely confirmed with the sample data, create the vendor bill.

Expected result:

- upload succeeds
- extraction succeeds
- review-required or low-confidence guidance is visible
- raw provider or stack-trace text is not shown to the user
- the draft can be reviewed and saved when the sample data permits it

Failure triage:

- If upload fails, verify the OCR runtime settings on the deployed API.
- If the UI shows raw provider text, stop immediately and treat it as a blocker.
- If the save or confirm action stays disabled, inspect the draft mappings, duplicate warnings, and sample-file quality.

### 10. Vendor Statement

- Open `/vendors/statement`.
- Confirm the statement page loads.
- Confirm the dues summary, payable bill list, settlement history, and timeline sections render.

Expected result:

- vendor statement loads for the selected vendor and branch
- bill numbers are readable
- internal IDs are not leaked in the rendered statement

## Explicit Azure OCR Verification

The OCR portion is not complete until all of these are confirmed:

- upload succeeds against the deployed API
- OCR extraction returns a draft
- provider warnings or low-confidence review state are visible and phrased for operators
- raw provider payloads are not surfaced in the UI
- raw provider error text, stack traces, JSON problem details, and secret values are not shown
- the operator can review the draft and save it when the sample data permits
- the created or reviewed draft remains auditable

If you need a negative-path OCR check, run it separately with a deliberately malformed sample file and confirm the UI still shows only the safe error message.

## Failure Triage Notes

Use this order when something fails:

1. Stop at the first unexpected result.
2. Record the exact route, account, time, and visible error text.
3. Capture the screenshot or browser state before refreshing or retrying.
4. Verify whether the failure is login, permission, seed data, or environment configuration.
5. Do not move on to the next step until the root cause is understood.
6. If the issue affects money, kitchen handoff, cashier-shift control, or OCR safety, treat the pilot as blocked.

Common blockers:

| Symptom | Likely cause | Next step |
|---|---|---|
| Login fails | Wrong credentials or wrong restaurant code | Recheck the account and the deployed auth endpoint. |
| Dashboard or report shows not-authorized | Missing `Report.View` or owner permission mapping | Verify the owner/admin account. |
| POS confirm does not create a kitchen ticket | POS confirm path regressed | Stop and inspect the confirm API response. |
| Cash payment is allowed without an open shift | Shift gating regressed | This is a release blocker. |
| OCR shows raw provider text | Safe-error filtering regressed | Stop immediately and treat as a blocker. |
| OCR save/confirm stays disabled | Draft mappings or sample data are incomplete | Fix the sample or verify the draft state. |

## GO / NO-GO Criteria

GO only if all of the following are true:

- staff login works
- owner login works
- owner dashboard loads
- daily cash sales report loads
- POS create/edit/confirm/cancel works
- confirmed POS orders create kitchen tickets
- billing only creates bills from confirmed orders
- cash payment requires an open cashier shift
- vendor OCR upload/extract/review/save behaves safely
- vendor statement loads and shows readable bill numbers

NO-GO if any of the following are true:

- login is broken
- report or dashboard access is missing
- POS confirm does not create a kitchen ticket
- billing ignores the confirmed-order rule
- cash payment bypasses shift control
- OCR leaks raw provider text or secrets
- vendor statement cannot load

