import { defineConfig, devices } from '@playwright/test';

/**
 * The client origin the journeys drive, and the API they assert against directly. Both are
 * configuration rather than constants: SPEC section 9 leaves the hosting target undecided, so
 * nothing here may assume one.
 */
export const CLIENT_URL = process.env.TCM_E2E_CLIENT_URL ?? 'http://localhost:4200';
export const API_URL = process.env.TCM_E2E_API_URL ?? 'http://localhost:5102';

/**
 * Playwright starts both halves of the stack itself, so `npm test` in this folder is a single
 * command. An already-running dev server is reused rather than fought over, which is what makes
 * the suite usable while working on a screen.
 *
 * The SQL Server container is *not* started here — it is long-lived, shared with ordinary
 * development, and starting it per run would be both slow and surprising. `docker start tcm-sql`
 * is the one prerequisite, and the API fails loudly without it.
 */
export default defineConfig({
  testDir: './tests',
  outputDir: './test-results',

  // The journeys share one club's data, so they run in file order rather than racing each other
  // through the same member list.
  fullyParallel: false,
  workers: 1,

  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  timeout: 60_000,
  expect: { timeout: 10_000 },

  reporter: process.env.CI
    ? [['list'], ['html', { open: 'never' }]]
    : [['list'], ['html', { open: 'never' }]],

  use: {
    baseURL: CLIENT_URL,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },

  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],

  webServer: [
    {
      command: 'dotnet run --project ../server/TCM.Api --launch-profile http',
      url: `${API_URL}/swagger/index.html`,
      reuseExistingServer: true,
      // A cold `dotnet run` builds four projects first.
      timeout: 180_000,
      stdout: 'pipe',
      stderr: 'pipe',
    },
    {
      command: 'npm --prefix ../client start',
      url: CLIENT_URL,
      reuseExistingServer: true,
      timeout: 180_000,
      stdout: 'pipe',
      stderr: 'pipe',
    },
  ],
});
