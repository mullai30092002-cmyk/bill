/**
 * verify-inventory-ux-84-86.cjs
 *
 * Visual verification for the inventory UX hardening in issues #84-86.
 * Covers:
 *   - Add Item scroll/focus behavior
 *   - Adjust Stock scroll/focus behavior
 *   - Clean RFC7807 detail rendering for item, adjustment, batch production, and wastage failures
 *   - No horizontal overflow on the inventory workspace
 *
 * Usage:
 *   node scripts/playwright/verify-inventory-ux-84-86.cjs
 *
 * Environment variables:
 *   BILLSOFT_BROWSER_QA_APP_URL        - app origin (default http://127.0.0.1:3010)
 *   BILLSOFT_BROWSER_QA_SCREENSHOT_DIR - screenshot output dir
 *                                        (default output/playwright/inventory-ux-84-86)
 *   BILLSOFT_PLAYWRIGHT_CORE_PATH      - optional playwright-core path
 *   BILLSOFT_CHROMIUM_PATH             - optional browser executable path
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
  ?? path.join(repoRoot, 'output', 'playwright', 'inventory-ux-84-86');

const VIEWPORTS = [
  { label: '390x844', width: 390, height: 844 },
  { label: '768x1024', width: 768, height: 1024 },
  { label: '1366x768', width: 1366, height: 768 },
];

const LANGUAGES = ['en', 'ta'];

const AUTH_SESSION = {
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
  permissions: ['Branch.Manage', 'Inventory.View', 'Inventory.Adjust', 'MenuItem.View', 'MenuItem.Manage'],
  activeRole: 'Admin',
};

const NOW = '2026-06-24T09:00:00Z';

const BRANCH_LIST = {
  items: [
    {
      branchId: 'branch-1',
      restaurantId: 'restaurant-1',
      name: 'Main Branch',
      address: '123 Market Street',
      phone: '60000001',
      timezone: 'Asia/Singapore',
      currency: 'SGD',
      status: 'Active',
      createdAt: NOW,
      updatedAt: NOW,
    },
  ],
};

const INVENTORY_ITEM = {
  inventoryItemId: 'item-carrot',
  restaurantId: 'restaurant-1',
  branchId: 'branch-1',
  name: 'Carrot',
  normalizedName: 'CARROT',
  category: 'Vegetables',
  unitOfMeasure: 'kg',
  lowStockThreshold: 15,
  isActive: true,
  currentStock: 12,
  status: 'Low stock',
  createdAtUtc: NOW,
  updatedAtUtc: NOW,
};

const INVENTORY_SUMMARY = {
  restaurantId: 'restaurant-1',
  branchId: 'branch-1',
  totalItems: 1,
  activeItems: 1,
  inactiveItems: 0,
  lowStockCount: 1,
  outOfStockCount: 0,
  totalCurrentStock: 12,
  recentlyAdjustedCount: 1,
  lowStockItems: [
    {
      inventoryItemId: INVENTORY_ITEM.inventoryItemId,
      name: INVENTORY_ITEM.name,
      category: INVENTORY_ITEM.category,
      unitOfMeasure: INVENTORY_ITEM.unitOfMeasure,
      lowStockThreshold: INVENTORY_ITEM.lowStockThreshold,
      currentStock: INVENTORY_ITEM.currentStock,
      status: INVENTORY_ITEM.status,
    },
  ],
  outOfStockItems: [],
};

const MOVEMENTS = {
  items: [
    {
      inventoryMovementId: 'movement-1',
      inventoryItemId: INVENTORY_ITEM.inventoryItemId,
      restaurantId: 'restaurant-1',
      branchId: 'branch-1',
      movementType: 'AdjustmentIncrease',
      quantity: 2,
      unitCost: null,
      referenceNumber: null,
      reason: 'Physical count correction',
      notes: 'Baseline stock',
      movementDate: NOW,
      recordedByUserId: 'user-1',
      recordedByUserName: 'QA Tester',
      recordedByUserMobile: '9000000099',
      createdAtUtc: NOW,
      previousStock: 10,
      delta: 2,
      resultingStock: 12,
      resultingStatus: 'Low stock',
      expiresAtUtc: null,
      batchReference: null,
    },
  ],
};

const MENU_ITEMS = {
  items: [
    {
      menuItemId: 'menu-carrot',
      restaurantId: 'restaurant-1',
      menuCategoryId: 'category-veg',
      categoryName: 'Vegetables',
      name: 'Carrot',
      description: 'Prepared carrot batch',
      sku: 'CARROT-01',
      basePrice: 0,
      taxRate: 0,
      isVegetarian: true,
      isAvailableForEatIn: true,
      isAvailableForParcel: true,
      inventoryDeductionMode: 'BatchPrepared',
      stockInventoryItemId: 'item-carrot-prepared',
      stockInventoryItemName: 'Prepared Carrot',
      status: 'Active',
      createdAt: NOW,
      updatedAt: NOW,
    },
  ],
};

const BATCH_PRODUCTIONS = {
  items: [
    {
      batchProductionId: 'batch-1',
      restaurantId: 'restaurant-1',
      branchId: 'branch-1',
      menuItemId: 'menu-carrot',
      menuItemName: 'Carrot',
      preparedInventoryItemId: 'item-carrot-prepared',
      preparedInventoryItemName: 'Prepared Carrot',
      quantityProduced: 100,
      businessDate: '2026-06-24',
      producedAtUtc: NOW,
      producedByUserId: 'user-1',
      producedByUserName: 'QA Tester',
      notes: 'QA batch',
      totalRawQuantityConsumed: 50,
      createdAtUtc: NOW,
      shelfLifeHours: null,
      expiresAtUtc: null,
      storageNote: null,
      batchReference: null,
    },
  ],
};

function ensureDir(dirPath) {
  fs.mkdirSync(dirPath, { recursive: true });
}

function jsonResponse(body) {
  return {
    status: 200,
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

function labels(language) {
  const tamil = language === 'ta';
  return {
    workspaceTitle: tamil ? 'கையிருப்பு பணியிடம்' : 'Inventory workspace',
    addItemButton: tamil ? 'உருப்படியை சேர்' : 'Add item',
    addItemTitle: tamil ? 'கையிருப்பு உருப்படியை சேர்' : 'Add inventory item',
    itemsCardTitle: tamil ? 'கையிருப்பு உருப்படிகள்' : 'Inventory items',
    itemNameLabel: tamil ? 'உருப்படி பெயர்' : 'Item name',
    itemCategoryLabel: tamil ? 'வகை' : 'Category',
    itemUnitLabel: tamil ? 'அலகு' : 'Unit',
    itemThresholdLabel: tamil ? 'குறைந்த-ஸ்டாக் வரம்பு' : 'Low-stock threshold',
    createItemButton: tamil ? 'உருப்படியை உருவாக்கு' : 'Create item',
    adjustStockButton: tamil ? 'ஸ்டாக்கை சரிசெய்' : 'Adjust stock',
    adjustStockDialogTitle: tamil ? 'ஸ்டாக்கை சரிசெய்' : 'Adjust stock',
    adjustmentTypeLabel: tamil ? 'சரிசெய்தல் வகை' : 'Adjustment type',
    adjustmentQuantityLabel: tamil ? 'அளவு' : 'Quantity',
    adjustmentReasonLabel: tamil ? 'காரணம்' : 'Reason',
    adjustmentNoteLabel: tamil ? 'குறிப்பு' : 'Note',
    confirmAdjustmentButton: tamil ? 'சரிசெய்தலை உறுதிசெய்' : 'Confirm adjustment',
    batchProductionTitle: tamil ? 'தொகுதி தயாரிப்பு' : 'Batch production',
    batchProductionMenuItemLabel: tamil ? 'தயாரான மெனு உருப்படி' : 'Prepared menu item',
    batchProductionQuantityLabel: tamil ? 'தயாரிக்கப்பட்ட அளவு' : 'Quantity produced',
    batchProductionBusinessDateLabel: tamil ? 'வணிக தேதி' : 'Business date',
    batchProductionProducedAtLabel: tamil ? 'உற்பத்தி நேரம்' : 'Produced at',
    batchProductionNotesLabel: tamil ? 'குறிப்புகள்' : 'Notes',
    batchProductionSaveButton: tamil ? 'உற்பத்தியை பதிவு செய்' : 'Record production',
    wastageTitle: tamil ? 'தயாரான stock wastage' : 'Prepared stock wastage',
    wastageMenuItemLabel: tamil ? 'தயாரான மெனு உருப்படி' : 'Prepared menu item',
    wastageQuantityLabel: tamil ? 'wastage அளவு' : 'Wastage quantity',
    wastageReasonLabel: tamil ? 'காரணம்' : 'Reason',
    wastageRecordedAtLabel: tamil ? 'Wasted ஆன நேரம்' : 'Wasted at',
    wastageNotesLabel: tamil ? 'குறிப்புகள்' : 'Notes',
    wastageSaveButton: tamil ? 'Wastage-ஐ பதிவு செய்' : 'Record wastage',
  };
}

function createAuthInitScript(language) {
  return ({ sessionKey, languageKey, session, lang }) => {
    localStorage.setItem(languageKey, lang);
    localStorage.setItem(sessionKey, JSON.stringify(session));
  };
}

async function seedContext(context, language) {
  await context.addInitScript(createAuthInitScript(language), {
    sessionKey: 'billsoft.auth.session.v1',
    languageKey: 'billsoft.language',
    session: AUTH_SESSION,
    lang: language,
  });
}

async function routeInventoryApis(context) {
  let batchProductionAttempts = 0;

  await context.route('**/api/v1/**', async route => {
    const url = new URL(route.request().url());
    const { pathname } = url;
    const method = route.request().method().toUpperCase();

    if (method === 'GET' && pathname === '/api/v1/admin/branches') {
      return route.fulfill(jsonResponse(BRANCH_LIST));
    }

    if (method === 'GET' && pathname === '/api/v1/inventory/items') {
      return route.fulfill(jsonResponse({ items: [INVENTORY_ITEM] }));
    }

    if (method === 'GET' && pathname === '/api/v1/inventory/summary') {
      return route.fulfill(jsonResponse(INVENTORY_SUMMARY));
    }

    if (method === 'GET' && pathname === '/api/v1/admin/menu/items') {
      return route.fulfill(jsonResponse(MENU_ITEMS));
    }

    if (method === 'GET' && pathname === '/api/v1/inventory/batch-productions') {
      return route.fulfill(jsonResponse(BATCH_PRODUCTIONS));
    }

    if (method === 'GET' && pathname === `/api/v1/inventory/items/${INVENTORY_ITEM.inventoryItemId}/movements`) {
      return route.fulfill(jsonResponse(MOVEMENTS));
    }

    if (method === 'POST' && pathname === '/api/v1/inventory/items') {
      return route.fulfill(problemResponse('Inventory item name already exists in this branch.'));
    }

    if (method === 'POST' && pathname === `/api/v1/inventory/items/${INVENTORY_ITEM.inventoryItemId}/movements`) {
      return route.fulfill(problemResponse('Could not record the stock adjustment right now.'));
    }

    if (method === 'POST' && pathname === '/api/v1/inventory/batch-productions') {
      batchProductionAttempts += 1;
      return route.fulfill(
        problemResponse(
          batchProductionAttempts === 1
            ? 'Batch production requires a recipe for the current branch.'
            : 'Insufficient stock for batch production: Carrot requires 1400.'
        )
      );
    }

    if (method === 'POST' && pathname === '/api/v1/inventory/prepared-stock/wastage') {
      return route.fulfill(problemResponse('Could not record prepared stock wastage right now.'));
    }

    throw new Error(`Unhandled request: ${method} ${pathname}${url.search ? url.search : ''}`);
  });
}

async function checkNoOverflow(page, scopeLabel, viewportLabel) {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth + 1);
  if (overflow) {
    throw new Error(`Horizontal overflow detected for ${scopeLabel} at ${viewportLabel}`);
  }
}

async function expectCleanAlert(page, expectedMessage) {
  const alert = page.getByRole('alert');
  await alert.waitFor({ state: 'visible', timeout: 30000 });
  const text = (await alert.textContent()) ?? '';
  if (!text.includes(expectedMessage)) {
    throw new Error(`Expected alert to include "${expectedMessage}" but saw "${text}"`);
  }

  if (text.includes('datatracker.ietf.org') || text.includes('"type"') || text.includes('"detail"') || text.includes('{')) {
    throw new Error(`Alert still exposes raw RFC7807 JSON: "${text}"`);
  }
}

async function expectFocused(locator, label) {
  if (!(await locator.evaluate(element => document.activeElement === element))) {
    throw new Error(`${label} did not receive focus.`);
  }
}

async function expectNearTop(locator, label) {
  const metrics = await locator.evaluate(element => ({
    top: element.getBoundingClientRect().top,
    scrollY: window.scrollY,
  }));
  if (metrics.top > 220 && metrics.scrollY <= 0) {
    throw new Error(`${label} did not scroll near the top of the viewport (top=${metrics.top}, scrollY=${metrics.scrollY}).`);
  }
}

async function screenshot(page, viewportLabel, language, scenario) {
  const targetDir = path.join(screenshotDir, language, viewportLabel);
  ensureDir(targetDir);
  const screenshotPath = path.join(targetDir, `${scenario}.png`);
  await page.screenshot({ path: screenshotPath, fullPage: true });
  console.log('SCREENSHOT', screenshotPath);
}

async function loadInventoryPage(page, strings, viewportLabel, language) {
  await page.goto(`${appUrl}/inventory`, { waitUntil: 'networkidle', timeout: 30000 });
  await page.getByRole('heading', { name: strings.workspaceTitle }).waitFor({ state: 'visible', timeout: 30000 });
  await checkNoOverflow(page, `inventory-${language}`, viewportLabel);
}

async function verifyAddItemFlow(page, strings, viewportLabel, language) {
  await page.getByRole('button', { name: strings.addItemButton }).click();

  const itemCard = page.locator('section.ui-card').filter({ has: page.getByRole('heading', { name: strings.addItemTitle }) }).first();
  await itemCard.getByLabel(strings.itemNameLabel).waitFor({ state: 'visible', timeout: 30000 });

  const itemName = itemCard.getByLabel(strings.itemNameLabel);
  await expectFocused(itemName, 'Inventory item name input');
  await page.waitForTimeout(800);
  await expectNearTop(itemName, 'Inventory item name input');

  await itemName.fill('Duplicate Carrot');
  await itemCard.getByLabel(strings.itemCategoryLabel).fill('Vegetables');
  await itemCard.getByLabel(strings.itemUnitLabel).fill('kg');
  await itemCard.getByLabel(strings.itemThresholdLabel).fill('5');
  await itemCard.getByRole('button', { name: strings.createItemButton }).click();

  await expectCleanAlert(page, 'Inventory item name already exists in this branch.');
  await checkNoOverflow(page, `inventory-${language}`, viewportLabel);
  await screenshot(page, viewportLabel, language, 'add-item');
}

async function verifyAdjustmentFlow(page, strings, viewportLabel, language) {
  const itemsCard = page.locator('section.ui-card').filter({ has: page.getByRole('heading', { name: strings.itemsCardTitle }) }).first();
  await itemsCard.getByRole('button', { name: strings.adjustStockButton }).first().click();

  const dialog = page.getByRole('dialog', { name: strings.adjustStockDialogTitle });
  await dialog.waitFor({ state: 'visible', timeout: 30000 });

  const quantityInput = dialog.getByLabel(strings.adjustmentQuantityLabel);
  await expectFocused(quantityInput, 'Adjustment quantity input');
  await page.waitForTimeout(800);
  await expectNearTop(quantityInput, 'Adjustment quantity input');

  await quantityInput.fill('2');
  await dialog.getByLabel(strings.adjustmentReasonLabel).selectOption('Physical count correction');
  await dialog.getByLabel(strings.adjustmentNoteLabel).fill('QA stock adjustment');
  await dialog.getByRole('button', { name: strings.confirmAdjustmentButton }).click();

  await expectCleanAlert(page, 'Could not record the stock adjustment right now.');
  await checkNoOverflow(page, `inventory-${language}`, viewportLabel);
  await screenshot(page, viewportLabel, language, 'adjust-stock');
}

async function verifyBatchProductionFlow(page, strings, viewportLabel, language) {
  const batchCard = page.locator('section.ui-card').filter({ has: page.getByRole('heading', { name: strings.batchProductionTitle }) }).first();
  const batchForm = batchCard.locator('form').first();
  await batchForm.getByLabel(strings.batchProductionMenuItemLabel).waitFor({ state: 'visible', timeout: 30000 });

  await batchForm.getByLabel(strings.batchProductionMenuItemLabel).selectOption('menu-carrot');
  await batchForm.getByLabel(strings.batchProductionQuantityLabel).fill('1');
  await batchForm.getByRole('button', { name: strings.batchProductionSaveButton }).click();
  await expectCleanAlert(page, 'Batch production requires a recipe for the current branch.');

  await batchForm.getByLabel(strings.batchProductionQuantityLabel).fill('1400');
  await batchForm.getByRole('button', { name: strings.batchProductionSaveButton }).click();
  await expectCleanAlert(page, 'Insufficient stock for batch production: Carrot requires 1400.');

  await checkNoOverflow(page, `inventory-${language}`, viewportLabel);
  await screenshot(page, viewportLabel, language, 'batch-production');
}

async function verifyWastageFlow(page, strings, viewportLabel, language) {
  const batchCard = page.locator('section.ui-card').filter({ has: page.getByRole('heading', { name: strings.batchProductionTitle }) }).first();
  const wastageForm = batchCard.locator('form').nth(1);
  await wastageForm.getByLabel(strings.wastageMenuItemLabel).waitFor({ state: 'visible', timeout: 30000 });

  await wastageForm.getByLabel(strings.wastageMenuItemLabel).selectOption('menu-carrot');
  await wastageForm.getByLabel(strings.wastageQuantityLabel).fill('2');
  await wastageForm.getByLabel(strings.wastageReasonLabel).fill('Spoilage');
  await wastageForm.getByLabel(strings.wastageNotesLabel).fill('QA wastage check');
  await wastageForm.getByRole('button', { name: strings.wastageSaveButton }).click();

  await expectCleanAlert(page, 'Could not record prepared stock wastage right now.');
  await checkNoOverflow(page, `inventory-${language}`, viewportLabel);
  await screenshot(page, viewportLabel, language, 'wastage');
}

async function runViewportLanguage(browser, viewport, language) {
  const strings = labels(language);
  const context = await browser.newContext({ viewport });

  try {
    await seedContext(context, language);
    await routeInventoryApis(context);

    if (language === 'ta') {
      const page = await context.newPage();
      try {
        await loadInventoryPage(page, strings, viewport.label, language);
        await verifyAddItemFlow(page, strings, viewport.label, language);
      } finally {
        await page.close();
      }

      console.log('PASS', language, viewport.label, '(smoke)');
      return;
    }

    {
      const page = await context.newPage();
      try {
        await loadInventoryPage(page, strings, viewport.label, language);
        await verifyAddItemFlow(page, strings, viewport.label, language);
      } finally {
        await page.close();
      }
    }

    {
      const page = await context.newPage();
      try {
        await loadInventoryPage(page, strings, viewport.label, language);
        await verifyAdjustmentFlow(page, strings, viewport.label, language);
      } finally {
        await page.close();
      }
    }

    {
      const page = await context.newPage();
      try {
        await loadInventoryPage(page, strings, viewport.label, language);
        await verifyBatchProductionFlow(page, strings, viewport.label, language);
      } finally {
        await page.close();
      }
    }

    {
      const page = await context.newPage();
      try {
        await loadInventoryPage(page, strings, viewport.label, language);
        await verifyWastageFlow(page, strings, viewport.label, language);
      } finally {
        await page.close();
      }
    }

    console.log('PASS', language, viewport.label);
  } finally {
    await context.close();
  }
}

async function main() {
  ensureDir(screenshotDir);

  const browser = await chromium.launch({
    headless: true,
    executablePath: chromiumPath || undefined,
  });

  try {
    for (const viewport of VIEWPORTS) {
      for (const language of LANGUAGES) {
        await runViewportLanguage(browser, viewport, language);
      }
    }
  } finally {
    await browser.close();
  }
}

main().catch(error => {
  console.error('QA_FAIL', error && error.stack ? error.stack : String(error));
  process.exitCode = 1;
});
