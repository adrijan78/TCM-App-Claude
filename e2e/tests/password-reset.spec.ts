import { expect, test } from '@playwright/test';
import { API_URL } from '../playwright.config';
import { coach, createMember, signIn, tokenFor } from '../fixtures/accounts';

/**
 * The password-reset journey, as far as a browser can honestly drive it.
 *
 * The half that carries the token cannot be automated from here: the reset token is generated
 * cryptographically from the user's security stamp and only ever leaves the server inside an
 * email, so there is nothing in the database or the API to read it back from. Adding a route
 * that returned one would be a real hole in production for the sake of a test.
 *
 * So this covers everything either side of the inbox — the request, the enumeration guarantee,
 * the spent/forged token, and the form's own rules — and the delivery itself is verified by
 * hand. Closing the gap properly needs a mail catcher (Mailpit or similar) wired into the
 * `Gmail:*` settings; it is written up as a Phase 12 item in CLAUDE.md.
 */
test.describe('the password-reset journey', () => {
  let memberEmail: string;
  let memberPassword: string;

  test.beforeAll(async ({ playwright }) => {
    const request = await playwright.request.newContext();
    const coachToken = await tokenFor(request, coach.email, coach.password);
    const member = await createMember(request, coachToken, {
      firstName: 'Rosa',
      lastName: 'Resetter',
    });

    memberEmail = member.email;
    memberPassword = member.password;

    await request.dispose();
  });

  test('requesting a link confirms without saying whether the address exists', async ({ page }) => {
    await page.goto('/forgot-password');

    await page.getByLabel('Email', { exact: true }).fill(memberEmail);
    await page.getByRole('button', { name: /send|reset|email/i }).first().click();

    await expect(page.getByText(/back to sign in/i)).toBeVisible({ timeout: 15_000 });
  });

  test('an unknown address gets the same answer as a real one', async ({ page, request }) => {
    // Checked at the API too: a difference in wording here would let anyone enumerate the
    // club's member addresses from the login screen.
    const known = await request.post(`${API_URL}/api/account/forgot-password`, {
      data: { email: memberEmail },
    });
    const unknown = await request.post(`${API_URL}/api/account/forgot-password`, {
      data: { email: 'nobody.at.all@e2e.test' },
    });

    expect(known.status()).toBe(unknown.status());
    expect((await known.json()).message).toBe((await unknown.json()).message);

    await page.goto('/forgot-password');
    await page.getByLabel('Email', { exact: true }).fill('nobody.at.all@e2e.test');
    await page.getByRole('button', { name: /send|reset|email/i }).first().click();

    await expect(page.getByText(/back to sign in/i)).toBeVisible({ timeout: 15_000 });
  });

  test('a forged token is refused', async ({ page }) => {
    await page.goto(
      `/reset-password?email=${encodeURIComponent(memberEmail)}&token=not-a-real-token`,
    );

    await page.getByLabel('New password', { exact: true }).fill('BrandNewPass456!');
    await page.getByLabel('Confirm new password', { exact: true }).fill('BrandNewPass456!');
    await page.getByRole('button', { name: 'Save new password' }).click();

    // The screen says so inline — the error interceptor deliberately stays out of the way here.
    await expect(page.locator('app-form-alert')).toBeVisible({ timeout: 15_000 });

    // And the old password still works, which is the part that actually matters.
    await signIn(page, memberEmail, memberPassword);
  });

  test('the confirmation field has to match', async ({ page }) => {
    await page.goto(
      `/reset-password?email=${encodeURIComponent(memberEmail)}&token=not-a-real-token`,
    );

    await page.getByLabel('New password', { exact: true }).fill('BrandNewPass456!');
    await page.getByLabel('Confirm new password', { exact: true }).fill('SomethingElse789!');
    await page.getByLabel('Confirm new password', { exact: true }).blur();

    await expect(page.locator('mat-error')).toBeVisible();
  });

  test('the API refuses a forged token outright', async ({ request }) => {
    const response = await request.post(`${API_URL}/api/account/reset-password`, {
      // The DTO field is NewPassword; sending `password` would fail model binding instead of
      // the token check, which would make this test pass for the wrong reason.
      data: {
        email: memberEmail,
        token: 'made-up-token',
        newPassword: 'BrandNewPass456!',
        confirmPassword: 'BrandNewPass456!',
      },
    });

    expect((await response.json()).success).toBe(false);
  });
});
