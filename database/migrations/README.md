# BillSoft Database Migrations

Database migrations should be created using the selected backend migration tool.

**Schema design:** See `docs/database/database-schema.md` (master) and `docs/database/database-tables.md` (columns).

Current recommendation:

```text
.NET + EF Core migrations or DbUp
SQL Server / Azure SQL
```

Rules:

1. Do not create destructive migrations without review.
2. Use decimal types for money.
3. Use status fields instead of hard deletes.
4. Use `BusinessDate` for restaurant operating date.
5. Add indexes for reporting and lookup paths.

See:

- `docs/database/database-tables.md`
- `docs/database/naming-conventions.md`
- `docs/database/migration-guidelines.md`
