'use strict';

const fs = require('fs');
const path = require('path');

function resolveModule() {
  const candidates = [
    process.env.BILLSOFT_PLAYWRIGHT_CORE_PATH,
    process.env.BILLSOFT_PLAYWRIGHT_PACKAGE,
    'playwright-core',
    'playwright',
  ].filter(Boolean);

  for (const candidate of candidates) {
    try {
      return require(candidate);
    } catch {
      // Try the next candidate.
    }
  }

  throw new Error('Unable to load Playwright. Install playwright-core or set BILLSOFT_PLAYWRIGHT_CORE_PATH.');
}

const { chromium } = resolveModule();

const repoRoot = path.resolve(__dirname, '..', '..');
const appUrl = process.env.BILLSOFT_BROWSER_QA_APP_URL ?? 'http://127.0.0.1:3010';
const chromiumPath = process.env.BILLSOFT_CHROMIUM_PATH ?? '';
const screenshotDir = process.env.BILLSOFT_BROWSER_QA_SCREENSHOT_DIR
  ?? path.join(repoRoot, 'output', 'playwright', 'vendor-payables-validation-87-90');

const AUTH_SESSION_KEY = 'billsoft.auth.session.v1';
const LANGUAGE_KEY = 'billsoft.language';

const VIEWPORTS = [
  { label: '390x844', width: 390, height: 844 },
  { label: '768x1024', width: 768, height: 1024 },
  { label: '1366x768', width: 1366, height: 768 },
];

const DEMO_BRANCH = {
  branchId: 'branch-1',
  restaurantId: 'restaurant-1',
  name: 'Main Branch',
  address: '123 Market Street',
  phone: '60000000',
  timezone: 'Asia/Singapore',
  currency: 'SGD',
  status: 'Active',
  createdAt: '2026-06-11T09:00:00Z',
  updatedAt: '2026-06-11T09:30:00Z',
};

const DEMO_VENDOR = {
  vendorId: 'vendor-1',
  restaurantId: 'restaurant-1',
  branchId: 'branch-1',
  name: 'Fresh Rice',
  normalizedName: 'FRESH RICE',
  vendorType: 'Groceries',
  contactName: 'Kumar',
  mobileNumber: '90010001',
  address: 'Market Road',
  notes: null,
  isActive: true,
  createdAtUtc: '2026-06-11T09:00:00Z',
  updatedAtUtc: null,
};

const DEMO_BILL = {
  vendorBillId: 'bill-1',
  restaurantId: 'restaurant-1',
  branchId: 'branch-1',
  vendorId: 'vendor-1',
  vendorName: 'Fresh Rice',
  vendorType: 'Groceries',
  billNumber: 'VB-010',
  billDate: '2026-06-11T00:00:00Z',
  dueDate: null,
  status: 'PartiallyPaid',
  totalAmount: 100,
  paidAmount: 40,
  balanceAmount: 60,
  notes: 'Morning purchase',
  cancelledAtUtc: null,
  cancelledByUserId: null,
  cancellationReason: null,
  createdAtUtc: '2026-06-11T09:00:00Z',
  updatedAtUtc: null,
  lines: [
    {
      vendorBillLineId: 'line-1',
      inventoryItemId: null,
      inventoryItemName: null,
      inventoryMovementId: null,
      description: 'Rice',
      quantity: 10,
      unitCost: 10,
      lineTotal: 100,
      createdAtUtc: '2026-06-11T09:00:00Z',
      updatedAtUtc: null,
    },
  ],
  settlements: [],
};

