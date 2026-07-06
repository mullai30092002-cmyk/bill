# Azure DevOps Pipelines

## Overview

BillSoft now uses three Azure DevOps pipelines:

- [`azure-pipelines-ci.yml`](../../azure-pipelines-ci.yml) for build and test validation
- [`azure-pipelines-infra.yml`](../../azure-pipelines-infra.yml) for infrastructure provisioning with Bicep
- [`azure-pipelines-cd.yml`](../../azure-pipelines-cd.yml) for application deployment

CI produces build artifacts for later use, infra provisions Azure resources only when manually requested, and CD deploys those artifacts without rebuilding or provisioning infrastructure.

## CI Pipeline

### Purpose

The CI pipeline validates the repository only. It:

- restores and validates the .NET backend
- installs, tests, typechecks, and builds the frontend
- publishes build artifacts that can be reused later by a separate CD pipeline
- publishes backend .NET TRX test results
- publishes frontend Vitest JUnit test results

It does not:

- provision infrastructure
- deploy to Azure
- create or consume Azure secrets
- run any release or environment promotion flow

### Triggers

The CI pipeline is configured to run on:

- `main`
- `master`
- pull requests targeting `master`

It is also path-filtered so pipeline, docs, infra, and other non-source changes do not trigger CI. The source scope is limited to the solution, backend source, frontend source, and tests.

### Jobs

#### Backend

Runs on `ubuntu-latest` and uses the .NET 8 SDK.

Steps:

1. cache NuGet packages
2. `dotnet restore BillSoft.sln`
3. `dotnet build BillSoft.sln -c Release --no-restore`
4. `dotnet test BillSoft.sln -c Release --no-build --no-restore --logger "trx" --results-directory "$(Agent.TempDirectory)/TestResults/backend"`
5. publish backend TRX test results from `$(Agent.TempDirectory)/TestResults/backend`
6. `dotnet publish src/api/BillSoft.Api/BillSoft.Api.csproj -c Release --no-restore`
7. publish the `billsoft-api` pipeline artifact

#### Frontend

Runs on `ubuntu-latest` and uses Node.js 22.x with Corepack-managed pnpm 10.18.3.

Steps:

1. cache the pnpm store
2. `pnpm install --frozen-lockfile`
3. `pnpm run test:ci`
4. publish Vitest JUnit results from `src/web/test-results`
5. `pnpm run typecheck`
6. `pnpm run build`
7. publish the `billsoft-web` pipeline artifact

### Required Variables and Service Connections

None from Azure. The pipeline uses the repo-local .NET and pnpm toolchain only.

### Artifacts

The pipeline produces these artifacts:

- `billsoft-api`
- `billsoft-web`
- backend `.trx` test result files under the Azure DevOps test run history
- frontend Vitest JUnit XML under the Azure DevOps test run history

These are build outputs only. They are not deployed by this pipeline.

### Validation Commands

The CI pipeline mirrors these repository commands:

- `dotnet restore BillSoft.sln`
- `dotnet build BillSoft.sln`
- `dotnet test BillSoft.sln` with TRX output
- `pnpm run test:ci` from `src/web`
- `pnpm run typecheck` from `src/web`
- `pnpm run build` from `src/web`

The frontend build script now also verifies that `dist/staticwebapp.config.json` is present before the artifact is published.

### Notes

- The frontend workspace uses pnpm only.
- The CI pipeline activates the pnpm version declared in `src/web/package.json`.
- The backend targets `net8.0`.

## Infrastructure Pipeline

### Purpose

The infrastructure pipeline provisions the initial Azure foundation for BillSoft using Bicep. It is manual by default and supports previewing changes with what-if before deployment.

### Pipeline File

- [`azure-pipelines-infra.yml`](../../azure-pipelines-infra.yml)

### Triggers

The infrastructure pipeline is configured for manual queueing only.

### Stages

#### Validate

Runs `az bicep build` against `infra/main.bicep` and then runs `az deployment group what-if` against the target resource group.

#### Deploy

Runs only when the `deploymentAction` parameter is set to `deploy`. It repeats the Bicep build, ensures the target resource group exists, and then runs `az deployment group create`.

### Required Variables and Service Connections

The pipeline needs these Azure DevOps values:

- `azureServiceConnection` as a queue-time parameter for the approved Azure service connection used by `AzureCLI@2`
- when `enableKeyVault=true`, the Azure service connection must be able to manage the target Key Vault so the deploy stage can grant its own service principal secret access policy before seeding secrets
- `sqlAdministratorPassword` as a secret variable only when `enableKeyVault=false`

The SQL administrator login name is a queue-time parameter:

- `sqlAdministratorLogin`

### Queue-Time Parameters

The pipeline exposes these queue-time parameters:

