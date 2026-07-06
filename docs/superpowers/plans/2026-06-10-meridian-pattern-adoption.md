# Meridian Pattern Adoption Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add BillSoft architecture guardrails inspired by Meridian patterns without changing business behavior.

**Architecture:** Update the repository instructions and add focused architecture boundary docs that explain what BillSoft adopts, what it deliberately does not copy, and how restaurant/branch terminology differs from generic multi-tenant wording. Keep the scope documentation-only and validate the repo with the existing build and test commands.

**Tech Stack:** Markdown documentation, .NET solution validation, pnpm frontend validation

---

### Task 1: Update repository instructions

**Files:**
- Modify: `AGENTS.md`

- [ ] **Step 1: Add Meridian-inspired guardrails**

```markdown
- Secure by default.
- Restaurant/branch-safe by default.
- Existing capability first.
- No invented permissions, routes, or entities.
- Small, reviewable changes.
- Audit sensitive actions.
```

- [ ] **Step 2: Keep the current BillSoft-specific rules intact**

```markdown
Preserve the existing product non-negotiables, backend rules, frontend rules, database rules, OCR rules, testing expectations, and output format.
```

### Task 2: Add Meridian pattern adoption guidance

**Files:**
- Create: `docs/architecture/patterns-from-meridian.md`

- [ ] **Step 1: Document adopted patterns and exclusions**

```markdown
Adopt:
- secure-by-default configuration
- least-privilege access
- explicit audit trails for sensitive actions
- reuse-first implementation

Do not copy:
- any Meridian-specific names, permissions, or routes
- any assumptions that would weaken BillSoft's restaurant/branch model
```

- [ ] **Step 2: Document naming differences**

```markdown
Use BillSoft terms:
- Tenant -> Restaurant / Branch
- Tenant-scoped -> restaurant-scoped or branch-scoped
```

### Task 3: Add boundary docs

**Files:**
- Create: `docs/architecture/frontend-boundary.md`
- Create: `docs/architecture/backend-boundary.md`

- [ ] **Step 1: Document frontend boundaries**

```markdown
Feature pages must not import UI vendor components directly.
Use local `components/ui` and `components/layout`.
Staff screens must be touch-friendly and simple.
```

- [ ] **Step 2: Document backend boundaries**

```markdown
Domain stays persistence-ignorant.
Infrastructure owns EF Core.
Application owns use cases.
API owns endpoints only.
Sensitive actions require authorization and audit records.
```

### Task 4: Validate the repository

**Files:**
- None

- [ ] **Step 1: Run backend validation**

```bash
dotnet restore BillSoft.sln
dotnet build BillSoft.sln
dotnet test BillSoft.sln
```

- [ ] **Step 2: Run frontend validation**

```bash
cd src/web
pnpm install
pnpm run typecheck
pnpm run build
```

- [ ] **Step 3: Confirm no business code or migrations were added**

```bash
git status -sb
```

---

# BillSoft Responsive Layout Idea Document

## Purpose

This layout section captures the screen ideas you asked for so the design can be reviewed before implementation starts, without removing the Meridian adoption plan above.

## Primary Screens Covered

- Login screen
- Home screen
- Dashboard screen

## Screen Size Breakpoints

Use these breakpoints as the base responsive design system:

- Mobile: `320px` to `767px`
- Tablet: `768px` to `1199px`
- Desktop: `1200px+`

These breakpoints should be treated as layout guidance, not hard visual limits. The interface should remain usable between sizes without breaking alignment, spacing, or readability.

## BillSoft UI Principles

- Keep staff screens very simple and touch-friendly.
- Keep owner/admin screens richer, but still readable at a glance.
- Use large buttons and strong contrast for fast restaurant workflows.
- Minimize typing wherever possible.
- Keep `Eat-in` and `Parcel / Takeaway` visually separate.
- Show the most important action first, not the most decorative one.
- Avoid crowded screens with too many equal-priority elements.
- Make the interface feel stable, fast, and easy to scan.

## 1. Login Screen

### Design Intent

The login screen should feel secure, clean, and low-friction. It should let staff sign in quickly without unnecessary noise.

### Mobile Layout

- Use a full-screen single-column layout.
- Place the logo or brand mark at the top.
- Keep the form in the center or upper-middle area.
- Show only essential fields:
  - username or mobile
  - password
  - sign in button
- Use large input fields and a large primary button.
- Keep helper text very short.
- Avoid side panels, illustrations, and dense descriptions.
- Ensure the keyboard does not hide the submit button or key fields.

### Tablet Layout

