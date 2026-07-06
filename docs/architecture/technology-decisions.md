# BillSoft Technology Decisions

## Purpose

This document records baseline technology decisions for BillSoft. Changes must be deliberate and documented.

---

# Current Recommended Stack

| Area | Decision | Rationale |
|---|---|---|
| Backend | .NET / C# | Good fit for transactional business systems |
| Database | SQL Server / Azure SQL | Good fit for relational consistency, reporting, and audit-heavy workflows |
| Frontend | React or Next.js | Suitable for POS screens, admin dashboards, and owner monitoring |
| Object Storage | Azure Blob Storage | Required for vendor bill images, PDFs, OCR JSON, and report exports |
| OCR | Azure AI Document Intelligence or equivalent OCR service | Required for English/Tamil/mixed vendor bill scanning |
| Background Jobs | Worker service or hosted jobs | Required for OCR processing, reports, alerts, and scheduled summaries |

---

# Database Principles

1. Use relational tables for money, stock, orders, billing, vendors, and audit data.
2. Use decimal types for money and precise quantities.
3. Do not use floating point types for financial values.
4. Store UTC timestamps and separate `BusinessDate`.
5. Use transactions for billing, payment, vendor confirmation, stock movement, and cash drawer closing.
6. Store uploaded files in object storage and keep metadata in SQL.

---

# Application Design Principles

1. Keep domain rules out of controllers.
2. Keep UI simple for waiter, cashier, and kitchen users.
3. Use owner/admin dashboards for complex monitoring.
4. Treat audit and traceability as core product behavior.
5. Do not implement OCR as a direct stock update mechanism.

---

# Decision Log

| Date | Decision | Status | Notes |
|---|---|---|---|
| 2026-06-10 | Use SQL Server/Azure SQL as recommended database | Proposed | Best fit for transactional and audit-heavy product |
| 2026-06-10 | Store vendor documents in object storage | Proposed | Database stores metadata/path/hash only |
| 2026-06-10 | OCR requires user confirmation before stock update | Accepted | Non-negotiable control rule |
