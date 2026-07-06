/**
 * verify-cash-reconciliation-report.cjs
 *
 * Playwright verification for the cash reconciliation report.
 *
 * Usage:
 *   node scripts/playwright/verify-cash-reconciliation-report.cjs
 *
 * Environment variables:
 *   BILLSOFT_BROWSER_QA_APP_URL        - app origin (default http://127.0.0.1:3010)
 *   BILLSOFT_CHROMIUM_PATH             - custom Chromium binary path
 *   BILLSOFT_BROWSER_QA_SCREENSHOT_DIR  - output dir (default output/playwright/cash-reconciliation)
 */

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
      // Keep trying.
    }
  }

  throw new Error('Unable to load Playwright. Install playwright-core or set BILLSOFT_PLAYWRIGHT_CORE_PATH.');
}

const { chromium } = resolveModule();

const repoRoot = path.resolve(__dirname, '..', '..');
const appUrl = process.env.BILLSOFT_BROWSER_QA_APP_URL ?? 'http://127.0.0.1:3010';
const chromiumPath = process.env.BILLSOFT_CHROMIUM_PATH ?? '';
const screenshotDir = process.env.BILLSOFT_BROWSER_QA_SCREENSHOT_DIR
  ?? path.join(repoRoot, 'output', 'playwright', 'cash-reconciliation');
const userDataRoot = path.join(repoRoot, '.tmp', 'playwright-user-data');

const VIEWPORTS = [
  { label: '390x844', width: 390, height: 844 },
  { label: '768x1024', width: 768, height: 1024 },
  { label: '1366x768', width: 1366, height: 768 },
];

const AUTH_SESSION = {
  accessToken: 'access-token-playwright',
  refreshToken: 'refresh-token-playwright',
  accessTokenExpiresAtUtc: '2099-06-11T10:15:00Z',
  refreshTokenExpiresAtUtc: '2099-06-18T10:15:00Z',
  userId: 'user-playwright',
  restaurantId: 'restaurant-1',
  restaurantCode: 'DEMO',
  countryCode: 'SG',
  currencyCode: 'SGD',
  timeZoneId: 'Asia/Singapore',
  branchId: 'branch-1',
  fullName: 'QA Tester',
  mobileNumber: '9000000099',
  roles: ['Admin'],
  permissions: ['Report.View', 'Branch.Manage'],
  activeRole: 'Admin',
};

const NOW = '2026-06-23T10:00:00Z';

const BRANCHES = {
  items: [
    {
      branchId: 'branch-1',
      restaurantId: 'restaurant-1',
      name: 'Main Branch',
      address: '123 Market Street',
      phone: '60000000',
      timezone: 'Asia/Singapore',
      currency: 'SGD',
      status: 'Active',
      createdAt: NOW,
      updatedAt: NOW,
    },
  ],
};

const CASH_RECONCILIATION_REPORT = {
  restaurantId: 'restaurant-1',
  restaurantName: 'Demo Restaurant',
  branchId: 'branch-1',
  branchName: 'Main Branch',
  businessDate: '2026-06-23',
  generatedAtUtc: NOW,
  currencyCode: 'SGD',
  totals: {
    shiftCount: 2,
    openShiftCount: 1,
    closedShiftCount: 1,
    openingCashTotal: 150,
    cashPaymentTotal: 110,
    cashInTotal: 20,
    cashOutTotal: 10,
    adjustmentTotal: 5,
    expectedCashTotal: 275,
    declaredCashTotal: 280,
    varianceTotal: 5,
    majorVarianceCount: 0,
    minorVarianceCount: 1,
    balancedShiftCount: 0,
  },
  shifts: [
    {
      cashierShiftId: 'shift-1',
      branchId: 'branch-1',
      branchName: 'Main Branch',
      cashierUserId: 'user-1',
      cashierName: 'Asha',
      status: 'Closed',
      openedAt: '2026-06-23T02:00:00Z',
      closedAt: '2026-06-23T10:00:00Z',
      openingCashAmount: 100,
      cashPaymentTotal: 90,
      cashInTotal: 20,
      cashOutTotal: 10,
      adjustmentTotal: 5,
      expectedCashAmount: 205,
      declaredClosingCashAmount: 210,
      varianceAmount: 5,
      varianceStatus: 'MinorVariance',
      paymentCount: 2,
      movementCount: 3,
      closingNote: 'Counted at close',
    },
    {
      cashierShiftId: 'shift-2',
      branchId: 'branch-1',
      branchName: 'Main Branch',
      cashierUserId: 'user-2',
      cashierName: 'Mohan',
      status: 'Open',
      openedAt: '2026-06-23T06:00:00Z',
      closedAt: null,
      openingCashAmount: 50,
      cashPaymentTotal: 20,
      cashInTotal: 0,
      cashOutTotal: 0,
      adjustmentTotal: 0,
      expectedCashAmount: 70,
      declaredClosingCashAmount: null,
      varianceAmount: null,
      varianceStatus: 'OpenShift',
      paymentCount: 1,
      movementCount: 0,
      closingNote: null,
    },
  ],
};

