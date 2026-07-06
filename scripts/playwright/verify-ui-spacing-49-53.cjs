/**
 * verify-ui-spacing-49-53.cjs
 *
 * Browser verification for the UI spacing fixes in issues #49-53.
 * Visits only these routes:
 *   - /inventory
 *   - /orders-preview
 *   - /pos/orders
 *   - /admin/menu
 *
 * Usage:
 *   node scripts/playwright/verify-ui-spacing-49-53.cjs
 *
 * Environment variables:
 *   BILLSOFT_BROWSER_QA_APP_URL       - app origin (default http://localhost:3010)
 *   BILLSOFT_CHROMIUM_PATH            - custom Chromium binary path
 *   BILLSOFT_BROWSER_QA_SCREENSHOT_DIR - output dir (default output/playwright/ui-spacing-49-53)
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
const appUrl = process.env.BILLSOFT_BROWSER_QA_APP_URL ?? 'http://localhost:3010';
const chromiumPath = process.env.BILLSOFT_CHROMIUM_PATH ?? '';
const screenshotDir = process.env.BILLSOFT_BROWSER_QA_SCREENSHOT_DIR
  ?? path.join(repoRoot, 'output', 'playwright', 'ui-spacing-49-53');
const userDataRoot = path.join(repoRoot, '.tmp', 'playwright-user-data');

const VIEWPORTS = [
  { label: '1366x768', width: 1366, height: 768 },
  { label: '1024x768', width: 1024, height: 768 },
  { label: '768x1024', width: 768, height: 1024 },
  { label: '390x844', width: 390, height: 844 },
];

const AUTH_SESSION = {
  accessToken: 'access-token-playwright',
  refreshToken: 'refresh-token-playwright',
  accessTokenExpiresAtUtc: '2099-06-11T10:15:00Z',
  refreshTokenExpiresAtUtc: '2099-06-18T10:15:00Z',
  userId: 'user-playwright',
  restaurantId: 'restaurant-1',
  restaurantCode: 'BILL01',
  countryCode: 'IN',
  currencyCode: 'INR',
  timeZoneId: 'Asia/Kolkata',
  branchId: 'branch-1',
  fullName: 'QA Tester',
  mobileNumber: '9000000099',
  roles: ['Admin'],
  permissions: [
    'Inventory.View',
    'Inventory.Adjust',
    'Branch.Manage',
    'User.Manage',
    'MenuCategory.Manage',
    'MenuItem.Manage',
    'MenuItem.View',
    'Order.View',
    'Order.Create',
    'Order.Cancel',
  ],
  activeRole: 'Admin',
};

const now = '2026-06-21T10:00:00Z';

const BRANCHES = {
  items: [
    {
      branchId: 'branch-1',
      restaurantId: 'restaurant-1',
      name: 'Main Branch',
      address: '123 Market Street',
      phone: '60000000',
      timezone: 'Asia/Kolkata',
      currency: 'INR',
      status: 'Active',
      createdAt: now,
      updatedAt: now,
    },
  ],
};

const INVENTORY_ITEMS = {
  items: [
    {
      inventoryItemId: 'inventory-1',
      restaurantId: 'restaurant-1',
      branchId: 'branch-1',
      name: 'Rice',
      normalizedName: 'RICE',
      category: 'Grains',
      unitOfMeasure: 'kg',
      lowStockThreshold: 5,
      isActive: true,
      currentStock: 12,
      status: 'Healthy',
      createdAtUtc: now,
      updatedAtUtc: now,
    },
    {
      inventoryItemId: 'inventory-2',
      restaurantId: 'restaurant-1',
      branchId: 'branch-1',
      name: 'Oil',
      normalizedName: 'OIL',
      category: 'Cooking',
      unitOfMeasure: 'l',
      lowStockThreshold: 4,
      isActive: true,
      currentStock: 2,
      status: 'Low stock',
      createdAtUtc: now,
      updatedAtUtc: now,
    },
    {
      inventoryItemId: 'inventory-3',
      restaurantId: 'restaurant-1',
      branchId: 'branch-1',
      name: 'Chilli Powder',
      normalizedName: 'CHILLI POWDER',
      category: 'Spices',
      unitOfMeasure: 'kg',
      lowStockThreshold: 2,
      isActive: true,
      currentStock: 0,
      status: 'Out of stock',
      createdAtUtc: now,
      updatedAtUtc: now,
    },
  ],
};

const INVENTORY_SUMMARY = {
  restaurantId: 'restaurant-1',
  branchId: 'branch-1',
  totalItems: 3,
  activeItems: 3,
  inactiveItems: 0,
  lowStockCount: 1,
  outOfStockCount: 1,
  totalCurrentStock: 14,
  recentlyAdjustedCount: 1,
  lowStockItems: [
    {
      inventoryItemId: 'inventory-2',
      name: 'Oil',
      category: 'Cooking',
      unitOfMeasure: 'l',
      lowStockThreshold: 4,
      currentStock: 2,
      status: 'Low stock',
    },
  ],
  outOfStockItems: [
    {
      inventoryItemId: 'inventory-3',
      name: 'Chilli Powder',
      category: 'Spices',
      unitOfMeasure: 'kg',
      lowStockThreshold: 2,
      currentStock: 0,
      status: 'Out of stock',
    },
  ],
};

const MENU_CATEGORIES = {
  items: [
    {
      menuCategoryId: 'category-1',
      restaurantId: 'restaurant-1',
      name: 'Breakfast',
      displayOrder: 1,
      status: 'Active',
      createdAt: now,
      updatedAt: now,
    },
  ],
};

const MENU_ITEMS = {
  items: [
    {
      menuItemId: 'item-1',
      restaurantId: 'restaurant-1',
      menuCategoryId: 'category-1',
      categoryName: 'Breakfast',
      name: 'Masala Dosa',
      description: 'Crisp rice crepe',
      sku: 'DOSA-01',
      basePrice: 2.5,
      taxRate: 0,
      isVegetarian: true,
      isAvailableForEatIn: true,
      isAvailableForParcel: true,
      status: 'Active',
      createdAt: now,
      updatedAt: now,
    },
    {
      menuItemId: 'item-2',
      restaurantId: 'restaurant-1',
      menuCategoryId: 'category-1',
      categoryName: 'Breakfast',
      name: 'Idli Sambar',
      description: 'Steamed rice cakes',
      sku: 'IDLI-01',
      basePrice: 1.75,
      taxRate: 0,
      isVegetarian: true,
      isAvailableForEatIn: true,
      isAvailableForParcel: true,
      status: 'Active',
      createdAt: now,
      updatedAt: now,
    },
  ],
};

const MENU_ITEM_DETAIL = {
  menuItemId: 'item-1',
  restaurantId: 'restaurant-1',
  menuCategoryId: 'category-1',
  categoryName: 'Breakfast',
  name: 'Masala Dosa',
  description: 'Crisp rice crepe',
  sku: 'DOSA-01',
  basePrice: 2.5,
  taxRate: 0,
  isVegetarian: true,
  isAvailableForEatIn: true,
  isAvailableForParcel: true,
  status: 'Active',
  createdAt: now,
  updatedAt: now,
};

const MENU_ITEM_RECIPE = {
  menuItemId: 'item-1',
  menuItemName: 'Masala Dosa',
  branchId: 'branch-1',
  branchName: 'Main Branch',
  ingredients: [
    {
      menuItemRecipeIngredientId: 'recipe-1',
      menuItemId: 'item-1',
      inventoryItemId: 'inventory-1',
      inventoryItemName: 'Rice',
      quantityRequired: 1.5,
      createdAtUtc: now,
      updatedAtUtc: now,
    },
  ],
};

const MENU_PRICE_HISTORY = {
  items: [
    {
      menuItemPriceHistoryId: 'price-1',
      menuItemId: 'item-1',
      restaurantId: 'restaurant-1',
      branchId: 'branch-1',
      oldBasePrice: 2,
      newBasePrice: 2.5,
      oldTaxRate: 0,
      newTaxRate: 0,
      changedByUserName: 'QA Tester',
      reason: 'Initial pricing',
      createdAt: now,
    },
  ],
};

const POS_ORDER_LIST = {
  items: [
    {
      posOrderId: 'order-1',
      branchId: 'branch-1',
      orderNumber: 'ORD-20260621-0001',
      orderType: 'EatIn',
      status: 'Draft',
      tableName: 'T1',
      customerName: 'Walk-in',
      grandTotal: 2.5,
      lineCount: 1,
      createdAt: now,
    },
  ],
};

const POS_ORDER_DETAIL = {
  posOrderId: 'order-1',
  restaurantId: 'restaurant-1',
  branchId: 'branch-1',
  orderNumber: 'ORD-20260621-0001',
  orderType: 'EatIn',
  status: 'Draft',
  tableName: 'T1',
  customerName: 'Walk-in',
  customerMobile: null,
  notes: null,
  subtotal: 2.5,
  taxTotal: 0,
  grandTotal: 2.5,
  confirmedAt: null,
  cancelledAt: null,
  cancelReason: null,
  createdByUserId: 'user-1',
  confirmedByUserId: null,
  cancelledByUserId: null,
  createdAt: now,
  updatedAt: null,
  kitchenTicketId: null,
  kitchenTicketNumber: null,
  kitchenTicketStatus: null,
  lines: [
    {
      posOrderLineId: 'line-1',
      menuItemId: 'item-1',
      menuCategoryId: 'category-1',
      menuItemNameSnapshot: 'Masala Dosa',
      menuCategoryNameSnapshot: 'Breakfast',
      skuSnapshot: 'DOSA-01',
      unitPrice: 2.5,
      taxRate: 0,
      quantity: 1,
      lineSubtotal: 2.5,
      lineTax: 0,
      lineTotal: 2.5,
      notes: 'Less spicy',
      displayOrder: 1,
      createdAt: now,
      updatedAt: null,
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
  await page.addInitScript(({ key, session }) => {
    localStorage.setItem(key, JSON.stringify(session));
  }, { key: 'billsoft.auth.session.v1', session: AUTH_SESSION });
}

async function setupRoutes(page) {
  await page.route('**/api/v1/admin/branches**', route => {
    route.fulfill(json(BRANCHES));
  });

  await page.route('**/api/v1/inventory/items**', route => {
    const url = new URL(route.request().url());
    if (route.request().method() === 'GET' && url.pathname.match(/\/api\/v1\/inventory\/items\/[^/]+\/movements$/)) {
      return route.fulfill(json({ items: [] }));
    }

    return route.fulfill(json(INVENTORY_ITEMS));
  });

  await page.route('**/api/v1/inventory/summary**', route => {
    route.fulfill(json(INVENTORY_SUMMARY));
  });

  await page.route('**/api/v1/admin/menu/categories**', route => {
    route.fulfill(json(MENU_CATEGORIES));
  });

  await page.route('**/api/v1/admin/menu/items**', route => {
    const url = new URL(route.request().url());
    const match = url.pathname.match(/\/api\/v1\/admin\/menu\/items\/([^/]+)(?:\/(recipe|price-history))?$/);

    if (match && route.request().method() === 'GET') {
      if (match[2] === 'recipe') {
        return route.fulfill(json(MENU_ITEM_RECIPE));
      }

      if (match[2] === 'price-history') {
        return route.fulfill(json(MENU_PRICE_HISTORY));
      }

      if (match[1] === 'item-1') {
        return route.fulfill(json(MENU_ITEM_DETAIL));
      }
    }

    return route.fulfill(json(MENU_ITEMS));
  });

  await page.route('**/api/v1/menu/categories**', route => {
    route.fulfill(json(MENU_CATEGORIES));
  });

  await page.route('**/api/v1/menu/items**', route => {
    route.fulfill(json(MENU_ITEMS));
  });

  await page.route('**/api/v1/pos/workspace/branches**', route => {
    route.fulfill(json(BRANCHES));
  });

  await page.route('**/api/v1/pos/orders**', route => {
    const url = new URL(route.request().url());
    const pathname = url.pathname;

    if (route.request().method() === 'GET' && /\/api\/v1\/pos\/orders\/[^/]+$/.test(pathname)) {
      return route.fulfill(json(POS_ORDER_DETAIL));
    }

    return route.fulfill(json(POS_ORDER_LIST));
  });
}

