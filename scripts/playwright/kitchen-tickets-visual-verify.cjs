/**
 * kitchen-tickets-visual-verify.cjs
 *
 * Visual verification script for /kitchen/tickets across 6 viewports and 11 scenarios.
 * Auth is injected via addInitScript (no real backend needed).
 * All /api/v1/kitchen/ calls are intercepted and fulfilled with realistic mock data.
 *
 * Usage:
 *   node scripts/playwright/kitchen-tickets-visual-verify.cjs
 *
 * Environment variables:
 *   BILLSOFT_BROWSER_QA_APP_URL        — app origin (default http://localhost:3010)
 *   BILLSOFT_CHROMIUM_PATH             — custom Chromium binary path
 *   BILLSOFT_BROWSER_QA_SCREENSHOT_DIR — output dir (default output/playwright/kitchen-tickets)
 */

'use strict';

const fs   = require('fs');
const path = require('path');

// ── Playwright resolution ─────────────────────────────────────────────────────
function resolveModule() {
  const candidates = [
    process.env.BILLSOFT_PLAYWRIGHT_CORE_PATH,
    process.env.BILLSOFT_PLAYWRIGHT_PACKAGE,
    'playwright-core',
    'playwright',
  ].filter(Boolean);
  for (const candidate of candidates) {
    try { return require(candidate); } catch { /* try next */ }
  }
  throw new Error(
    'Unable to load Playwright. Set BILLSOFT_PLAYWRIGHT_CORE_PATH or install playwright-core.'
  );
}

const { chromium } = resolveModule();

// ── Config ───────────────────────────────────────────────────────────────────
const repoRoot      = path.resolve(__dirname, '..', '..');
const appUrl        = process.env.BILLSOFT_BROWSER_QA_APP_URL ?? 'http://localhost:3010';
const chromiumPath  = process.env.BILLSOFT_CHROMIUM_PATH ?? '';
const screenshotDir = process.env.BILLSOFT_BROWSER_QA_SCREENSHOT_DIR
  ?? path.join(repoRoot, 'output', 'playwright', 'kitchen-tickets');
const userDataRoot  = path.join(repoRoot, '.tmp', 'playwright-user-data');

const VIEWPORTS = [
  { label: '1366x768',  width: 1366, height: 768  },
  { label: '1280x720',  width: 1280, height: 720  },
  { label: '1024x768',  width: 1024, height: 768  },
  { label: '768x1024',  width: 768,  height: 1024 },
  { label: '430x932',   width: 430,  height: 932  },
  { label: '390x844',   width: 390,  height: 844  },
];

// ── Auth session (mirrors authTestUtils.ts) ───────────────────────────────────
const AUTH_SESSION = {
  accessToken:              'access-token-playwright',
  refreshToken:             'refresh-token-playwright',
  accessTokenExpiresAtUtc:  '2099-06-11T10:15:00Z',
  refreshTokenExpiresAtUtc: '2099-06-18T10:15:00Z',
  userId:                   'user-playwright',
  restaurantId:             'restaurant-1',
  restaurantCode:           'BILL01',
  countryCode:              'IN',
  currencyCode:             'INR',
  timeZoneId:               'Asia/Kolkata',
  branchId:                 null,
  fullName:                 'Kitchen Tester',
  mobileNumber:             '9000000099',
  roles:                    ['KitchenUser'],
  permissions:              ['KitchenTicket.View', 'KitchenTicket.UpdateStatus', 'KitchenTicket.Manage'],
  activeRole:               'KitchenUser',
};

