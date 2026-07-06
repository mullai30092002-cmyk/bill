# Foundation Seed Execution Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the foundation seed path deterministic and explicitly controllable at startup before authentication work begins.

**Architecture:** Keep seeding inside Infrastructure and expose only a controlled startup path in the API host. The seed service returns a typed result, the seed catalog owns deterministic role-permission IDs, and the API host decides whether to invoke seeding based on command-line and config flags.

**Tech Stack:** .NET 8, EF Core 8, xUnit, SQLite in-memory tests

---

### Task 1: Add deterministic seed behavior tests

**Files:**
- Modify: `tests/BillSoft.Tests/FoundationModelTests.cs`
- Modify: `tests/BillSoft.Tests/FoundationSeedServiceTests.cs`
- Add: `tests/BillSoft.Tests/FoundationSeedCommandTests.cs`

- [ ] **Step 1: Write tests for deterministic role-permission IDs**
- [ ] **Step 2: Write tests for seed result counts and idempotency**
- [ ] **Step 3: Write tests for startup seed mode detection**

### Task 2: Update the foundation seed catalog and service

**Files:**
- Modify: `src/api/BillSoft.Infrastructure/Seed/FoundationSeedData.cs`
- Modify: `src/api/BillSoft.Infrastructure/Seed/FoundationSeedService.cs`
- Add: `src/api/BillSoft.Infrastructure/Seed/FoundationSeedResult.cs`

- [ ] **Step 1: Generate deterministic role-permission IDs from role + permission**
- [ ] **Step 2: Return inserted counts and timestamps from the seed service**
- [ ] **Step 3: Keep the seed catalog duplicate-free**

### Task 3: Add controlled startup seed execution

**Files:**
- Add: `src/api/BillSoft.Infrastructure/Setup/FoundationSeedRuntime.cs`
- Modify: `src/api/BillSoft.Api/Program.cs`
- Modify: `src/api/BillSoft.Api/appsettings.json`
- Modify: `src/api/BillSoft.Api/appsettings.Development.json`

- [ ] **Step 1: Parse `--seed-foundation` and config flags**
- [ ] **Step 2: Seed only when explicitly requested**
- [ ] **Step 3: Log start/completion counts and exit when required**

### Task 4: Update docs and verify

**Files:**
- Modify: `docs/development/local-setup.md`
- Modify: `docs/requirements/permission-matrix.md`

- [ ] **Step 1: Document the startup seed command and default-disabled config**
- [ ] **Step 2: Document the conservative baseline role-permission mapping**
- [ ] **Step 3: Run restore/build/test and frontend validation**