- `deploymentAction` (`what-if` or `deploy`)
- `environmentName` (`dev`, `stage`, or `prod`)
- `location`
- `resourcePrefix`
- `resourceGroupName`
- `sqlDatabaseName`
- `appServiceSkuName`
- `appServiceSkuTier`
- `sqlDatabaseSkuName`
- `sqlDatabaseTier`
- `sqlDatabaseCapacity`
- `staticWebAppSkuName`
- `staticWebAppLocation`
- `enableKeyVault`
- `enableMonitoring`

### Expected Resources

The Bicep foundation provisions:

- an Azure App Service Free plan for the API
- an Azure App Service for the API with system-assigned managed identity
- an Azure SQL Server
- an Azure SQL Database
- an Azure Static Web App Free plan for the frontend

Key Vault and monitoring are optional and disabled by default in the low-cost profile.

The API managed identity is kept because it has no direct hosting cost.

### What-If Behavior

The validate stage previews the deployment before any resource changes are made by Bicep. The pipeline still creates or updates the target resource group name before the what-if call so the deployment scope is always explicit.

### Deployment Behavior

Deployment uses `az deployment group create` against the target resource group when explicitly selected with `deploymentAction=deploy`.

The validate/what-if stage uses a fixed placeholder password because the SQL administrator password does not affect the resource graph. This keeps preview runs from depending on a secret that only deploy needs.

When `enableKeyVault=true`, the deploy stage also seeds the Key Vault with the CD secrets BillSoft needs:

- `Sql--AdministratorPassword`
- `Database--ConnectionString`
- `Jwt--SigningKey`
- `StaticWebApp--DeploymentToken`

`Database--ConnectionString` is refreshed from the deployed SQL server details on each deploy. `Sql--AdministratorPassword` is bootstrapped from the provided secret variable when present, or generated if the variable is missing and Key Vault is enabled. `Jwt--SigningKey` is created once and reused on later deploys if it already exists. `StaticWebApp--DeploymentToken` is resolved from the deployed Static Web App and stored for the CD pipeline.

If you want an environment to be CD-ready, leave `enableKeyVault` enabled for that deploy. The low-cost profile still keeps it disabled by default for environments that do not need CD.

The vault is created in access-policy mode. The deploy stage grants the Azure DevOps service principal `get`, `list`, and `set` permissions on the vault before writing any secrets, so the pipeline does not need Azure RBAC role assignment permissions.

### Intentionally Not Included

The infra pipeline does not:

- deploy application code
- run database migrations
- create SQL secrets in the repo
- add CD or environment promotion
- add private endpoints, networking hubs, or other advanced infra
- add application configuration secrets
- auto-rotate the JWT signing key after it has been created

### Example Parameter File

- [`infra/parameters/dev.bicepparam.example`](../../infra/parameters/dev.bicepparam.example)

This file is a starting point for local or manual deployments. When Key Vault is enabled, the infra pipeline can bootstrap the SQL password and persist it in Key Vault; otherwise the SQL password must come from a secure variable or equivalent secret-handling process.

## CD Pipeline

### Purpose

The CD pipeline deploys the already-built BillSoft application artifacts to Azure. It consumes the CI pipeline outputs and does not rebuild the frontend or backend.

### Pipeline File

- [`azure-pipelines-cd.yml`](../../azure-pipelines-cd.yml)

### Triggers

The CD pipeline is manual only.

### Deployment Sequence

The pipeline performs these steps in order:

1. download the `billsoft-api` artifact from the BillSoft CI pipeline
2. deploy the API artifact to Azure App Service
3. run the EF Core database migrations against Azure SQL
4. optionally seed foundation and demo data from the published API artifact when `seedDemoData=true`
5. download the `billsoft-web` artifact from the BillSoft CI pipeline
6. deploy the frontend artifact to Azure Static Web Apps
7. bind the dev or stage frontend custom domain when enabled
8. resolve the Static Web App hostname from Azure
9. write `runtime-config.js` into the published frontend artifact with the deployed API base URL
10. configure the API CORS origin for the deployed frontend URL
11. smoke test the API `/health` endpoint
12. smoke test the deployed frontend URL

### Required Variables and Service Connections

The CD pipeline needs these Azure DevOps values:

- `azureServiceConnection` for the approved Azure service connection used by `AzureWebApp@1` and `AzureCLI@2`
- the Azure service connection must be able to read Key Vault secrets in the target resource group
- `Database__ConnectionString` as a secret variable for the EF migration task environment
- `Jwt__SigningKey` and `StaticWebApp--DeploymentToken` are still required through the Key Vault-backed secret flow
- the API App Service, Static Web App, and Key Vault resource names must still be discoverable from the tagged resource group

The only queue-time environment selector is `environmentName` (`dev`, `stage`, or `prod`). The pipeline maps that to `rg-billsoft-dev`, `rg-billsoft-stage`, or `rg-billsoft-prod` and then discovers the API App Service, Static Web App, and Key Vault names from the tagged resources in that group. If a tag lookup fails, it falls back to the only resource of that type in the resource group.

The optional `seedDemoData` queue-time parameter runs the published API artifact with `--seed-demo-login` after migrations. That seed path also refreshes the foundation roles and permissions, so one deploy-time toggle can repopulate the demo restaurant, branch, owner login, and menu catalog for environments that intentionally carry demo data.

