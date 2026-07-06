# BillSoft Codex Instructions

## Project Context

BillSoft is a restaurant billing, kitchen display, inventory, vendor bill OCR, cash-control, and leakage-prevention system.

The product goal is not only billing. The core goal is owner control over orders, kitchen preparation, billing, payment collection, vendor purchases, inventory usage, money-in, money-out, and audit evidence.

## Default Engineering Approach

For every task:

1. Read this `AGENTS.md` file first.
2. Inspect relevant docs under `docs/` before editing code.
3. Keep changes scoped to the requested task.
4. Prefer small, reviewable commits.
5. Do not introduce unrelated refactors.
6. Update documentation when behavior, schema, workflow, or architecture changes.
7. Preserve auditability for billing, payment, inventory, vendor, OCR, and cash-control flows.

## Meridian-Inspired Guardrails

Apply these rules when adopting ideas from Meridian or when building new BillSoft functionality:

1. Secure by default.
2. Restaurant/branch-safe by default.
3. Use existing capability first before adding new abstractions.
4. Do not invent permissions, routes, entities, or workflow states that are not already supported by BillSoft.
5. Keep changes small, reviewable, and easy to audit.
6. Audit sensitive actions, especially anything that affects money, stock, vendors, permissions, or branch access.

## Product Non-Negotiables

1. No hard delete for business records such as orders, bills, payments, vendor bills, stock movements, expenses, or audit logs.
2. Use status transitions such as `Cancelled`, `Voided`, `Rejected`, or `Inactive`.
3. Every financial change must be traceable.
4. Every stock change must be represented by a stock movement record.
5. OCR-scanned vendor bill data must not update inventory without user confirmation.
6. Manual override of OCR values must require a reason and must be audited.
7. Uploaded vendor bills must be stored in secure object storage, with only metadata, path, and hash stored in the database.
8. Bill numbers must be immutable after issue.
9. Price changes must be recorded in price history.
10. Cash drawer differences must be visible to owner/admin users.

## Recommended Repository Structure

Use this structure unless the project stack later requires adjustment:

```text
billsoft/
├── docs/
│   ├── requirements/
│   ├── database/
│   ├── architecture/
│   ├── workflows/
│   └── codex/
├── src/
│   ├── api/
│   ├── web/
│   ├── worker/
│   └── shared/
├── database/
│   ├── migrations/
│   ├── seed/
│   ├── scripts/
│   └── diagrams/
└── README.md
```

## Backend Rules

When backend code is added:

- Prefer .NET/C# if no stack decision has replaced it.
- Prefer SQL Server or Azure SQL for relational data.
- Keep domain rules out of controllers.
- Use database transactions for multi-step financial or stock updates.
- Add authorization checks for owner/admin/staff role boundaries.
- Validate all money and quantity inputs server-side.

Critical transactional flows:

- Confirm customer bill and collect payment.
- Confirm vendor bill and update inventory.
- Reverse or refund payment.
- Apply discount.
- Cancel order item after kitchen submission.
- Close cash drawer session.

## Frontend Rules

When frontend code is added:

- Design for low-education/basic-technology users.
- Use large touch-friendly controls for order and kitchen screens.
- Minimize typing.
- Prefer clear action labels over icons-only interactions.
- Always distinguish parcel/takeaway and eat-in flows.
- Show warnings for unpaid orders, cancellations, cash differences, low stock, and duplicate vendor bills.
- Owner/admin screens may use tables and filters; staff screens should be simplified.

## Database Rules

Before changing schema, review:

- `docs/database/database-tables.md`

Database design must prioritize traceability, auditability, ledger-style movement records, immutable business references, clear status transitions, and business date support.

## OCR and Vendor Bill Rules

Vendor bill OCR must follow this flow:

```text
Upload bill document
  → Store original document
  → Run OCR for English/Tamil/mixed text
  → Store raw OCR result
  → Match items to inventory aliases
  → Show review screen
  → User confirms or overrides values
  → Audit overrides with reason
  → Confirm vendor bill
  → Create stock movements
  → Update vendor payable/payment status
```

Do not bypass the confirmation step.

## Testing Expectations

When tests exist, run the smallest relevant test set first.

Preferred order:

1. Unit tests for changed domain/service logic.
2. API tests for affected endpoints.
3. Frontend component tests for affected screens.
4. End-to-end tests for billing, kitchen, cash drawer, vendor bill, and inventory workflows.

If tests cannot be run, state exactly why in the final response.

## Documentation Expectations

Update docs when adding or changing database tables, API contracts, user roles, billing workflow, kitchen workflow, inventory workflow, vendor bill OCR workflow, cash drawer/reporting workflow, or audit behavior.

## Codex Output Format

For implementation tasks, Codex should report:

1. Summary
2. Files changed
3. Tests run
4. Risks or assumptions
5. Follow-up work, if any
