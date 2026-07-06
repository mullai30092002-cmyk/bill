# Database Migration Guidelines

## Purpose

This document defines how BillSoft database migrations should be created, reviewed, and applied.

BillSoft handles financial, stock, vendor, and audit data. Database changes must be deliberate and reversible where practical.

SQL Server is the production and default database provider. SQLite is limited to explicit local-development/testing use and must not become the basis for production migrations. The application must not call `Database.Migrate()` at startup.

---

# Local Development: Applying Migrations

EF Core design-time tools must target the same database as the running API. The
`appsettings.json` file intentionally has an empty connection string to prevent
accidental production credentials being checked in. This means `dotnet ef`
commands require an explicit connection string via environment variable.

**Always use this form when running EF commands locally:**

```bash
Database__ConnectionString="Server=localhost;Database=BillSoft;Trusted_Connection=True;TrustServerCertificate=True;" \
  dotnet ef database update \
  --project src/api/BillSoft.Infrastructure \
  --startup-project src/api/BillSoft.Api
```

Similarly for `migrations list`:

```bash
Database__ConnectionString="Server=localhost;Database=BillSoft;Trusted_Connection=True;TrustServerCertificate=True;" \
  dotnet ef migrations list \
  --project src/api/BillSoft.Infrastructure \
  --startup-project src/api/BillSoft.Api
```

**After applying, always verify the target database:**

```sql
SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;
```

Confirm the latest migration name appears. If it does not, the command targeted
the wrong database instance.

> **Warning:** Omitting `Database__ConnectionString` causes the EF design-time
> factory to throw an error. Previously it silently fell back to
> `(localdb)\MSSQLLocalDB`, which is a different database than the running API
> and would apply migrations to the wrong instance without any error.

---

# Migration Rules

1. Every schema change must have a migration.
2. Every migration must be reviewed before production deployment.
3. Migrations must not silently drop business data.
4. Destructive changes require a data migration plan.
5. New required columns on existing tables must include default/backfill strategy.
6. Index changes must consider reporting and daily dashboard queries.
7. Foreign keys must be explicit unless there is a documented reason not to use them.
8. Money columns must use decimal types.
9. Business records must use status transitions instead of physical deletion.
10. Stock and money ledgers must remain append-friendly and traceable.

---

# Required Migration Review Checklist

- [ ] Table/column names follow `docs/database/naming-conventions.md`.
- [ ] Foreign keys are defined where needed.
- [ ] Indexes are added for common lookup/reporting paths.
- [ ] Unique constraints are added for immutable business references.
- [ ] Money uses decimal type.
- [ ] Timestamps use UTC.
- [ ] `BusinessDate` is included for operational/reporting tables.
- [ ] Audit impact is understood.
- [ ] Seed data impact is understood.
- [ ] Rollback plan is documented where needed.

---

# Common Index Requirements

## Orders

```text
BranchId + BusinessDate
OrderNumber
Status
CreatedAt
```

## Bills

```text
BranchId + BusinessDate
BillNumber
Status
CreatedAt
```

## Payments

```text
BillId
PaymentMethodId
CollectedAt
```

## StockMovements

```text
InventoryItemId + CreatedAt
BranchId + BusinessDate
SourceType + SourceId
```

## VendorBills

```text
VendorId + BillDate
BranchId + BusinessDate
Status
BillNumber
```

## AuditLogs

```text
RestaurantId + BranchId + CreatedAt
EntityType + EntityId
UserId + CreatedAt
Action + CreatedAt
```

---

# Seed Data

Seed data should include:

- Default roles
- Default permissions
- Payment methods
- Inventory units
- Vendor categories
- Expense categories
- Kitchen stations
- Report types

Seed data must be idempotent.

---

# Migration Naming

Use descriptive migration names.

Examples:

```text
AddOrdersAndOrderItems
AddBillingAndPaymentTables
AddInventoryStockMovements
AddVendorBillOcrTables
AddCashDrawerSessions
AddAuditLogs
```

Avoid vague names:

```text
UpdateDb
FixTables
Changes1
```

---

# Rollback Guidance

Rollback must be considered for:

- Dropping columns
- Renaming columns
- Changing money/quantity precision
- Changing foreign keys
- Changing unique constraints
- Moving data between tables

For high-risk migrations, prefer expand-and-contract:

1. Add new column/table.
2. Backfill data.
3. Deploy application using new schema.
4. Verify data.
5. Remove old column/table in a later migration.
