# BillSoft Branding

## Brand Personality

BillSoft should feel:

- warm
- direct
- practical
- calm under pressure
- audit-friendly

The visual system should suit a busy restaurant floor, not a corporate SaaS dashboard.

## Logo Placeholder Approach

- Use a local placeholder mark until a real asset exists.
- The placeholder mark is text-first, not image-first.
- The current implementation uses a simple `BS` mark in a rounded tile.
- No external image dependency is required.
- The brand system must support replacing the placeholder with a real logo later without changing the shell structure.

## Color Tokens

Primary tokens:

- Primary: `#d97706`
- Accent: `#0f766e`
- Success: `#15803d`
- Warning: `#b45309`
- Danger: `#b91c1c`
- Info: `#2563eb`
- Background: `#f7f2eb`
- Surface: `#fffdf9`
- Surface alt: `#f5ede2`
- Border: `#e8d8c4`
- Text: `#1f2937`
- Muted text: `#5b6472`

The palette intentionally uses a warm food-service accent with a stable, neutral base.

## Typography Guidance

- Use a clean system-based stack with a slightly modern lean.
- Prefer strong heading weight and tight title spacing.
- Keep body text readable and not overly condensed.
- Do not introduce an external font dependency for the foundation.

## Spacing Scale

Use a compact but comfortable spacing scale:

- `0.25rem`
- `0.5rem`
- `0.75rem`
- `1rem`
- `1.5rem`
- `2rem`
- `3rem`

This supports both dense admin views and large staff touch targets.

## Status Colors

- Success: positive, completed, ready, paid, confirmed
- Warning: pending, low stock, attention needed, review required
- Danger: cancelled, voided, rejected, out of stock, risky
- Info: in progress, informative, system state

Status colors must still be paired with text labels so color-blind users can read state clearly.

## Module Color Accents

Module accents are used sparingly:

- Dashboard: blue
- Orders: warm primary amber
- Inventory: teal
- Admin: slate

Accents should guide context, not replace the shared brand system.

## Dark Mode Decision

The foundation is light-mode first.

Reason:

- restaurant staff need maximum clarity
- the brand palette is warm and surface-driven
- the shell is being built before authentication and user preference storage exist

Dark mode can be added later if product usage shows it is needed, but it is not part of this foundation.

## White-Label Support

BillSoft should remain ready for restaurant-level branding later.

Support approach:

- keep the shell brand config-driven
- keep restaurant name display configurable
- keep logo mark replaceable
- isolate color tokens in a small theme layer
- avoid hardcoding restaurant-specific language in shared components

Future restaurant white-label support should be able to swap the restaurant name, mark, and selected accents without rewriting the layout shell.
