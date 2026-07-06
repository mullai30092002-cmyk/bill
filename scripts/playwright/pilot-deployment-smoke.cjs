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

  throw new Error(
    [
      'Unable to load Playwright.',
      'Set BILLSOFT_PLAYWRIGHT_CORE_PATH to a resolvable playwright-core install,',
      'or run this script in an environment where `playwright-core`/`playwright` is available.',
    ].join(' ')
  );
}

function readRequiredEnv(name) {
  const value = process.env[name];
  if (!value || !value.trim()) {
    throw new Error(`Missing required environment variable: ${name}`);
  }

  return value.trim();
}

function ensureDir(dirPath) {
  fs.mkdirSync(dirPath, { recursive: true });
}

function assertFileExists(filePath, envName) {
  if (!fs.existsSync(filePath)) {
    throw new Error(`The file referenced by ${envName} does not exist: ${filePath}`);
  }
}

function safeFileName(value) {
  return value.replace(/[^a-z0-9._-]+/gi, '-').replace(/-+/g, '-').replace(/^-|-$/g, '').toLowerCase();
}

const repoRoot = path.resolve(__dirname, '..', '..');
const baseUrl = new URL(readRequiredEnv('BILLSOFT_BASE_URL'));
const restaurantCode = readRequiredEnv('BILLSOFT_RESTAURANT_CODE');
const staffMobile = readRequiredEnv('BILLSOFT_STAFF_MOBILE');
const staffPassword = readRequiredEnv('BILLSOFT_STAFF_PASSWORD');
const ownerMobile = readRequiredEnv('BILLSOFT_OWNER_MOBILE');
const ownerPassword = readRequiredEnv('BILLSOFT_OWNER_PASSWORD');
const ocrSampleFile = path.resolve(readRequiredEnv('BILLSOFT_OCR_SAMPLE_FILE'));
const chromiumPath = process.env.BILLSOFT_CHROMIUM_PATH ?? '';
const screenshotDir = process.env.BILLSOFT_SMOKE_SCREENSHOT_DIR
  ?? path.join(repoRoot, 'output', 'playwright', 'pilot-deployment-smoke');

const { chromium } = resolveModule();

assertFileExists(ocrSampleFile, 'BILLSOFT_OCR_SAMPLE_FILE');

const languageStorageKey = 'billsoft.language';
const viewport = { width: 1440, height: 1000 };
const pageTimeoutMs = 30000;

const results = [];

function record(step, status, details = '') {
  const payload = details ? `${step} :: ${status} :: ${details}` : `${step} :: ${status}`;
  results.push({ step, status, details });
  console.log(payload);
}

function resolveUrl(pathname, search = '') {
  const url = new URL(pathname, baseUrl);
  if (search) {
    url.search = search;
  }

  return url.toString();
}

async function capture(page, label) {
  ensureDir(screenshotDir);
  const screenshotPath = path.join(screenshotDir, `${safeFileName(label)}.png`);
  await page.screenshot({ path: screenshotPath, fullPage: true });
  console.log(`SCREENSHOT ${screenshotPath}`);
}

async function createContext(browser) {
  const context = await browser.newContext({ viewport });
  await context.addInitScript(
    ({ key, value }) => {
      localStorage.setItem(key, value);
    },
    {
      key: languageStorageKey,
      value: 'en',
    }
  );
  return context;
}

async function signIn(page, { mobileNumber, password }) {
  await page.goto(resolveUrl('/login'), { waitUntil: 'networkidle' });
  await page.getByLabel('Restaurant code').fill(restaurantCode);
  await page.getByLabel('Mobile number').fill(mobileNumber);
  await page.locator('#password').fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await page.waitForURL(url => !url.pathname.startsWith('/login'), { timeout: pageTimeoutMs });
  await page.waitForLoadState('networkidle');
}

async function expectHeading(page, heading, label) {
  const headingLocator = page.getByRole('heading', { name: heading });
  await headingLocator.waitFor({ state: 'visible', timeout: pageTimeoutMs });

  const notAuthorized = page.getByRole('heading', { name: /not authorized/i });
  if (await notAuthorized.count()) {
    throw new Error(`${label} loaded a not-authorized state instead of the expected workspace.`);
  }
}

async function verifyRoute(page, route, heading, label, extraAssertions = []) {
  await page.goto(resolveUrl(route), { waitUntil: 'networkidle' });
  await expectHeading(page, heading, label);

  for (const assertion of extraAssertions) {
    await assertion(page);
  }

  record(label, 'PASS', page.url());
  await capture(page, label);
}

async function selectFirstNonEmptyOption(selectLocator) {
  const optionCount = await selectLocator.locator('option').count();
  if (optionCount > 1) {
    await selectLocator.selectOption({ index: 1 });
    return true;
  }

  return false;
}

