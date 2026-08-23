/**
 * The visual sweep behind phase 8b: every screen, at four widths, in both themes.
 *
 * It is not a test — nothing here asserts. It captures what the app actually looks like so a
 * person can review it, and it fails loudly on console errors and dropped requests, which is
 * how the stale-page-title and visible-screen-reader-text bugs were found.
 *
 * Playwright is not a dependency of this project yet; phase 11 adds it for the e2e suite.
 * Until then:
 *
 *   npx playwright install chromium
 *   npm i --no-save playwright
 *   node scripts/screenshots.mjs ./shots
 *
 * Needs the API, the database container and `npm start` all running — see the
 * `tcm-run-local` skill.
 */
import { chromium } from 'playwright';

const OUT = process.argv[2];
const COACH = { email: 'coach@tcmclub.local', password: 'CoachaJDQOy5eWcVn1w9' };
const BASE = 'http://localhost:4200';

const WIDTHS = { phone: 360, tablet: 768, laptop: 1024, desktop: 1440 };

const browser = await chromium.launch();
const problems = [];

async function shoot(page, name) {
  await page.waitForTimeout(700);
  await page.screenshot({ path: `${OUT}/${name}.png`, fullPage: false });
}

// Catch anything the console complains about — a broken token or a missing asset shows up
// here long before it shows up in a screenshot.
function watch(page, tag) {
  page.on('console', (m) => {
    if (m.type() === 'error') problems.push(`[${tag}] console: ${m.text()}`);
  });
  page.on('pageerror', (e) => problems.push(`[${tag}] pageerror: ${e.message}`));
  page.on('requestfailed', (r) => {
    if (!r.url().includes('favicon')) problems.push(`[${tag}] failed: ${r.url()}`);
  });
}

for (const [label, width] of Object.entries(WIDTHS)) {
  for (const scheme of ['light', 'dark']) {
    const ctx = await browser.newContext({
      viewport: { width, height: width < 500 ? 780 : 900 },
      colorScheme: scheme,
      deviceScaleFactor: 1,
    });
    const page = await ctx.newPage();
    watch(page, `${label}/${scheme}`);

    await page.goto(`${BASE}/login`, { waitUntil: 'networkidle' });
    await shoot(page, `login-${label}-${scheme}`);

    await page.fill('input[formcontrolname="email"]', COACH.email);
    await page.fill('input[formcontrolname="password"]', COACH.password);
    await page.click('button[type="submit"]');
    await page.waitForURL('**/dashboard', { timeout: 15000 });
    await shoot(page, `dashboard-${label}-${scheme}`);

    await page.goto(`${BASE}/dashboard/register-member`, { waitUntil: 'networkidle' });
    await shoot(page, `register-${label}-${scheme}`);

    await ctx.close();
  }
}

// The remaining screens are signed-out ones. They need their own context: `guestGuard`
// bounces a signed-in visitor off /forgot-password and /reset-password, which is correct
// behaviour and exactly what broke the first version of this script.
for (const scheme of ['light', 'dark']) {
  const ctx = await browser.newContext({
    viewport: { width: 1440, height: 900 },
    colorScheme: scheme,
  });
  const page = await ctx.newPage();
  watch(page, `signed-out/${scheme}`);

  await page.goto(`${BASE}/forgot-password`, { waitUntil: 'networkidle' });
  await shoot(page, `forgot-${scheme}`);

  await page.goto(`${BASE}/reset-password?email=ana%40example.test&token=abc123`, {
    waitUntil: 'networkidle',
  });
  await page.fill('input[formcontrolname="newPassword"]', 'Sup3rSec');
  await shoot(page, `reset-${scheme}`);

  await page.goto(`${BASE}/reset-password`, { waitUntil: 'networkidle' });
  await shoot(page, `reset-brokenlink-${scheme}`);

  await page.goto(`${BASE}/nowhere`, { waitUntil: 'networkidle' });
  await shoot(page, `notfound-${scheme}`);

  await ctx.close();
}

await browser.close();
console.log(problems.length ? problems.join('\n') : 'no console errors, no failed requests');
