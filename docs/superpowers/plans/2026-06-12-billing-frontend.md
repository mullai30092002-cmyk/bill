# Billing Frontend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a list-first `/billing` cashier workspace for listing bills, selecting a bill intentionally, creating bills from confirmed POS orders, and recording or cancelling payments without adding backend changes.

**Architecture:** Use one coordinator page that owns auth checks, API loading, selection state, notices, and mutations. Child panels render bill list, bill detail, create-bill candidates, payment entry, and status actions only. Keep the screen list-first so bill lookup and payment status stay in front of the cashier, while confirmed POS order candidates remain available but secondary.

**Tech Stack:** React, React Router, Vitest, React Testing Library, local BillSoft layout/UI primitives, existing `requestJson` API client, TypeScript.

---

### Task 1: Add billing API client and types

**Files:**
- Create: `src/web/src/features/billing/billingApi.ts`
- Create: `src/web/src/features/billing/billingTypes.ts`
- Create: `src/web/src/features/billing/billingDisplay.ts`
- Create: `src/web/src/features/billing/billingValidation.ts`
- Create: `src/web/src/features/billing/billingErrorDisplay.ts`
- Create: `src/web/src/features/billing/billingValidation.test.ts`

- [ ] **Step 1: Write the failing type-level and helper tests**

Add a focused helper test file that imports the new billing helpers and asserts:

```ts
import { describe, expect, it } from 'vitest';
import { buildBillingPaymentValidationErrors, getSafeBillingErrorMessage } from './billingValidation';

describe('billing helpers', () => {
  it('blocks amount zero and amount above balance due', () => {
    expect(buildBillingPaymentValidationErrors({ amount: '0', balanceDue: 10 })).toMatchObject({
      amount: 'Amount must be greater than 0.',
    });
    expect(buildBillingPaymentValidationErrors({ amount: '12', balanceDue: 10 })).toMatchObject({
      amount: 'Amount must not exceed the selected bill balance due.',
    });
  });

  it('sanitizes exception-like errors', () => {
    expect(
      getSafeBillingErrorMessage(new Error('Microsoft.Data.SqlClient.SqlException: invalid column name'), 'Fallback')
    ).toBe('Fallback');
  });
});
```

- [ ] **Step 2: Run the helper-focused test**

Run:

```bash
cd src/web
pnpm vitest run src/features/billing/billingValidation.test.ts
```

Expected: fail until the helper file and test file are implemented.

- [ ] **Step 3: Implement the API surface and shared billing types**

Add the full request/response contracts from the backend DTOs:

```ts
export type BillStatus = 'Unpaid' | 'PartiallyPaid' | 'Paid' | 'Cancelled';
export type PaymentStatus = 'Recorded' | 'Cancelled';
export type PaymentMode = 'Cash' | 'Card' | 'Upi' | 'Other';

export interface BillListItem {
  billId: string;
  branchId: string;
  posOrderId: string;
  billNumber: string;
  status: BillStatus;
  grandTotal: number;
  amountPaid: number;
  balanceDue: number;
  createdAt: string;
}

export interface BillDetail {
  billId: string;
  restaurantId: string;
  branchId: string;
  posOrderId: string;
  billNumber: string;
  status: BillStatus;
  subtotal: number;
  taxTotal: number;
  grandTotal: number;
  amountPaid: number;
  balanceDue: number;
  createdByUserId: string | null;
  cancelledByUserId: string | null;
  cancelledAt: string | null;
  cancelReason: string | null;
  createdAt: string;
  updatedAt: string | null;
  lines: BillLine[];
  payments: Payment[];
}

export interface BillLine {
  billLineId: string;
  posOrderLineId: string;
  menuItemId: string;
  menuCategoryId: string;
  menuItemNameSnapshot: string;
  menuCategoryNameSnapshot: string;
  skuSnapshot: string | null;
  unitPrice: number;
  taxRate: number;
  quantity: number;
  lineSubtotal: number;
  lineTax: number;
  lineTotal: number;
  notes: string | null;
  displayOrder: number;
  createdAt: string;
}

export interface Payment {
  paymentId: string;
  billId: string;
  paymentNumber: string;
  paymentMode: PaymentMode;
  status: PaymentStatus;
  amount: number;
  referenceNumber: string | null;
  notes: string | null;
  recordedByUserId: string | null;
  cancelledByUserId: string | null;
  cancelledAt: string | null;
  cancelReason: string | null;
  createdAt: string;
  updatedAt: string | null;
}
```

Wire the request helpers:

