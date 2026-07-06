# BillSoft Database Documentation

This folder contains the authoritative database design for BillSoft.

## Documents

| Document | Purpose | Audience |
|----------|---------|----------|
| [database-schema.md](database-schema.md) | **Master schema** — domains, ER model, requirement mapping, constraints, implementation order | Product, backend, DBA |
| [database-tables.md](database-tables.md) | Column-level reference for every table | Developers implementing entities/migrations |
| [naming-conventions.md](naming-conventions.md) | Naming standards for tables, keys, indexes | Developers |
| [migration-guidelines.md](migration-guidelines.md) | Rules for creating and reviewing migrations | Developers, reviewers |

## Current Status

| Item | Status |
|------|--------|
| Schema design (54 MVP tables) | **Complete** — see `database-schema.md` |
| Column reference | **Complete** — see `database-tables.md` |
| EF Core entities | Not started |
| SQL Server migrations | Not started |
| Seed data scripts | Not started |

## Recommended Reading Order

1. `database-schema.md` — understand the full model and how requirements map to tables
2. `database-tables.md` — implement column definitions
3. `naming-conventions.md` + `migration-guidelines.md` — before first migration

## Related Docs

- [Product Requirements](../requirements/product-requirements.md)
- [Permission Matrix](../requirements/permission-matrix.md)
- [Status Transitions](../architecture/status-transitions.md)
- [Order to Billing Workflow](../workflows/order-to-billing.md)
- [Vendor Stock-In and Settlement Workflow](../workflows/vendor-stock-in-settlement.md)
- [Vendor Bill OCR Draft Workflow](../workflows/vendor-bill-ocr.md)