async function verifyOcr(page) {
  await page.goto(resolveUrl('/vendors'), { waitUntil: 'networkidle' });
  await expectHeading(page, /vendor workspace/i, 'Vendor OCR');

  await page.getByLabel('Vendor bill file').setInputFiles(ocrSampleFile);
  await page.getByRole('button', { name: /review extracted bill/i }).click();

  await expectHeading(page, /review extracted bill/i, 'Vendor OCR review');

  const warningsRegion = page.getByLabel('OCR warnings');
  const warningsVisible = await warningsRegion.count();
  if (!warningsVisible) {
    throw new Error('OCR did not render the warnings region after upload.');
  }

  const reviewRequired = await page.getByText(/review required/i).count();
  const lowConfidence = await page.getByText(/low confidence/i).count();
  if (!reviewRequired && !lowConfidence) {
    throw new Error('OCR sample did not trigger review-required or low-confidence guidance.');
  }

  const bodyText = await page.locator('body').innerText();
  if (/rfc7807|stack trace|sqlexception|raw-azure/i.test(bodyText)) {
    throw new Error('OCR page exposed raw provider or stack-trace text.');
  }

  const vendorSelect = page.getByLabel('Vendor');
  if (await vendorSelect.count()) {
    await selectFirstNonEmptyOption(vendorSelect.first());
  }

  const inventorySelects = page.getByLabel('Inventory item');
  const inventoryCount = await inventorySelects.count();
  for (let index = 0; index < inventoryCount; index++) {
    const select = inventorySelects.nth(index);
    if (await select.isDisabled()) {
      continue;
    }

    await selectFirstNonEmptyOption(select);
  }

  const saveButton = page.getByRole('button', { name: /save review/i });
  if (await saveButton.isEnabled()) {
    await saveButton.click();
    await page.getByText(/draft review saved/i).waitFor({ state: 'visible', timeout: pageTimeoutMs });
    record('OCR save review', 'PASS');
  } else {
    record('OCR save review', 'SKIP', 'Draft remained blocked after upload and mapping.');
  }

  const createButton = page.getByRole('button', { name: /create vendor bill/i });
  if (await createButton.isEnabled()) {
    await createButton.click();
    await page.getByText(/vendor bill created/i).waitFor({ state: 'visible', timeout: pageTimeoutMs });
    record('OCR create bill', 'PASS');
  } else {
    record('OCR create bill', 'SKIP', 'Draft remained blocked after review.');
  }

  await capture(page, 'vendor-ocr');
}

async function main() {
  ensureDir(screenshotDir);

  const browser = await chromium.launch({
    headless: true,
    ...(chromiumPath ? { executablePath: chromiumPath } : {}),
    args: [
      '--disable-gpu',
      '--disable-software-rasterizer',
      '--disable-dev-shm-usage',
      '--no-first-run',
      '--no-default-browser-check',
    ],
  });

  try {
    const staffContext = await createContext(browser);
    const staffPage = await staffContext.newPage();
    staffPage.setDefaultTimeout(pageTimeoutMs);
    staffPage.setDefaultNavigationTimeout(pageTimeoutMs);

    await signIn(staffPage, { mobileNumber: staffMobile, password: staffPassword });
    record('Staff login', 'PASS', staffPage.url());
    await capture(staffPage, 'staff-login');

    await verifyRoute(staffPage, '/pos/orders', /pos order capture/i, 'POS orders');
    await verifyRoute(staffPage, '/kitchen/tickets', /kitchen display/i, 'Kitchen tickets');
    await verifyRoute(staffPage, '/billing', /billing workspace/i, 'Billing workspace');
    await verifyRoute(staffPage, '/cashier/shifts', /cashier shifts/i, 'Cashier shifts');

    const ownerContext = await createContext(browser);
    const ownerPage = await ownerContext.newPage();
    ownerPage.setDefaultTimeout(pageTimeoutMs);
    ownerPage.setDefaultNavigationTimeout(pageTimeoutMs);

    await signIn(ownerPage, { mobileNumber: ownerMobile, password: ownerPassword });
    record('Owner login', 'PASS', ownerPage.url());
    await capture(ownerPage, 'owner-login');

    await verifyRoute(ownerPage, '/owner/dashboard', /owner dashboard/i, 'Owner dashboard');
    await verifyRoute(ownerPage, '/reports/daily-cash-sales', /daily cash sales report/i, 'Daily cash sales report', [
      async page => {
        await page.getByText(/gross bill total/i).waitFor({ state: 'visible', timeout: pageTimeoutMs });
      },
    ]);
    await verifyRoute(ownerPage, '/vendors/statement', /vendor statement/i, 'Vendor statement', [
      async page => {
        await page.getByText(/current outstanding/i).waitFor({ state: 'visible', timeout: pageTimeoutMs });
      },
    ]);

    await verifyOcr(ownerPage);

    console.log('RESULT', JSON.stringify(results));
  } finally {
    await browser.close();
  }
}

main().catch(error => {
  console.error('QA_FAIL', error && error.stack ? error.stack : String(error));
  process.exitCode = 1;
});
