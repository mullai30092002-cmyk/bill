# Country, Currency, Timezone, and Mobile Foundation

BillSoft is being prepared for a multi-country architecture while still rolling out one country at a time.

## Goals

- Make restaurant and branch country, currency, and timezone explicit.
- Store user mobile numbers canonically in E.164 format.
- Keep login UX as `RestaurantCode + MobileNumber + Password`.
- Normalize the supplied mobile number using the restaurant country context before lookup.
- Keep the implementation small and deterministic until broader localization is needed.

## Current Supported Profiles

- Singapore: `SG`, `SGD`, `Asia/Singapore`, `+65`
- India: `IN`, `INR`, `Asia/Kolkata`, `+91`

## Pilot Default

- New restaurant and branch records default to `IN` / `INR` / `Asia/Kolkata` unless explicitly configured otherwise.

## Mobile Rules

- Raw mobile text is not the canonical lookup key.
- Canonical lookup uses `MobileE164`.
- User uniqueness is scoped by `RestaurantId + MobileE164`.
- Display can continue to use the national number form.

## Deferred

- Tax engine
- GST/VAT/GSTIN compliance
- Receipt legal-format engine
- Translation/localization engine
- Payment gateway localization
- Country rollout automation
