# BillSoft Local Setup

## Prerequisites

Recommended tools:

- .NET 8 SDK
- Node.js 20.19+ or Node.js 22.12+
- pnpm 10.18.3
- SQL Server Developer Edition or Azure SQL connection
- Azure Storage Emulator/Azurite or Azure Blob Storage

Use Corepack to activate the expected pnpm version:

```bash
corepack enable
corepack prepare pnpm@10.18.3 --activate
pnpm -v
```

Do not use npm for the frontend workspace. The frontend uses `pnpm-lock.yaml` as the reproducible dependency baseline.

---

# Backend

From repository root:

```bash
dotnet restore BillSoft.sln
dotnet build BillSoft.sln
dotnet test BillSoft.sln
```

Run API:

```bash
dotnet run --project src/api/BillSoft.Api/BillSoft.Api.csproj --urls http://localhost:5000
```

The local API URL is `http://localhost:5000`. The VS Code task `backend: run api` binds the API to that port explicitly, and `app: run api and web` starts the API together with the frontend on their matching local ports.

If the API was previously started on another port, stop the stale process before using the task again. A still-running API process can also cause `dotnet build` to fail with file-lock errors on Windows.

The API project launch profile sets `ASPNETCORE_ENVIRONMENT=Development`, so `dotnet run` loads `src/api/BillSoft.Api/appsettings.Development.json` by default during local development.

JWT token foundation:

- `Jwt__SigningKey` must be set before any code path creates access tokens.
- `src/api/BillSoft.Api/appsettings.Development.json` contains a fake local-only signing key for development.
- Production must override the JWT signing key through deployment configuration or environment variables.

Auth slice:

- `POST /api/v1/auth/login` issues access and refresh tokens.
- `POST /api/v1/auth/refresh` rotates the refresh token.
- `POST /api/v1/auth/logout` revokes the refresh token if present.
- `GET /api/v1/auth/me` requires a Bearer access token.
- Login uses `RestaurantCode + MobileNumber + Password`; there is no PIN login yet.
- Login normalizes the submitted mobile number using the restaurant country profile and looks up the canonical `MobileE164` value.

Health endpoint:

```text
GET /health
```

Database foundation:

- Database provider: SQL Server / Azure SQL by default
- SQLite is local-development/testing only and must be enabled explicitly with `Database__Provider=Sqlite` plus a matching `Database__ConnectionString`
- `src/api/BillSoft.Api/appsettings.json` stays environment-neutral and must not carry a machine-specific connection string
- Local SQL Server values belong in `src/api/BillSoft.Api/appsettings.Development.json` or environment variables such as `Database__ConnectionString`
- If you want local SQLite, set the provider and connection string explicitly in your local environment instead of changing the production default
- EF Core foundation migrations already exist
- Current migration command for future schema changes:

```bash
dotnet ef migrations add <MigrationName> --project src/api/BillSoft.Infrastructure --startup-project src/api/BillSoft.Api
```

- For SQL Server migrations, keep `Database__Provider=SqlServer` and point `Database__ConnectionString` at the SQL Server instance you want to target.
- The design-time factory falls back to the SQL Server localdb connection string when no explicit database configuration is supplied.
- Do not auto-migrate on app startup
- The design-time factory falls back to SQL Server unless `Database__Provider` and `Database__ConnectionString` are supplied explicitly
- `appsettings.Development.json` should not be used to switch migration generation onto SQLite
- Production and staging database values must come from deployment configuration or environment variables, not from `appsettings.json`

Foundation seed command:

```bash
dotnet run --project src/api/BillSoft.Api/BillSoft.Api.csproj -- --seed-foundation
```

Demo login seed command:

```bash
dotnet run --project src/api/BillSoft.Api/BillSoft.Api.csproj -- --seed-demo-login
```

Optional startup config flags:

- `Setup:RunFoundationSeed=false` by default
- `Setup:ExitAfterSeed=false` by default

You can override these in `appsettings.Development.json` or environment variables such as `Setup__RunFoundationSeed=true`.

