# BillSoft

BillSoft is a restaurant billing, kitchen display, inventory, vendor bill OCR, cash-control, and leakage-prevention system.

The goal is not just to create a POS billing app. The goal is to help restaurant owners control:

- Orders created
- Items sent to the kitchen
- Menu categories and menu items configured
- Bills generated from confirmed orders
- Payments recorded against bills
- Stock purchased and used
- Vendor bills and settlements
- Money-in and money-out
- Staff actions and suspicious activity

## Documentation

### Product and Requirements

- [Product Requirements](docs/requirements/product-requirements.md)
- [Permission Matrix](docs/requirements/permission-matrix.md)

### Architecture

- [Technology Decisions](docs/architecture/technology-decisions.md)
- [Status Transitions](docs/architecture/status-transitions.md)

### Database

- [Database Schema](docs/database/database-schema.md) — master schema, requirement mapping, ER model
- [Database Tables](docs/database/database-tables.md) — column-level reference
- [Database Naming Conventions](docs/database/naming-conventions.md)
- [Database Migration Guidelines](docs/database/migration-guidelines.md)

### Workflows

- [Vendor Bill OCR Workflow](docs/workflows/vendor-bill-ocr.md)
- [Order to Billing Workflow](docs/workflows/order-to-billing.md)

### Development

- [Local Setup](docs/development/local-setup.md)
- [Pilot Runbook](docs/runbooks/pilot-runbook.md)
- [GitHub Actions CI](docs/devops/github-actions-ci.md)
- [GitHub Actions CD](docs/devops/github-actions-cd.md)
- [Pilot RC 001](docs/releases/pilot-rc-001.md)

### Codex

- [Codex Instructions](AGENTS.md)

## Repository Setup

The repository contains the product, database, permission, architecture, workflow, and Codex guardrail documentation required before code generation starts.

The initial application skeleton is now present:

```text
src/api/BillSoft.Api
src/api/BillSoft.Application
src/api/BillSoft.Domain
src/api/BillSoft.Infrastructure
tests/BillSoft.Tests
src/web
database/migrations
database/seed
```

## Backend

Recommended commands:

```bash
dotnet restore BillSoft.sln
dotnet build BillSoft.sln
dotnet test BillSoft.sln
dotnet run --project src/api/BillSoft.Api/BillSoft.Api.csproj --urls http://localhost:5000
```

Current foundation status:

- Restaurants and branches now carry explicit country, currency, and timezone metadata.
- User mobile login is normalized to canonical E.164 values with Singapore and India profiles in place.
- The rollout remains multi-country-ready while still supporting one-country-at-a-time execution.

Health endpoint:

```text
GET /health
```

## Frontend

The frontend uses pnpm only. Do not use npm in `src/web`.

Activate the expected pnpm version:

```bash
corepack enable
corepack prepare pnpm@10.18.3 --activate
```

From `src/web`:

```bash
pnpm install
pnpm run test
pnpm run typecheck
pnpm run build
pnpm run dev
```

Frontend test foundation:

- `pnpm run test` runs the Vitest suite once.
- `pnpm run test:watch` keeps Vitest running in watch mode.
- The import-boundary test guards feature and app code from direct vendor UI imports.

Frontend auth shell:

- `VITE_BILLSOFT_API_BASE_URL` points the web app at the backend API during local development.
- Local API URL: `http://localhost:5000`
- Local web URL: `http://localhost:3010`
- `backend: run api` starts the API explicitly on port `5000`
- `web: run dev` starts the web dev server on port `3010`
- `app: run api and web` starts both services with matching local URLs
- `backend: database update` applies local EF migrations
- `backend: seed foundation` seeds roles and permissions
- `backend: seed demo login` creates the local demo restaurant owner login and demo menu catalog
- `backend: setup local dev data` runs the local migration and seed sequence
- If your local database already exists from an earlier run, rerun `backend: seed demo login` after pulling the latest code so the demo menu catalog is repaired in place.
- `app: setup seed and run` prepares local data and then starts both services
- Phase 1 stores the auth session in `localStorage` so protected routes can work before a safer cookie/session model exists.
- `/login` is the public entry point; direct login uses a deterministic role/permission landing only when no preserved protected return path exists, and `/` remains the explicit dashboard shell.
- `/owner/dashboard` is the explicit owner/admin landing route for `Report.View`; it does not replace `/` and it does not change the backend/session shape.
- `/admin/users` is the protected admin shell for create/edit/role/status actions, optional branch assignment, and admin-only staff password reset/reissue; it still requires `User.Manage`.
- Branch choices in `/admin/users` come from `GET /api/v1/admin/branches`; do not hardcode branch options in the frontend.

Demo login for local development:

- Restaurant code: `DEMO`
- Email: `owner@demo.billsoft.local`
- Mobile number: `9123456789`
- Password: `DemoOwner123!`
- Role: `RestaurantOwner`
- Demo menu categories and items are seeded automatically for `DEMO` so the POS order screen can be exercised without manual menu setup.

Vendor OCR demo login for local QA:

- Restaurant code: `DEMO`
- Email: `inventory@demo.billsoft.local`
- Mobile number: `9000000002`
- Password: `DemoInventory123!`
- Role: `InventoryUser`
- This account includes the vendor bill upload and confirm permissions needed for OCR browser QA.

## Pending Setup

- Real database migrations
- Seed data implementation
- Authentication and authorization implementation
- CI workflow
- Deployment workflow
- Business feature implementation

## Core Principles

1. No hard delete for business records.
2. Every money movement must be traceable.
3. Every stock movement must be traceable.
4. Vendor bill OCR must require user confirmation before inventory update.
5. Manual overrides must be audited with reason.
6. Bill numbers must be immutable after creation.
7. Cash drawer differences must be visible to owner/admin users.
