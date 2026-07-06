# Owner Dashboard Inventory Alerts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire real inventory low-stock and out-of-stock alert data into the owner dashboard using the existing dashboard response so owners see inventory risk without opening the inventory module first.

**Architecture:** Extend the existing owner dashboard read model with a small inventory alert summary block built from the current inventory summary logic. Keep all scoping and permission checks server-side in the dashboard service so the frontend still makes one dashboard request. The UI should render a compact, read-only inventory alert panel plus counts and a simple navigation link to the inventory page.

**Tech Stack:** ASP.NET Core 8 minimal APIs, EF Core, existing BillSoft dashboard/inventory services, React 19, TypeScript, Vitest, Playwright CLI.

---

### Task 1: Extend the owner-dashboard contract and service with inventory alert summary data

**Files:**
- Modify: `src/api/BillSoft.Application/Dashboard/OwnerDashboardDtos.cs`
- Modify: `src/api/BillSoft.Application/Dashboard/IOwnerDashboardService.cs`
- Modify: `src/api/BillSoft.Infrastructure/Dashboard/OwnerDashboardService.cs`
- Modify: `src/api/BillSoft.Infrastructure/DependencyInjection.cs` only if new service dependencies are required

- [ ] **Step 1: Write the failing test**

Add a dashboard service test in the existing dashboard test area that asserts the owner dashboard response includes:
```csharp
InventoryAlerts.LowStockCount
InventoryAlerts.OutOfStockCount
InventoryAlerts.TotalAlertCount
InventoryAlerts.CriticalItems
```
and that `CriticalItems` is capped and sorted with out-of-stock items first.

- [ ] **Step 2: Run the focused test to verify it fails**

Run:
```powershell
dotnet test BillSoft.sln --no-build --filter FullyQualifiedName~OwnerDashboard
```
Expected: fail because `InventoryAlerts` does not exist yet.

- [ ] **Step 3: Write the minimal implementation**

Add new DTOs:
```csharp
public sealed record OwnerDashboardInventoryAlerts(
    int LowStockCount,
    int OutOfStockCount,
    int TotalAlertCount,
    IReadOnlyCollection<OwnerDashboardInventoryAlertItem> CriticalItems);

public sealed record OwnerDashboardInventoryAlertItem(
    Guid InventoryItemId,
    string Name,
    string Category,
    string Unit,
    decimal CurrentQuantity,
    decimal MinimumQuantity,
    string Status,
    DateTimeOffset? LastUpdatedAt);
```

Extend `OwnerDashboardResponse` to include `OwnerDashboardInventoryAlerts InventoryAlerts`.

Inject `IInventoryService` into `OwnerDashboardService` and build inventory alert data by reusing the inventory summary/item list logic already present in the application. Keep the dashboard read-only and only use current stock, minimum threshold, status, and last-updated values.

Cap `CriticalItems` at 5 and sort:
1. Out of stock
2. Low stock
3. Higher current quantity first only as a stable tie-breaker

- [ ] **Step 4: Run the focused test to verify it passes**

Run:
```powershell
dotnet test BillSoft.sln --no-build --filter FullyQualifiedName~OwnerDashboard
```
Expected: pass.

- [ ] **Step 5: Commit**

```powershell
git add src/api/BillSoft.Application/Dashboard/OwnerDashboardDtos.cs src/api/BillSoft.Application/Dashboard/IOwnerDashboardService.cs src/api/BillSoft.Infrastructure/Dashboard/OwnerDashboardService.cs
git commit -m "feat: add inventory alerts to owner dashboard contract"
```

### Task 2: Render inventory alert counts and critical items on the owner dashboard

**Files:**
- Modify: `src/web/src/features/dashboard/ownerDashboardTypes.ts`
- Modify: `src/web/src/features/dashboard/ownerDashboardApi.ts`
- Modify: `src/web/src/features/dashboard/OwnerDashboardPage.tsx`
- Modify: `src/web/src/features/dashboard/OwnerDashboardPage.test.tsx`
- Modify: `src/web/src/features/dashboard/ownerDashboardDisplay.ts` only if small display helpers are needed

- [ ] **Step 1: Write the failing test**

Add a dashboard page test that renders a payload with:
```ts
inventoryAlerts: {
  lowStockCount: 2,
  outOfStockCount: 1,
  totalAlertCount: 3,
  criticalItems: [...]
}
```
and asserts the page shows:
- low stock count
- out of stock count
- a compact inventory alert panel
- empty state when the list is empty
- a `View inventory` link/button

- [ ] **Step 2: Run the focused test to verify it fails**