// ── Mock data helpers ─────────────────────────────────────────────────────────
function makeListItem(overrides = {}) {
  const base = {
    kitchenTicketId:     overrides.kitchenTicketId     ?? 'ticket-1',
    branchId:            'branch-1',
    posOrderId:          overrides.posOrderId          ?? 'order-1',
    ticketNumber:        overrides.ticketNumber        ?? 'KIT-20260621-0001',
    orderNumberSnapshot: overrides.orderNumberSnapshot ?? 'ORD-20260621-0001',
    orderTypeSnapshot:   overrides.orderTypeSnapshot   ?? 'EatIn',
    tableNameSnapshot:   overrides.tableNameSnapshot   ?? null,
    customerNameSnapshot: overrides.customerNameSnapshot ?? null,
    orderNotesSnapshot:  overrides.orderNotesSnapshot  ?? null,
    status:              overrides.status              ?? 'Pending',
    lineCount:           overrides.lineCount           ?? 2,
    createdAt:           overrides.createdAt           ?? new Date(Date.now() - 5 * 60 * 1000).toISOString(),
    updatedAt:           null,
    cancelledAt:         overrides.cancelledAt         ?? null,
    cancelReason:        overrides.cancelReason        ?? null,
  };
  return base;
}

function makeDetail(overrides = {}) {
  return {
    kitchenTicketId:     overrides.kitchenTicketId     ?? 'ticket-1',
    restaurantId:        'restaurant-1',
    branchId:            'branch-1',
    posOrderId:          'order-1',
    ticketNumber:        overrides.ticketNumber        ?? 'KIT-20260621-0001',
    orderNumberSnapshot: overrides.orderNumberSnapshot ?? 'ORD-20260621-0001',
    orderTypeSnapshot:   overrides.orderTypeSnapshot   ?? 'EatIn',
    tableNameSnapshot:   overrides.tableNameSnapshot   ?? 'Table 3',
    customerNameSnapshot: overrides.customerNameSnapshot ?? null,
    orderNotesSnapshot:  overrides.orderNotesSnapshot  ?? null,
    status:              overrides.status              ?? 'Pending',
    createdByUserId:     'user-1',
    lastStatusChangedByUserId: 'user-1',
    cancelledByUserId:   overrides.cancelledByUserId   ?? null,
    cancelledAt:         overrides.cancelledAt         ?? null,
    cancelReason:        overrides.cancelReason        ?? null,
    createdAt:           new Date(Date.now() - 5 * 60 * 1000).toISOString(),
    updatedAt:           null,
    preparingAt:         overrides.preparingAt         ?? null,
    readyAt:             overrides.readyAt             ?? null,
    servedAt:            overrides.servedAt            ?? null,
    lines: overrides.lines ?? [
      {
        kitchenTicketLineId:     'line-1',
        posOrderLineId:          'pos-line-1',
        menuItemId:              'item-1',
        menuCategoryId:          'cat-1',
        menuItemNameSnapshot:    'Masala Dosa',
        menuCategoryNameSnapshot: 'Breakfast',
        skuSnapshot:             'DOSA-01',
        quantity:                2,
        notes:                   overrides.lineNotes ?? null,
        displayOrder:            1,
        createdAt:               new Date(Date.now() - 5 * 60 * 1000).toISOString(),
      },
      {
        kitchenTicketLineId:     'line-2',
        posOrderLineId:          'pos-line-2',
        menuItemId:              'item-2',
        menuCategoryId:          'cat-1',
        menuItemNameSnapshot:    'Idli Sambar',
        menuCategoryNameSnapshot: 'Breakfast',
        skuSnapshot:             'IDLI-01',
        quantity:                3,
        notes:                   null,
        displayOrder:            2,
        createdAt:               new Date(Date.now() - 5 * 60 * 1000).toISOString(),
      },
    ],
  };
}

const DEDUCTION_PREVIEW = {
  ticketId:    'ticket-1',
  canComplete: true,
  lines: [{
    menuItemName:      'Masala Dosa',
    inventoryItemName: 'Rice Flour',
    requiredQuantity:  2,
    availableQuantity: 10,
    resultingQuantity: 8,
    status:            'Sufficient',
  }],
};

// ── Scenario datasets ─────────────────────────────────────────────────────────