```ts
export const listBills = (query?: BillListQuery) => requestJson<BillListResponse>(`/api/v1/billing/bills${buildQueryString(query)}`);
export const getBill = (billId: string) => requestJson<BillDetail>(`/api/v1/billing/bills/${billId}`);
export const createBill = (request: CreateBillRequest) => requestJson<BillDetail>('/api/v1/billing/bills', { method: 'POST', body: request });
export const cancelBill = (billId: string, request: CancelBillRequest) => requestJson<BillDetail>(`/api/v1/billing/bills/${billId}/cancel`, { method: 'POST', body: request });
export const recordPayment = (billId: string, request: RecordPaymentRequest) => requestJson<BillDetail>(`/api/v1/billing/bills/${billId}/payments`, { method: 'POST', body: request });
export const cancelPayment = (paymentId: string, request: CancelPaymentRequest) => requestJson<BillDetail>(`/api/v1/billing/payments/${paymentId}/cancel`, { method: 'POST', body: request });
```

- [ ] **Step 4: Run the new helper/type tests**

Run:

```bash
cd src/web
pnpm vitest run src/features/billing/billingValidation.test.ts
```

Expected: pass once the helpers and types are in place.

- [ ] **Step 5: Commit the API foundation**

```bash
git add src/web/src/features/billing/billingApi.ts src/web/src/features/billing/billingTypes.ts src/web/src/features/billing/billingDisplay.ts src/web/src/features/billing/billingValidation.ts src/web/src/features/billing/billingErrorDisplay.ts src/web/src/features/billing/billingValidation.test.ts
git commit -m "feat: add billing api foundation"
```

### Task 2: Build the billing workspace coordinator and panels

**Files:**
- Create: `src/web/src/features/billing/BillingPage.tsx`
- Create: `src/web/src/features/billing/BillListPanel.tsx`
- Create: `src/web/src/features/billing/BillDetailPanel.tsx`
- Create: `src/web/src/features/billing/CreateBillPanel.tsx`
- Create: `src/web/src/features/billing/PaymentPanel.tsx`
- Create: `src/web/src/features/billing/BillStatusActions.tsx`
- Modify: `src/web/src/App.tsx`
- Modify: `src/web/src/components/layout/navigation.ts`
- Modify: `src/web/src/styles/components.css`

- [ ] **Step 1: Write the failing page test**

Create `src/web/src/features/billing/BillingPage.test.tsx` with cases that assert:

```ts
import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

it('renders a list-first billing workspace and shows the empty detail state before any bill is selected', async () => {
  // seed Billing.View or Billing.Manage session, stub fetch for bills + confirmed POS orders,
  // render /billing, assert list loads, no bill is selected, and empty detail copy is visible
});

it('blocks payment entry when the bill is paid and validates amount locally', async () => {
  // select a paid bill and verify no payment form is shown
  // for an unpaid bill, verify amount 0 and amount above balance due show local validation messages
});
```

Include one route test that checks `/billing` renders only when the user has billing permissions and does not auto-select a bill.

- [ ] **Step 2: Run the page test to confirm it fails**

Run:

```bash
cd src/web
pnpm vitest run src/features/billing/BillingPage.test.tsx
```

Expected: fail until the page and panels exist.

- [ ] **Step 3: Implement the coordinator page**

`BillingPage.tsx` should:

```tsx
export const BillingPage = ({ navItems, restaurantName, branchName, operatorLabel }: BillingPageProps) => {
  // auth gate
  // load bills
  // load confirmed POS order candidates
  // keep selectedBillId null until a bill is deliberately chosen
  // show the empty detail state copy when nothing is selected
  // create bill, record payment, cancel payment, cancel bill
  // after create: select the created bill
  // after payment/cancel success: replace the selected bill with the backend response
};
```

Coordinator responsibilities:

- list-first order of sections
- selected bill is intentional, never implicit
- bill list is primary
- create-bill candidates are secondary and visually lighter than the list/detail flow
- read-only mode hides all mutation controls
- error messages use the safe billing error helper

- [ ] **Step 4: Implement the child panels**

`BillListPanel.tsx`:

- shows newest-first bills
- exposes selection only
- shows bill number, status, totals, created date

`BillDetailPanel.tsx`:

- shows the empty detail state when no bill is selected
- shows bill summary, lines, and payments when selected
- shows cancelled reason when relevant

`CreateBillPanel.tsx`:

- lists confirmed POS orders
- offers a deliberate “create bill” action
- does not dominate the workspace above bill list/detail

`PaymentPanel.tsx`:

- renders record-payment form only for payable bills
- blocks amount `<= 0`
- blocks amount above selected bill `balanceDue`
- hides when bill is `Paid` or `Cancelled`

`BillStatusActions.tsx`:

- renders cancel bill and cancel payment controls only when allowed
- requires a reason for each cancel action

- [ ] **Step 5: Wire route and nav**

Update the app routing and nav:

