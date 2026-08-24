import { APIRequestContext, Page, expect } from '@playwright/test';
import { API_URL } from '../playwright.config';

/**
 * The seeded coach. Credentials come from the environment, never from source — they are the same
 * `Seed:CoachEmail` / `Seed:CoachPassword` the API is configured with (SPEC section 9).
 */
export const coach = {
  email: required('TCM_E2E_COACH_EMAIL'),
  password: required('TCM_E2E_COACH_PASSWORD'),
};

function required(key: string): string {
  const value = process.env[key];

  if (!value) {
    throw new Error(
      `${key} is not set. The end-to-end suite signs in as the seeded coach, so it needs the ` +
        `same credentials the API was seeded with. See the Phase 11 section of CLAUDE.md.`,
    );
  }

  return value;
}

/** A password that satisfies the API's policy, for members this suite creates. */
export const MEMBER_PASSWORD = 'E2ePassw0rd!';

/** A fresh address per run, so re-running the suite never collides with its own leftovers. */
export function uniqueEmail(prefix: string): string {
  return `${prefix}.${Date.now()}${Math.floor(Math.random() * 1000)}@e2e.test`;
}

/** Signs in through the real login screen and waits for the dashboard to take over. */
export async function signIn(page: Page, email: string, password: string): Promise<void> {
  await page.goto('/login');

  // Exact labels: the password field shares the toolbar with a "Show password" toggle whose
  // aria-label would also match a loose /password/i.
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Password', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();

  await expect(page).toHaveURL(/\/dashboard/, { timeout: 20_000 });
}

export async function signOut(page: Page): Promise<void> {
  await page.locator('.shell-account').click();
  await page.getByRole('menuitem', { name: 'Sign out' }).click();
  await expect(page).toHaveURL(/\/login/);
}

/** A bearer token straight from the API, for the checks that bypass the UI entirely. */
export async function tokenFor(
  request: APIRequestContext,
  email: string,
  password: string,
): Promise<string> {
  const response = await request.post(`${API_URL}/api/account/login`, {
    data: { email, password },
  });

  expect(response.ok(), `login failed for ${email}`).toBeTruthy();

  const body = await response.json();
  return body.data.token as string;
}

/**
 * Registers a member through the API using a coach's token. The coach journey exercises the
 * registration *form*; the other journeys just need an account to exist, and going through the
 * API keeps them from failing for reasons that have nothing to do with what they test.
 */
export async function createMember(
  request: APIRequestContext,
  coachToken: string,
  overrides: Partial<{ firstName: string; lastName: string; email: string }> = {},
): Promise<{ id: string; email: string; password: string }> {
  const email = overrides.email ?? uniqueEmail('member');

  const response = await request.post(`${API_URL}/api/account/register`, {
    headers: { Authorization: `Bearer ${coachToken}` },
    data: {
      firstName: overrides.firstName ?? 'Eve',
      lastName: overrides.lastName ?? 'Twotest',
      email,
      password: MEMBER_PASSWORD,
      height: 168,
      weight: 60,
      dateOfBirth: '2004-04-04',
      beltId: 1,
      role: 'Member',
    },
  });

  expect(response.ok(), `member registration failed: ${await response.text()}`).toBeTruthy();

  const body = await response.json();
  return { id: body.data.id, email, password: MEMBER_PASSWORD };
}
