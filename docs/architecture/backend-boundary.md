# Backend Boundary

## Purpose

This document defines the backend layering rules for BillSoft.

## Rules

- Domain code must stay persistence-ignorant.
- Infrastructure owns EF Core, database configuration, and persistence concerns.
- Application owns use cases, orchestration, and business workflows.
- API owns endpoints only and should not contain domain rules.
- Sensitive actions must require authorization and create an audit trail.

## Layer Responsibilities

### Domain

- Holds business concepts and rules.
- Does not know about EF Core, HTTP, configuration, or infrastructure concerns.

### Application

- Coordinates use cases.
- Enforces application-level workflows.
- Calls into domain logic and abstractions without binding to EF Core details.

### Infrastructure

- Implements persistence with EF Core.
- Provides database configuration and external system integrations.
- Contains the concrete `DbContext` and related persistence setup.

### API

- Exposes endpoints and translates HTTP requests and responses.
- Delegates work to application services or infrastructure registration.
- Does not own domain rules.

## Sensitive Actions

Any action that changes money, stock, vendor settlement state, permissions, or branch access must have:

- Authorization checks appropriate to the role.
- An auditable record of the action.
- Clear status transitions instead of hard deletes.

## Outcome

These boundaries keep BillSoft easier to reason about and reduce the risk of leaking persistence concerns into business logic.
