import { expect, test } from '@playwright/test';
import { coach, createMember, signIn, tokenFor } from '../fixtures/accounts';

/**
 * The restricted half of SPEC section 5, from the member's side. What matters here is not only
 * that the member's own screens work, but that they are the *only* screens on offer — the coach's
 * controls have to be absent rather than merely disabled.
 */
test.describe('the member journey', () => {
  let email: string;
  let password: string;

  test.beforeAll(async ({ playwright }) => {
    const request = await playwright.request.newContext();
    const coachToken = await tokenFor(request, coach.email, coach.password);
    const member = await createMember(request, coachToken, {
      firstName: 'Mira',
      lastName: 'Memberton',
    });

    email = member.email;
    password = member.password;

    await request.dispose();
  });

  test('signs in and lands on the member home, not the club dashboard', async ({ page }) => {
    await signIn(page, email, password);

    await expect(page).toHaveURL(/\/dashboard$/);

    // `coachHomeMatch` picks a different component for a member, so the club-wide figures and
    // the quick member search must not be here.
    await expect(page.getByRole('link', { name: 'Register a member' })).toHaveCount(0);
  });

  test('sees only the navigation a member is entitled to', async ({ page }) => {
    await signIn(page, email, password);

    const nav = page.getByRole('navigation', { name: 'Main' });

    await expect(nav.getByRole('link', { name: 'Home' })).toBeVisible();
    await expect(nav.getByRole('link', { name: 'My profile' })).toBeVisible();
    await expect(nav.getByRole('link', { name: 'Members' })).toHaveCount(0);
  });

  test('opens their own profile', async ({ page }) => {
    await signIn(page, email, password);

    await page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'My profile' }).click();

    // "My profile" resolves to the same component a coach uses, addressed by the member's own
    // id. The heading, not a bare text match: the name is also in the toolbar account button.
    await expect(page.getByRole('heading', { name: 'Mira Memberton', level: 1 })).toBeVisible({
      timeout: 15_000,
    });
    await expect(page).toHaveURL(/\/dashboard\/(profile|members\/[^/]+)$/);
  });

  test('reaching for another member’s profile lands back on their own', async ({ page }) => {
    await signIn(page, email, password);

    await page.goto('/dashboard/members/00000000-0000-0000-0000-000000000000');

    // profileAccessGuard redirects rather than rendering a screen full of 403s.
    await expect(page).not.toHaveURL(/00000000-0000-0000-0000-000000000000/);
  });

  test('the session survives a reload', async ({ page }) => {
    await signIn(page, email, password);

    await page.reload();

    await expect(page).toHaveURL(/\/dashboard/);
    await expect(page.locator('.shell-account')).toBeVisible();
  });
});
