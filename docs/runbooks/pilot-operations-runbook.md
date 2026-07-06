# BillSoft Pilot Operations Runbook

## Purpose

This runbook describes the operator path for a controlled BillSoft pilot.

It covers deployment prerequisites, release order, first-time restaurant setup, pilot-day operating flow, and support actions that are already supported by the repository.

Use this document together with:

- [Azure DevOps Pipelines](../devops/azure-devops-pipelines.md)
- [Local Setup](../development/local-setup.md)
- [Order to Billing Workflow](../workflows/order-to-billing.md)
- [Cashier Shift Workflow](../workflows/cashier-shift.md)
- [Daily Cash Sales Exception Report](../workflows/daily-cash-sales-exception-report.md)
- [Owner Dashboard](../workflows/owner-dashboard.md)
- [Database Migration Guidelines](../database/migration-guidelines.md)
- [Pilot Smoke Checklist](./pilot-smoke-checklist.md)
- [Pilot Deployment Smoke Checklist](./pilot-deployment-smoke-checklist.md)
- [Database Migration and Backup Runbook](./database-migration-and-backup-runbook.md)

## Scope

In scope:

- deployment sequencing
- restaurant and branch setup
- billing and receipt operations
- cashier shift operations
- daily owner review
- support actions that are already implemented

Out of scope:

- printer hardware integration
- inventory deduction
- vendor settlement
- OCR
- refund expansion
- accounting export
- new payment modes
- cashier shift expected cash mutation changes
- deployment pipeline changes

## Prerequisites

Confirm these items before the pilot starts:

| Item | What to confirm | Where it lives |
|---|---|---|
| Azure DevOps service connection | The `azureServiceConnection` parameter points to the approved Azure subscription connection. | `azure-pipelines-infra.yml`, `azure-pipelines-cd.yml` |
| Azure DevOps environment | The target `environmentName` is correct for the pilot environment. | `azure-pipelines-cd.yml` |
| SQL admin secret | `sqlAdministratorPassword` is available when Key Vault is not used, or the SQL password secret already exists in Key Vault. | `azure-pipelines-infra.yml` |
| Static Web App deployment token | `StaticWebApp--DeploymentToken` exists in Key Vault for the target environment. | `azure-pipelines-infra.yml`, `azure-pipelines-cd.yml` |
| API runtime settings | `Database__ConnectionString` and `Jwt__SigningKey` are configured for the deployed API. | `azure-pipelines-cd.yml` |
| Database connection string | The target SQL connection string is valid for the environment and points to the correct database. | Infra/CD secret flow |
| JWT signing key | The signing key is present before any login or token flow is exercised. | Infra/CD secret flow |
| Runtime host settings | CORS origin, API base URL, and Static Web App hostname are aligned with the deployed environment. | `azure-pipelines-cd.yml` |

Operational prerequisites:

- .NET 8 SDK
- pnpm 10.18.3
- approved access to the Azure subscription and resource group
- access to the pilot database backup location
- a browser for smoke verification
- an owner/admin account with `User.Manage`, `Branch.Manage`, `MenuCategory.Manage`, `MenuItem.Manage`, `Billing.Manage`, `Payment.Record`, `Payment.Cancel`, `CashShift.Manage`, `CashMovement.Record`, and `Report.View` as appropriate for the pilot

## Deployment Sequence

Follow this sequence in order:

1. Run CI.
   - Confirm the backend and frontend validation jobs pass.
   - Do not promote an unverified artifact.

2. Run infra what-if.
   - Use the infrastructure pipeline in `what-if` mode first.
   - Review the preview before applying any resource changes.

3. Run infra deploy.
   - Deploy only after the what-if output is understood and approved.
   - Ensure the target resource group is correct.

4. Configure app settings and secrets.
   - Confirm `Database__ConnectionString` is available to the API.
   - Confirm `Jwt__SigningKey` is available to the API.
   - Confirm `StaticWebApp--DeploymentToken` is available for frontend deployment.
   - Verify any environment-specific runtime settings before traffic is directed to the environment.

5. Run CD.
   - Deploy the published API artifact.
   - Run the EF Core migration step only as part of the controlled CD process for that environment.
   - Deploy the published web artifact.
   - Apply any environment-specific CORS or hostname settings required by the deployed frontend.

6. Verify deployed URLs.
   - Check the API health endpoint.
   - Open the frontend URL.
   - Confirm the login screen loads and the authenticated routes render.

## First Restaurant Setup

BillSoft currently exposes admin surfaces for branches, users, and menu management. It does not expose a standalone restaurant-create operator screen in the current app surface.

Use the environment bootstrap or seed path that created the restaurant record, then complete the pilot setup through the existing admin screens and API surfaces.

Recommended setup order:

1. Create or verify the pilot restaurant record through the approved bootstrap or seed flow.
   - Do not hand-edit the database to create the restaurant if an approved bootstrap path is available.
   - Record the restaurant code, name, timezone, and currency used for the pilot.