const SCENARIOS = {
  // S1: Empty active queue
  empty: {
    list: { items: [] },
    detail: null,
    label: 's1-empty-queue',
  },

  // S2: Single new (pending) eat-in ticket with table name
  newEatIn: {
    list: { items: [
      makeListItem({ tableNameSnapshot: 'Table 5', status: 'Pending' }),
    ]},
    detail: makeDetail({ tableNameSnapshot: 'Table 5', status: 'Pending' }),
    label: 's2-new-eatin-table',
  },

  // S3: Parcel ticket with customer name
  parcel: {
    list: { items: [
      makeListItem({ orderTypeSnapshot: 'Parcel', customerNameSnapshot: 'Ravi Kumar', status: 'Preparing', ticketNumber: 'KIT-20260621-0002', orderNumberSnapshot: 'ORD-20260621-0002', kitchenTicketId: 'ticket-2' }),
    ]},
    detail: makeDetail({ kitchenTicketId: 'ticket-2', ticketNumber: 'KIT-20260621-0002', orderNumberSnapshot: 'ORD-20260621-0002', orderTypeSnapshot: 'Parcel', tableNameSnapshot: null, customerNameSnapshot: 'Ravi Kumar', status: 'Preparing' }),
    label: 's3-parcel-customer',
  },

  // S4: Ready ticket (green)
  ready: {
    list: { items: [
      makeListItem({ status: 'Ready', tableNameSnapshot: 'Table 2', ticketNumber: 'KIT-20260621-0003', kitchenTicketId: 'ticket-3' }),
    ]},
    detail: makeDetail({ kitchenTicketId: 'ticket-3', ticketNumber: 'KIT-20260621-0003', status: 'Ready', tableNameSnapshot: 'Table 2', readyAt: new Date().toISOString() }),
    label: 's4-ready-ticket',
  },

  // S5: Cancelled ticket (red banner)
  cancelled: {
    list: { items: [
      makeListItem({ status: 'Cancelled', tableNameSnapshot: 'Table 7', cancelReason: 'Customer walked out', cancelledAt: new Date().toISOString(), ticketNumber: 'KIT-20260621-0004', kitchenTicketId: 'ticket-4' }),
    ]},
    detail: makeDetail({ kitchenTicketId: 'ticket-4', ticketNumber: 'KIT-20260621-0004', status: 'Cancelled', tableNameSnapshot: 'Table 7', cancelReason: 'Customer walked out', cancelledAt: new Date().toISOString() }),
    label: 's5-cancelled-ticket',
    filter: 'All',
  },

  // S6: Ticket with item notes
  withNotes: {
    list: { items: [
      makeListItem({ orderNotesSnapshot: 'Allergy: no peanuts', tableNameSnapshot: 'Table 9', status: 'Pending' }),
    ]},
    detail: makeDetail({ tableNameSnapshot: 'Table 9', orderNotesSnapshot: 'Allergy: no peanuts', status: 'Pending', lineNotes: 'Extra chutney please' }),
    label: 's6-ticket-with-notes',
  },

  // S7: Many tickets (6 mixed statuses)
  manyTickets: {
    list: { items: [
      makeListItem({ kitchenTicketId: 't1', ticketNumber: 'KIT-0001', status: 'Pending',   tableNameSnapshot: 'Table 1', createdAt: new Date(Date.now() - 2  * 60 * 1000).toISOString() }),
      makeListItem({ kitchenTicketId: 't2', ticketNumber: 'KIT-0002', status: 'Preparing', tableNameSnapshot: 'Table 2', createdAt: new Date(Date.now() - 8  * 60 * 1000).toISOString() }),
      makeListItem({ kitchenTicketId: 't3', ticketNumber: 'KIT-0003', status: 'Pending',   tableNameSnapshot: 'Table 3', createdAt: new Date(Date.now() - 12 * 60 * 1000).toISOString() }),
      makeListItem({ kitchenTicketId: 't4', ticketNumber: 'KIT-0004', status: 'Ready',     tableNameSnapshot: 'Table 4', createdAt: new Date(Date.now() - 15 * 60 * 1000).toISOString() }),
      makeListItem({ kitchenTicketId: 't5', ticketNumber: 'KIT-0005', status: 'Preparing', tableNameSnapshot: 'Table 5', createdAt: new Date(Date.now() - 22 * 60 * 1000).toISOString() }),
      makeListItem({ kitchenTicketId: 't6', ticketNumber: 'KIT-0006', status: 'Pending',   tableNameSnapshot: 'Table 6', createdAt: new Date(Date.now() - 25 * 60 * 1000).toISOString() }),
    ]},
    detail: null,
    label: 's7-many-tickets',
  },

  // S8: Urgent ticket (>10 min old — should show warning border)
  urgentTicket: {
    list: { items: [
      makeListItem({ status: 'Pending', tableNameSnapshot: 'Table 11', createdAt: new Date(Date.now() - 14 * 60 * 1000).toISOString() }),
    ]},
    detail: null,
    label: 's8-urgent-ticket',
  },

  // S9: Critical ticket (>20 min old — red border)
  criticalTicket: {
    list: { items: [
      makeListItem({ status: 'Pending', tableNameSnapshot: 'Table 12', createdAt: new Date(Date.now() - 25 * 60 * 1000).toISOString() }),
    ]},
    detail: null,
    label: 's9-critical-ticket',
  },

  // S10: Inline cancel confirm
  cancelConfirm: {
    list: { items: [
      makeListItem({ status: 'Pending', tableNameSnapshot: 'Table 3' }),
    ]},
    detail: makeDetail({ tableNameSnapshot: 'Table 3', status: 'Pending' }),
    label: 's10-cancel-confirm',
    triggerCancel: true,
  },

  // S11: Loading state (controlled via slow response)
  loading: {
    list: { items: [] },
    detail: null,
    label: 's11-loading',
    slowLoad: true,
  },
};