const DEMO_VENDOR_STATEMENT = {
  restaurantId: 'restaurant-1',
  branchId: 'branch-1',
  branchName: 'Main Branch',
  vendorId: 'vendor-1',
  vendorName: 'Fresh Rice',
  vendorType: 'Groceries',
  currencyCode: 'SGD',
  fromDate: '2026-06-01',
  toDate: '2026-06-30',
  generatedAt: '2026-06-11T09:00:00Z',
  openingOutstandingAmount: 0,
  currentOutstandingAmount: 60,
  summary: {
    totalBillAmount: 100,
    totalSettlementAmount: 40,
    payableBillCount: 1,
    settlementCount: 1,
    overdueBillCount: 0,
  },
  payableBills: [
    {
      vendorBillId: 'bill-1',
      branchId: 'branch-1',
      branchName: 'Main Branch',
      billNumber: 'VB-010',
      billDate: '2026-06-11T00:00:00Z',
      dueDate: null,
      status: 'PartiallyPaid',
      totalAmount: 100,
      paidAmount: 40,
      outstandingAmount: 60,
      notes: 'Morning purchase',
      createdAtUtc: '2026-06-11T09:00:00Z',
    },
  ],
  settlements: [
    {
      vendorSettlementId: 'settlement-1',
      vendorBillId: 'bill-1',
      branchId: 'branch-1',
      branchName: 'Main Branch',
      billNumber: 'VB-010',
      paidAtUtc: '2026-06-11T09:30:00Z',
      paymentMode: 'Cash',
      amount: 40,
      referenceNumberMasked: null,
      notes: null,
      previousOutstandingAmount: 100,
      newOutstandingAmount: 60,
      status: 'Active',
    },
  ],
  timeline: [
    {
      entryType: 'Settlement',
      timestampUtc: '2026-06-11T09:30:00Z',
      billNumber: 'VB-010',
      reference: null,
      description: 'Settlement recorded',
      debitAmount: 0,
      creditAmount: 40,
      runningBalance: 60,
      paymentMode: 'Cash',
      status: 'Active',
    },
  ],
};

function ensureDir(dirPath) {
  fs.mkdirSync(dirPath, { recursive: true });
}

function jsonResponse(body, init = {}) {
  return {
    status: init.status ?? 200,
    contentType: 'application/json',
    body: JSON.stringify(body),
  };
}

function problemResponse(detail, status = 400, title = 'Bad Request') {
  return {
    status,
    contentType: 'application/problem+json',
    body: JSON.stringify({
      type: 'https://datatracker.ietf.org/doc/html/rfc7807',
      title,
      status,
      detail,
    }),
  };
}

function authSession(permissions, language = 'en') {
  return {
    accessToken: 'access-token-playwright',
    refreshToken: 'refresh-token-playwright',
    accessTokenExpiresAtUtc: '2099-06-11T10:15:00Z',
    refreshTokenExpiresAtUtc: '2099-06-18T10:15:00Z',
    userId: 'user-playwright',
    restaurantId: 'restaurant-1',
    restaurantCode: 'BILL01',
    countryCode: 'SG',
    currencyCode: 'SGD',
    timeZoneId: 'Asia/Singapore',
    branchId: 'branch-1',
    fullName: 'QA Tester',
    mobileNumber: '9000000099',
    roles: ['Admin'],
    permissions,
    activeRole: 'Admin',
    language,
  };
}

async function seedAuth(context, permissions, language) {
  await context.addInitScript(
    ({ authKey, languageKey, authValue, languageValue }) => {
      localStorage.setItem(authKey, authValue);
      localStorage.setItem(languageKey, languageValue);
    },
    {
      authKey: AUTH_SESSION_KEY,
      languageKey: LANGUAGE_KEY,
      authValue: JSON.stringify(authSession(permissions, language)),
      languageValue: language,
    }
  );

  await context.addInitScript(() => {
    const original = HTMLElement.prototype.scrollIntoView;
    window.__scrollIntoViewCalls = [];

    HTMLElement.prototype.scrollIntoView = function (...args) {
      const label = this.getAttribute('aria-label') || this.getAttribute('role') || this.tagName;
      window.__scrollIntoViewCalls.push({
        label,
        text: (this.textContent || '').trim().slice(0, 120),
      });

      if (typeof original === 'function') {
        return original.apply(this, args);
      }

      return undefined;
    };
  });
}

async function assertNoOverflow(page, label) {
  const hasOverflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth + 1);
  if (hasOverflow) {
    throw new Error(`Horizontal overflow detected on ${label}.`);
  }
}

