# Patterns Adopted from Meridian

## Purpose

This document records the architecture patterns BillSoft adopts from Meridian-style implementations and what BillSoft deliberately does not copy.

BillSoft uses these patterns only when they fit the restaurant billing and branch-control domain. The goal is reuse, safety, and auditability, not mechanical duplication.

## Adopted Patterns

- Secure by default configuration and access control.
- Least-privilege design for staff, branch, and owner/admin workflows.
- Reuse existing capabilities first before adding new routes, services, or entities.
- Keep sensitive changes auditable, especially billing, payment, inventory, vendor, and permission changes.
- Prefer small, reviewable changes over broad refactors.
- Keep boundaries explicit between UI, API, application, domain, and infrastructure layers.

## Intentionally Not Copied

- Meridian-specific permission names, route names, entity names, or feature assumptions.
- Generic tenant terminology when BillSoft needs restaurant and branch terminology.
- Any pattern that weakens BillSoft auditability, traceability, or branch safety.
- Any shortcut that bypasses existing approval, authorization, or confirmation flows.

## BillSoft Naming Differences

BillSoft uses restaurant-domain terms instead of generic multi-tenant terms:

- `Tenant` becomes `Restaurant` or `Branch`.
- `Tenant-scoped` becomes `restaurant-scoped` or `branch-scoped`.
- `Tenant admin` becomes `restaurant owner`, `branch manager`, or the closest BillSoft role that already exists.

When new terminology is introduced, it should match the real BillSoft operating model instead of copying external platform language.
