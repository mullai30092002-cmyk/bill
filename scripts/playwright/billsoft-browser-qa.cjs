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
    } catch (error) {
      // Keep trying the next candidate.
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

const { chromium } = resolveModule();

const repoRoot = path.resolve(__dirname, '..', '..');
const appUrl = process.env.BILLSOFT_BROWSER_QA_APP_URL ?? 'http://localhost:3010';
const apiUrl = process.env.BILLSOFT_BROWSER_QA_API_URL ?? 'http://127.0.0.1:5000';
const restaurantCode = process.env.BILLSOFT_BROWSER_QA_RESTAURANT_CODE ?? 'DEMO';
const mobileNumber = process.env.BILLSOFT_BROWSER_QA_MOBILE_NUMBER ?? '9000000002';
const password = process.env.BILLSOFT_BROWSER_QA_PASSWORD ?? 'DemoInventory123!';
const reviewDraftId = process.env.BILLSOFT_BROWSER_QA_REVIEW_DRAFT_ID ?? '';
const duplicateDraftId = process.env.BILLSOFT_BROWSER_QA_DUPLICATE_DRAFT_ID ?? '';
const chromiumPath = process.env.BILLSOFT_CHROMIUM_PATH ?? '';
const userDataRoot = process.env.BILLSOFT_BROWSER_QA_USER_DATA_ROOT
  ?? path.join(repoRoot, '.tmp', 'playwright-user-data');
const screenshotDir = process.env.BILLSOFT_BROWSER_QA_SCREENSHOT_DIR
  ?? path.join(repoRoot, 'output', 'playwright', 'vendors');

const wait = ms => new Promise(resolve => setTimeout(resolve, ms));

function ensureDirectory(dirPath) {
  fs.mkdirSync(dirPath, { recursive: true });
}

async function login(page) {
  await page.goto(`${appUrl}/login`, { waitUntil: 'networkidle' });
  await page.getByLabel('Restaurant code').fill(restaurantCode);
  await page.getByLabel('Mobile number').fill(mobileNumber);
  await page.locator('#password').fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await page.waitForURL(url => !url.pathname.startsWith('/login'), { timeout: 30000 });
}

async function selectDraft(page, draftId) {
  if (!draftId) {
    throw new Error('Missing vendor OCR draft id. Set BILLSOFT_BROWSER_QA_REVIEW_DRAFT_ID.');
  }

  await page.locator('select').nth(0).selectOption({ value: draftId });
}

async function run() {
  ensureDirectory(userDataRoot);
  ensureDirectory(screenshotDir);

  const userDataDir = path.join(userDataRoot, `browser-qa-${Date.now()}`);
  ensureDirectory(userDataDir);

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
    viewport: { width: 1440, height: 1000 },
  });

  try {
    const page = context.pages()[0] ?? await context.newPage();

    await page.goto('about:blank');
    console.log('ABOUT_BLANK_OK', JSON.stringify({ url: page.url(), title: await page.title() }));

    await login(page);
    console.log('LOGIN_OK', page.url());

    await page.goto(`${appUrl}/vendors`, { waitUntil: 'networkidle' });
    await page.waitForSelector('text=Upload vendor bill', { timeout: 30000 });

    const result = {
      appUrl,
      apiUrl,
      browserStayedAlive: true,
      reviewDraftId: reviewDraftId || null,
      duplicateDraftId: duplicateDraftId || null,
    };

    if (reviewDraftId && duplicateDraftId) {
      await selectDraft(page, reviewDraftId);
      await page.waitForSelector('text=Review extracted bill', { timeout: 30000 });

      result.readyForConfirm = await page.getByRole('button', { name: 'Create vendor bill' }).isEnabled();
      console.log('READY_FOR_CONFIRM', result.readyForConfirm);
      if (!result.readyForConfirm) {
        throw new Error('Vendor bill confirm did not become enabled.');
      }

      await page.screenshot({ path: path.join(screenshotDir, 'desktop-review.png'), fullPage: true });
      await page.getByRole('button', { name: 'Create vendor bill' }).click();
      await page.waitForSelector('text=Vendor bill created', { timeout: 30000 });
      console.log('CONFIRMED_FIRST', page.url());

      await page.screenshot({ path: path.join(screenshotDir, 'desktop-confirmed.png'), fullPage: true });

      await selectDraft(page, duplicateDraftId);
      await page.waitForSelector('[aria-label="duplicate receipt warning"]', { timeout: 30000 });
      result.duplicateVisible = await page.locator('[aria-label="duplicate receipt warning"]').isVisible();
      result.duplicateConfirmEnabled = await page.getByRole('button', { name: 'Create vendor bill' }).isEnabled();
      console.log('DUPLICATE_WARNING_VISIBLE', result.duplicateVisible);
      console.log('DUPLICATE_CONFIRM_ENABLED', result.duplicateConfirmEnabled);
      await page.screenshot({ path: path.join(screenshotDir, 'desktop-duplicate.png'), fullPage: true });
    }

    for (const [name, size] of [
      ['tablet', { width: 1024, height: 768 }],
      ['mobile430', { width: 430, height: 932 }],
      ['mobile390', { width: 390, height: 844 }],
    ]) {
      await page.setViewportSize(size);
      await wait(500);
      await page.screenshot({ path: path.join(screenshotDir, `${name}.png`), fullPage: true });
      console.log('VIEWPORT_OK', name, JSON.stringify(size));
    }

    result.screenshots = fs.readdirSync(screenshotDir).sort();
    console.log('RESULT', JSON.stringify(result));
  } finally {
    await context.close();
  }
}

run().catch(error => {
  console.error('QA_FAIL', error && error.stack ? error.stack : String(error));
  process.exitCode = 1;
});
