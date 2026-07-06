# Frontend Boundary

## Purpose

This document defines the frontend boundaries for BillSoft feature pages and shared UI composition.

## Rules

- Feature pages must not import UI vendor libraries directly.
- Use local shared UI primitives from `src/web/src/components/ui`.
- Use local layout composition from `src/web/src/components/layout`.
- Use local brand primitives from `src/web/src/components/brand`.
- Use local brand tokens and shell constants from `src/web/src/brand`.
- Use the local API client from `src/web/src/api`.
- Keep feature pages focused on business flow and screen behavior, not vendor-specific presentation code.
- Staff-facing screens must stay touch-friendly, simple, and easy to operate under pressure.
- Feature pages under `src/web/src/features/*` should only consume the local wrappers above, the local API client layer, and standard React/router APIs already in the app shell.

## Practical Guidance

- Prefer clear action labels over icon-only controls.
- Keep tap targets large enough for handheld use.
- Minimize typing on staff screens.
- Reserve denser tables, filters, and advanced controls for owner/admin screens.
- Make warnings obvious for unpaid orders, cancellations, low stock, and duplicate or risky actions.
- If a new primitive is needed, create a simple local wrapper instead of importing the vendor library in the page layer.

## Examples

Good:

- Feature page composes `components/ui/Button` and `components/layout/ModuleLayout`.
- Staff screen presents a small number of large, explicit actions.
- Brand shell uses `components/brand/BrandHeader` and `components/layout/ResponsiveNav`.

Not allowed:

- Importing a vendor design system directly inside a feature page.
- Building a staff workflow around dense desktop-only interactions.
- Importing vendor UI components directly from a preview or feature page when a local wrapper exists.

## Outcome

These rules keep the frontend consistent, maintainable, and safe for restaurant staff who need fast, low-friction screens.