2. Create the first branch.
   - Use the branch admin surface at `/api/v1/admin/branches` or the matching UI.
   - Confirm the branch belongs to the pilot restaurant and is active.

3. Create the owner/admin user.
   - Use `/api/v1/admin/users`.
   - Assign the minimum roles and permissions needed for owner and support operations.

4. Create staff users.
   - Create cashier, waiter, kitchen, and support accounts as needed.
   - Assign branch scope only where the role requires it.

5. Assign roles and permissions.
   - Keep staff users limited to the actions they need.
   - Keep admin-only powers restricted to owner/support users.

6. Configure menu categories and items.
   - Use `/api/v1/admin/menu/categories`.
   - Use `/api/v1/admin/menu/items`.
   - Confirm categories and items are active before POS testing.

7. Verify master data.
   - Confirm branch scoping is correct.
   - Confirm menu item price history exists when prices change.
   - Confirm no secret values or internal setup details are exposed on customer-facing routes.

If the pilot environment was provisioned from demo seed data, use the seeded restaurant and branch as the starting point and only adjust the pilot-specific branch, user, and menu records that must differ.

## Pilot Day Operating Flow

Use this flow on the day of the pilot:

1. Staff login.
   - Sign in with the intended staff account.
   - Confirm the landing route matches the user’s role.

2. Open cashier shift.
   - Go to `/cashier/shifts`.
   - Open a shift for the active branch.
   - Confirm the shift starts with the expected opening cash.

3. Create POS order.
   - Go to `/pos/orders`.
   - Create a test dine-in or parcel order with known items.
   - Confirm the order uses the expected branch and business date.

4. Confirm order.
   - Confirm the POS order only after checking the lines and quantities.
   - Verify the kitchen ticket is created from the confirmed snapshot.

5. Check kitchen ticket visibility.
   - Open `/kitchen/tickets`.
   - Confirm the ticket appears with the expected item lines and status.

6. Generate bill.
   - Open `/billing`.
   - Create the bill from the confirmed order.
   - Confirm the bill number and bill lines are snapshot-based.

7. Record payment.
   - Record the payment against the bill.
   - Confirm the payment mode breakdown and balance update are correct.

8. Print or preview receipt.
   - Open the receipt preview card from `/billing`.
   - Confirm the receipt shows the restaurant, branch, bill number, business date, bill generated time, item lines, totals, paid amount, balance, and payment breakdown.
   - Use the print or reprint action only when the receipt is ready to be produced.

9. View daily cash sales.
   - Open `/reports/daily-cash-sales`.
   - Confirm the report reflects the bill, payment, receipt print, and any cash shift activity.

10. Close cashier shift.
    - Return to `/cashier/shifts`.
    - Close the shift using the counted cash amount.
    - Confirm variance is calculated from the recorded shift data.

11. Owner dashboard review.
    - Open `/owner/dashboard`.
    - Confirm the top-line metrics, open-shift status, and receipt reprint signals are visible.

## Support Operations

### Admin Password Reset

- Use `/api/v1/admin/users/{userId}/reset-password` or the matching admin UI.
- Reset only another user in the same restaurant.
- Confirm the password reset is audited.
- Never expose the new password after it is set.

### Cancelled Bill Handling

- Cancelled bills must remain cancelled rather than being physically removed.
- A cancelled bill should display a cancelled state.
- Do not treat a cancelled bill as a normal paid receipt.
- If the current workflow supports a cancelled-copy receipt format, use that format explicitly and label it as cancelled.

### Receipt Reprint and Audit Check

- Receipt reads come from the stored bill and payment snapshots.
- Receipt reprints are recorded through `POST /api/v1/billing/bills/{billId}/receipt/print-events`.
- Reprint actions must be auditable and must not mutate bill totals or payment totals.
- Reprint flows should not expose internal IDs or secret values on the customer receipt.

### Failed Payment Correction Policy

- If a payment was recorded with the wrong reference or the wrong supported payment mode, use the supported cancellation or correction path if one exists.
- If the workflow does not support the correction safely, stop and escalate.
- Do not edit payment rows directly in the database as an operational shortcut.

### What Not To Do Manually In The Database

- do not edit bills to change totals, tax, line items, or bill numbers
- do not edit payments to hide or rewrite payment history
- do not edit receipt print events to hide reprints
- do not edit audit logs to remove evidence
- do not change cashier shift expected cash amounts by hand
- do not hard-delete business records
- do not insert or update menu snapshots to rewrite historical receipts
- do not run migrations automatically from the CD pipeline if manual approval is required for the environment

## Exit Criteria

The pilot is ready to proceed only when:

- deployment completed in the expected environment
- restaurant, branch, users, and menu setup are complete
- login works for the intended staff roles
- billing, payment, receipt, cashier shift, report, and owner dashboard smoke checks pass
- receipt reprint audit behavior is understood
- no unsupported manual DB edits were required
