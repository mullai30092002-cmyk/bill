# GitHub Actions CD

## Overview

BillSoft includes a GitHub Actions CD workflow in [`.github/workflows/cd.yml`](../../.github/workflows/cd.yml).
It deploys the already-built `billsoft-api` and `billsoft-web` artifacts from CI and does not rebuild the app.

The workflow runs on:

- `workflow_run` after the `CI` workflow completes successfully on `master`
- `workflow_dispatch` for manual environment-specific runs

## Deployment Flow

The workflow performs these steps in order:

1. log in to Azure with GitHub OIDC
2. resolve the CI run ID from the current event or the latest successful CI run on `master`
3. resolve the environment resource group and tagged Azure resources
4. read deployment secrets from Key Vault
5. configure API app settings
6. download the CI artifacts
7. deploy the API artifact to App Service
8. run EF Core migrations
9. optionally seed foundation and demo data
10. write `runtime-config.js` into the frontend artifact
11. deploy the frontend artifact to Static Web Apps
12. configure the frontend custom domain when one is set
13. configure API CORS origins
14. run smoke checks against the deployed API and frontend

## Required GitHub Secrets

The workflow now uses GitHub OIDC instead of a long-lived `AZURE_CREDENTIALS` JSON secret.
Each GitHub environment used by the workflow should provide these secrets:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`

These secrets should point at the Azure app registration or managed identity that has permission to manage the BillSoft dev, stage, or prod resource group.

## Required Workflow Permissions

The workflow requests these GitHub token permissions:

- `contents: read` for checkout
- `actions: read` for artifact download
- `id-token: write` for Azure OIDC login

## Notes

- The workflow still reads runtime secrets such as `Database--ConnectionString`, `Jwt--SigningKey`, and `StaticWebApp--DeploymentToken` from the environment Key Vault.
- No Azure DevOps service connection is involved in this GitHub Actions CD path.
- The environment name controls both the GitHub environment selected by the job and the Azure resource group it targets.
- Manual `workflow_dispatch` runs can accept an explicit CI run ID, but it is optional. If the value is missing or invalid the workflow falls back to the latest successful CI run on `master`.
