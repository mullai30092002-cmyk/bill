# Login Page UX Alignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Align the BillSoft login page with restaurant operations language, safer shared-device guidance, and shared UI primitives without changing authentication behavior.

**Architecture:** Keep the existing split login layout and auth flow. Move the login form inputs and actions onto the local shared UI primitives under `src/web/src/components/ui`, add a small shared checkbox primitive for the trust-device control, and keep all session handling and backend calls unchanged. Update the login page CSS only where needed for responsive layout, helper text, and visibility of the password toggle.

**Tech Stack:** React 19, TypeScript, React Router, local shared UI components, Vitest, Testing Library, CSS

---

### Task 1: Add shared checkbox primitive

**Files:**
- Create: `src/web/src/components/ui/Checkbox.tsx`
- Modify: `src/web/src/components/ui/index.ts`
- Modify: `src/web/src/styles/components.css`

- [ ] **Step 1: Add the failing surface through a page expectation**

Add a login-page test that expects `Trust this device` and the shared-counter warning text to render as accessible checkbox copy.

- [ ] **Step 2: Implement the shared checkbox**

Create a local checkbox primitive that supports a label and helper text, wires `aria-describedby`, and matches the BillSoft touch-friendly field styling.

- [ ] **Step 3: Export the primitive from the shared UI barrel**

Expose the checkbox from `src/web/src/components/ui/index.ts` so feature pages can import it without vendor UI imports.

### Task 2: Update the login page

**Files:**
- Modify: `src/web/src/features/auth/LoginPage.tsx`
- Modify: `src/web/src/features/auth/LoginPage.css`

- [ ] **Step 1: Update the login test to describe the intended copy**

Assert the operational left-panel headline, restaurant-code helper text, mobile helper text, trust-device label, warning text, and staff-safe access wording.

- [ ] **Step 2: Replace custom field markup with local UI primitives where practical**

Use `Input` for restaurant code, mobile number, and password entry, `Button` for primary/secondary actions, and the shared checkbox for persistent login copy.

- [ ] **Step 3: Keep auth behavior unchanged**

Preserve login submission, redirect behavior, and session storage handling exactly as-is.

- [ ] **Step 4: Tune the responsive CSS**

Keep the split desktop layout, compress the hero panel on tablet/mobile, and ensure the form remains readable and touch-friendly without the hero dominating narrow screens.

### Task 3: Update login tests and validation

**Files:**
- Modify: `src/web/src/features/auth/LoginPage.test.tsx`

- [ ] **Step 1: Add assertions for the new copy and helper text**

Verify the operational headline, helper text, checkbox label, shared-counter warning, and staff-safe access wording.

- [ ] **Step 2: Run the login-focused tests**

Run: `pnpm test -- LoginPage`
Expected: the login-page suite passes with the new copy and shared UI structure.

- [ ] **Step 3: Run the frontend validation commands**

Run:

```bash
pnpm run test
pnpm run typecheck
pnpm run build
```

Expected: all frontend checks pass.

### Task 4: Verify boundary rules

**Files:**
- None

- [ ] **Step 1: Confirm no direct vendor UI imports were added**

Review the modified feature files and confirm they only import local shared UI components.

- [ ] **Step 2: Confirm no auth/session behavior changed**

Check the login flow still uses the existing auth provider, storage, and redirect logic.
