# BillSoft Frontend Layouts

## Purpose

BillSoft uses one shared brand shell and a small local UI kit, but it does not force every workflow into the same workspace layout.

## Layout Decision

Should inventory/stock management and restaurant order management use the same layout?

No. They should share the same brand shell and UI primitives, but use different workspace layouts.

Reason:

- Order management is speed, touch, and kitchen-flow oriented.
- Inventory management is data, filter, audit, and reporting oriented.
- Forcing both into one layout makes one of them harder to use under pressure.

## Shared Shell

All operational surfaces share:

- `AppShell`
- `ResponsiveNav`
- `BrandHeader`
- `PageHeader`
- `Button`, `Card`, `Badge`, `StatusBadge`, `Input`, `Select`, `EmptyState`
- `SummaryCard`, `ActionTile`, `ResponsiveDataList`

These shared pieces carry the BillSoft brand, spacing, color tokens, and touch rules.

## Layout Types

### 1. Order Management Layout

Use for:

- order taking
- parcel/takeaway
- eat-in table orders
- billing counter preview surfaces
- billing workspace
- kitchen display preview surfaces
- kitchen tickets
- cashier shift control

Characteristics:

- touch-first
- large action buttons
- category and product grids
- persistent current order / ticket panel
- split-pane layout on tablet and desktop
- minimal table usage
- mobile cards and stacked sections instead of dense lists
- landscape tablet support
- current-state controls stay visible while history and verification stay secondary

### 2. Inventory Management Layout

Use for:

- groceries stock
- low stock and out of stock monitoring
- daily usage
- vendor bills
- stock adjustments
- reports and audit review

Characteristics:

- data-management focused
- filter panels and summary cards
- tables on desktop with card fallback on mobile
- document upload / scan / review actions
- visible audit and override indicators
- export-friendly structure later if needed

### 3. Admin Layout

Use for:

- users
- roles
- branches
- menu configuration
- system setup

Characteristics:

- sidebar on desktop
- compact top or bottom navigation on mobile
- clear breadcrumbs
- form + table combination
- configuration-first, not ticket-first
- branch management uses a split-pane data workflow on desktop and tablet, then collapses into stacked sections on mobile

## Responsive Breakpoints

BillSoft uses these responsive thresholds for the frontend shell:

- Mobile: below `768px`
- Tablet: `768px` to `1023px`
- Desktop/laptop: `1024px` and above

## Mobile Layout Behavior

- No dense sidebar.
- Use bottom navigation for the shell.
- Use stacked cards and large touch targets.
- Replace wide tables with card-based lists when space is tight.
- Keep primary actions obvious and reachable.
- Avoid requiring horizontal scrolling for core workflows.

## Tablet Layout Behavior

- Use the same brand shell as desktop.
- Navigation becomes collapsible instead of permanently open.
- Order layouts should favor split-pane views in landscape.
- Inventory and admin layouts may still use two-column content, but must collapse cleanly.
- Touch targets remain large enough for staff use.

## Desktop / Laptop Layout Behavior

- Use a left sidebar for navigation.
- Keep the top bar visible for brand and context.
- Allow wider content areas for inventory and admin data.
- Use max-width rules to avoid overly wide text blocks.
- Keep order layout readable without forcing dense table assumptions.

## Navigation Model

- Desktop: left sidebar.
- Tablet: collapsible navigation panel.
- Mobile: bottom navigation.
- The shell navigation stays shallow and preview-friendly.
- Navigation labels should be short and task-oriented.
- The public entry point is `/login`; protected routes redirect unauthenticated users there.
- The authenticated nav should include the dashboard, owner dashboard, orders preview, orders, billing, daily report, kitchen tickets, inventory preview, admin users, branches, and menu management.
- Hide the admin users nav item when the current session lacks `User.Manage`, but keep the page permission-gated as well.
- Hide the branch management nav item when the current session lacks `Branch.Manage`, but keep the page permission-gated as well.
- Hide the menu management nav item when the current session lacks `MenuCategory.Manage`, `MenuItem.Manage`, and `MenuItem.View`, but keep the page permission-gated as well.

## Authenticated Shell Behavior

- `/login` is public.
- `/` is the authenticated dashboard landing page and remains the explicit home route.
- `/owner/dashboard`, `/orders-preview`, `/pos/orders`, `/billing`, `/reports/daily-cash-sales`, `/kitchen/tickets`, `/inventory-preview`, `/admin-preview`, `/admin/users`, `/admin/branches`, and `/admin/menu` are protected.
- `/owner/dashboard` is read-only, uses `Report.View`, and stays separate from the authenticated home route.
- Preserve the intended return path when redirecting to login from a protected route.
- If a safe preserved return path is absent, direct login uses a deterministic landing route from the authenticated session roles and permissions.
- The landing route is frontend-only and does not change backend APIs, session shape, or `/` behavior.
- Surface restaurant context and the signed-in user label in the top bar.

## Accessibility Expectations

- Maintain readable contrast across background, surface, and status colors.
- Use semantic headings and landmarks.
- Preserve keyboard focus visibility.
- Ensure preview pages remain understandable without hover.
- Avoid relying on color alone to communicate a warning or state.

## Touch Target Expectations

- Minimum tap target: `44px`.
- Comfortable target: `48px` or higher for primary staff actions.
- Use full-width buttons in narrow layouts where it improves speed.
- Favor tiles and segmented controls over tiny icon-only controls.

## No Direct Vendor UI Import Rule

Feature and page code must not import vendor UI components directly when a local wrapper exists.

Allowed import surfaces for feature code:

- `src/web/src/components/ui`
- `src/web/src/components/layout`
- `src/web/src/components/brand`
- `src/web/src/brand`
- `src/web/src/features/*`

Vendor UI libraries may only be used by the wrapper layer if the project ever introduces one.

## When To Use Each Layout

- Use `OrderManagementLayout` for order entry, parcel, kitchen preview, billing, and billing-counter preview workspaces.
- Use `OrderManagementLayout` for cashier shift control because it is an operational money-control workspace with a primary live state and secondary verification panels.
- The `/billing` workspace includes an inline receipt preview card for print-ready bills and browser-only reprint actions.
- Use `InventoryManagementLayout` for stock, vendor bill, audit, and reporting workspaces.
- The `/reports/daily-cash-sales` workspace is a read-only reporting surface for daily cash control, exception review, and shift variance inspection.
- The `/owner/dashboard` workspace is a read-only landing surface for daily control signals and reuses the daily report data path without replacing the full report.
- Use `AdminLayout` for user, role, branch, and configuration workspaces.
- Use the shared `AppShell` and `PageHeader` everywhere the app needs consistent BillSoft branding.