async function assertNoRawProblemText(page, scopeLabel) {
  const text = (await page.locator(scopeLabel).textContent()) ?? '';
  if (text.includes('datatracker.ietf.org') || text.includes('"type"') || text.includes('"detail"') || text.includes('{')) {
    throw new Error(`Raw problem JSON leaked in ${scopeLabel}: ${text}`);
  }
}

async function screenshot(page, scenario, viewportLabel, language) {
  const dir = path.join(screenshotDir, language, scenario, viewportLabel);
  ensureDir(dir);
  const file = path.join(dir, 'page.png');
  await page.screenshot({ path: file, fullPage: true });
  console.log('SCREENSHOT', file);
}

async function openPage(context, route, headingName) {
  const page = await context.newPage();
  await page.goto(`${appUrl}${route}`, { waitUntil: 'networkidle', timeout: 30000 });
  await page.getByRole('heading', { name: headingName }).waitFor({ state: 'visible', timeout: 30000 });
  return page;
}

async function getSelectByFieldLabel(page, label) {
  const selects = page.locator('select');
  const count = await selects.count();

  for (let index = 0; index < count; index += 1) {
    const select = selects.nth(index);
    const fieldLabel = await select.evaluate(element => {
      const field = element.closest('.ui-field');
      return field?.querySelector('.ui-field__label')?.textContent?.trim() ?? null;
    });

    if (fieldLabel === label) {
      return select;
    }
  }

  throw new Error(`Could not find a select labeled "${label}".`);
}

async function verifyBranchDuplicateMobile(browser, viewport, language) {
  const context = await browser.newContext({ viewport });
  try {
    await seedAuth(context, ['Branch.Manage'], language);
    await context.route('**/api/v1/**', async route => {
      const request = route.request();
      const url = new URL(request.url());
      const method = request.method().toUpperCase();
      const { pathname } = url;

      if (method === 'GET' && pathname === '/api/v1/admin/branches') {
        return route.fulfill(jsonResponse({ items: [DEMO_BRANCH] }));
      }

      if (method === 'POST' && pathname === '/api/v1/admin/branches') {
        return route.fulfill(problemResponse('Branch mobile number already exists.'));
      }

      throw new Error(`Unhandled request: ${method} ${pathname}`);
    });

    const page = await openPage(context, '/admin/branches', language === 'ta' ? 'கிளை நிர்வாகம்' : 'Branch management');

    await page.getByRole('button', { name: language === 'ta' ? 'புதிய கிளை' : 'New Branch' }).click();
    await page.getByPlaceholder('Main Outlet').fill('Harbour Branch');
    await page.getByPlaceholder('60000001').fill('60000001');
    await page.getByRole('button', { name: language === 'ta' ? 'கிளையை உருவாக்கு' : 'Create branch' }).click();

    const alert = page.getByRole('alert');
    await alert.waitFor({ state: 'visible', timeout: 30000 });
    const alertText = (await alert.textContent()) ?? '';
    if (!alertText.includes('Branch mobile number already exists.')) {
      throw new Error(`Unexpected branch alert text: ${alertText}`);
    }

    await assertNoRawProblemText(page, 'body');
    await assertNoOverflow(page, `branches-${language}-${viewport.label}`);
    await screenshot(page, 'branches-duplicate-mobile', viewport.label, language);
  } finally {
    await context.close();
  }
}

