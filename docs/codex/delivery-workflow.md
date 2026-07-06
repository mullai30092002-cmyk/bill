# BillSoft Codex Delivery Workflow

This file records the repo-specific closeout routine Codex should follow when a task is finished in `C:\MyDrive\repos\billsoft`.

## Default flow

1. Read `AGENTS.md`, the linked issue, and the relevant docs before editing code.
2. Keep the change scoped to the issue.
3. Run the smallest relevant tests first, then the broader validation stack if files changed.
4. Inspect `git status --short` before staging.
5. Stage only the files that belong to the task.
6. Commit the changes with a direct message.
7. Push the branch.
8. Add the requested issue comment.
9. Close the issue when the work is complete.

## Standard validation order

Use the repository's normal validation order unless the issue says otherwise:

```text
pnpm run test
pnpm run typecheck
pnpm run build
dotnet restore BillSoft.sln
dotnet build BillSoft.sln
dotnet test BillSoft.sln
```

If only backend files changed, run the relevant backend tests and then the solution build/test pass.
If only frontend files changed, run the relevant frontend tests and then typecheck/build.

## GitHub closeout

BillSoft issue work usually ends with:

- a concise issue comment that states what was verified or fixed
- the issue closed as completed when the requested work is done

Use the repository's existing GitHub connector or CLI tools to perform those actions.

## Local setup reminder

- API local URL: `http://localhost:5000`
- Web local URL: `http://localhost:3010`
- Demo login:
  - Restaurant code: `DEMO`
  - Mobile: `90000001`
  - Password: `DemoOwner123!`

## Notes

- Do not expand scope beyond the issue without a clear request.
- Do not add unrelated refactors.
- Preserve auditability and traceability for billing, cash-control, inventory, and vendor flows.