// ── Utilities ─────────────────────────────────────────────────────────────────
const ensureDir = d => fs.mkdirSync(d, { recursive: true });

async function screenshot(page, vp, tag) {
  const dir = path.join(screenshotDir, vp.label);
  ensureDir(dir);
  const file = path.join(dir, `${tag}.png`);
  await page.screenshot({ path: file, fullPage: false });
  console.log('  SCREENSHOT', path.relative(repoRoot, file));
  return file;
}

async function checkOverflow(page) {
  return page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 2);
}

async function checkTouchTargets(page) {
  return page.evaluate(() => {
    const MIN = 44;
    const buttons = Array.from(document.querySelectorAll('button:not([aria-hidden])'));
    return buttons
      .map(b => {
        const r = b.getBoundingClientRect();
        return { text: b.textContent?.trim().slice(0, 40), w: Math.round(r.width), h: Math.round(r.height) };
      })
      .filter(b => b.h > 0 && b.h < MIN);
  });
}

// ── Per-scenario mock setup ───────────────────────────────────────────────────
function setupKitchenMocks(page, scenario) {
  const routes = [];

  // List endpoint
  const listRoute = page.route('**/api/v1/kitchen/tickets', async route => {
    const method = route.request().method();
    if (method !== 'GET') {
      return route.continue();
    }
    if (scenario.slowLoad) {
      await new Promise(r => setTimeout(r, 2000));
    }
    return route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify(scenario.list),
    });
  });
  routes.push(listRoute);

  // Detail endpoint
  if (scenario.detail) {
    const id = scenario.detail.kitchenTicketId;
    const detailRoute = page.route(`**/api/v1/kitchen/tickets/${id}`, route =>
      route.fulfill({
        contentType: 'application/json',
        body: JSON.stringify(scenario.detail),
      })
    );
    routes.push(detailRoute);
  }

  // Deduction preview
  const previewRoute = page.route('**/api/v1/kitchen/tickets/**/deduction-preview', route =>
    route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify(DEDUCTION_PREVIEW),
    })
  );
  routes.push(previewRoute);

  // Status update — return detail with next status
  const statusRoute = page.route('**/api/v1/kitchen/tickets/**/status', route =>
    route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify(makeDetail({ status: 'Preparing', tableNameSnapshot: 'Table 3' })),
    })
  );
  routes.push(statusRoute);

  // Cancel — return detail with Cancelled
  const cancelRoute = page.route('**/api/v1/kitchen/tickets/**/cancel', route =>
    route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify(makeDetail({ status: 'Cancelled', cancelReason: 'Customer changed mind', cancelledAt: new Date().toISOString() })),
    })
  );
  routes.push(cancelRoute);

  return Promise.all(routes);
}

