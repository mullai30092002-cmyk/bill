/**
 * verify-admin-validation-ux-76-79.cjs
 *
 * Visual verification for the admin validation UX hardening in issues #76-79.
 * Covers:
 *   - /admin/branches
 *   - /admin/menu
 *
 * The script seeds an authenticated session in localStorage and mocks the admin
 * API so the checks stay deterministic without requiring a backend.
 *
 * Usage:
 *   node scripts/playwright/verify-admin-validation-ux-76-79.cjs
 *
 * Environment variables:
 *   BILLSOFT_BROWSER_QA_APP_URL        - app origin (default http://localhost:3000)
 *   BILLSOFT_BROWSER_QA_SCREENSHOT_DIR - screenshot output dir
 *                                        (default output/playwright/admin-validation-ux-76-79)
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
const appUrl = process.env.BILLSOFT_BROWSER_QA_APP_URL ?? 'http://localhost:3000';
const chromiumPath = process.env.BILLSOFT_CHROMIUM_PATH ?? '';
const screenshotDir = process.env.BILLSOFT_BROWSER_QA_SCREENSHOT_DIR
  ?? path.join(repoRoot, 'output', 'playwright', 'admin-validation-ux-76-79');

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
  restaurantCode: 'BILL01',
  countryCode: 'IN',
  currencyCode: 'INR',
  timeZoneId: 'Asia/Singapore',
  branchId: 'branch-1',
  fullName: 'QA Tester',
  mobileNumber: '9000000099',
  roles: ['Admin'],
  permissions: [
    'Branch.Manage',
    'MenuCategory.Manage',
    'MenuItem.Manage',
    'MenuItem.View',
    'Inventory.View',
  ],
  activeRole: 'Admin',
};

const NOW = '2026-06-24T09:00:00Z';

const jsonResponse = body => ({
  status: 200,
  contentType: 'application/json',
  body: JSON.stringify(body),
});

const problemResponse = (detail, status = 400, title = 'Bad Request') => ({
  status,
  contentType: 'application/problem+json',
  body: JSON.stringify({
    type: 'https://datatracker.ietf.org/doc/html/rfc7807',
    title,
    status,
    detail,
  }),
});

const BRANCH_LIST = {
  items: [
    {
      branchId: 'branch-1',
      restaurantId: 'restaurant-1',
      name: 'Main Branch',
      address: '123 Market Street',
      phone: '60000001',
      timezone: 'Asia/Singapore',
      currency: 'INR',
      status: 'Active',
      createdAt: NOW,
      updatedAt: NOW,
    },
    {
      branchId: 'branch-2',
      restaurantId: 'restaurant-1',
      name: 'Side Branch',
      address: '456 Side Street',
      phone: '60000002',
      timezone: 'Asia/Singapore',
      currency: 'INR',
      status: 'Inactive',
      createdAt: NOW,
      updatedAt: NOW,
    },
  ],
};

const BRANCH_DETAIL = {
  branchId: 'branch-1',
  restaurantId: 'restaurant-1',
  name: 'Main Branch',
  address: '123 Market Street',
  phone: '60000001',
  timezone: 'Asia/Singapore',
  currency: 'INR',
  status: 'Active',
  createdAt: NOW,
  updatedAt: NOW,
};

const CATEGORY_LIST = {
  items: [
    {
      menuCategoryId: 'category-1',
      restaurantId: 'restaurant-1',
      name: 'Breakfast',
      displayOrder: 1,
      status: 'Active',
      createdAt: NOW,
      updatedAt: NOW,
    },
    {
      menuCategoryId: 'category-2',
      restaurantId: 'restaurant-1',
      name: 'Snacks',
      displayOrder: 2,
      status: 'Inactive',
      createdAt: NOW,
      updatedAt: NOW,
    },
  ],
};

const CATEGORY_DETAIL = {
  menuCategoryId: 'category-1',
  restaurantId: 'restaurant-1',
  name: 'Breakfast',
  displayOrder: 1,
  status: 'Active',
  createdAt: NOW,
  updatedAt: NOW,
};

const ITEM_LIST = {
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
      isVegetarian: false,
      isAvailableForEatIn: true,
      isAvailableForParcel: true,
      status: 'Active',
      createdAt: NOW,
      updatedAt: NOW,
    },
    {
      menuItemId: 'item-2',
      restaurantId: 'restaurant-1',
      menuCategoryId: 'category-2',
      categoryName: 'Snacks',
      name: 'Vada',
      description: null,
      sku: 'VADA-01',
      basePrice: 1.75,
      taxRate: 0,
      isVegetarian: false,
      isAvailableForEatIn: true,
      isAvailableForParcel: false,
      status: 'Inactive',
      createdAt: NOW,
      updatedAt: NOW,
    },
  ],
};

const ITEM_DETAIL = {
  menuItemId: 'item-1',
  restaurantId: 'restaurant-1',
  menuCategoryId: 'category-1',
  categoryName: 'Breakfast',
  name: 'Masala Dosa',
  description: 'Crisp rice crepe',
  sku: 'DOSA-01',
  basePrice: 2.5,
  taxRate: 0,
  isVegetarian: false,
  isAvailableForEatIn: true,
  isAvailableForParcel: true,
  status: 'Active',
  createdAt: NOW,
  updatedAt: NOW,
};

const INVENTORY_LIST = {
  items: [
    {
      inventoryItemId: 'inventory-1',
      restaurantId: 'restaurant-1',
      branchId: 'branch-1',
      name: 'Rice Flour',
      normalizedName: 'RICE FLOUR',
      category: 'Ingredients',
      unitOfMeasure: 'kg',
      lowStockThreshold: 5,
      isActive: true,
      createdAt: NOW,
      updatedAt: NOW,
    },
  ],
};

const emptyRecipe = {
  menuItemId: 'item-1',
  menuItemName: 'Masala Dosa',
  branchId: 'branch-1',
  branchName: 'Main Branch',
  recipeItems: [],
  totalIngredientCount: 0,
  lastUpdatedAt: NOW,
};

const priceHistory = {
  items: [
    {
      menuItemId: 'item-1',
      previousBasePrice: 2.25,
      newBasePrice: 2.5,
      changedAt: NOW,
      changedByUserName: 'QA Tester',
    },
  ],
};

function ensureDir(dirPath) {
  fs.mkdirSync(dirPath, { recursive: true });
}

async function seedAuth(page, language) {
  await page.addInitScript(
    ({ session, languageKey, sessionKey, lang }) => {
      localStorage.setItem(languageKey, lang);
      localStorage.setItem(sessionKey, JSON.stringify(session));
    },
    {
      session: AUTH_SESSION,
      languageKey: 'billsoft.language',
      sessionKey: 'billsoft.auth.session.v1',
      lang: language,
    }
  );
}

async function routeAdminApis(page) {
  await page.route('**/api/v1/**', async route => {
    const url = new URL(route.request().url());
    const { pathname, searchParams } = url;
    const method = route.request().method().toUpperCase();

    if (method === 'GET' && pathname === '/api/v1/admin/branches') {
      return route.fulfill(jsonResponse(BRANCH_LIST));
    }

    if (method === 'GET' && pathname === '/api/v1/admin/branches/branch-1') {
      return route.fulfill(jsonResponse(BRANCH_DETAIL));
    }

    if (method === 'POST' && pathname === '/api/v1/admin/branches') {
      return route.fulfill(problemResponse('Branch name already exists in this restaurant.'));
    }

    if (method === 'PUT' && pathname === '/api/v1/admin/branches/branch-1') {
      return route.fulfill(jsonResponse(BRANCH_DETAIL));
    }

    if (method === 'POST' && pathname === '/api/v1/admin/branches/branch-1/deactivate') {
      return route.fulfill(problemResponse('Branch cannot be deactivated while active users are assigned.'));
    }

    if (method === 'POST' && pathname === '/api/v1/admin/branches/branch-1/activate') {
      return route.fulfill(jsonResponse(BRANCH_DETAIL));
    }

    if (method === 'GET' && pathname === '/api/v1/admin/menu/categories') {
      return route.fulfill(jsonResponse(CATEGORY_LIST));
    }

    if (method === 'GET' && pathname === '/api/v1/admin/menu/categories/category-1') {
      return route.fulfill(jsonResponse(CATEGORY_DETAIL));
    }

    if (method === 'POST' && pathname === '/api/v1/admin/menu/categories') {
      return route.fulfill(problemResponse('Category name already exists in this restaurant.'));
    }

    if (method === 'PUT' && pathname === '/api/v1/admin/menu/categories/category-1') {
      return route.fulfill(jsonResponse(CATEGORY_DETAIL));
    }

    if (method === 'POST' && pathname === '/api/v1/admin/menu/categories/category-1/deactivate') {
      return route.fulfill(problemResponse('Category cannot be deactivated while active menu items exist.'));
    }

    if (method === 'POST' && pathname === '/api/v1/admin/menu/categories/category-1/activate') {
      return route.fulfill(jsonResponse(CATEGORY_DETAIL));
    }

    if (method === 'GET' && pathname === '/api/v1/admin/menu/items') {
      return route.fulfill(jsonResponse(ITEM_LIST));
    }

    if (method === 'GET' && pathname === '/api/v1/admin/menu/items/item-1') {
      return route.fulfill(jsonResponse(ITEM_DETAIL));
    }

    if (method === 'GET' && pathname === '/api/v1/admin/menu/items/item-1/recipe') {
      return route.fulfill(jsonResponse(emptyRecipe));
    }

    if (method === 'GET' && pathname === '/api/v1/admin/menu/items/item-1/price-history') {
      return route.fulfill(jsonResponse(priceHistory));
    }

    if (method === 'POST' && pathname === '/api/v1/admin/menu/items') {
      return route.fulfill(problemResponse('SKU already exists. Please enter a unique SKU.'));
    }

    if (method === 'PUT' && pathname === '/api/v1/admin/menu/items/item-1') {
      return route.fulfill(jsonResponse(ITEM_DETAIL));
    }

    if (method === 'POST' && pathname === '/api/v1/admin/menu/items/item-1/deactivate') {
      return route.fulfill(jsonResponse(ITEM_DETAIL));
    }

    if (method === 'POST' && pathname === '/api/v1/admin/menu/items/item-1/activate') {
      return route.fulfill(jsonResponse(ITEM_DETAIL));
    }

    if (method === 'PUT' && pathname === '/api/v1/admin/menu/items/item-1/recipe') {
      return route.fulfill(jsonResponse(emptyRecipe));
    }

    if (method === 'POST' && pathname === '/api/v1/admin/menu/import/preview') {
      return route.fulfill(problemResponse('Could not preview the CSV right now.'));
    }

    if (method === 'POST' && pathname === '/api/v1/admin/menu/import/confirm') {
      return route.fulfill(problemResponse('Could not import the CSV right now.'));
    }

    if (method === 'GET' && pathname === '/api/v1/inventory/items') {
      return route.fulfill(jsonResponse(INVENTORY_LIST));
    }

    if (pathname === '/api/v1/admin/users' || pathname.startsWith('/api/v1/admin/users/')) {
      return route.fulfill(jsonResponse({ items: [] }));
    }

    throw new Error(`Unhandled request: ${method} ${pathname}${searchParams.toString() ? `?${searchParams.toString()}` : ''}`);
  });
}

async function verifyNoOverflow(page) {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth + 1);
  if (overflow) {
    throw new Error('Horizontal overflow detected.');
  }
}

async function expectCleanNotice(page, expectedMessage) {
  const notice = page.getByRole('alert');
  await notice.waitFor({ state: 'visible' });
  const text = (await notice.textContent()) ?? '';
  if (!text.includes(expectedMessage)) {
    throw new Error(`Expected notice to include "${expectedMessage}" but saw "${text}"`);
  }
  if (text.includes('"type"') || text.includes('rfc7807') || text.includes('{')) {
    throw new Error(`Notice still appears to expose raw problem JSON: "${text}"`);
  }
}

async function verifyBranchPage(page, language, viewport) {
  await page.goto(`${appUrl}/admin/branches`);
  await page.getByRole('heading', { name: language === 'ta' ? 'கிளை நிர்வாகம்' : 'Branch management' }).waitFor({ state: 'visible' });

  await verifyNoOverflow(page);

  await page.getByRole('button', { name: language === 'ta' ? 'புதிய கிளை' : 'New branch' }).click();
  const branchName = page.getByRole('textbox', { name: language === 'ta' ? 'பெயர்' : 'Name' });
  await branchName.waitFor({ state: 'visible' });
  if (!(await branchName.evaluate(element => document.activeElement === element))) {
    throw new Error('Branch form did not focus the name field.');
  }

  if ((await page.evaluate(() => window.scrollY)) <= 0) {
    throw new Error('Branch form did not scroll into view.');
  }

  await branchName.fill('Dup Branch');
  await page.getByRole('textbox', { name: language === 'ta' ? 'முகவரி' : 'Address' }).fill('123 Market Street');
  await page.getByRole('textbox', { name: language === 'ta' ? 'தொலைபேசி' : 'Phone' }).fill('60000009');
  await page.getByRole('textbox', { name: language === 'ta' ? 'நேர மண்டலம்' : 'Timezone' }).fill('Asia/Singapore');
  await page.getByRole('textbox', { name: language === 'ta' ? 'நாணயம்' : 'Currency' }).fill('INR');
  await page.getByRole('button', { name: language === 'ta' ? 'கிளையை உருவாக்கு' : 'Create branch' }).click();
  await expectCleanNotice(page, 'Branch name already exists in this restaurant.');

  await page.getByRole('button', { name: /edit/i }).first().click();
  await page.getByRole('button', { name: /deactivate branch/i }).click();
  await page.getByRole('button', { name: /confirm deactivate/i }).click();
  await expectCleanNotice(page, 'Branch cannot be deactivated while active users are assigned.');

  await page.screenshot({
    path: path.join(screenshotDir, `branches-${language}-${viewport.label}.png`),
    fullPage: true,
  });
}

async function verifyMenuPage(page, language, viewport) {
  await page.goto(`${appUrl}/admin/menu`);
  await page.getByRole('heading', { name: language === 'ta' ? 'மெனு நிர்வாகம்' : 'Menu management' }).waitFor({ state: 'visible' });

  await verifyNoOverflow(page);

  await page.getByRole('button', { name: language === 'ta' ? 'மெனுவை இறக்குமதி' : 'Import menu' }).click();
  const importTextarea = page.getByLabel(language === 'ta' ? 'CSV மூலம்' : 'CSV source');
  await importTextarea.waitFor({ state: 'visible' });
  if (!(await importTextarea.evaluate(element => document.activeElement === element))) {
    throw new Error('Import panel did not focus the CSV textarea.');
  }
  if ((await page.evaluate(() => window.scrollY)) <= 0) {
    throw new Error('Import panel did not scroll into view.');
  }
  await page.getByRole('button', { name: language === 'ta' ? 'CSV முன்னோட்டம்' : 'Preview CSV' }).click();
  await expectCleanNotice(page, 'Could not preview the CSV right now.');

  await page.getByRole('button', { name: language === 'ta' ? 'புதிய வகை' : 'New category' }).click();
  const categoryName = page.getByRole('textbox', { name: language === 'ta' ? 'வகை பெயர்' : 'Category name' });
  await categoryName.waitFor({ state: 'visible' });
  if (!(await categoryName.evaluate(element => document.activeElement === element))) {
    throw new Error('Category form did not focus the name field.');
  }
  if ((await page.evaluate(() => window.scrollY)) <= 0) {
    throw new Error('Category form did not scroll into view.');
  }
  await categoryName.fill('Duplicate Category');
  await page.getByRole('button', { name: language === 'ta' ? 'வகையை உருவாக்கு' : 'Create category' }).click();
  await expectCleanNotice(page, 'Category name already exists in this restaurant.');

  await page.locator('.menu-management__main').getByRole('button', { name: /edit/i }).first().click();
  await page.getByRole('button', { name: /deactivate category/i }).click();
  await page.getByRole('button', { name: /confirm deactivate/i }).click();
  await expectCleanNotice(page, 'Category cannot be deactivated while active menu items exist.');

  await page.getByRole('button', { name: language === 'ta' ? 'புதிய உருப்படி' : 'New item' }).click();
  const itemCategory = page.locator('.menu-management__detail').getByRole('combobox', { name: language === 'ta' ? 'வகை' : 'Category' });
  await itemCategory.waitFor({ state: 'visible' });
  if (!(await itemCategory.evaluate(element => document.activeElement === element))) {
    throw new Error('Item form did not focus the category select.');
  }
  if ((await page.evaluate(() => window.scrollY)) <= 0) {
    throw new Error('Item form did not scroll into view.');
  }

  await itemCategory.selectOption('category-1');
  await page.getByRole('textbox', { name: language === 'ta' ? 'உருப்படி பெயர்' : 'Item name' }).fill('Duplicate Item');
  await page.getByRole('textbox', { name: language === 'ta' ? 'SKU' : 'SKU' }).fill('SKU-01');
  await page.getByRole('textbox', { name: language === 'ta' ? 'விவரம்' : 'Description' }).fill('Crisp rice crepe');
  await page.getByRole('textbox', { name: language === 'ta' ? 'அடிப்படை விலை' : 'Base price' }).fill('2.50');
  await page.getByRole('textbox', { name: language === 'ta' ? 'வரி வீதம்' : 'Tax rate' }).fill('0');
  await page.getByRole('button', { name: language === 'ta' ? 'உருப்படியை உருவாக்கு' : 'Create item' }).click();
  await expectCleanNotice(page, 'SKU already exists. Please enter a unique SKU.');

  await page.screenshot({
    path: path.join(screenshotDir, `menu-${language}-${viewport.label}.png`),
    fullPage: true,
  });
}

async function main() {
  ensureDir(screenshotDir);
  const browser = await chromium.launch({
    headless: true,
    executablePath: chromiumPath || undefined,
  });

  try {
    for (const viewport of VIEWPORTS) {
      for (const language of ['en', 'ta']) {
        const context = await browser.newContext({ viewport });
        const page = await context.newPage();
        try {
          await seedAuth(page, language);
          await routeAdminApis(page);
          await verifyBranchPage(page, language, viewport);
          await verifyMenuPage(page, language, viewport);
        } finally {
          await page.close();
          await context.close();
        }
      }
    }
  } finally {
    await browser.close();
  }
}

main().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