Run:
```powershell
pnpm --dir src/web test -- OwnerDashboardPage.test.tsx
```
Expected: fail because the new inventory alert block is not rendered yet.

- [ ] **Step 3: Write the minimal implementation**

Extend the frontend contract types to match the new backend block:
```ts
export interface OwnerDashboardInventoryAlertItem { ... }
export interface OwnerDashboardInventoryAlerts { ... }
```

Render a new `Inventory alerts` card in `OwnerDashboardPage` that:
- shows low-stock and out-of-stock KPI chips/counters
- lists the critical items in a compact responsive layout
- marks `Out of stock` and `Low stock` clearly
- shows current quantity, unit, minimum quantity, and last updated time
- shows an empty state `No inventory alerts`
- includes a `View inventory` button that navigates to `/inventory`

Do not add any dashboard-side adjustment actions or mutation controls.

- [ ] **Step 4: Run the focused test to verify it passes**

Run:
```powershell
pnpm --dir src/web test -- OwnerDashboardPage.test.tsx
```
Expected: pass.

- [ ] **Step 5: Commit**

```powershell
git add src/web/src/features/dashboard/ownerDashboardTypes.ts src/web/src/features/dashboard/ownerDashboardApi.ts src/web/src/features/dashboard/OwnerDashboardPage.tsx src/web/src/features/dashboard/OwnerDashboardPage.test.tsx
git commit -m "feat: show inventory alerts on owner dashboard"
```

### Task 3: Add inventory-scope coverage and dashboard alert edge cases on the backend

**Files:**
- Modify: `tests/BillSoft.Tests/OwnerDashboardTests.cs` or the existing owner-dashboard test file in the test project
- Modify: `tests/BillSoft.Tests/InventoryFoundationEndpointTests.cs` only if shared helpers are needed

- [ ] **Step 1: Write the failing tests**

Add tests that assert:
```csharp
// current restaurant/branch only
// out-of-stock counted correctly
// low-stock counted correctly
// in-stock items excluded from counts
// critical list capped and ordered
// empty inventory still returns a valid dashboard response
```

- [ ] **Step 2: Run the focused test to verify it fails**

Run:
```powershell
dotnet test BillSoft.sln --no-build --filter FullyQualifiedName~OwnerDashboard
```
Expected: fail until the service logic is in place.

- [ ] **Step 3: Write the minimal implementation**

Implement the backend coverage using real inventory items and movements only. Do not require movement `Reason` data for the dashboard summary because the dashboard only needs current quantity, minimum threshold, status, and timestamps.

- [ ] **Step 4: Run the focused test to verify it passes**

Run:
```powershell
dotnet test BillSoft.sln --no-build --filter FullyQualifiedName~OwnerDashboard
```
Expected: pass.

- [ ] **Step 5: Commit**

```powershell
git add tests/BillSoft.Tests/OwnerDashboardTests.cs
git commit -m "test: cover owner dashboard inventory alerts"
```

### Task 4: Run full validation and browser QA

**Files:**
- None expected unless validation reveals a real bug

- [ ] **Step 1: Run the full web validation**

Run:
```powershell
pnpm --dir src/web test
pnpm --dir src/web run typecheck
pnpm --dir src/web run build
```

- [ ] **Step 2: Run the full backend validation**

Run:
```powershell
dotnet restore BillSoft.sln
dotnet build BillSoft.sln --no-restore
dotnet test BillSoft.sln --no-build
```

- [ ] **Step 3: Run browser QA**

Use the repo-backed Playwright helper to verify:
- desktop
- 1024x768
- 430x932
- 390x844

Check:
- dashboard loads
- inventory alert KPI/panel is visible
- no-alert state is clean
- low/out rows are readable
- inventory link works
- no horizontal overflow

- [ ] **Step 4: Apply the deployment-sequencing note**

Document explicitly that this dashboard wiring does **not** require the `InventoryMovementReasonRequired` migration because it only consumes current stock, minimum quantity, status, and timestamps from inventory summary data.

- [ ] **Step 5: Final commit**

```powershell
git add src/api/BillSoft.Application/Dashboard/OwnerDashboardDtos.cs src/api/BillSoft.Application/Dashboard/IOwnerDashboardService.cs src/api/BillSoft.Infrastructure/Dashboard/OwnerDashboardService.cs src/web/src/features/dashboard/ownerDashboardTypes.ts src/web/src/features/dashboard/ownerDashboardApi.ts src/web/src/features/dashboard/OwnerDashboardPage.tsx src/web/src/features/dashboard/OwnerDashboardPage.test.tsx tests/BillSoft.Tests/OwnerDashboardTests.cs
git commit -m "feat: wire inventory alerts into owner dashboard"
```