async function expectVisible(page, selector, label) {
  await page.waitForSelector(selector, { timeout: 30000 });
  console.log('VISIBLE', label);
}

async function checkOverflow(page) {
  return page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
}

async function screenshot(page, viewportLabel, routeLabel) {
  const targetDir = path.join(screenshotDir, viewportLabel);
  ensureDir(targetDir);
  const file = path.join(targetDir, `${routeLabel}.png`);
  await page.screenshot({ path: file, fullPage: true });
  console.log('SCREENSHOT', file);
}

async function runRoute(page, route, viewportLabel) {
  await page.goto(`${appUrl}${route.path}`, { waitUntil: 'domcontentloaded', timeout: 30000 });
  await route.ready(page);
  const overflow = await checkOverflow(page);
  console.log('OVERFLOW', route.label, viewportLabel, overflow ? 'yes' : 'no');
  if (overflow) {
    throw new Error(`Horizontal overflow detected on ${route.path} at ${viewportLabel}`);
  }
  await screenshot(page, viewportLabel, route.label);
}

async function main() {
  ensureDir(userDataRoot);
  ensureDir(screenshotDir);

  const userDataDir = path.join(userDataRoot, `ui-spacing-${Date.now()}`);
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
        label: 'inventory',
        path: '/inventory',
        ready: async currentPage => {
          await expectVisible(currentPage, 'text=Inventory workspace', 'inventory heading');
          await currentPage.getByRole('checkbox', { name: /active item/i }).waitFor({ state: 'visible', timeout: 30000 });
        },
      },
      {
        label: 'orders-preview',
        path: '/orders-preview',
        ready: async currentPage => {
          await expectVisible(currentPage, 'text=Order workspace preview', 'orders preview heading');
        },
      },
      {
        label: 'pos-orders',
        path: '/pos/orders',
        ready: async currentPage => {
          await expectVisible(currentPage, 'text=POS order capture', 'pos heading');
          await currentPage.getByRole('button', { name: /masala dosa/i }).first().click();
          await currentPage.getByRole('button', { name: /remove line/i }).waitFor({ state: 'visible', timeout: 30000 });
        },
      },
      {
        label: 'admin-menu',
        path: '/admin/menu',
        ready: async currentPage => {
          await expectVisible(currentPage, 'text=Menu management', 'menu heading');
        },
      },
    ];

    for (const viewport of VIEWPORTS) {
      await page.setViewportSize({ width: viewport.width, height: viewport.height });
      console.log('VIEWPORT', viewport.label);
      for (const route of routes) {
        await runRoute(page, route, viewport.label);
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
