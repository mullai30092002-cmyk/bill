# Authentication Plan

## Purpose

This document defines the first BillSoft authentication phase and the token foundation that will be used before login endpoints are added.

BillSoft keeps identity restaurant-scoped in phase 1. The login flow will come later.

## Phase 1 Login Identity

Planned login identifier:

- `RestaurantCode`
- `MobileNumber`
- `Password`

Why this shape:

- `RestaurantCode` scopes the user to a restaurant before looking up the account.
- `MobileNumber` is already unique per restaurant.
- `Email` remains optional and unique per restaurant when present, so it is not the primary login identifier.
- The model stays restaurant-scoped instead of introducing a global platform identity too early.
- The mobile lookup path normalizes input using the restaurant country profile before querying the canonical `MobileE164` value.
- Restaurant and branch locale metadata stays explicit so the login path can remain multi-country-ready without adding a full localization engine.

## What Is Deferred

Intentionally deferred to later work:

- PIN login
- role switching
- external identity providers
- MFA
- frontend refresh-token rotation logic in the shell
- payment gateway integration
- inventory deduction

## Token Model

Phase 1 uses JWT access tokens plus persisted refresh tokens.

### Access Token

- Symmetric HMAC signing
- Short-lived access token
- Default lifetime: 15 minutes
- Contains the BillSoft restaurant and branch context required for authorization

### Refresh Token

- Persisted only as a SHA-256 hash
- Plaintext refresh tokens are never stored
- Default lifetime: 7 days
- Rotation will be handled later when the login flow is added

### Auth Response

- `POST /api/v1/auth/login` and `POST /api/v1/auth/refresh` return the stable authenticated `userId` alongside the token and restaurant context.
- The frontend session stores `userId` so user-administration screens can identify the signed-in account without comparing mutable fields such as mobile number.

## Phase 1 Auth Slice

The first backend auth slice exposes these endpoints:

- `POST /api/v1/auth/login`
- `POST /api/v1/auth/refresh`
- `POST /api/v1/auth/logout`
- `GET /api/v1/auth/me`

Behavior:

- Login uses `RestaurantCode + MobileNumber + Password`
- Refresh rotates the persisted refresh token
- Logout is idempotent and revokes the matching refresh token when present
- `/me` returns the claim-derived authenticated context
- Role switching remains deferred
- Deactivating a user revokes that user's active refresh tokens
- Country support in this foundation is intentionally limited to the small deterministic profiles already cataloged in code: Singapore (`SG`) and India (`IN`).

## User Administration Foundation

The backend user-admin foundation is restaurant-scoped and protected by `User.Manage`.

Planned endpoints:

- `GET /api/v1/admin/users`
- `POST /api/v1/admin/users`
- `GET /api/v1/admin/users/{userId}`
- `PUT /api/v1/admin/users/{userId}`
- `PUT /api/v1/admin/users/{userId}/roles`
- `POST /api/v1/admin/users/{userId}/activate`
- `POST /api/v1/admin/users/{userId}/deactivate`
- `POST /api/v1/admin/users/{userId}/reset-password`
- `GET /api/v1/admin/branches`
- `GET /api/v1/admin/branches/{branchId}`
- `POST /api/v1/admin/branches`
- `PUT /api/v1/admin/branches/{branchId}`
- `POST /api/v1/admin/branches/{branchId}/activate`
- `POST /api/v1/admin/branches/{branchId}/deactivate`

Menu catalog foundation endpoints:

- `GET /api/v1/admin/menu/categories`
- `GET /api/v1/admin/menu/categories/{categoryId}`
- `POST /api/v1/admin/menu/categories`
- `PUT /api/v1/admin/menu/categories/{categoryId}`
- `POST /api/v1/admin/menu/categories/{categoryId}/activate`
- `POST /api/v1/admin/menu/categories/{categoryId}/deactivate`
- `GET /api/v1/admin/menu/items`
- `GET /api/v1/admin/menu/items/{itemId}`
- `POST /api/v1/admin/menu/items`
- `PUT /api/v1/admin/menu/items/{itemId}`
- `POST /api/v1/admin/menu/items/{itemId}/activate`
- `POST /api/v1/admin/menu/items/{itemId}/deactivate`
- `GET /api/v1/admin/menu/items/{itemId}/price-history`

POS order foundation endpoints:

- `GET /api/v1/pos/orders`
- `GET /api/v1/pos/orders/{orderId}`
- `POST /api/v1/pos/orders`
- `PUT /api/v1/pos/orders/{orderId}`
- `POST /api/v1/pos/orders/{orderId}/confirm`
- `POST /api/v1/pos/orders/{orderId}/cancel`

Rules:

- Restaurant scope comes from the JWT `restaurant_id` claim.
- POS order reads require `Order.View` or `Order.Create`.
- POS order writes require `Order.Create`, except cancellation which allows `Order.Cancel` or `Order.Create`.
- The POS order request body never accepts `RestaurantId` or `OrderNumber`.
- Line items snapshot current menu name, category name, SKU, price, and tax at order time.
- Later menu price changes do not alter historical POS order totals.
- Billing and payment foundation backend screens are in scope for the current shell, while payment gateway integration and inventory deduction remain deferred.

