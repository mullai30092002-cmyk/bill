# GitHub Actions CI

## Overview

BillSoft includes a GitHub Actions CI workflow in [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml).
It validates the repo on `push` and `pull_request` events against `master`.

## Trigger Policy

The workflow only runs when files under `src/` change on `push` or `pull_request` events against `master`.

## Jobs

The workflow still runs the same two jobs:

1. backend build and test
2. frontend test and build

## Notes

- CI does not deploy to Azure.
- CD is triggered from successful CI runs on `master`, so source-only CI triggers also make CD source-only.
