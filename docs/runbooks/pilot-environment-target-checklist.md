# BillSoft Pilot Environment Target Checklist

## 1. Purpose

Complete this checklist before any Pilot RC 003 backup, migration, deployment, or smoke testing.

No backup, migration, deployment, or smoke test should proceed until every required field below is completed and reviewed.

## 2. Required BillSoft Environment Details

Fill in the BillSoft-specific target details below. Use the exact names provided by the BillSoft environment owner.

| Field | Value |
|---|---|
| BillSoft Azure subscription name | `[BillSoft Azure subscription name]` |
| BillSoft Azure resource group name | `[BillSoft Azure resource group name]` |
| BillSoft API App Service name | `[BillSoft API App Service name]` |
| BillSoft frontend hosting resource name | `[BillSoft frontend hosting resource name]` |
| BillSoft Azure SQL Server name | `[BillSoft Azure SQL Server name]` |
| BillSoft Azure SQL Database name | `[BillSoft Azure SQL Database name]` |
| BillSoft Azure DevOps project name | `[BillSoft Azure DevOps project name]` |
| BillSoft CI pipeline name | `[BillSoft CI pipeline name]` |
| BillSoft infra pipeline name | `[BillSoft infra pipeline name]` |
| BillSoft CD pipeline name | `[BillSoft CD pipeline name]` |

## 3. Required Secure Settings Confirmation

Confirm whether each secure setting is configured for the BillSoft pilot target.

| Setting | Confirmed | Notes |
|---|---|---|
| `Database__ConnectionString` configured | `Yes / No` |  |
| `Jwt__SigningKey` configured | `Yes / No` |  |
| `staticWebAppDeploymentToken` configured | `Yes / No` |  |
| `sqlAdministratorPassword` configured | `Yes / No` |  |

## 4. Required Permissions Confirmation

Confirm whether the operator can perform each required action on the BillSoft pilot target.

| Permission | Confirmed | Notes |
|---|---|---|
| Can run CI pipeline | `Yes / No` |  |
| Can run infra what-if | `Yes / No` |  |
| Can run infra deploy | `Yes / No` |  |
| Can run CD pipeline | `Yes / No` |  |
| Can backup/export SQL database | `Yes / No` |  |
| Can apply migrations manually | `Yes / No` |  |
| Can configure App Service settings | `Yes / No` |  |
| Can verify Static Web App deployment | `Yes / No` |  |

## 5. Pre-Deployment Gate

Do not continue until all required environment details, secure settings confirmations, and permission confirmations are complete and reviewed.

## 6. Handoff Instructions

Send this to the BillSoft environment owner:

Please provide the BillSoft-specific pilot environment target details and confirm the required secure settings and permissions in `docs/runbooks/pilot-environment-target-checklist.md`.

The BillSoft pilot environment remains blocked until the checklist is complete. No backup, migration, deployment, or smoke test should proceed until the BillSoft target is confirmed.
