/**
 * pos-orders-visual-verify.cjs
 *
 * Visual verification script for /pos/orders across 6 viewports and 10 scenarios.
 * Auth is injected via addInitScript (no real backend needed).
 * All /api/v1/ calls are intercepted and fulfilled with realistic mock data.
 *
 * Usage:
 *   node scripts/playwright/pos-orders-visual-verify.cjs
 *
 * Environment variables:
 *   BILLSOFT_BROWSER_QA_APP_URL      — app origin (default http://localhost:3010)
 *   BILLSOFT_CHROMIUM_PATH           — custom Chromium binary path
 *   BILLSOFT_BROWSER_QA_SCREENSHOT_DIR — output dir (default output/playwright/pos-orders)
 */

'use strict';

const fs = require('fs');
const path = require('path');

// ── Playwright resolution (mirrors billsoft-browser-qa.cjs) ──────────────────
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
const repoRoot     = path.resolve(__dirname, '..', '..');
const appUrl       = process.env.BILLSOFT_BROWSER_QA_APP_URL ?? 'http://localhost:3010';
const chromiumPath = process.env.BILLSOFT_CHROMIUM_PATH ?? '';
const screenshotDir = process.env.BILLSOFT_BROWSER_QA_SCREENSHOT_DIR
  ?? path.join(repoRoot, 'output', 'playwright', 'pos-orders');

const userDataRoot = path.join(repoRoot, '.tmp', 'playwright-user-data');

const VIEWPORTS = [
  { label: '1366x768',  width: 1366, height: 768  },
  { label: '1280x720',  width: 1280, height: 720  },
  { label: '1024x768',  width: 1024, height: 768  },
  { label: '768x1024',  width: 768,  height: 1024 },
  { label: '430x932',   width: 430,  height: 932  },
  { label: '390x844',   width: 390,  height: 844  },
];

// ── Auth session shape (mirrors authTestUtils.ts) ─────────────────────────────
const AUTH_SESSION = {
  accessToken:               'access-token-playwright',
  refreshToken:              'refresh-token-playwright',
  accessTokenExpiresAtUtc:   '2099-06-11T10:15:00Z',
  refreshTokenExpiresAtUtc:  '2099-06-18T10:15:00Z',
  userId:                    'user-playwright',
  restaurantId:              'restaurant-1',
  restaurantCode:            'BILL01',
  countryCode:               'IN',
  currencyCode:              'INR',
  timeZoneId:                'Asia/Kolkata',
  branchId:                  null,
  fullName:                  'POS Tester',
  mobileNumber:              '9000000099',
  roles:                     ['Cashier'],
  permissions:               ['Order.Create', 'Order.View', 'Order.Cancel'],
  activeRole:                'Cashier',
};

// ── Mock API responses ────────────────────────────────────────────────────────
const MOCK_BRANCHES = { items: [
  { branchId: 'branch-1', restaurantId: 'restaurant-1', name: 'Main Branch',
    address: '1 Market St', phone: '60000000', timezone: 'Asia/Kolkata',
    currency: 'INR', status: 'Active',
    createdAt: '2026-01-01T00:00:00Z', updatedAt: '2026-01-01T00:00:00Z' },
]};

const MOCK_CATEGORIES = { items: [
  { menuCategoryId: 'cat-1', restaurantId: 'restaurant-1', name: 'Starters', displayOrder: 1, status: 'Active', createdAt: '2026-01-01T00:00:00Z', updatedAt: '2026-01-01T00:00:00Z' },
  { menuCategoryId: 'cat-2', restaurantId: 'restaurant-1', name: 'Mains',    displayOrder: 2, status: 'Active', createdAt: '2026-01-01T00:00:00Z', updatedAt: '2026-01-01T00:00:00Z' },
]};

function makeItem(id, catId, name, price, eatIn = true, parcel = true) {
  return {
    menuItemId: id, restaurantId: 'restaurant-1', menuCategoryId: catId,
    categoryName: 'Starters', name, description: '', sku: id.toUpperCase(),
    basePrice: price, taxRate: 5, isVegetarian: true,
    isAvailableForEatIn: eatIn, isAvailableForParcel: parcel,
    status: 'Active', createdAt: '2026-01-01T00:00:00Z', updatedAt: '2026-01-01T00:00:00Z',
  };
}