async function verifyVendorDuplicateErrors(browser, viewport) {
  const context = await browser.newContext({ viewport });
  try {
    await seedAuth(context, ['Branch.Manage', 'VendorBill.Confirm', 'VendorPayment.Create'], 'en');
    let vendorPostCount = 0;

    await context.route('**/api/v1/**', async route => {
      const request = route.request();
      const url = new URL(request.url());
      const method = request.method().toUpperCase();
      const { pathname } = url;

      if (method === 'GET' && pathname === '/api/v1/admin/branches') {
        return route.fulfill(jsonResponse({ items: [DEMO_BRANCH] }));
      }

      if (method === 'GET' && pathname === '/api/v1/vendors') {
        return route.fulfill(jsonResponse({ items: [] }));
      }

      if (method === 'GET' && pathname === '/api/v1/vendor-bills') {
        return route.fulfill(jsonResponse({ items: [] }));
      }

      if (method === 'POST' && pathname === '/api/v1/vendors') {
        vendorPostCount += 1;
        return route.fulfill(
          problemResponse(
            vendorPostCount === 1
              ? 'Vendor name already exists in this scope.'
              : 'Vendor mobile number already exists.'
          )
        );
      }

      throw new Error(`Unhandled request: ${method} ${pathname}`);
    });

    const page = await openPage(context, '/vendors', 'Vendor workspace');

    await page.getByRole('button', { name: 'Create vendor' }).click();
    await page.getByPlaceholder('Fresh Rice').fill('Fresh Rice');
    await page.getByLabel('Vendor type').selectOption('Groceries');
    await page.getByPlaceholder('90010001').fill('90010001');
    await page.getByRole('button', { name: 'Create vendor' }).click();

    const alert = page.getByRole('alert');
    await alert.waitFor({ state: 'visible', timeout: 30000 });
    const firstAlert = (await alert.textContent()) ?? '';
    if (!firstAlert.includes('Vendor name already exists in this scope.')) {
      throw new Error(`Unexpected vendor name alert text: ${firstAlert}`);
    }

    await page.getByPlaceholder('Fresh Rice').fill('Fresh Rice 2');
    await page.getByRole('button', { name: 'Create vendor' }).click();
    await alert.waitFor({ state: 'visible', timeout: 30000 });
    const secondAlert = (await alert.textContent()) ?? '';
    if (!secondAlert.includes('Vendor mobile number already exists.')) {
      throw new Error(`Unexpected vendor mobile alert text: ${secondAlert}`);
    }

    await assertNoRawProblemText(page, 'body');
    await assertNoOverflow(page, `vendors-create-${viewport.label}`);
    await screenshot(page, 'vendors-duplicate-create-errors', viewport.label, 'en');
  } finally {
    await context.close();
  }
}

