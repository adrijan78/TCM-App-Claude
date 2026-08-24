import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import {
  ActivatedRouteSnapshot,
  Route,
  Router,
  RouterStateSnapshot,
  UrlSegment,
  UrlTree,
  convertToParamMap,
  provideRouter,
} from '@angular/router';
import { authGuard } from './auth.guard';
import { coachGuard } from './coach.guard';
import { coachHomeMatch } from './home.guard';
import { guestGuard } from './guest.guard';
import { profileAccessGuard } from './own-profile.guard';

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

/** `profileAccessGuard` reads the `:id` route parameter, so it needs a snapshot that has one. */
function runOnProfile(id: string): boolean | UrlTree {
  const route = { paramMap: convertToParamMap({ id }) } as ActivatedRouteSnapshot;

  return TestBed.runInInjectionContext(() =>
    profileAccessGuard(route, { url: `/dashboard/members/${id}` } as RouterStateSnapshot),
  ) as boolean | UrlTree;
}

function matchHome(): boolean {
  // Angular 22's CanMatchFn takes a third argument, the partial snapshot of the match so
  // far. `coachHomeMatch` ignores all three — it only asks who is signed in.
  return TestBed.runInInjectionContext(() =>
    coachHomeMatch({} as Route, [] as UrlSegment[], {} as Parameters<typeof coachHomeMatch>[2]),
  ) as boolean;
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

describe('profileAccessGuard', () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  it('lets a coach open anyone in the club', () => {
    storeSession(['Coach']);
    configure();

    expect(runOnProfile('someone-else')).toBe(true);
  });

  it('lets a member open their own profile', () => {
    // The note-notification email links a member straight here, so this path has to work.
    storeSession(['Member']);
    configure();

    expect(runOnProfile('user-1')).toBe(true);
  });

  it('sends a member reaching for another id back to their own profile', () => {
    storeSession(['Member']);
    configure();

    const result = runOnProfile('someone-else');

    expect(result).toBeInstanceOf(UrlTree);
    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/dashboard/members/user-1');
  });
});

describe('coachHomeMatch', () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  it('matches the club dashboard for a coach', () => {
    storeSession(['Coach']);
    configure();

    expect(matchHome()).toBe(true);
  });

  it('does not match for a member, so the member home is used instead', () => {
    storeSession(['Member']);
    configure();

    expect(matchHome()).toBe(false);
  });

  it('does not match for a signed-out visitor', () => {
    configure();

    expect(matchHome()).toBe(false);
  });
});

/**
 * `guestGuard` is the mirror of `authGuard`. Its `returnUrl` handling repeats the same rule the
 * login screen uses: only a path starting with a single `/` is followed, so a crafted link
 * cannot turn a successful sign-in into an open redirect.
 */
describe('guestGuard', () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  function runAsGuest(queryParams: Record<string, string> = {}): boolean | UrlTree {
    const route = {
      queryParamMap: convertToParamMap(queryParams),
    } as ActivatedRouteSnapshot;

    return TestBed.runInInjectionContext(() =>
      guestGuard(route, { url: '/login' } as RouterStateSnapshot),
    ) as boolean | UrlTree;
  }

  it('lets a signed-out visitor reach the login screen', () => {
    configure();

    expect(runAsGuest()).toBe(true);
  });

  it('sends a signed-in user to the dashboard instead', () => {
    storeSession(['Member']);
    configure();

    expect(runAsGuest().toString()).toBe('/dashboard');
  });

  it('honours a returnUrl so a bookmarked login link still lands where it pointed', () => {
    storeSession(['Coach']);
    configure();

    expect(runAsGuest({ returnUrl: '/dashboard/members' }).toString()).toBe('/dashboard/members');
  });

  it('ignores an absolute returnUrl pointing off-site', () => {
    storeSession(['Coach']);
    configure();

    expect(runAsGuest({ returnUrl: 'https://evil.test/steal' }).toString()).toBe('/dashboard');
  });

  it('ignores a protocol-relative returnUrl', () => {
    // "//evil.test" starts with a slash but is still off-site.
    storeSession(['Coach']);
    configure();

    const destination = runAsGuest({ returnUrl: '//evil.test/steal' }).toString();

    expect(destination.startsWith('//')).toBe(false);
  });

  it('treats an expired session as signed out', () => {
    storeSession(['Member'], -5);
    configure();

    expect(runAsGuest()).toBe(true);
  });
});