const MOCK_ITEMS = { items: [
  makeItem('item-1', 'cat-1', 'Masala Dosa',      120),
  makeItem('item-2', 'cat-1', 'Idli Sambar',        80),
  makeItem('item-3', 'cat-1', 'Vada',               60),
  makeItem('item-4', 'cat-2', 'Paneer Butter Masala', 220),
  makeItem('item-5', 'cat-2', 'Dal Tadka',          150),
  makeItem('item-6', 'cat-2', 'Jeera Rice',          90),
  makeItem('item-7', 'cat-2', 'Naan',                40),
  makeItem('item-8', 'cat-2', 'Gulab Jamun',         55),
]};

function makeOrderListItem(id, num, status, type, table) {
  return {
    posOrderId: id, branchId: 'branch-1', orderNumber: num,
    orderType: type, status, tableName: table ?? null,
    customerName: null, grandTotal: 500, lineCount: 2,
    createdAt: '2026-06-21T10:00:00Z',
  };
}

const MOCK_ORDERS = { items: [
  makeOrderListItem('order-draft',     'ORD-0001', 'Draft',     'EatIn',  'T1'),
  makeOrderListItem('order-confirmed', 'ORD-0002', 'Confirmed', 'EatIn',  'T2'),
  makeOrderListItem('order-parcel',    'ORD-0003', 'Draft',     'Parcel', null),
]};

function makeOrderDetail(id, num, status, type, table) {
  return {
    posOrderId: id, restaurantId: 'restaurant-1', branchId: 'branch-1',
    orderNumber: num, orderType: type, status,
    tableName: table ?? null, customerName: null, customerMobile: null, notes: null,
    subtotal: 400, taxTotal: 20, grandTotal: 420,
    confirmedAt:  status === 'Confirmed' ? '2026-06-21T10:05:00Z' : null,
    cancelledAt:  null, cancelReason: null,
    createdByUserId: 'user-1', confirmedByUserId: status === 'Confirmed' ? 'user-1' : null,
    cancelledByUserId: null,
    createdAt: '2026-06-21T10:00:00Z', updatedAt: null,
    kitchenTicketId:     status === 'Confirmed' ? 'ticket-1' : null,
    kitchenTicketNumber: status === 'Confirmed' ? 'KIT-0001'  : null,
    kitchenTicketStatus: status === 'Confirmed' ? 'Pending'   : null,
    lines: [
      { posOrderLineId: 'line-1', menuItemId: 'item-1', menuCategoryId: 'cat-1',
        menuItemNameSnapshot: 'Masala Dosa', menuCategoryNameSnapshot: 'Starters',
        skuSnapshot: 'ITEM-1', unitPrice: 120, taxRate: 5, quantity: 2,
        lineSubtotal: 240, lineTax: 12, lineTotal: 252,
        notes: null, displayOrder: 1,
        createdAt: '2026-06-21T10:00:00Z', updatedAt: null },
      { posOrderLineId: 'line-2', menuItemId: 'item-2', menuCategoryId: 'cat-1',
        menuItemNameSnapshot: 'Idli Sambar', menuCategoryNameSnapshot: 'Starters',
        skuSnapshot: 'ITEM-2', unitPrice: 80, taxRate: 5, quantity: 2,
        lineSubtotal: 160, lineTax: 8, lineTotal: 168,
        notes: null, displayOrder: 2,
        createdAt: '2026-06-21T10:00:00Z', updatedAt: null },
    ],
  };
}

// ── Utilities ─────────────────────────────────────────────────────────────────
const ensureDir = d => fs.mkdirSync(d, { recursive: true });

async function screenshot(page, vp, tag) {
  const dir = path.join(screenshotDir, vp.label);
  ensureDir(dir);
  const file = path.join(dir, `${tag}.png`);
  await page.screenshot({ path: file, fullPage: false });
  console.log('SCREENSHOT', file);
  return file;
}

async function checkOverflow(page) {
  return page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
}

