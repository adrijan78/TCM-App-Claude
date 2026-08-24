import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { errorInterceptor } from './error.interceptor';
import { AuthService } from '../_services/auth.service';
import { environment } from '../../environments/environment';

/**
 * The interceptor's whole job is deciding which failures are the app's story and which are the
 * screen's. The rule that matters most: a 401 from an endpoint a signed-out visitor is meant to
 * call is a rejected credential, not an expired session — clearing storage and redirecting there
 * would bounce someone off the login page they are already standing on.
 */
describe('errorInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;
  let router: Router;
  let snackBar: { open: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    localStorage.clear();
    snackBar = { open: vi.fn() };

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        { provide: MatSnackBar, useValue: snackBar },
      ],
    });

    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
  });

  afterEach(() => {
    controller.verify();
    localStorage.clear();
    vi.restoreAllMocks();
  });

  /** Fires a request, fails it with the given status, and returns once the error has settled. */
  function failWith(url: string, status: number, body: object = {}): Promise<unknown> {
    const settled = new Promise((resolve) => {
      http.get(url).subscribe({
        next: resolve,
        error: (error) => resolve(error),
      });
    });

    controller.expectOne(url).flush(body, { status, statusText: 'Error' });
    return settled;
  }

  function storeSession(): void {
    localStorage.setItem(
      'tcm.session',
      JSON.stringify({
        id: 'member-1',
        firstName: 'Ana',
        lastName: 'Petrova',
        email: 'ana@example.test',
        isCoach: false,
        roles: ['Member'],
        token: 'a.test.token',
        expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
        photoUrl: null,
      }),
    );
  }

  it('leaves a 401 from the login endpoint to the screen', async () => {
    await failWith(`${environment.apiUrl}/account/login`, 401, {
      success: false,
      message: 'Email or password is incorrect.',
    });

    expect(router.navigate).not.toHaveBeenCalled();
    expect(snackBar.open).not.toHaveBeenCalled();
  });

  it.each([
    ['/account/forgot-password'],
    ['/account/reset-password'],
  ])('leaves a 401 from %s to the screen', async (path) => {
    await failWith(`${environment.apiUrl}${path}`, 401, { success: false, message: 'No.' });

    expect(router.navigate).not.toHaveBeenCalled();
    expect(snackBar.open).not.toHaveBeenCalled();
  });

  it('treats a 401 from the coach-only register endpoint as a lost session', async () => {
    // /account/register is deliberately not exempt: it is coach-authenticated.
    storeSession();

    await failWith(`${environment.apiUrl}/account/register`, 401);

    expect(router.navigate).toHaveBeenCalledWith(['/login'], expect.anything());
    expect(localStorage.getItem('tcm.session')).toBeNull();
  });

  it('clears the session and redirects on a 401 from any other endpoint', async () => {
    storeSession();
    expect(TestBed.inject(AuthService).isAuthenticated()).toBe(true);

    await failWith(`${environment.apiUrl}/members`, 401);

    expect(localStorage.getItem('tcm.session')).toBeNull();
    expect(router.navigate).toHaveBeenCalledWith(['/login'], expect.anything());
    expect(snackBar.open).toHaveBeenCalled();
  });

  it('shows a message on a 403 without signing the user out', async () => {
    storeSession();

    await failWith(`${environment.apiUrl}/members`, 403);

    expect(snackBar.open).toHaveBeenCalled();
    expect(localStorage.getItem('tcm.session')).not.toBeNull();
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it.each([[400], [404]])('stays quiet on a %i so the screen can render it', async (status) => {
    await failWith(`${environment.apiUrl}/members`, status, {
      success: false,
      message: 'Check the form.',
    });

    expect(snackBar.open).not.toHaveBeenCalled();
  });

  it('reports an unreachable server', async () => {
    const settled = new Promise((resolve) => {
      http.get(`${environment.apiUrl}/members`).subscribe({ error: resolve });
    });
    controller.expectOne(`${environment.apiUrl}/members`).error(new ProgressEvent('error'));
    await settled;

    expect(snackBar.open).toHaveBeenCalledWith(
      expect.stringContaining('Cannot reach the server'),
      'Dismiss',
      expect.anything(),
    );
  });

  it('reports a 500 with the envelope message when there is one', async () => {
    await failWith(`${environment.apiUrl}/members`, 500, {
      success: false,
      message: 'Something failed on the server.',
    });

    expect(snackBar.open).toHaveBeenCalledWith(
      'Something failed on the server.',
      'Dismiss',
      expect.anything(),
    );
  });

  it('rethrows so the component still learns its request failed', async () => {
    const error = await failWith(`${environment.apiUrl}/members`, 500);

    expect(error).toBeTruthy();
  });
});