Billing and payment foundation endpoints:

- `GET /api/v1/billing/bills`
- `GET /api/v1/billing/bills/{billId}`
- `GET /api/v1/billing/bills/{billId}/receipt`
- `POST /api/v1/billing/bills`
- `POST /api/v1/billing/bills/{billId}/cancel`
- `POST /api/v1/billing/bills/{billId}/payments`
- `POST /api/v1/billing/bills/{billId}/receipt/print-events`
- `POST /api/v1/billing/payments/{paymentId}/cancel`

Rules:

- Restaurant scope comes from the JWT `restaurant_id` claim.
- Billing reads require `Billing.View`, `Billing.Manage`, or `Payment.Record`.
- Billing and bill cancellation require `Billing.Manage`.
- Payment recording requires `Payment.Record`.
- Payment cancellation requires `Payment.Cancel`.
- Receipt reads and print-event recording require `Billing.View`, `Billing.Manage`, or `Payment.Record`; print events are persisted and audited, and browser printing only happens after the print-event write succeeds.
- Bill lines are copied from confirmed POS order snapshots, and later menu price changes do not alter historical bill totals.
- Frontend billing screens remain in scope for the current shell; the inline receipt preview card is part of `/billing`, while payment gateway integration and inventory deduction remain deferred.

Reporting endpoints:

- `GET /api/v1/reports/daily-cash-sales?date={yyyy-MM-dd}&branchId={branchId}`

Rules:

- Restaurant scope comes from the JWT `restaurant_id` claim.
- The report requires `Report.View`.
- The report is read-only and uses persisted bills, payments, receipt print events, and cashier shifts only.
- The `date` filter defaults to today in the restaurant or branch timezone when not supplied.
- `branchId`, when supplied, must belong to the current restaurant.
- The `/reports/daily-cash-sales` frontend route is protected and stays read-only.

Kitchen ticket foundation endpoints:

- `GET /api/v1/kitchen/tickets`
- `GET /api/v1/kitchen/tickets/{ticketId}`
- `POST /api/v1/kitchen/tickets`
- `POST /api/v1/kitchen/tickets/{ticketId}/status`
- `POST /api/v1/kitchen/tickets/{ticketId}/cancel`

Rules:

- Restaurant scope comes from the JWT `restaurant_id` claim.
- Kitchen tickets are created only from confirmed POS order snapshots.
- Kitchen ticket lines copy POS order line snapshots and do not read current menu data.
- Kitchen ticket creation, status changes, and cancellation remain backend-authoritative.
- The authenticated shell now exposes `/kitchen/tickets` as a protected frontend route for ticket display and operational status workflow.
- Printer integration and hardware assignment remain deferred.

Cashier shift foundation endpoints:

- `GET /api/v1/cashier/shifts`
- `GET /api/v1/cashier/shifts/current?branchId={branchId}`
- `GET /api/v1/cashier/shifts/{shiftId}`
- `POST /api/v1/cashier/shifts/open`
- `POST /api/v1/cashier/shifts/{shiftId}/movements`
- `POST /api/v1/cashier/shifts/{shiftId}/close`

Rules:

- Restaurant scope comes from the JWT `restaurant_id` claim.
- Shift reads require `CashShift.View` or `CashShift.Manage`.
- Shift open and close require `CashShift.Manage`.
- Shift movements require `CashMovement.Record`.
- Cash payments require an open cashier shift for the current bill branch and link to it.
- Cancelling a linked cash payment adjusts the open shift expected cash; if the linked shift is closed, cancellation is rejected.
- The frontend cashier-shift shell route remains deferred for now; only the backend contract is added in this slice.

Read-only role and permission endpoints for admin screens:

- `GET /api/v1/admin/roles`
- `GET /api/v1/admin/roles/{roleId}`
- `GET /api/v1/admin/permissions`

Rules:

- Restaurant scope comes from the JWT `restaurant_id` claim.
- Branch read endpoints require `Branch.Manage` or `User.Manage` and are limited to the current restaurant.
- Branch mutation endpoints require `Branch.Manage` only and are limited to the current restaurant.
- Users can be created with an initial password hash only; no plaintext password is stored.
- Privileged initial passwords for `SuperAdmin`, `RestaurantOwner`, `Admin`, `AccountsUser`, and `InventoryUser` require a stronger minimum length than staff accounts.
- Staff password reset is an admin-only support action under `User.Manage`, is scoped to the current restaurant, rejects self-reset, and is audited without storing or returning plaintext passwords.
- The admin staff password reset request accepts `newPassword` plus optional `confirmPassword`; the backend rejects confirm mismatches and applies the existing password hashing and minimum-length policy before writing audit records.
- Role reads are restaurant-scoped to the current JWT `restaurant_id` claim and include system roles plus current-restaurant roles only.
- Permission catalog reads from the database and is exposed for admin UI visibility only.
- Self-signup remains deferred.
- Admin-only staff password reset is supported; public self-service recovery remains deferred.
- PIN login remains deferred.
- The user-admin frontend branch selector sources branch options from `GET /api/v1/admin/branches`; branch assignment is optional and backend validation remains authoritative.

