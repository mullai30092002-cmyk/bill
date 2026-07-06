# BillSoft Database Migration and Backup Runbook

## Purpose

This runbook describes the controlled process for backing up, applying, verifying, and rolling back BillSoft database migrations in a pilot environment.

It follows the repository rule that BillSoft must not auto-migrate the database at application startup.

Use this document together with:

- [Database Migration Guidelines](../database/migration-guidelines.md)
- [Azure DevOps Pipelines](../devops/azure-devops-pipelines.md)
- [Pilot Operations Runbook](./pilot-operations-runbook.md)

## Before Migration

1. Identify the target environment.
   - Confirm the Azure environment name.
   - Confirm the resource group, SQL server, and database name.
   - Confirm whether the environment is pilot, stage, or production.

2. Confirm the latest commit.
   - Record the deployed commit SHA.
   - Compare it with the commit that introduced the migration.
   - Ensure the migration is being applied to the intended release.

3. Confirm pending migration names.
   - Review the EF Core migration list in `src/api/BillSoft.Infrastructure/Migrations`.
   - Run the controlled list command, passing the target connection string explicitly:

```bash
Database__ConnectionString="<target connection string>" \
  dotnet ef migrations list \
  --project src/api/BillSoft.Infrastructure \
  --startup-project src/api/BillSoft.Api
```

   - Confirm the next migration name matches the intended schema change.
   - Confirm the output shows `(Pending)` only for migrations not yet applied to this specific database instance.

4. Take a database backup or export.
   - Use the approved Azure SQL backup, export, or snapshot method for the target environment.
   - If the ops image supports `sqlpackage`, export to a known backup file path.
   - Do not skip the backup step even for small schema changes.
   - If the CD pipeline will apply the migration, confirm the backup or export exists before the pipeline starts the EF migration step.

5. Record the backup location.
   - Capture the storage account, container, file name, or recovery point.
   - Record the backup timestamp and operator.

6. Verify the backup exists.
   - Confirm the file or recovery point is present.
   - Confirm the backup is readable or restorable before proceeding.

## Migration Application

1. Obtain manual approval.
   - Migration application must be explicitly approved by the release owner or environment owner.
   - Do not start the migration from an unattended CD job if manual approval is required.

2. Use a controlled command or approved deployment shell.
   - Run the migration from a known operator workstation or release shell.
   - Load the correct environment configuration before execution.

3. Apply the EF Core migration manually.

```bash
Database__ConnectionString="<target connection string>" \
  dotnet ef database update \
  --project src/api/BillSoft.Infrastructure \
  --startup-project src/api/BillSoft.Api
```

   - Always pass `Database__ConnectionString` explicitly. The design-time factory
     will throw if it is not provided — this is intentional to prevent silent
     targeting of the wrong database instance.
   - If the CD pipeline applies the migration, it must pass `Database__ConnectionString`
     only as a secure environment variable and must not log the value.
   - If a specific target migration must be used, pass the intended migration name explicitly.
   - Use the environment’s approved connection string source.
   - Do not add application startup migration logic to force this step.

4. Confirm the migration completed cleanly.
   - Capture console output.
   - Record any warnings.
   - Stop immediately if the migration fails or if the schema is only partially updated.

5. Verify `__EFMigrationsHistory` on the target database.

```sql
SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;
```

   - Confirm the latest migration name appears in the history table.
   - If the migration name is absent, the command targeted the wrong database instance.
   - Do not proceed to smoke testing until the history table confirms the migration.

6. If CD applies migrations, keep the backup-first gate explicit.
   - Do not let the CD migration step run until the backup or export has been confirmed.
   - Do not log the connection string value in pipeline output or scripts.
   - Keep the migration target explicit and avoid adding any startup-time migration logic.

## Post-Migration Verification

After the migration completes:

1. Check schema presence.
   - Verify the expected tables, columns, indexes, and constraints exist.
   - Confirm the schema matches the migration intent.

2. Run smoke endpoints.
   - Check `GET /health`.
   - Check the login flow.
   - Check the billing receipt endpoint if the migration touched billing data.

3. Verify login.
   - Sign in with a known account.
   - Confirm token issuance works.
   - Confirm the session is scoped to the correct restaurant.

4. Verify billing and report pages.
   - Open `/billing`.
   - Open `/reports/daily-cash-sales`.
   - Open `/owner/dashboard`.
   - Confirm the pages load and the migrated data renders correctly.

5. Verify data integrity.
   - Confirm historical bills, payments, shifts, and receipt print events still load.
   - Confirm no totals were rewritten during the schema update.

## Rollback Plan

If the migration causes a failure:

1. Stop further writes.
   - Do not continue with additional deployments or schema changes.
   - Preserve the failing console output and error message.

2. Restore the backup.
   - Restore the database from the backup or recovery point taken before the migration.
   - Verify the restored database can be opened and queried.

3. Redeploy the previous app version if needed.
   - Roll the app back to the last known good build artifact.
   - Confirm the app version and database state are compatible after restore.

4. Record incident notes.
   - Capture the migration name, backup location, restore timestamp, operator, and user-visible impact.
   - Document whether the failure was schema-related, data-related, or deployment-related.

5. Reassess before retrying.
   - Do not retry the same migration until the root cause is understood.
   - If the schema change is high risk, prefer an expand-and-contract approach in a later release.

## Operator Notes

- Keep this process manual and auditable.
- Never assume the CD pipeline has applied a migration unless the release notes and the database state both confirm it.
- Do not use direct SQL edits as a substitute for a missing application migration.
- Do not delete business records to recover from a bad deploy.
