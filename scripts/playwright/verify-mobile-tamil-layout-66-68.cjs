/**
 * verify-mobile-tamil-layout-66-68.cjs
 *
 * Browser verification for the Tamil/mobile layout hardening in issues #66-68.
 * Visits only these routes:
 *   - /owner/dashboard
 *   - /vendors
 *   - /reports/vendor-payables
 *
 * Checks:
 *   - Tamil chrome is visible on each route
 *   - card headers with actions stack vertically on narrow viewports
 *   - key headings and buttons stay visible
 *   - no horizontal overflow at 360px and 390px
 *
 * Usage:
 *   node scripts/playwright/verify-mobile-tamil-layout-66-68.cjs
 *
 * Environment variables:
 *   BILLSOFT_BROWSER_QA_APP_URL        - app origin (default http://127.0.0.1:3010)
 *   BILLSOFT_CHROMIUM_PATH             - custom Chromium binary path
 *   BILLSOFT_BROWSER_QA_SCREENSHOT_DIR  - output dir (default output/playwright/mobile-tamil-66-68)
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
  ?? path.join(repoRoot, 'output', 'playwright', 'mobile-tamil-66-68');
const userDataRoot = path.join(repoRoot, '.tmp', 'playwright-user-data');

const VIEWPORTS = [
  { label: '360x800', width: 360, height: 800 },
  { label: '390x844', width: 390, height: 844 },
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
  permissions: [
    'Report.View',
    'VendorBill.Confirm',
    'VendorPayment.Create',
    'Branch.Manage',
    'Inventory.Adjust',
  ],
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

const EMPTY_ITEMS = { items: [] };

const OWNER_DASHBOARD = {
  restaurantId: 'restaurant-1',
  restaurantCode: 'DEMO',
  restaurantName: 'Demo Restaurant',
  branchId: 'branch-1',
  branchName: 'Main Branch',
  businessDate: '2026-06-23',
  currencyCode: 'SGD',
  generatedAt: NOW,
  metrics: {
    grossSales: 44,
    netSales: 33,
    cashPayments: 10,
    nonCashPayments: 6,
    totalAmountPaid: 16,
    totalBalanceDue: 28,
    unpaidBills: 1,
    cancelledBills: 1,
    cancelledPayments: 1,
    receiptReprints: 1,
    cashVarianceTotal: 5,
    openShifts: 1,
  },
  alerts: [],
  quickLinks: [],
  vendorDues: {
    totalVendorOutstanding: 0,
    overdueVendorCount: 0,
    vendorsWithOutstandingCount: 0,
    criticalVendors: [],
  },
  inventoryAlerts: {
    lowStockCount: 0,
    outOfStockCount: 0,
    totalAlertCount: 0,
    criticalItems: [],
  },
};

const VENDOR_PAYABLES = {
  restaurantId: 'restaurant-1',
  restaurantCode: 'DEMO',
  restaurantName: 'Demo Restaurant',
  branchId: 'branch-1',
  branchName: 'Main Branch',
  fromDate: '2026-06-01',
  toDate: '2026-06-23',
  currencyCode: 'SGD',
  generatedAt: NOW,
  summary: {
    totalVendorBills: 0,
    totalPurchaseAmount: 0,
    totalPaidAmount: 0,
    totalOutstandingAmount: 0,
    unpaidBillCount: 0,
    partiallyPaidBillCount: 0,
    paidBillCount: 0,
    cancelledBillCount: 0,
    overdueBillCount: 0,
  },
  vendorBalances: [],
  overdueBills: [],
  recentSettlements: [],
  inventoryPurchaseTotals: [],
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
    localStorage.setItem(languageKey, 'ta');
    localStorage.setItem(sessionKey, JSON.stringify(session));
  }, {
    sessionKey: 'billsoft.auth.session.v1',
    languageKey: 'billsoft.language',
    session: AUTH_SESSION,
  });
}

async function setupRoutes(page) {
  await page.route('**/api/v1/admin/branches**', route => route.fulfill(json(BRANCHES)));
  await page.route('**/api/v1/inventory/items**', route => route.fulfill(json(EMPTY_ITEMS)));
  await page.route('**/api/v1/admin/menu/items**', route => route.fulfill(json(EMPTY_ITEMS)));
  await page.route('**/api/v1/admin/menu/categories**', route => route.fulfill(json(EMPTY_ITEMS)));
  await page.route('**/api/v1/menu/items**', route => route.fulfill(json(EMPTY_ITEMS)));
  await page.route('**/api/v1/menu/categories**', route => route.fulfill(json(EMPTY_ITEMS)));
  await page.route('**/api/v1/vendor-bills**', route => route.fulfill(json(EMPTY_ITEMS)));
  await page.route('**/api/v1/vendors**', route => {
    const url = new URL(route.request().url());
    if (url.pathname === '/api/v1/vendors') {
      return route.fulfill(json(EMPTY_ITEMS));
    }

    if (/\/api\/v1\/vendors\/[^/]+\/statement$/.test(url.pathname)) {
      return route.fulfill(json({
        restaurantId: 'restaurant-1',
        branchId: 'branch-1',
        vendorId: 'vendor-1',
        vendorName: 'Vendor',
        vendorType: 'Groceries',
        fromDate: '2026-06-01',
        toDate: '2026-06-23',
        currencyCode: 'SGD',
        openingOutstandingAmount: 0,
        currentOutstandingAmount: 0,
        branchName: 'Main Branch',
        summary: {
          payableBillCount: 0,
          settlementCount: 0,
          overdueBillCount: 0,
          totalBillAmount: 0,
          totalSettlementAmount: 0,
        },
        payableBills: [],
        settlements: [],
        timeline: [],
      }));
    }

    return route.fulfill(json(EMPTY_ITEMS));
  });
  await page.route('**/api/v1/reports/vendor-payables**', route => route.fulfill(json(VENDOR_PAYABLES)));
  await page.route('**/api/v1/dashboard/owner**', route => route.fulfill(json(OWNER_DASHBOARD)));
}

