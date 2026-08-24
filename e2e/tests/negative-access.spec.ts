import { expect, test } from '@playwright/test';
import { API_URL } from '../playwright.config';
import { coach, createMember, signIn, tokenFor } from '../fixtures/accounts';

/**
 * The journey this application most depends on. SPEC section 5 draws a line between what a coach
 * may do and what a member may do, and the client's guards are only the polite half of it — the
 * API has to refuse the same calls when the UI is bypassed entirely.
 *
 * Both halves are checked here: the URL a member is redirected away from, and the endpoint that
 * still says no when a member's own token is presented to it directly.
 */
test.describe('a member cannot reach the coach half of the app', () => {
  let memberToken: string;
  let coachToken: string;
  let memberId: string;
  let memberEmail: string;
  let memberPassword: string;

  test.beforeAll(async ({ playwright }) => {
    const request = await playwright.request.newContext();

    coachToken = await tokenFor(request, coach.email, coach.password);
    const member = await createMember(request, coachToken, { firstName: 'Nadia', lastName: 'Negative' });

    memberId = member.id;
    memberEmail = member.email;
    memberPassword = member.password;
    memberToken = await tokenFor(request, member.email, member.password);

    await request.dispose();
  });

  // ---- The API, called directly --------------------------------------------------------------

  const coachOnlyEndpoints: ReadonlyArray<[string, string]> = [
    ['GET', '/api/members'],
    ['GET', '/api/roles'],
    ['GET', '/api/notes'],
    ['GET', '/api/payments'],
    ['GET', '/api/trainings'],
    ['GET', '/api/trainings/calendar'],
    ['POST', '/api/trainings'],
    ['POST', '/api/payments/cash'],
  ];

  for (const [method, path] of coachOnlyEndpoints) {
    test(`${method} ${path} answers 403 to a member token`, async ({ request }) => {
      const response = await request.fetch(`${API_URL}${path}`, {
        method,
        headers: { Authorization: `Bearer ${memberToken}` },
        data: method === 'GET' ? undefined : {},
      });

      expect(response.status()).toBe(403);
    });
  }

  test('a member cannot read another member with their own token', async ({ request }) => {
    const other = await request.post(`${API_URL}/api/account/register`, {
      headers: { Authorization: `Bearer ${coachToken}` },
      data: {
        firstName: 'Other',
        lastName: 'Person',
        email: `other.${Date.now()}@e2e.test`,
        password: 'E2ePassw0rd!',
        height: 170,
        weight: 65,
        dateOfBirth: '2003-03-03',
        beltId: 1,
        role: 'Member',
      },
    });
    const otherId = (await other.json()).data.id;

    const response = await request.get(`${API_URL}/api/members/${otherId}`, {
      headers: { Authorization: `Bearer ${memberToken}` },
    });

    expect(response.status()).toBe(403);
  });

  test('a member can read their own record', async ({ request }) => {
    // The mirror of the test above: the boundary has to let the member through, or it is
    // just a broken app rather than a secured one.
    const response = await request.get(`${API_URL}/api/members/${memberId}`, {
      headers: { Authorization: `Bearer ${memberToken}` },
    });

    expect(response.status()).toBe(200);
  });

  test('every protected endpoint refuses an anonymous call', async ({ request }) => {
    // Each route is called with its own verb: routing picks the method before the authorization
    // filter runs, so a GET at a POST-only route answers 405 and proves nothing.
    for (const [method, path] of coachOnlyEndpoints) {
      const response = await request.fetch(`${API_URL}${path}`, {
        method,
        data: method === 'GET' ? undefined : {},
      });

      expect(response.status(), `${method} ${path} should be 401 without a token`).toBe(401);
    }
  });

  test('a tampered token is refused', async ({ request }) => {
    const tampered = `${memberToken.slice(0, -6)}AAAAAA`;

    const response = await request.get(`${API_URL}/api/members/${memberId}`, {
      headers: { Authorization: `Bearer ${tampered}` },
    });

    expect(response.status()).toBe(401);
  });

  // ---- The UI, forced ------------------------------------------------------------------------

  const coachOnlyRoutes = [
    '/dashboard/members',
    '/dashboard/payments',
    '/dashboard/notes',
    '/dashboard/trainings',
    '/dashboard/register-member',
  ];

  for (const route of coachOnlyRoutes) {
    test(`typing ${route} sends a member back to their own dashboard`, async ({ page }) => {
      await signIn(page, memberEmail, memberPassword);

      await page.goto(route);

      // Landing back on /dashboard, not on a rendered page full of 403s.
      await expect(page).toHaveURL(/\/dashboard$/);
    });
  }

  test('the coach-only navigation is absent for a member', async ({ page }) => {
    await signIn(page, memberEmail, memberPassword);

    const nav = page.getByRole('navigation', { name: 'Main' });

    await expect(nav.getByRole('link', { name: 'Members' })).toHaveCount(0);
    await expect(nav.getByRole('link', { name: 'Payments' })).toHaveCount(0);
    await expect(nav.getByRole('link', { name: 'Notes' })).toHaveCount(0);
    await expect(page.getByRole('link', { name: 'Register a member' })).toHaveCount(0);

    // What they should see.
    await expect(nav.getByRole('link', { name: 'Home' })).toBeVisible();
    await expect(nav.getByRole('link', { name: 'My profile' })).toBeVisible();
  });

  test('a signed-out visitor is sent to login and returned afterwards', async ({ page }) => {
    await page.goto('/dashboard/members');

    await expect(page).toHaveURL(/\/login/);
    expect(page.url()).toContain('returnUrl');
  });
});