async function verifyVendorBillFlow(browser, viewport) {
  const context = await browser.newContext({ viewport });
  try {
    await seedAuth(context, ['Branch.Manage', 'VendorBill.Confirm', 'VendorPayment.Create'], 'en');
    let billPostCount = 0;

    await context.route('**/api/v1/**', async route => {
      const request = route.request();
      const url = new URL(request.url());
      const method = request.method().toUpperCase();
      const { pathname } = url;

      if (method === 'GET' && pathname === '/api/v1/admin/branches') {
        return route.fulfill(jsonResponse({ items: [DEMO_BRANCH] }));
      }

      if (method === 'GET' && pathname === '/api/v1/vendors') {
        return route.fulfill(
          jsonResponse({
            items: [DEMO_VENDOR],
          })
        );
      }

      if (method === 'GET' && pathname === '/api/v1/vendors/vendor-1/statement') {
        return route.fulfill(jsonResponse(DEMO_VENDOR_STATEMENT));
      }

      if (method === 'GET' && pathname === '/api/v1/vendor-bills') {
        return route.fulfill(
          jsonResponse({
            items: billPostCount === 0 ? [] : [DEMO_BILL_CREATED],
          })
        );
      }

      if (method === 'GET' && pathname === '/api/v1/vendor-bills/bill-2') {
        return route.fulfill(jsonResponse(DEMO_BILL_CREATED));
      }

      if (method === 'POST' && pathname === '/api/v1/vendor-bills') {
        billPostCount += 1;
        if (billPostCount === 1) {
          return route.fulfill(problemResponse('This bill number already exists for the selected vendor.'));
        }

        return route.fulfill(jsonResponse(DEMO_BILL_CREATED, { status: 201 }));
      }

      if (method === 'POST' && pathname === '/api/v1/vendor-bills/bill-2/settlements') {
        return route.fulfill(problemResponse('Could not record the settlement right now.'));
      }

      throw new Error(`Unhandled request: ${method} ${pathname}`);
    });

    const page = await openPage(context, '/vendors', 'Vendor workspace');

    await (await getSelectByFieldLabel(page, 'Vendor')).selectOption('vendor-1');
    await page.getByPlaceholder('VB-001').fill('VB-001');
    await page.getByLabel(/^Description$/i).fill('Rice');
    await page.getByLabel(/^Quantity$/i).fill('10');
    await page.getByLabel(/^Unit cost$/i).fill('10');
    await page.getByRole('button', { name: 'Create bill' }).click();

    const alert = page.getByRole('alert');
    await alert.waitFor({ state: 'visible', timeout: 30000 });
    const firstAlert = (await alert.textContent()) ?? '';
    if (!firstAlert.includes('This bill number already exists for the selected vendor.')) {
      throw new Error(`Unexpected bill alert text: ${firstAlert}`);
    }

    await page.getByPlaceholder('VB-001').fill('VB-002');
    await page.getByRole('button', { name: 'Create bill' }).click();
    await page.getByRole('heading', { name: /bill detail: vb-002/i }).waitFor({ state: 'visible', timeout: 30000 });

    const scrollCallCountBeforeView = await page.evaluate(() => window.__scrollIntoViewCalls.length);
    await page.getByRole('button', { name: 'View' }).first().click();

    await page.waitForFunction(previousCount => window.__scrollIntoViewCalls.length > previousCount, scrollCallCountBeforeView);
    const scrollCalls = await page.evaluate(() => window.__scrollIntoViewCalls);
    if (!scrollCalls.some(call => String(call.label).includes('scroll-target') || String(call.label).includes('Bill detail: VB-002'))) {
      throw new Error(`Scroll-to-details was not recorded: ${JSON.stringify(scrollCalls)}`);
    }

    const detailsHeading = page.getByRole('heading', { name: /bill detail: vb-002/i });
    await detailsHeading.waitFor({ state: 'visible', timeout: 30000 });
    const activeLabel = await page.evaluate(() => document.activeElement?.getAttribute('aria-label'));
    if (activeLabel !== 'Bill detail: VB-002') {
      throw new Error(`Expected details section to receive focus, saw "${activeLabel}"`);
    }

    await page.getByRole('button', { name: 'Record settlement' }).click();
    const dialog = page.getByRole('dialog', { name: 'Record vendor settlement' });
    await dialog.waitFor({ state: 'visible', timeout: 30000 });

    const dialogText = (await dialog.textContent()) ?? '';
    if (!dialogText.includes('VB-002')) {
      throw new Error(`Settlement dialog did not show the readable bill number: ${dialogText}`);
    }

    await dialog.getByLabel('Amount').fill('10');
    await dialog.getByRole('button', { name: 'Confirm settlement' }).click();

    await alert.waitFor({ state: 'visible', timeout: 30000 });
    const secondAlert = (await alert.textContent()) ?? '';
    if (!secondAlert.includes('Could not record the settlement right now.')) {
      throw new Error(`Unexpected settlement alert text: ${secondAlert}`);
    }

    await assertNoRawProblemText(page, 'body');
    await assertNoOverflow(page, `vendors-bills-${viewport.label}`);
    await screenshot(page, 'vendors-bill-flow', viewport.label, 'en');
  } finally {
    await context.close();
  }
}

