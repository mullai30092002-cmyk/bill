# Vendor Bill OCR Draft Workflow

## Purpose

This workflow covers the review-first OCR draft slice for BillSoft.

OCR output is untrusted until a user reviews it. OCR draft records are stored separately from trusted vendor bills. Confirmation reuses the existing vendor bill creation path, and OCR never creates settlements or stock-in movements before confirmation.

---

## Actors

| Actor | Responsibility |
|---|---|
| Owner | Uploads vendor bills, reviews extracted data, confirms or cancels drafts |
| Admin | Uploads vendor bills, reviews extracted data, confirms or cancels drafts |
| System | Stores the original document, extracts draft data, and writes audit logs |

---

## 1. Upload Vendor Bill

Owner or admin uploads a JPEG, PNG, or PDF bill file.

System creates an OCR draft record and stores the original file using the document storage abstraction.

Rules:

- uploaded files must not be stored under the public web directory
- file type and size are validated before storage
- the original copy is retained for audit and review
- OCR is provider-neutral at the application boundary
- a fake provider is available for local development and automated tests
- Azure Document Intelligence is selected only through runtime configuration
- Azure provider settings must be present before Azure mode is enabled

If OCR extraction fails, the system returns a safe draft state with a sanitized message such as:

```text
OCR service is temporarily unavailable.
```

---

## 2. Extract Draft Values

The OCR provider extracts draft values such as:

- vendor name
- bill number
- bill date
- line descriptions
- quantities
- unit costs
- line totals
- total amount
- confidence scores
- provider warnings when extraction is partial

If extraction fails, the draft remains available for review or cancellation and no vendor bill is created.
Partial extraction is allowed when the document is useful but some fields are missing. The draft stays reviewable and warning badges show what still needs attention.

For development and tests, BillSoft supports a fake OCR provider.

---

## 3. Review and Correct

User reviews the extracted draft and may correct:

- vendor
- bill number
- bill date
- line descriptions
- quantities
- unit costs
- line totals
- inventory item link per line
- ignored / non-stock line flag

Rules:

- OCR values are never trusted as final values
- user corrections are audited
- duplicate receipts are warned during review and blocked from confirmation unless override permission is available
- cross-restaurant and cross-branch access is rejected
- no settlement or payment is created in this step
- no stock-in movement is created in this step
- ignored lines remain in the bill draft but do not create stock-in movements on confirmation

---

## 4. Confirm Vendor Bill

After review, the user explicitly confirms the draft.

Confirmation:

- reuses the existing vendor bill creation path
- creates the trusted vendor bill and bill lines
- may create or link stock-in movements only through the existing vendor bill rules
- does not create a settlement or payment

After confirmation, the OCR draft is marked confirmed and linked to the created vendor bill.

---

## 5. Cancel Draft

A draft may be cancelled before confirmation.

Rules:

- cancellation preserves audit history
- cancellation does not remove the stored original file
- confirmed drafts cannot be reconfirmed

---

## Runtime Configuration

The current implementation uses these runtime settings:

- `Ocr:Provider` - `Fake` or `AzureDocumentIntelligence`
- `Ocr:MaxUploadBytes` - upload cap in bytes
- `Ocr:StorageRootPath` - local storage root for uploaded documents
- `Ocr:AzureDocumentIntelligence:Endpoint` - required only when Azure mode is selected
- `Ocr:AzureDocumentIntelligence:ApiKey` - required only when Azure mode is selected
- `Ocr:AzureDocumentIntelligence:ModelId` - required only when Azure mode is selected

If `Ocr:Provider` is `AzureDocumentIntelligence` and any Azure setting is missing, startup validation fails fast.
Raw Azure errors are sanitized into draft-safe messages and warning labels. No raw endpoint, key, request ID, or stack trace is shown in the UI or audit logs.

---

## Audit Events

BillSoft writes audit logs for:

- OCR draft uploaded
- OCR draft extracted
- OCR draft extraction failed
- OCR draft updated
- OCR draft confirmed
- OCR draft cancelled
- OCR draft partial extraction warnings are stored with the draft and surfaced on review

---

## Acceptance Criteria

1. Uploaded bill files are retained for audit/reference.
2. OCR data is stored separately from confirmed vendor bills.
3. User review is required before confirmation.
4. User corrections are audited.
5. Confirmation reuses the trusted vendor bill creation path.
6. OCR cannot create settlements or payments.
7. OCR cannot create stock-in movements before confirmation.
8. Missing OCR configuration fails safely.
9. Fake OCR is available for development and tests.
10. Partial OCR extraction remains reviewable with warnings.