async function checkNoOverflow(page, routeLabel, viewportLabel) {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
  if (overflow) {
    throw new Error(`Horizontal overflow detected on ${routeLabel} at ${viewportLabel}`);
  }
}

async function checkStackedCardHeaders(page, routeLabel, viewportLabel) {
  const results = await page.locator('.ui-card__header').evaluateAll(nodes =>
    nodes.map((node, index) => {
      const title = node.querySelector('.ui-card__title');
      const actions = node.querySelector('.ui-card__actions');
      const header = node;
      const titleRect = title?.getBoundingClientRect();
      const actionsRect = actions?.getBoundingClientRect();
      const headerStyle = getComputedStyle(header);

      return {
        index,
        title: title?.textContent?.trim() ?? '',
        actionText: actions?.textContent?.replace(/\s+/g, ' ').trim() ?? '',
        headerFlexDirection: headerStyle.flexDirection,
        hasActions: Boolean(actions),
        titleWidth: titleRect?.width ?? 0,
        titleBottom: titleRect?.bottom ?? 0,
        actionsTop: actionsRect?.top ?? 0,
      };
    })
  );

  for (const result of results) {
    if (!result.hasActions) {
      continue;
    }

    if (result.headerFlexDirection !== 'column') {
      throw new Error(
        [
          `Card header is not stacked on ${routeLabel} at ${viewportLabel}.`,
          `title="${result.title}"`,
          `action="${result.actionText}"`,
          `flexDirection=${result.headerFlexDirection}`,
        ].join(' ')
      );
    }

    if (result.titleWidth <= 0) {
      throw new Error(
        [
          `Card title collapsed on ${routeLabel} at ${viewportLabel}.`,
          `title="${result.title}"`,
          `action="${result.actionText}"`,
        ].join(' ')
      );
    }

    if (result.actionsTop < result.titleBottom - 1) {
      throw new Error(
        [
          `Card actions still overlap the title on ${routeLabel} at ${viewportLabel}.`,
          `title="${result.title}"`,
          `action="${result.actionText}"`,
          `titleBottom=${result.titleBottom}`,
          `actionsTop=${result.actionsTop}`,
        ].join(' ')
      );
    }
  }
}

async function verifyRoute(page, route, viewportLabel) {
  await page.goto(`${appUrl}${route.path}`, { waitUntil: 'networkidle', timeout: 30000 });
  await route.ready(page);
  await checkNoOverflow(page, route.label, viewportLabel);
  await checkStackedCardHeaders(page, route.label, viewportLabel);

  const screenshotPath = path.join(screenshotDir, viewportLabel, `${route.label}.png`);
  ensureDir(path.dirname(screenshotPath));
  await page.screenshot({ path: screenshotPath, fullPage: true });
  console.log('SCREENSHOT', screenshotPath);
  console.log('PASS', route.label, viewportLabel);
}

async function main() {
  ensureDir(userDataRoot);
  ensureDir(screenshotDir);

  const userDataDir = path.join(userDataRoot, `mobile-tamil-66-68-${Date.now()}`);
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

    const routes = [
      {
        label: 'owner-dashboard',
        path: '/owner/dashboard',
        ready: async currentPage => {
          await currentPage.getByRole('heading', { name: /உரிமையாளர் டாஷ்போர்டு/i }).waitFor({ state: 'visible', timeout: 30000 });
          await currentPage.getByRole('button', { name: /டாஷ்போர்டை புதுப்பி/i }).waitFor({ state: 'visible', timeout: 30000 });
        },
      },
      {
        label: 'vendors',
        path: '/vendors',
        ready: async currentPage => {
          await currentPage.getByRole('heading', { name: /வெண்டர் பணியிடம்/i }).waitFor({ state: 'visible', timeout: 30000 });
          await currentPage.getByRole('heading', { name: /வெண்டர் பாக்கி அறிக்கை/i }).waitFor({ state: 'visible', timeout: 30000 });
          await currentPage.getByRole('button', { name: /வெண்டர் பாக்கி அறிக்கையை பார்/i }).waitFor({ state: 'visible', timeout: 30000 });
          await currentPage.getByRole('heading', { name: /^வெண்டர்கள்$/i }).waitFor({ state: 'visible', timeout: 30000 });
          await currentPage.getByRole('button', { name: /புதிய வெண்டர்/i }).waitFor({ state: 'visible', timeout: 30000 });
        },
      },
      {
        label: 'vendor-payables',
        path: '/reports/vendor-payables',
        ready: async currentPage => {
          await currentPage.getByRole('heading', { name: /வெண்டர் பாக்கிகள் அறிக்கை/i }).waitFor({ state: 'visible', timeout: 30000 });
          await currentPage.getByRole('button', { name: /அறிக்கையை புதுப்பி/i }).waitFor({ state: 'visible', timeout: 30000 });
        },
      },
    ];

    for (const viewport of VIEWPORTS) {
      await page.setViewportSize({ width: viewport.width, height: viewport.height });
      console.log('VIEWPORT', viewport.label);

      for (const route of routes) {
        await verifyRoute(page, route, viewport.label);
      }
    }
  } finally {
    await context.close();
  }
}

main().catch(error => {
  console.error('QA_FAIL', error && error.stack ? error.stack : String(error));
  process.exitCode = 1;
});