// ── Core checks ───────────────────────────────────────────────────────────────
async function runChecks(page, vp, scenario, results) {
  const checks = {};

  checks.overflow = await checkOverflow(page);
  if (checks.overflow) {
    results.defects.push(`${scenario.label}@${vp.label}: horizontal overflow`);
  }

  // Ticket number visible
  const firstTicket = scenario.list.items[0];
  if (firstTicket) {
    const ticketVisible = await page.getByText(firstTicket.ticketNumber).isVisible().catch(() => false);
    checks.ticketNumberVisible = ticketVisible;
    if (!ticketVisible) {
      results.defects.push(`${scenario.label}@${vp.label}: ticket number not visible`);
    }

    // Status visible
    const statusText = firstTicket.status === 'Pending' ? 'Pending' : firstTicket.status;
    const statusVisible = await page.getByText(new RegExp(statusText, 'i')).isVisible().catch(() => false);
    checks.statusVisible = statusVisible;

    // Table/reference visible
    if (firstTicket.tableNameSnapshot) {
      const tableVisible = await page.getByText(firstTicket.tableNameSnapshot).isVisible().catch(() => false);
      checks.tableVisible = tableVisible;
      if (!tableVisible) {
        results.defects.push(`${scenario.label}@${vp.label}: table name not visible`);
      }
    }

    if (firstTicket.customerNameSnapshot) {
      const customerVisible = await page.getByText(firstTicket.customerNameSnapshot).isVisible().catch(() => false);
      checks.customerVisible = customerVisible;
    }

    // Cancelled banner
    if (firstTicket.status === 'Cancelled') {
      const bannerVisible = await page.getByText(/do not prepare/i).isVisible().catch(() => false);
      checks.cancelledBannerVisible = bannerVisible;
      if (!bannerVisible) {
        results.defects.push(`${scenario.label}@${vp.label}: cancelled banner not visible`);
      }
    }
  }

  // Touch targets on mobile
  if (vp.width <= 768) {
    const smallTargets = await checkTouchTargets(page);
    checks.smallTouchTargets = smallTargets;
    if (smallTargets.length > 0) {
      results.defects.push(`${scenario.label}@${vp.label}: ${smallTargets.length} button(s) below 44px height`);
    }
  }

  return checks;
}

