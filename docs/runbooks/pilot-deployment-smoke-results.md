# BillSoft Pilot Deployment Smoke Results

## Smoke Run Metadata

- Tested commit SHA: `25bc6f1`
- Environment tested: local workspace only, live deployed pilot smoke not executed
- Operator: `Codex`
- Date: `2026-06-25`

## Executive Verdict

- NO-GO

Reason:

- live deployment smoke was not executed because the deployed runtime values and pilot credentials were not available in this shell session
- Azure OCR live smoke was therefore not verified

## Smoke Checks

| Check | Status | Notes |
|---|---|---|
| Staff login with restaurant code + mobile + password | NOT RUN | Missing live pilot credentials and deployed base URL. |
| Owner dashboard loads | NOT RUN | Missing live pilot credentials and deployed base URL. |
| Daily cash sales report loads | NOT RUN | Missing live pilot credentials and deployed base URL. |
| POS order create/edit/confirm/cancel | NOT RUN | Not executed against a live deployment. |
| Confirmed POS order creates kitchen ticket | NOT RUN | Not executed against a live deployment. |
| Bill creation from confirmed order only | NOT RUN | Not executed against a live deployment. |
| Cash payment requires current cashier open shift | NOT RUN | Not executed against a live deployment. |
| Vendor bill OCR upload/extract/review/save | NOT RUN | `BILLSOFT_BASE_URL`, OCR sample file, and live OCR runtime settings were not available. |
| Vendor statement / dues summary loads | NOT RUN | Missing live pilot credentials and deployed base URL. |

## Azure OCR Live Smoke Result

- NOT RUN

## Required Runtime Values Missing

The following values were not available in this session:

- `BILLSOFT_BASE_URL`
- `BILLSOFT_RESTAURANT_CODE`
- `BILLSOFT_STAFF_MOBILE`
- `BILLSOFT_STAFF_PASSWORD`
- `BILLSOFT_OWNER_MOBILE`
- `BILLSOFT_OWNER_PASSWORD`
- `BILLSOFT_OCR_SAMPLE_FILE`

## Validation Results

| Command | Result |
|---|---|
| `pnpm --dir src/web test` | PASS |
| `pnpm --dir src/web run typecheck` | PASS |
| `pnpm --dir src/web run build` | PASS |
| `dotnet test BillSoft.sln` | PASS |

## Blockers

- No deployed pilot URL was provided to this shell.
- No live pilot credentials were available.
- No OCR sample file path was available.
- Live Azure OCR runtime access was not present, so the live OCR path could not be verified.

## GO / NO-GO Verdict

- NO-GO

