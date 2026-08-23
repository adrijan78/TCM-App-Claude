import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from './auth.service';
import { CommonService } from './common.service';
import { environment } from '../../environments/environment';
import { MemberToken } from '../_models/auth.model';

const STORAGE_KEY = 'tcm.session';

function tokenFor(roles: string[], minutesUntilExpiry = 60): MemberToken {
  return {
    id: 'member-1',
    firstName: 'Ana',
    lastName: 'Petrova',
    email: 'ana@example.test',
    isCoach: roles.includes('Coach'),
    roles,
    token: 'a.test.token',
    expiresAt: new Date(Date.now() + minutesUntilExpiry * 60 * 1000).toISOString(),
    photoUrl: null,
  };
}

describe('AuthService', () => {
  let auth: AuthService;
  let controller: HttpTestingController;

  function configure(): void {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });

    auth = TestBed.inject(AuthService);
    controller = TestBed.inject(HttpTestingController);
  }

  beforeEach(() => {
    localStorage.clear();
    configure();
  });

  afterEach(() => {
    controller.verify();
    localStorage.clear();
  });

  it('starts signed out when storage is empty', () => {
    expect(auth.isAuthenticated()).toBe(false);
    expect(auth.token).toBeNull();
    expect(auth.fullName()).toBe('');
  });

  it('stores the session on a successful login', () => {
    const user = tokenFor(['Coach']);
    let received: MemberToken | undefined;

    auth.login({ email: 'ana@example.test', password: 'Sup3rSecret!' }).subscribe((value) => {
      received = value;
    });

    const request = controller.expectOne(`${environment.apiUrl}/account/login`);
    expect(request.request.method).toBe('POST');
    request.flush({ success: true, data: user, message: null, errors: [] });

    expect(received).toEqual(user);
    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.isCoach()).toBe(true);
    expect(auth.fullName()).toBe('Ana Petrova');
    expect(JSON.parse(localStorage.getItem(STORAGE_KEY)!).token).toBe('a.test.token');
  });

  it('turns a success:false envelope into an error and stores nothing', () => {
    let failure: unknown;

    auth.login({ email: 'ana@example.test', password: 'wrong' }).subscribe({
      error: (error: unknown) => (failure = error),
    });

    controller
      .expectOne(`${environment.apiUrl}/account/login`)
      .flush({ success: false, data: null, message: 'Invalid email or password.', errors: [] });

    expect((failure as Error).message).toBe('Invalid email or password.');
    expect(auth.isAuthenticated()).toBe(false);
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
  });

  it('does not disturb the coach session when registering someone else', () => {
    auth.login({ email: 'coach@example.test', password: 'Sup3rSecret!' }).subscribe();
    controller
      .expectOne(`${environment.apiUrl}/account/login`)
      .flush({ success: true, data: tokenFor(['Coach']), message: null, errors: [] });

    auth
      .register({
        firstName: 'Marko',
        lastName: 'Ilic',
        email: 'marko@example.test',
        password: 'Sup3rSecret1',
        height: 180,
        weight: 75,
        dateOfBirth: '2005-04-11',
        beltId: 1,
        role: 'Member',
      })
      .subscribe();

    controller.expectOne(`${environment.apiUrl}/account/register`).flush({
      success: true,
      data: {
        id: 'member-2',
        firstName: 'Marko',
        lastName: 'Ilic',
        email: 'marko@example.test',
        isCoach: false,
        roles: ['Member'],
      },
      message: null,
      errors: [],
    });

    // Still the coach, not the member just created.
    expect(auth.currentUser()?.email).toBe('ana@example.test');
    expect(auth.isCoach()).toBe(true);
  });

  it('falls back to a friendly message when forgot-password returns none', () => {
    let message: string | undefined;

    auth.forgotPassword({ email: 'ana@example.test' }).subscribe((value) => (message = value));

    controller
      .expectOne(`${environment.apiUrl}/account/forgot-password`)
      .flush({ success: true, data: null, message: null, errors: [] });

    expect(message).toBe('Check your inbox.');
  });

  it('clears storage and the lookup cache on logout', () => {
    auth.login({ email: 'ana@example.test', password: 'Sup3rSecret!' }).subscribe();
    controller
      .expectOne(`${environment.apiUrl}/account/login`)
      .flush({ success: true, data: tokenFor(['Coach']), message: null, errors: [] });

    const common = TestBed.inject(CommonService);
    common.getBelts().subscribe();
    controller.expectOne(`${environment.apiUrl}/common/belts`).flush({
      success: true,
      data: [{ id: 1, beltName: 'White', rank: 1 }],
      message: null,
      errors: [],
    });

    auth.logout(null);

    expect(auth.isAuthenticated()).toBe(false);
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull();

    // The cache is gone, so the next caller fetches again rather than reading the
    // previous account's coach-only data.
    common.getBelts().subscribe();
    controller
      .expectOne(`${environment.apiUrl}/common/belts`)
      .flush({ success: true, data: [], message: null, errors: [] });
  });

  it('discards a stored session that has already expired', () => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(tokenFor(['Member'], -5)));
    configure();

    expect(auth.isAuthenticated()).toBe(false);
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
  });
});