- Use a centered login card.
- Keep the logo at the top of the card.
- Place the form inside the same card for a focused layout.
- Add a small trust or info section below or beside the form.
- Keep spacing airy and aligned.
- Use a layout that feels professional but still compact.

### Desktop Layout

- Use a split layout.
- Left side:
  - branding
  - illustration or product message
  - short explanation of what BillSoft helps control
- Right side:
  - login card
  - input fields
  - submit button
- Keep the login card visually dominant so sign-in remains the main action.
- Use a balanced composition that feels polished without distracting from authentication.

## 2. Home Screen

### Design Intent

The home screen should act as the staff launch point. It should help users start common tasks immediately and reduce decision time.

### Main Content Pattern

The home screen can include:

- New Order
- Bills
- Kitchen
- Stock
- Reports
- Quick actions or shortcuts
- Recent activity

### Mobile Layout

- Use a one-column stack.
- Show action cards one below another.
- Make each card large enough for easy tapping.
- Keep labels short and direct.
- Prioritize the most common tasks first.
- Show only a small amount of supporting text.
- Keep status indicators visible but lightweight.

### Tablet Layout

- Use a two-column grid.
- Place quick actions at the top.
- Place recent activity or alerts below the quick actions.
- Keep the layout structured but not crowded.
- Allow the user to scan the screen in one or two glances.

### Desktop Layout

- Use a wider information layout.
- Add a top summary strip for key numbers or current status.
- Place quick action tiles prominently.
- Show recent orders and recent activity in the main section.
- Add alerts or operational warnings in a side panel.
- Include live status elements where useful, such as open orders, pending bills, or kitchen load.

## 3. Dashboard

### Design Intent

The dashboard should support owners and admins with control, visibility, and decision-making. It can be richer than staff screens because its purpose is monitoring and oversight.

### Mobile Layout

- Use stacked cards only.
- Avoid wide tables.
- Use collapsible sections where needed.
- Show the most important numbers first.
- Keep charts simple and readable.
- Reduce visual clutter so the user can scroll comfortably.
- Focus on summary information instead of deep reporting detail.

### Tablet Layout

- Put summary cards at the top.
- Use one or two column chart arrangements.
- Place tables below the visual summaries.
- Keep filters accessible but not overwhelming.
- Use enough spacing so charts and tables do not feel cramped.

### Desktop Layout

- Use a left sidebar and a top bar if the information density requires it.
- Use the main content area for KPIs, charts, tables, and alerts.
- Group related metrics together.
- Keep operational warnings visible.
- Make the hierarchy clear:
  - summary first
  - details second
  - tables and reports last
- Support richer monitoring without making the page feel heavy.

## BillSoft-Specific Layout Rules

### Staff Screens

- Keep screens simple.
- Keep actions large and obvious.
- Minimize the number of choices shown at one time.
- Avoid complex dashboards on staff-facing screens.
- Use touch-friendly spacing and readable labels.

### Owner and Admin Screens

- Allow more dense information.
- Use charts, tables, filters, and alerts where they help decision-making.
- Keep the layout organized so the screen still feels easy to scan.
- Avoid mixing owner controls into staff flows.

### Eat-in and Parcel Separation

- Always make `Eat-in` and `Parcel / Takeaway` visually distinct.
- Do not rely only on small text labels.
- Use different badges, status chips, or layout cues.
- Keep the flow separation obvious on both mobile and desktop.

### Button and Interaction Rules

- Use large buttons for mobile and tablet.
- Prefer direct action labels over icon-only actions.
- Reduce typing for restaurant staff.
- Keep the primary action obvious on each screen.

## Recommended Content Priority

For all three screens, use this priority order:

1. Primary action
2. Current status
3. Supporting information
4. Secondary actions
5. Extra detail

This keeps the interface focused and prevents the design from becoming too busy.

## Design Review Checklist

Before moving to implementation, confirm that the design:

- Works cleanly on mobile, tablet, and desktop.
- Uses consistent spacing and alignment.
- Keeps login simple and secure.
- Makes home screen actions obvious.
- Keeps dashboard information scannable.
- Separates staff and owner/admin complexity.
- Distinguishes `Eat-in` and `Parcel / Takeaway` clearly.
- Remains touch-friendly for restaurant workflows.

## Implementation Readiness

This document is intended as the first design reference for code conversion.

Once the visual design is finalized, it can be read into the implementation workflow and used to build:

- responsive layout components
- screen-level page structure
- spacing and breakpoint rules
- shared action card patterns
- dashboard summary card patterns

## Next Step

After the design reference is approved, the next step is to convert this layout idea into the actual frontend implementation for login, home, and dashboard screens.