// ── Route mocking ─────────────────────────────────────────────────────────────
async function setupMockRoutes(page) {
  await page.route('**/api/v1/pos/workspace/branches**', r =>
    r.fulfill({ contentType: 'application/json', body: JSON.stringify(MOCK_BRANCHES) }));

  await page.route('**/api/v1/menu/categories**', r =>
    r.fulfill({ contentType: 'application/json', body: JSON.stringify(MOCK_CATEGORIES) }));

  await page.route('**/api/v1/menu/items**', r =>
    r.fulfill({ contentType: 'application/json', body: JSON.stringify(MOCK_ITEMS) }));

  await page.route('**/api/v1/pos/orders**', async r => {
    const url = r.request().url();
    const method = r.request().method();
    if (method === 'GET' && !url.match(/\/orders\/[^/]+$/)) {
      return r.fulfill({ contentType: 'application/json', body: JSON.stringify(MOCK_ORDERS) });
    }
    if (method === 'GET' && url.includes('order-confirmed')) {
      return r.fulfill({ contentType: 'application/json', body: JSON.stringify(makeOrderDetail('order-confirmed', 'ORD-0002', 'Confirmed', 'EatIn', 'T2')) });
    }
    if (method === 'GET' && url.includes('order-draft')) {
      return r.fulfill({ contentType: 'application/json', body: JSON.stringify(makeOrderDetail('order-draft', 'ORD-0001', 'Draft', 'EatIn', 'T1')) });
    }
    // POST create draft
    if (method === 'POST') {
      return r.fulfill({ contentType: 'application/json', body: JSON.stringify(makeOrderDetail('order-new', 'ORD-0004', 'Draft', 'EatIn', null)) });
    }
    return r.fulfill({ contentType: 'application/json', body: JSON.stringify(MOCK_ORDERS) });
  });
}

