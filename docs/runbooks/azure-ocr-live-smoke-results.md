# Azure OCR Live Smoke Results

Date: 2026-06-19

## Result

Blocked. Live Azure Document Intelligence smoke could not be completed from this shell because the secure OCR runtime settings were not present in the current process environment and no repo-local secret source was available.

## Runtime Config Source

- No `Ocr__*` environment variables were present in the current shell.
- No repo-local `.env` file or user-secrets source was available for the Azure OCR settings.
- `src/api/BillSoft.Api/appsettings.Development.json` contains only placeholder OCR settings.

## Known-Good Bill Smoke

- Not run.

## Malformed Document Smoke

- Not run.

## Review and Confirmation

- Not run.

## No Auto-Payment / No Auto-Settlement

- Not verified in live Azure smoke because the upload/confirm flow was not run.

## Stock Movement Timing

- Not verified in live Azure smoke because the upload/confirm flow was not run.

## Validation Results

- `pnpm --dir src/web run typecheck`: PASS
- `pnpm --dir src/web run build`: PASS
- `dotnet test BillSoft.sln`: PASS
- `pnpm --dir src/web test`: FAIL in this run
  - Observed failures were in unrelated frontend tests, including timeout and assertion flakes in inventory, login, kitchen, vendor, POS, menu, and app route suites.

## Notes

- The OCR-specific frontend and backend tests added for the Azure provider work remain covered by the codebase, but the live Azure smoke still requires secure endpoint and key access from the target runtime.
- No secrets were printed, committed, or staged.

## Remaining Issues

- Secure Azure OCR runtime settings must be supplied to the target API runtime before live smoke can proceed.
- The full frontend suite is currently not stable in this run and needs separate follow-up if it is treated as a release gate.