```tsx
<Route path="/billing" element={<BillingPage {...workspaceProps} />} />
```

```ts
{
  label: 'Billing',
  to: '/billing',
  hint: 'Bills and payments',
  requiredPermissions: ['Billing.View', 'Billing.Manage', 'Payment.Record'],
}
```

- [ ] **Step 6: Run the page tests**

Run:

```bash
cd src/web
pnpm vitest run src/features/billing/billingValidation.test.ts src/features/billing/BillingPage.test.tsx src/App.routes.test.tsx
```

Expected: pass.

- [ ] **Step 7: Commit the workspace implementation**

```bash
git add src/web/src/features/billing/BillingPage.tsx src/web/src/features/billing/BillListPanel.tsx src/web/src/features/billing/BillDetailPanel.tsx src/web/src/features/billing/CreateBillPanel.tsx src/web/src/features/billing/PaymentPanel.tsx src/web/src/features/billing/BillStatusActions.tsx src/web/src/App.tsx src/web/src/components/layout/navigation.ts src/web/src/styles/components.css src/web/src/features/billing/BillingPage.test.tsx
git commit -m "feat: add billing workspace"
```

### Task 3: Harden billing error handling and local display helpers

**Files:**
- Create or modify: `src/web/src/features/billing/billingErrorDisplay.ts`
- Modify: `src/web/src/api/apiErrors.ts` only if the local helper needs a shared sanitizer
- Modify: `src/web/src/features/billing/BillingPage.tsx`
- Modify: `src/web/src/features/billing/BillingPage.test.tsx`

- [ ] **Step 1: Write a failing sanitization test**

Add tests that prove raw SQL or exception text is not shown:

```ts
it('sanitizes SQL-like error text in billing panels', () => {
  const message = getSafeBillingErrorMessage(
    new Error('Microsoft.Data.SqlClient.SqlException: invalid column name'),
    'Unable to load bills. Please refresh or try again.'
  );

  expect(message).toBe('Unable to load bills. Please refresh or try again.');
});
```

- [ ] **Step 2: Implement the billing error helper**

Mirror the POS helper style:

```ts
export const getSafeBillingErrorMessage = (error: unknown, fallback: string): string => {
  // treat 401 and 403 specially
  // allow short clean 4xx validation details
  // reject SQL, stack traces, token blobs, and HTML
  // clip unexpected text
};
```

- [ ] **Step 3: Re-run the billing page tests**

Run:

```bash
cd src/web
pnpm vitest run src/features/billing/BillingPage.test.tsx
```

Expected: pass with sanitized messages.

- [ ] **Step 4: Commit the error hardening**

```bash
git add src/web/src/features/billing/billingErrorDisplay.ts src/web/src/features/billing/BillingPage.tsx src/web/src/features/billing/BillingPage.test.tsx
git commit -m "feat: harden billing error handling"
```

### Task 4: Update docs and verify the frontend

**Files:**
- Modify: `docs/architecture/frontend-layouts.md`
- Modify: `docs/architecture/authentication-plan.md`
- Modify: `README.md` if the billing route is part of the high-level feature list

- [ ] **Step 1: Write the doc updates**

Document:

- `/billing`
- billing permissions
- list-first cashier workflow
- bill creation from confirmed POS orders
- payment recording and cancellation
- unpaid bill cancellation
- no gateway, cash drawer, kitchen, or inventory behavior yet

- [ ] **Step 2: Run the frontend verification suite**

Run:

```bash
cd src/web
pnpm install
pnpm run test
pnpm run typecheck
pnpm run build
```

Expected: all pass.

- [ ] **Step 3: Commit the documentation updates**

```bash
git add docs/architecture/frontend-layouts.md docs/architecture/authentication-plan.md README.md
git commit -m "docs: add billing frontend guidance"
```

### Task 5: Final repository verification and publish

**Files:**
- No new files expected

- [ ] **Step 1: Run backend sanity checks**

Run:

```bash
dotnet restore BillSoft.sln
dotnet build BillSoft.sln
dotnet test BillSoft.sln
```

Expected: all pass.

- [ ] **Step 2: Check the worktree**

Run:

```bash
git status --short
```

Expected: clean or only the intended billing frontend/doc changes if you are batching commits.

- [ ] **Step 3: Commit and push the final changes**

```bash
git add src/web docs
git commit -m "Add billing frontend"
git push origin master
```

- [ ] **Step 4: Close GitHub issue #12**

Use the exact closure comment:

```text
Billing frontend added at /billing with bill listing, confirmed POS order billing, bill detail/line/payment viewing, payment recording/cancellation, unpaid bill cancellation, and permission-aware read-only behavior. Payment gateway, cash drawer, kitchen tickets, and inventory deduction remain deferred.
```