// ── Main ─────────────────────────────────────────────────────────────────────
async function runViewport(context, vp, results) {
  const page = await context.newPage();
  await page.setViewportSize({ width: vp.width, height: vp.height });

  // Inject auth before React mounts
  await page.addInitScript(({ key, session }) => {
    localStorage.setItem(key, JSON.stringify(session));
  }, { key: 'billsoft.auth.session.v1', session: AUTH_SESSION });

  await setupMockRoutes(page);

  const vpResults = { viewport: vp.label, defects: [], scenarios: {} };

  try {
    // ── S1: Empty cart (EatIn default) ────────────────────────────────────────
    await page.goto(`${appUrl}/pos/orders`, { waitUntil: 'domcontentloaded', timeout: 30000 });
    await page.waitForSelector('.pos-workspace', { timeout: 15000 });

    const overflow_s1 = await checkOverflow(page);
    if (overflow_s1) vpResults.defects.push('S1: horizontal overflow');
    await screenshot(page, vp, 's1-empty-eatin');
    vpResults.scenarios.s1 = { overflow: overflow_s1 };

    // Check compact empty-cart element present
    const compactEmpty = await page.locator('.pos-cart--empty').count();
    vpResults.scenarios.s1.compactEmptyPresent = compactEmpty > 0;

    // Check no-table warning for EatIn with blank table
    const noTableWarn = await page.locator('.pos-context-bar__table-warn').isVisible().catch(() => false);
    vpResults.scenarios.s1.eatInNoTableWarn = noTableWarn;

    // ── S2: Cart with 1 item ──────────────────────────────────────────────────
    await page.click('[aria-label="Masala Dosa"]');
    await page.waitForSelector('.pos-cart-line', { timeout: 5000 });
    const overflow_s2 = await checkOverflow(page);
    if (overflow_s2) vpResults.defects.push('S2: horizontal overflow');
    await screenshot(page, vp, 's2-cart-1-item');

    // Check primary action visibility
    const createDraftBtn = page.getByRole('button', { name: /create draft/i });
    const createDraftVisible = await createDraftBtn.isVisible().catch(() => false);
    vpResults.scenarios.s2 = { overflow: overflow_s2, createDraftVisible };
    if (!createDraftVisible) vpResults.defects.push('S2: "Create draft" button not visible without scrolling');

    // ── S4: Cart with 8 items ─────────────────────────────────────────────────
    for (const name of ['Idli Sambar', 'Vada', 'Paneer Butter Masala', 'Dal Tadka', 'Jeera Rice', 'Naan', 'Gulab Jamun']) {
      await page.click(`[aria-label="${name}"]`).catch(() => {});
    }
    await page.waitForTimeout(400);
    const overflow_s4 = await checkOverflow(page);
    if (overflow_s4) vpResults.defects.push('S4: horizontal overflow');
    await screenshot(page, vp, 's4-cart-8-items');

    // On ≤1023px check cart-lines scroll containment
    if (vp.width <= 1023) {
      const cartLines = page.locator('.pos-cart-lines');
      const box = await cartLines.boundingBox();
      const styles = await cartLines.evaluate(el => ({
        maxHeight: window.getComputedStyle(el).maxHeight,
        overflowY: window.getComputedStyle(el).overflowY,
      }));
      vpResults.scenarios.s4 = { cartLinesMaxHeight: styles.maxHeight, cartLinesOverflowY: styles.overflowY };
      if (styles.overflowY !== 'auto' && styles.overflowY !== 'scroll') {
        vpResults.defects.push('S4: .pos-cart-lines has no scroll containment on mobile');
      }
    }

    // On desktop check cart column height cap
    if (vp.width > 1023) {
      const cartColStyles = await page.locator('.pos-workspace__cart').evaluate(el => ({
        maxHeight: window.getComputedStyle(el).maxHeight,
        overflowY: window.getComputedStyle(el).overflowY,
      }));
      vpResults.scenarios.s4 = { cartColMaxHeight: cartColStyles.maxHeight, cartColOverflowY: cartColStyles.overflowY };
      const draftActionsVisible = await page.locator('.pos-draft-actions').isVisible().catch(() => false);
      vpResults.scenarios.s4.draftActionsVisible = draftActionsVisible;
      if (!draftActionsVisible) vpResults.defects.push('S4: draft actions not visible on desktop with 8 cart items');
    }

    // ── S5/S6: EatIn no-table warning ─────────────────────────────────────────
    // Clear cart first for cleaner state
    await page.reload({ waitUntil: 'domcontentloaded' });
    await page.waitForSelector('.pos-workspace', { timeout: 15000 });

    const warnVisible = await page.locator('.pos-context-bar__table-warn').isVisible().catch(() => false);
    vpResults.scenarios.s5 = { noTableWarnVisible: warnVisible };
    if (!warnVisible) vpResults.defects.push('S5: EatIn no-table warning not visible');

    await screenshot(page, vp, 's5-eatin-no-table');

    // Fill table → warning gone
    await page.locator('.pos-context-bar__table input').fill('T3').catch(() => {});
    await page.waitForTimeout(200);
    const warnGone = !(await page.locator('.pos-context-bar__table-warn').isVisible().catch(() => true));
    vpResults.scenarios.s6 = { warnGoneAfterFill: warnGone };
    if (!warnGone) vpResults.defects.push('S6: EatIn no-table warning persists after table filled');
    await screenshot(page, vp, 's6-eatin-table-filled');

    // ── S7: Parcel order ──────────────────────────────────────────────────────
    const parcelBtn = page.getByRole('button', { name: /^parcel$/i });
    await parcelBtn.click();
    await page.waitForTimeout(200);
    const tableFieldGone = !(await page.locator('.pos-context-bar__table').isVisible().catch(() => true));
    vpResults.scenarios.s7 = { tableFieldHiddenForParcel: tableFieldGone };
    if (!tableFieldGone) vpResults.defects.push('S7: Table field still visible for Parcel order');
    await screenshot(page, vp, 's7-parcel');

    // ── S8: Draft saved → "Confirm and send to kitchen" visible ───────────────
    await page.getByRole('button', { name: /^eat-in$/i }).click();
    await page.click('[aria-label="Masala Dosa"]');
    await page.waitForSelector('.pos-cart-line', { timeout: 5000 });
    await page.getByRole('button', { name: /create draft/i }).click();
    await page.waitForTimeout(800);
    const confirmKitchenBtn = page.getByRole('button', { name: /confirm and send to kitchen/i });
    const confirmKitchenVisible = await confirmKitchenBtn.isVisible().catch(() => false);
    vpResults.scenarios.s8 = { confirmAndSendVisible: confirmKitchenVisible };
    if (!confirmKitchenVisible) vpResults.defects.push('S8: "Confirm and send to kitchen" not visible after draft save');
    await screenshot(page, vp, 's8-draft-saved');

    // ── S9: Confirm step open ─────────────────────────────────────────────────
    const orderBtn = page.getByRole('button', { name: 'ORD-0001' });
    if (await orderBtn.isVisible().catch(() => false)) {
      await orderBtn.click();
      await page.waitForTimeout(600);
      const confirmOrderBtn = page.getByRole('button', { name: /confirm order/i });
      if (await confirmOrderBtn.isVisible().catch(() => false)) {
        await confirmOrderBtn.click();
        await page.waitForTimeout(300);
        const confirmStepVisible = await page.locator('.pos-confirm-step').isVisible().catch(() => false);
        vpResults.scenarios.s9 = { confirmStepVisible };
        if (!confirmStepVisible) vpResults.defects.push('S9: Confirm step panel not visible');
        await screenshot(page, vp, 's9-confirm-step');
      }
    }

    // ── S10: Confirmed order cancel warning ───────────────────────────────────
    const confirmedOrderBtn = page.getByRole('button', { name: 'ORD-0002' });
    if (await confirmedOrderBtn.isVisible().catch(() => false)) {
      await confirmedOrderBtn.click();
      await page.waitForTimeout(600);
      const cancelWarnVisible = await page.locator('.pos-cancel-confirmed-warn').isVisible().catch(() => false);
      vpResults.scenarios.s10 = { cancelWarnVisible };
      if (!cancelWarnVisible) vpResults.defects.push('S10: Confirmed-order cancel warning not visible');
      await screenshot(page, vp, 's10-confirmed-cancel-warn');
    }

    // ── Touch targets (390/430 only) ──────────────────────────────────────────
    if (vp.width <= 430) {
      const smButtons = page.locator('.ui-button--sm');
      const count = await smButtons.count();
      const belowTarget = [];
      for (let i = 0; i < count; i++) {
        const btn = smButtons.nth(i);
        const box = await btn.boundingBox();
        if (box && box.height < 43) belowTarget.push({ text: await btn.textContent(), height: box.height });
      }
      vpResults.scenarios.touchTargets = { smButtonsBelowMin: belowTarget };
      if (belowTarget.length > 0) {
        vpResults.defects.push(`D3: ${belowTarget.length} sm buttons below 44px: ${belowTarget.map(b => b.text?.trim()).join(', ')}`);
      }

      const allChip = page.locator('.pos-category-chip').first();
      const chipBox = await allChip.boundingBox();
      if (chipBox && (chipBox.height < 43 || chipBox.width < 43)) {
        vpResults.defects.push(`D3: "All" chip too small (${Math.round(chipBox.width)}×${Math.round(chipBox.height)})`);
      }
    }

  } catch (err) {
    vpResults.defects.push(`EXCEPTION: ${err.message}`);
    console.error(`Error in viewport ${vp.label}:`, err.message);
  } finally {
    await page.close();
  }

  results.push(vpResults);
  const pass = vpResults.defects.length === 0;
  console.log(`${pass ? 'PASS' : 'FAIL'} ${vp.label} — ${vpResults.defects.length} defect(s)${vpResults.defects.length ? ': ' + vpResults.defects.join(' | ') : ''}`);
}