### Artifact Dependency

The CD pipeline depends on the CI pipeline artifacts:

- `billsoft-api`
- `billsoft-web`

The API deployment uses the published API output directly. The frontend deployment uses the CI-produced `src/web/dist` contents directly. The CD pipeline does not run `dotnet build`, `dotnet test`, `pnpm test`, `pnpm run typecheck`, or `pnpm run build`.

### Custom Domain Pattern

BillSoft mirrors the Meridian pattern for custom domains:

- the frontend custom domain, when enabled for the dev or stage environment, uses the approved BillSoft custom domain configured for that environment
- the API custom domain is left blank by default
- the pipeline binds the Static Web App hostname when the frontend custom domain is configured
- the pipeline writes `runtime-config.js` into the published SPA so the deployed frontend points at the Azure API host instead of the local-development fallback
- the API CORS origin is updated to allow the custom frontend domain and the deployed default hostname
- smoke tests prefer the custom frontend URL only after the hostname binding exists
- if the custom frontend certificate is still propagating, the smoke test falls back to the default hostname instead of failing the deployment

If the DNS CNAME is not yet propagated, the pipeline logs a warning and continues using the default Static Web App hostname until the binding is active.

### Key Vault Secret Flow

The CD pipeline loads these secrets from the environment Key Vault before deployment:

- `Database--ConnectionString`
- `Jwt--SigningKey`
- `StaticWebApp--DeploymentToken`

It then applies the runtime API app settings from those secrets and uses the Static Web App deployment token for frontend publishing. The pipeline does not keep these values in Azure DevOps secret variables.

The pipeline reads the Key Vault by name, so the environment must already have the Key Vault deployed and populated by the infra pipeline.

### Smoke Checks

The CD pipeline performs two simple post-deployment checks:

- `GET /health` on the App Service API
- `GET` on the Azure Static Web Apps default hostname

If the smoke checks fail, the pipeline fails.

### First-Deploy Checklist

Before the first CD run, confirm these manual settings exist for the API app:

- the target environment already has Key Vault enabled in infra
- the infra pipeline has seeded `Database--ConnectionString`, `Jwt--SigningKey`, and `StaticWebApp--DeploymentToken`
- the Azure DevOps service connection can read the Key Vault secrets in the target subscription/resource group
- the target environment resource group exists and contains the BillSoft-tagged API App Service, Static Web App, and Key Vault resources
- the dev or stage DNS CNAME for the approved BillSoft custom domain points to the Static Web App default hostname if you want the custom domain active
- any other runtime secrets the app needs for the target environment

The pipeline sets the API CORS origin to the deployed Static Web Apps hostname after frontend deployment. That setting is non-secret and safe to manage in CD.
It also writes `runtime-config.js` into the published frontend artifact so the deployed SPA uses the Azure API base URL instead of the local-development fallback.
The EF migration task receives `Database__ConnectionString` only as an environment variable and does not log it or pass it on the command line.

If deployment approvals are desired, configure them on the Azure DevOps environment referenced by the `environmentName` parameter.

### Database Migrations

The CD pipeline runs EF Core database migrations as part of the deployment flow after the API artifact is deployed and before the frontend smoke checks run. The migration step uses the checked-out source and the existing `BillSoft.Infrastructure` migrations.

### Intentionally Not Included

The CD pipeline does not:

- provision infrastructure
- add deployment secrets to YAML
- rebuild the frontend or backend
- add release promotion logic beyond the manual CD run
- add new Azure resources
- write secrets into Azure DevOps
- provision or rotate Key Vault secrets

## Low-Cost Dev Infrastructure Profile

BillSoft includes a low-cost dev profile designed to minimize ongoing Azure spend.

### Expected Free Resources

- API App Service plan at Free `F1`
- API App Service hosted on the Free plan
- Frontend on Azure Static Web App Free
- Managed identity on the API app

The Static Web App uses its own location parameter because Azure only supports a limited set of regions for that resource type. The default is `eastus2`, while the rest of the foundation can still use the resource-group region such as `southeastasia`.

### Paid Resource

- Azure SQL Database

SQL remains paid because the repository still needs a relational database for application data, and the low-cost profile keeps the database on the least-cost practical DTU option: `Basic` with `5` DTU.

### Optional Resources

- Key Vault is disabled by default
- Monitoring is disabled by default

These are optional because they can introduce extra charges. Enable them only when the environment needs secret storage or telemetry beyond the base dev profile.

### Free Tier Limits

The Free App Service tier is dev/test only. It has shared compute, no SLA, and limited CPU/runtime capacity, so it is not suitable for production.

The Static Web App Free plan is also limited. It is intended for smaller apps and has tighter app-size and bandwidth quotas than Standard.

### Billing Note

Azure pricing can change. Check Azure Cost Management and the current service pricing pages before treating any environment as zero-cost in practice.
