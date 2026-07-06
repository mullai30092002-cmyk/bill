# Identity and Audit Foundation Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Clean up the identity and audit foundation so restaurant codes, per-restaurant user email uniqueness, audit snapshots, and deterministic role/permission seeding are in place before authentication work begins.

**Architecture:** Keep the existing table and key names, but make the domain model initialize IDs and timestamps safely through a shared base helper. Enforce canonical restaurant codes and email normalization in entity methods, wire the EF model with the required unique indexes and snapshot columns, and add an idempotent runtime seed service backed by deterministic system catalogs.

**Tech Stack:** .NET 8, EF Core 8, SQL Server migrations, xUnit, SQLite in-memory tests

---

### Task 1: Add foundation tests first

**Files:**
- Modify: `tests/BillSoft.Tests/FoundationModelTests.cs`
- Modify: `tests/BillSoft.Tests/BillSoft.Tests.csproj`
- Add: `tests/BillSoft.Tests/FoundationSeedServiceTests.cs`

- [ ] **Step 1: Write failing metadata and normalization tests**

```csharp
Assert.NotEqual(Guid.Empty, new Restaurant().RestaurantId);
Assert.Equal("ABC123", restaurant.NormalizedRestaurantCode);
Assert.Equal("USER@EXAMPLE.COM", user.NormalizedEmail);
```

- [ ] **Step 2: Write failing seeder tests**

```csharp
await seedService.SeedAsync();
Assert.Equal(expectedPermissionCount, context.Permissions.Count());
Assert.Equal(expectedRoleCount, context.Roles.Count());
```

### Task 2: Update domain foundation entities

**Files:**
- Modify: `src/api/BillSoft.Domain/Common/BaseEntity.cs`
- Modify: `src/api/BillSoft.Domain/Restaurants/Restaurant.cs`
- Modify: `src/api/BillSoft.Domain/Restaurants/Branch.cs`
- Modify: `src/api/BillSoft.Domain/Users/User.cs`
- Modify: `src/api/BillSoft.Domain/Security/Role.cs`
- Modify: `src/api/BillSoft.Domain/Security/Permission.cs`
- Modify: `src/api/BillSoft.Domain/Security/UserRole.cs`
- Modify: `src/api/BillSoft.Domain/Security/RolePermission.cs`
- Modify: `src/api/BillSoft.Domain/Auditing/AuditLog.cs`
- Modify: `src/api/BillSoft.Domain/Security/SystemRoles.cs`
- Modify: `src/api/BillSoft.Domain/Security/SystemPermissions.cs`

- [ ] **Step 1: Add shared helpers for safe GUID and UTC initialization**
- [ ] **Step 2: Add restaurant code and email normalization methods**
- [ ] **Step 3: Add deterministic IDs to system catalogs**

### Task 3: Update EF model and infrastructure seeding

**Files:**
- Modify: `src/api/BillSoft.Infrastructure/Persistence/Configurations/RestaurantConfiguration.cs`
- Modify: `src/api/BillSoft.Infrastructure/Persistence/Configurations/UserConfiguration.cs`
- Modify: `src/api/BillSoft.Infrastructure/Persistence/Configurations/AuditLogConfiguration.cs`
- Modify: `src/api/BillSoft.Infrastructure/DependencyInjection.cs`
- Add: `src/api/BillSoft.Infrastructure/Seed/FoundationSeedData.cs`
- Add: `src/api/BillSoft.Infrastructure/Seed/FoundationSeedService.cs`
- Add: `src/api/BillSoft.Infrastructure/Seed/IFoundationSeedService.cs`

- [ ] **Step 1: Add unique restaurant code and normalized email indexes**
- [ ] **Step 2: Add audit snapshot columns**
- [ ] **Step 3: Implement idempotent seed inserts and role-permission mapping**

### Task 4: Add cleanup migration

**Files:**
- Add: `src/api/BillSoft.Infrastructure/Migrations/*_IdentityFoundationCleanup.cs`
- Add: `src/api/BillSoft.Infrastructure/Migrations/*_IdentityFoundationCleanup.Designer.cs`
- Modify: `src/api/BillSoft.Infrastructure/Migrations/BillSoftDbContextModelSnapshot.cs`

- [ ] **Step 1: Backfill existing restaurant codes**
- [ ] **Step 2: Add normalized email and audit snapshot schema**
- [ ] **Step 3: Add indexes and update the snapshot**

### Task 5: Update docs and verify

**Files:**
- Modify: `docs/database/database-tables.md`
- Modify: `docs/development/local-setup.md` if seed commands change
- Modify: `docs/requirements/permission-matrix.md` if role defaults are documented

- [ ] **Step 1: Document the new columns and seeding behavior**
- [ ] **Step 2: Run restore/build/test and frontend validation**
- [ ] **Step 3: Commit and push the cleanup**