async function run() {
  ensureDir(screenshotDir);
  ensureDir(userDataRoot);

  const userDataDir = path.join(userDataRoot, `pos-verify-${Date.now()}`);
  ensureDir(userDataDir);

  const context = await chromium.launchPersistentContext(userDataDir, {
    ...(chromiumPath ? { executablePath: chromiumPath } : {}),
    headless: true,
    args: ['--disable-gpu', '--disable-dev-shm-usage', '--no-first-run'],
    viewport: { width: 1366, height: 768 },
  });

  const results = [];

  try {
    for (const vp of VIEWPORTS) {
      await runViewport(context, vp, results);
    }
  } finally {
    await context.close();
  }

  // Summary
  const totalDefects = results.reduce((n, r) => n + r.defects.length, 0);
  const verdict = totalDefects === 0 ? 'PASS'
    : results.some(r => r.defects.some(d => d.includes('overflow') || d.includes('EXCEPTION')))
      ? 'FAIL' : 'PASS WITH ISSUES';

  console.log('\n══════════════════════════════════════════════');
  console.log(`VERDICT: ${verdict}  (${totalDefects} total defect(s) across ${VIEWPORTS.length} viewports)`);
  console.log('══════════════════════════════════════════════\n');

  const reportPath = path.join(screenshotDir, 'report.json');
  fs.writeFileSync(reportPath, JSON.stringify({ verdict, results }, null, 2));
  console.log('Report:', reportPath);
  console.log('Screenshots:', screenshotDir);
}

run().catch(err => { console.error('FATAL', err); process.exit(1); });