## Required Claims

BillSoft claims used in the access token:

- `restaurant_id`
- `restaurant_code`
- `branch_id`
- `session_id`
- `permission`
- `active_role`
- `must_change_password`

Standard identity claims also include:

- `NameIdentifier`
- `Name`
- `Email` when available
- role claims using `ClaimTypes.Role`

## Token Lifetime Policy

- Access token: 15 minutes
- Refresh token: 7 days
- These defaults are configurable through `Jwt` options

## Password Policy

Password policy will be enforced when the login flow is added.

For now, the token foundation only assumes password-based authentication and does not implement password entry, validation, or reset endpoints.

Current user-admin foundation rule:

- Standard staff accounts: minimum 8 characters for initial passwords
- Privileged roles (`SuperAdmin`, `RestaurantOwner`, `Admin`, `AccountsUser`, `InventoryUser`): minimum 12 characters for initial passwords

## Notes

- JWT signing uses a symmetric secret in phase 1.
- The development signing key is fake/local only and must never be used in production.
- Normal API startup does not require a JWT signing key unless token creation is used.
- The frontend login shell currently stores the phase-1 auth session in `localStorage`; this is temporary until a safer cookie/session model is introduced.
- The frontend login route is `/login`, and protected routes send unauthenticated users there while preserving the intended return path when possible.
- After successful direct login, the frontend uses a deterministic landing route from the authenticated session roles and permissions only when no safe preserved return path exists.
- Protected-route return paths always win over landing-route defaults, and this remains a frontend-only routing change with no backend session-shape change.
- Country uses ISO 3166 alpha-2, currency uses ISO 4217, and timezone uses IANA IDs.
- User mobile numbers are normalized to E.164 before storage and lookup; raw mobile text is no longer the canonical identity key.
- The API enables CORS for the local web origin in development so browser login preflight requests succeed during local work, including the Vite fallback range when 3010 is already taken.
- `/admin/users` is a protected shell route that requires `User.Manage` in the frontend, while backend authorization remains the source of truth.
- `/admin/menu` is a protected shell route that requires `MenuCategory.Manage`, `MenuItem.Manage`, or `MenuItem.View` in the frontend, while backend authorization remains the source of truth.
- `/pos/orders` is a protected operational route that requires `Order.Create` or `Order.View` in the frontend, while backend authorization remains the source of truth.
- `/billing` is a protected cashier workspace route that requires `Billing.View`, `Billing.Manage`, or `Payment.Record` in the frontend, while backend authorization remains the source of truth.
- `/reports/daily-cash-sales` is a protected daily-report route that requires `Report.View` in the frontend, while backend authorization remains the source of truth.
- `/owner/dashboard` is a protected owner/admin route that requires `Report.View` in the frontend, while backend authorization remains the source of truth.
- `/owner/dashboard` is explicit only in this slice; it does not replace `/`, and direct login landing now uses roles and permissions without changing the backend/session shape.
- `/kitchen/tickets` is a protected operational route that requires `KitchenTicket.View`, `KitchenTicket.Manage`, or `KitchenTicket.UpdateStatus` in the frontend, while backend authorization remains the source of truth.
- `/cashier/shifts` is a protected cashier-control route that requires `CashShift.View`, `CashShift.Manage`, or `CashMovement.Record` in the frontend shell, while backend authorization remains the source of truth.
- `/admin/users` now supports create user, edit profile, replace roles, activate/deactivate actions, and optional branch assignment in the frontend shell.
- `/admin/menu` now supports category and item catalog management, activate/deactivate actions, and read-only item price history in the frontend shell.
- `/pos/orders` now supports restaurant-scoped POS order capture with branch selection, EatIn/Parcel flow, menu browsing, cart lines, draft create/update, confirm/cancel, and recent-order inspection in the frontend shell.
- `/billing` now supports list-first bill review, confirmed-order billing, bill detail and line snapshots, payment recording/cancellation, and unpaid bill cancellation in the frontend shell.
- `/reports/daily-cash-sales` now supports read-only sales, cash, receipt-print, cancellation, and cashier-variance review in the frontend shell.
- `/kitchen/tickets` now supports operational ticket queue review, ticket detail inspection, status transitions, and managed cancellation in the frontend shell.
- `/cashier/shifts` now tracks cashier shift opening, current shift lookup, cash movements, and shift closing with variance calculation in the backend foundation, and the frontend shell now exposes the operational workflow.
- Branch selection uses the branch read API and should never be hardcoded in the frontend.
