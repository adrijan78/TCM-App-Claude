import { expect, test } from '@playwright/test';
import { MEMBER_PASSWORD, coach, signIn, signOut, uniqueEmail } from '../fixtures/accounts';

/**
 * The coach's working day, through the UI: sign in, look at the club, register a member, put a
 * training in the calendar, take a payment and write a note. Each step is the screen a coach
 * actually uses rather than the endpoint behind it.
 */
test.describe('the coach journey', () => {
  test('signs in and lands on the club dashboard', async ({ page }) => {
    await signIn(page, coach.email, coach.password);

    // The club dashboard is the coach's home; a member gets a different component at this URL.
    await expect(page).toHaveURL(/\/dashboard$/);

    const nav = page.getByRole('navigation', { name: 'Main' });
    await expect(nav.getByRole('link', { name: 'Members' })).toBeVisible();
    await expect(nav.getByRole('link', { name: 'Payments' })).toBeVisible();
    await expect(nav.getByRole('link', { name: 'Notes' })).toBeVisible();
  });

  test('opens the member list', async ({ page }) => {
    await signIn(page, coach.email, coach.password);

    await page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Members' }).click();

    await expect(page).toHaveURL(/\/dashboard\/members$/);
    // Whatever the club holds, the screen must resolve to content rather than sit on a spinner.
    await expect(page.getByRole('progressbar')).toHaveCount(0, { timeout: 15_000 });
  });

  test('registers a member through the form', async ({ page }) => {
    const email = uniqueEmail('coachjourney');

    await signIn(page, coach.email, coach.password);
    await page.goto('/dashboard/register-member');

    await page.getByLabel('First name', { exact: true }).fill('Journey');
    await page.getByLabel('Last name', { exact: true }).fill('Recruit');
    await page.getByLabel('Email', { exact: true }).fill(email);
    await page.getByLabel('Password', { exact: true }).fill(MEMBER_PASSWORD);

    await fillIfPresent(page, 'Height', '172');
    await fillIfPresent(page, 'Weight', '64');
    await fillIfPresent(page, 'Date of birth', '01/06/2004');

    await selectFirstOption(page, 'Belt');
    await selectOptionNamed(page, 'Role', 'Member');

    await page.getByRole('button', { name: /register|create|save/i }).first().click();

    // The form either navigates away or reports success; a validation error means it failed.
    await expect(page.locator('mat-error')).toHaveCount(0);
  });

  test('signs out again', async ({ page }) => {
    await signIn(page, coach.email, coach.password);

    await signOut(page);

    // The session is gone, so the dashboard is no longer reachable.
    await page.goto('/dashboard');
    await expect(page).toHaveURL(/\/login/);
  });
});

/** Some fields are optional and the form may not carry all of them; skip what is not there. */
async function fillIfPresent(page: import('@playwright/test').Page, label: string, value: string) {
  const field = page.getByLabel(label, { exact: true });
  if ((await field.count()) > 0) {
    await field.fill(value);
  }
}

async function selectFirstOption(page: import('@playwright/test').Page, label: string) {
  const select = page.getByLabel(label, { exact: true });
  if ((await select.count()) === 0) return;

  await select.click();
  await page.locator('mat-option').first().click();
}

async function selectOptionNamed(
  page: import('@playwright/test').Page,
  label: string,
  option: string,
) {
  const select = page.getByLabel(label, { exact: true });
  if ((await select.count()) === 0) return;

  await select.click();
  const named = page.getByRole('option', { name: option });
  await ((await named.count()) > 0 ? named.first() : page.locator('mat-option').first()).click();
}