The seed command:

- runs only when explicitly requested
- does not call `Database.Migrate()`
- uses a scoped `IFoundationSeedService`
- exits after seeding when `--seed-foundation` is used or `Setup:ExitAfterSeed=true`
- is intended for local/manual setup until a proper authenticated admin flow exists

The demo login seed command:

- runs only when explicitly requested
- seeds the foundation roles/permissions first if they are missing
- creates or repairs a deterministic local demo restaurant, branch, owner login, and demo menu catalog
- exits after seeding
- is local-development only
- If you already have a local database from an earlier run, rerun `backend: seed demo login` after pulling the latest code so the demo menu catalog is repaired in place.

Demo login credentials:

- Restaurant code: `DEMO`
- Mobile number: `90000001`
- Password: `DemoOwner123!`
- Role: `RestaurantOwner`
- Demo owner mobile is stored canonically as `+6590000001`.

Demo login account:

- Full name: `Demo Owner`
- Email: `owner@demo.billsoft.local`
- Branch: `Main Branch`
- Timezone: `Asia/Singapore`
- Currency: `SGD`
- Demo menu catalog: seeded automatically for the `DEMO` restaurant so POS orders can be tested immediately after login
- Demo restaurant, branch, and owner seed data are added only by the explicit demo seed command; startup does not auto-seed production data.

JWT environment example:

```bash
Jwt__Issuer=BillSoft
Jwt__Audience=BillSoft
Jwt__SigningKey=replace-with-secure-secret
Jwt__AccessTokenLifetimeMinutes=15
Jwt__RefreshTokenLifetimeDays=7
```

---

# Frontend

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
- `pnpm run test:watch` runs Vitest in watch mode for iterative work.
- The frontend boundary test protects `src/web/src/features/*` and `src/web/src/App.tsx` from direct vendor UI imports.

Frontend auth shell:

- Set `VITE_BILLSOFT_API_BASE_URL=http://localhost:5000` in `.env` for local API access.
- The API enables CORS for `http://localhost:3010` through `http://localhost:3013` in development so the browser can complete login preflight requests even if Vite falls back to a higher port.
- The login route is `/login`.
- Phase 1 stores the auth session in `localStorage`; this is temporary and should be replaced with a safer session model later.
- `/admin/users` requires `User.Manage` in the frontend shell, but backend authorization remains the source of truth.
- `/admin/users` now includes create user, edit profile, replace roles, activate/deactivate actions, and optional branch assignment.
- Branch choices come from `GET /api/v1/admin/branches`; do not hardcode branch options in the frontend.

Default local web URL:

```text
http://localhost:3010
```

Frontend and backend local URLs:

```text
API:  http://localhost:5000
Web:  http://localhost:3010
```

---

# Environment

Copy `.env.example` for local development values.

```bash
cp .env.example .env
```

The example file includes .NET-compatible configuration names such as `Database__ConnectionString`, which maps to `Database:ConnectionString` in the application configuration system.

Do not commit real local values.

---

# VS Code Tasks

Available tasks:

```text
backend: restore
backend: build
backend: test
backend: run api
backend: database update
backend: seed foundation
backend: seed demo login
backend: setup local dev data
web: install
web: typecheck
web: build
web: run dev
app: build all
app: run api and web
app: setup seed and run
```

Use this to start both services:

```text
Terminal -> Run Task -> app: run api and web
```

Use this to set up local data and run everything:

```text
Terminal -> Run Task -> app: setup seed and run
```

The backend API launch, migration, and seed tasks use the repo's default local SQL Server connection string from the checked-in development configuration, so they target the intended local SQL Server instance without prompting.

---

# Current Bootstrap Scope

This repository currently contains:

- Product requirements
- Database documentation
- Architecture docs
- Workflow docs
- Permission matrix
- .NET API skeleton
- React/Vite frontend skeleton
- Test project skeleton
- Seed/migration placeholders

Business features are not implemented yet.
