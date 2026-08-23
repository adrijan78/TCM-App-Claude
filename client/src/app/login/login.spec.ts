import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { FormGroup } from '@angular/forms';
import { Login } from './login';
import { environment } from '../../environments/environment';

/** The component's own members are protected; the spec reaches them through this shape. */
interface LoginInternals {
  form: FormGroup;
  submit(): void;
  error(): string | null;
  submitting(): boolean;
}

describe('Login', () => {
  let fixture: ComponentFixture<Login>;
  let component: LoginInternals;
  let controller: HttpTestingController;
  let router: Router;

  const session = {
    id: 'member-1',
    firstName: 'Ana',
    lastName: 'Petrova',
    email: 'ana@example.test',
    isCoach: false,
    roles: ['Member'],
    token: 'a.test.token',
    expiresAt: new Date(Date.now() + 3600_000).toISOString(),
    photoUrl: null,
  };

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });

    fixture = TestBed.createComponent(Login);
    component = fixture.componentInstance as unknown as LoginInternals;
    controller = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);

    vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
    fixture.detectChanges();
  });

  afterEach(() => {
    controller.verify();
    localStorage.clear();
    vi.restoreAllMocks();
  });

  function fill(email = 'ana@example.test', password = 'Sup3rSecret'): void {
    component.form.setValue({ email, password });
  }

  it('starts invalid and sends nothing', () => {
    expect(component.form.invalid).toBe(true);

    component.submit();

    controller.expectNone(`${environment.apiUrl}/account/login`);
    expect(component.form.controls['email'].touched).toBe(true);
  });

  it('rejects an address that is not an email', () => {
    fill('not-an-email');

    expect(component.form.controls['email'].hasError('email')).toBe(true);
  });

  it('signs in and lands on the dashboard', () => {
    fill();
    component.submit();

    const request = controller.expectOne(`${environment.apiUrl}/account/login`);
    expect(request.request.body).toEqual({
      email: 'ana@example.test',
      password: 'Sup3rSecret',
    });
    request.flush({ success: true, data: session, message: null, errors: [] });

    expect(router.navigateByUrl).toHaveBeenCalledWith('/dashboard');
    expect(component.submitting()).toBe(false);
  });

  it('follows a same-origin returnUrl', () => {
    fixture.componentRef.setInput('returnUrl', '/dashboard/members');
    fixture.detectChanges();

    fill();
    component.submit();
    controller
      .expectOne(`${environment.apiUrl}/account/login`)
      .flush({ success: true, data: session, message: null, errors: [] });

    expect(router.navigateByUrl).toHaveBeenCalledWith('/dashboard/members');
  });

  it('refuses an off-site returnUrl rather than becoming an open redirect', () => {
    fixture.componentRef.setInput('returnUrl', 'https://evil.example/steal');
    fixture.detectChanges();

    fill();
    component.submit();
    controller
      .expectOne(`${environment.apiUrl}/account/login`)
      .flush({ success: true, data: session, message: null, errors: [] });

    expect(router.navigateByUrl).toHaveBeenCalledWith('/dashboard');
  });

  it('refuses a protocol-relative returnUrl too', () => {
    fixture.componentRef.setInput('returnUrl', '//evil.example/steal');
    fixture.detectChanges();

    fill();
    component.submit();
    controller
      .expectOne(`${environment.apiUrl}/account/login`)
      .flush({ success: true, data: session, message: null, errors: [] });

    expect(router.navigateByUrl).toHaveBeenCalledWith('/dashboard');
  });

  it('shows the server message inline and clears the password on a rejected sign-in', () => {
    fill('ana@example.test', 'wrong-password');
    component.submit();

    controller
      .expectOne(`${environment.apiUrl}/account/login`)
      .flush(
        { success: false, data: null, message: 'Invalid email or password.', errors: [] },
        { status: 401, statusText: 'Unauthorized' },
      );

    expect(component.error()).toBe('Invalid email or password.');
    expect(component.form.controls['password'].value).toBe('');
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });

  it('reports a lockout in the words the server chose', () => {
    fill();
    component.submit();

    const message =
      'This account is temporarily locked after too many failed attempts. Try again later.';
    controller
      .expectOne(`${environment.apiUrl}/account/login`)
      .flush(
        { success: false, data: null, message, errors: [] },
        { status: 401, statusText: 'Unauthorized' },
      );

    expect(component.error()).toBe(message);
  });
});