async function verifyTamilSmoke(browser) {
  const context = await browser.newContext({ viewport: VIEWPORTS[0] });
  try {
    await seedAuth(context, ['Branch.Manage', 'VendorBill.Confirm', 'VendorPayment.Create'], 'ta');
    await context.route('**/api/v1/**', async route => {
      const request = route.request();
      const url = new URL(request.url());
      const method = request.method().toUpperCase();
      const { pathname } = url;

      if (method === 'GET' && pathname === '/api/v1/admin/branches') {
        return route.fulfill(jsonResponse({ items: [DEMO_BRANCH] }));
      }

      if (method === 'GET' && pathname === '/api/v1/vendors') {
        return route.fulfill(jsonResponse({ items: [DEMO_VENDOR] }));
      }

      if (method === 'GET' && pathname === '/api/v1/vendors/vendor-1/statement') {
        return route.fulfill(jsonResponse(DEMO_VENDOR_STATEMENT));
      }

      if (method === 'GET' && pathname === '/api/v1/vendor-bills') {
        return route.fulfill(jsonResponse({ items: [DEMO_BILL_CREATED] }));
      }

      if (method === 'GET' && pathname === '/api/v1/vendor-bills/bill-2') {
        return route.fulfill(jsonResponse(DEMO_BILL_CREATED));
      }

      throw new Error(`Unhandled request: ${method} ${pathname}`);
    });

    const page = await openPage(context, '/vendors', 'வெண்டர் பணியிடம்');
    await assertNoOverflow(page, 'vendors-ta-smoke');
    await screenshot(page, 'vendors-smoke', VIEWPORTS[0].label, 'ta');

    const branchesContext = await browser.newContext({ viewport: VIEWPORTS[0] });
    try {
      await seedAuth(branchesContext, ['Branch.Manage'], 'ta');
      await branchesContext.route('**/api/v1/**', async route => {
        const request = route.request();
        const url = new URL(request.url());
        const method = request.method().toUpperCase();
        const { pathname } = url;

        if (method === 'GET' && pathname === '/api/v1/admin/branches') {
          return route.fulfill(jsonResponse({ items: [DEMO_BRANCH] }));
        }

        if (method === 'POST' && pathname === '/api/v1/admin/branches') {
          return route.fulfill(problemResponse('Branch mobile number already exists.'));
        }

        throw new Error(`Unhandled request: ${method} ${pathname}`);
      });

      const branchPage = await openPage(branchesContext, '/admin/branches', 'கிளை நிர்வாகம்');
      await assertNoOverflow(branchPage, 'branches-ta-smoke');
      await screenshot(branchPage, 'branches-smoke', VIEWPORTS[0].label, 'ta');
    } finally {
      await branchesContext.close();
    }
  } finally {
    await context.close();
  }
}

const DEMO_BILL_CREATED = {
  vendorBillId: 'bill-2',
  restaurantId: 'restaurant-1',
  branchId: 'branch-1',
  vendorId: 'vendor-1',
  vendorName: 'Fresh Rice',
  vendorType: 'Groceries',
  billNumber: 'VB-002',
  billDate: '2026-06-11T00:00:00Z',
  dueDate: null,
  status: 'Unpaid',
  totalAmount: 100,
  paidAmount: 0,
  balanceAmount: 100,
  notes: 'Morning purchase',
  cancelledAtUtc: null,
  cancelledByUserId: null,
  cancellationReason: null,
  createdAtUtc: '2026-06-11T09:00:00Z',
  updatedAtUtc: null,
  lines: [
    {
      vendorBillLineId: 'line-2',
      inventoryItemId: null,
      inventoryItemName: null,
      inventoryMovementId: null,
      description: 'Rice',
      quantity: 10,
      unitCost: 10,
      lineTotal: 100,
      createdAtUtc: '2026-06-11T09:00:00Z',
      updatedAtUtc: null,
    },
  ],
  settlements: [],
};

async function main() {
  ensureDir(screenshotDir);

  const browser = await chromium.launch({
    headless: true,
    executablePath: chromiumPath || undefined,
  });

  try {
    for (const viewport of VIEWPORTS) {
      await verifyBranchDuplicateMobile(browser, viewport, 'en');
      await verifyVendorDuplicateErrors(browser, viewport);
      await verifyVendorBillFlow(browser, viewport);
    }

    await verifyTamilSmoke(browser);
  } finally {
    await browser.close();
  }

  console.log(
    'RESULT',
    JSON.stringify({
      appUrl,
      screenshotDir,
      viewports: VIEWPORTS.map(viewport => viewport.label),
      languages: ['en', 'ta'],
    })
  );
}

main().catch(error => {
  console.error('QA_FAIL', error && error.stack ? error.stack : String(error));
  process.exitCode = 1;
});
