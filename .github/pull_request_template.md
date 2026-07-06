# Pull Request

## Summary

Describe the change and why it is needed.

## Linked Requirement / Issue

- Closes: <!-- issue number -->

## BillSoft Checklist

- [ ] Requirement or issue is linked.
- [ ] Product behavior matches `docs/requirements/product-requirements.md`.
- [ ] Database impact reviewed.
- [ ] Permission impact reviewed.
- [ ] Audit impact reviewed.
- [ ] Tests added or updated where applicable.
- [ ] Documentation updated where applicable.

## Non-Negotiable Controls

- [ ] No hard delete introduced for business records.
- [ ] No direct stock update without `StockMovements`.
- [ ] Vendor bill OCR does not update inventory without user confirmation.
- [ ] Manual override requires reason and audit record.
- [ ] Bill number remains immutable after issue.
- [ ] Price changes are recorded in price history.
- [ ] Cash drawer difference remains visible to owner/admin.

## Tests Run

```text
<!-- commands and result -->
```

## Risks / Assumptions

List any known risks, assumptions, or deferred follow-up work.