function ensureDir(dirPath) {
  fs.mkdirSync(dirPath, { recursive: true });
}

function json(response, status = 200) {
  return {
    status,
    contentType: 'application/json',
    body: JSON.stringify(response),
  };
}

async function setSession(page) {
  await page.addInitScript(({ sessionKey, languageKey, session }) => {
    localStorage.setItem(languageKey, 'en');
    localStorage.setItem(sessionKey, JSON.stringify(session));
  }, {
    sessionKey: 'billsoft.auth.session.v1',
    languageKey: 'billsoft.language',
    session: AUTH_SESSION,
  });
}

async function setupRoutes(page) {
  await page.route('**/api/v1/admin/branches**', route => route.fulfill(json(BRANCHES)));
  await page.route('**/api/v1/reports/cash-reconciliation**', route => route.fulfill(json(CASH_RECONCILIATION_REPORT)));
}

async function checkNoOverflow(page, routeLabel, viewportLabel) {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
  if (overflow) {
    throw new Error(`Horizontal overflow detected on ${routeLabel} at ${viewportLabel}`);
  }
}

async function verifyRoute(page, viewportLabel) {
  await page.goto(`${appUrl}/reports/cash-reconciliation?businessDate=2026-06-23&branchId=branch-1`, { waitUntil: 'networkidle', timeout: 30000 });
  await page.locator('h1.page-header__title').getByText('Cash reconciliation').waitFor({ state: 'visible', timeout: 30000 });
  await page.getByRole('button', { name: /refresh/i }).waitFor({ state: 'visible', timeout: 30000 });
  await checkNoOverflow(page, 'cash-reconciliation', viewportLabel);

  const screenshotPath = path.join(screenshotDir, viewportLabel, 'cash-reconciliation.png');
  ensureDir(path.dirname(screenshotPath));
  await page.screenshot({ path: screenshotPath, fullPage: true });
  console.log('SCREENSHOT', screenshotPath);
  console.log('PASS', viewportLabel);
}

async function main() {
  ensureDir(userDataRoot);
  ensureDir(screenshotDir);

  const userDataDir = path.join(userDataRoot, `cash-reconciliation-${Date.now()}`);
  ensureDir(userDataDir);

  const context = await chromium.launchPersistentContext(userDataDir, {
    ...(chromiumPath ? { executablePath: chromiumPath } : {}),
    headless: true,
    args: [
      '--disable-gpu',
      '--disable-software-rasterizer',
      '--disable-dev-shm-usage',
      '--no-first-run',
      '--no-default-browser-check',
    ],
    viewport: VIEWPORTS[0],
  });

  try {
    const page = context.pages()[0] ?? await context.newPage();

    await page.goto('about:blank');
    await setSession(page);
    await setupRoutes(page);

    for (const viewport of VIEWPORTS) {
      await page.setViewportSize({ width: viewport.width, height: viewport.height });
      await verifyRoute(page, viewport.label);
    }
  } finally {
    await context.close();
  }
}

main().catch(error => {
  console.error('QA_FAIL', error && error.stack ? error.stack : String(error));
  process.exitCode = 1;
});
