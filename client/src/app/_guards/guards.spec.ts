import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import {
  ActivatedRouteSnapshot,
  Router,
  RouterStateSnapshot,
  UrlTree,
  provideRouter,
} from '@angular/router';
import { authGuard } from './auth.guard';
import { coachGuard } from './coach.guard';

const STORAGE_KEY = 'tcm.session';

function storeSession(roles: string[], minutesUntilExpiry = 60): void {
  localStorage.setItem(
    STORAGE_KEY,
    JSON.stringify({
      id: 'user-1',
      firstName: 'Test',
      lastName: 'User',
      email: 'test@example.test',
      isCoach: roles.includes('Coach'),
      roles,
      token: 'a.test.token',
      expiresAt: new Date(Date.now() + minutesUntilExpiry * 60 * 1000).toISOString(),
      photoUrl: null,
    }),
  );
}

function run(guard: typeof authGuard, url: string): boolean | UrlTree {
  return TestBed.runInInjectionContext(() =>
    guard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot),
  ) as boolean | UrlTree;
}

function configure(): void {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
  });
}

describe('authGuard', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => localStorage.clear());

  it('sends a signed-out visitor to login and remembers where they were going', () => {
    configure();

    const result = run(authGuard, '/dashboard/members');

    expect(result).toBeInstanceOf(UrlTree);
    const tree = result as UrlTree;
    expect(TestBed.inject(Router).serializeUrl(tree)).toContain('/login');
    expect(TestBed.inject(Router).serializeUrl(tree)).toContain(
      `returnUrl=${encodeURIComponent('/dashboard/members')}`,
    );
  });

  it('lets a signed-in user through', () => {
    storeSession(['Member']);
    configure();

    expect(run(authGuard, '/dashboard')).toBe(true);
  });

  it('treats an expired session as signed out', () => {
    // A stored-but-stale token would otherwise render a signed-in shell whose every
    // request the API bounces.
    storeSession(['Member'], -5);
    configure();

    expect(run(authGuard, '/dashboard')).toBeInstanceOf(UrlTree);
  });
});

describe('coachGuard', () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  it('lets a coach through', () => {
    storeSession(['Coach']);
    configure();

    expect(run(coachGuard, '/dashboard/members')).toBe(true);
  });

  it('redirects a member away from a coach-only route', () => {
    storeSession(['Member']);
    configure();

    const result = run(coachGuard, '/dashboard/members');

    expect(result).toBeInstanceOf(UrlTree);
    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/dashboard');
  });

  it('sends a signed-out visitor to login rather than the dashboard', () => {
    configure();

    const result = run(coachGuard, '/dashboard/payments');

    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toContain('/login');
  });
});