// ── Per-viewport runner ───────────────────────────────────────────────────────
async function runViewport(context, vp, allResults) {
  const vpResults = { viewport: vp.label, defects: [], scenarios: {} };
  const page = await context.newPage();
  await page.setViewportSize({ width: vp.width, height: vp.height });

  // Inject auth
  await page.addInitScript(({ key, session }) => {
    localStorage.setItem(key, JSON.stringify(session));
  }, { key: 'billsoft.auth.session.v1', session: AUTH_SESSION });

  console.log(`\n[${vp.label}]`);

  for (const [scenarioKey, scenario] of Object.entries(SCENARIOS)) {
    try {
      console.log(`  Scenario: ${scenario.label}`);

      // Mount fresh mocks for each scenario
      await page.unrouteAll({ behavior: 'ignoreErrors' }).catch(() => {});
      await setupKitchenMocks(page, scenario);

      // Navigate
      await page.goto(`${appUrl}/kitchen/tickets`, { waitUntil: 'domcontentloaded', timeout: 30_000 });

      // Wait for queue card to be present
      await page.waitForSelector('.kitchen-tickets-workspace', { timeout: 15_000 }).catch(() => {});

      // Apply filter if needed (e.g. to show Cancelled)
      if (scenario.filter === 'All') {
        const allBtn = page.getByRole('button', { name: /^all$/i });
        if (await allBtn.isVisible().catch(() => false)) {
          await allBtn.click();
          await page.waitForTimeout(300);
        }
      }

      // Wait for tickets to appear if we expect some
      if (scenario.list.items.length > 0) {
        await page.waitForTimeout(600);
      }

      // Click first ticket to load detail if detail data provided
      if (scenario.detail && !scenario.slowLoad) {
        const firstTicketBtn = page.getByRole('button', { name: new RegExp(scenario.detail.ticketNumber, 'i') });
        if (await firstTicketBtn.isVisible().catch(() => false)) {
          await firstTicketBtn.click();
          await page.waitForTimeout(600);
        }
      }

      // Trigger cancel confirm if requested
      if (scenario.triggerCancel && scenario.detail) {
        const reasonInput = page.getByLabel(/cancel reason/i);
        if (await reasonInput.isVisible().catch(() => false)) {
          await reasonInput.fill('Customer changed mind');
          const cancelBtn = page.getByRole('button', { name: /cancel ticket/i });
          if (await cancelBtn.isVisible().catch(() => false)) {
            await cancelBtn.click();
            await page.waitForTimeout(300);
          }
        }
      }

      // Take screenshot
      await screenshot(page, vp, scenario.label);

      // Run checks
      const checks = await runChecks(page, vp, scenario, vpResults);
      vpResults.scenarios[scenarioKey] = { label: scenario.label, ...checks };

    } catch (err) {
      console.error(`  ERROR in ${scenario.label}:`, err.message);
      vpResults.defects.push(`${scenario.label}@${vp.label}: error — ${err.message}`);
      vpResults.scenarios[scenarioKey] = { label: scenario.label, error: err.message };
      await screenshot(page, vp, `${scenario.label}-error`).catch(() => {});
    }
  }

  await page.close();
  allResults.push(vpResults);
}

// ── Main ─────────────────────────────────────────────────────────────────────
async function main() {
  console.log('Kitchen Tickets Visual Verify');
  console.log('App URL:     ', appUrl);
  console.log('Screenshots: ', screenshotDir);
  console.log('');

  ensureDir(screenshotDir);

  const launchOptions = { headless: true };
  if (chromiumPath) {
    launchOptions.executablePath = chromiumPath;
  }

  const browser = await chromium.launch(launchOptions);
  const allResults = [];

  for (const vp of VIEWPORTS) {
    const context = await browser.newContext({
      userDataDir: path.join(userDataRoot, vp.label),
    });
    await runViewport(context, vp, allResults);
    await context.close();
  }

  await browser.close();

  // ── Write report ───────────────────────────────────────────────────────────
  const report = {
    generatedAt: new Date().toISOString(),
    appUrl,
    viewports: VIEWPORTS.map(v => v.label),
    results: allResults,
    totalDefects: allResults.reduce((sum, r) => sum + r.defects.length, 0),
    defectSummary: allResults.flatMap(r => r.defects),
  };

  const reportPath = path.join(screenshotDir, 'report.json');
  fs.writeFileSync(reportPath, JSON.stringify(report, null, 2));
  console.log('\nReport written to:', path.relative(repoRoot, reportPath));

  // Print summary
  console.log('\n── Summary ──');
  console.log(`Total defects found: ${report.totalDefects}`);
  if (report.defectSummary.length > 0) {
    console.log('Defects:');
    for (const d of report.defectSummary) {
      console.log('  ✗', d);
    }
  } else {
    console.log('  No defects detected.');
  }

  process.exit(report.totalDefects > 0 ? 1 : 0);
}

main().catch(err => {
  console.error('Fatal error:', err);
  process.exit(2);
});
