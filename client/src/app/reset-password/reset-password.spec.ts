import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { FormGroup } from '@angular/forms';
import { ResetPassword } from './reset-password';
import { environment } from '../../environments/environment';

interface ResetInternals {
  form: FormGroup;
  submit(): void;
  linkIsUsable(): boolean;
  error(): string | null;
  errorDetails(): readonly string[];
  done(): string | null;
  rules(): readonly { label: string; met: boolean }[];
}

describe('ResetPassword', () => {
  let fixture: ComponentFixture<ResetPassword>;
  let component: ResetInternals;
  let controller: HttpTestingController;

  const EMAIL = 'ana@example.test';
  const TOKEN = 'CfDJ8Fake+Reset/Token==';

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });

    fixture = TestBed.createComponent(ResetPassword);
    component = fixture.componentInstance as unknown as ResetInternals;
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    controller.verify();
    localStorage.clear();
  });

  function withLink(): void {
    fixture.componentRef.setInput('email', EMAIL);
    fixture.componentRef.setInput('token', TOKEN);
    fixture.detectChanges();
  }

  it('refuses to show a form when the link has no token', () => {
    fixture.componentRef.setInput('email', EMAIL);
    fixture.detectChanges();

    expect(component.linkIsUsable()).toBe(false);

    component.submit();
    controller.expectNone(`${environment.apiUrl}/account/reset-password`);

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('This link is incomplete');
  });

  it('blocks a password that does not meet the policy', () => {
    withLink();
    component.form.setValue({ newPassword: 'shortpass', confirmPassword: 'shortpass' });

    expect(component.form.controls['newPassword'].hasError('passwordPolicy')).toBe(true);

    component.submit();
    controller.expectNone(`${environment.apiUrl}/account/reset-password`);
  });

  it('blocks two passwords that do not match', () => {
    withLink();
    component.form.setValue({ newPassword: 'Sup3rSecret', confirmPassword: 'Sup3rSecrat' });

    expect(component.form.controls['confirmPassword'].hasError('fieldsMismatch')).toBe(true);

    component.submit();
    controller.expectNone(`${environment.apiUrl}/account/reset-password`);
  });

  it('ticks the checklist off as the password is typed', () => {
    withLink();
    component.form.controls['newPassword'].setValue('sh');

    expect(component.rules().every((rule) => rule.met)).toBe(false);

    component.form.controls['newPassword'].setValue('Sup3rSecret');

    expect(component.rules().every((rule) => rule.met)).toBe(true);
  });

  it('posts the email and token from the link, untouched', () => {
    withLink();
    component.form.setValue({ newPassword: 'Sup3rSecret', confirmPassword: 'Sup3rSecret' });

    component.submit();

    const request = controller.expectOne(`${environment.apiUrl}/account/reset-password`);
    expect(request.request.body).toEqual({
      email: EMAIL,
      // The token is passed through byte for byte: re-encoding it here would invalidate it.
      token: TOKEN,
      newPassword: 'Sup3rSecret',
      confirmPassword: 'Sup3rSecret',
    });

    request.flush({
      success: true,
      data: null,
      message: 'Your password has been changed.',
      errors: [],
    });

    expect(component.done()).toBe('Your password has been changed.');
  });

  it('surfaces a spent token and any rule messages Identity sent back', () => {
    withLink();
    component.form.setValue({ newPassword: 'Sup3rSecret', confirmPassword: 'Sup3rSecret' });

    component.submit();

    controller.expectOne(`${environment.apiUrl}/account/reset-password`).flush(
      {
        success: false,
        data: null,
        message: 'This reset link is no longer valid.',
        errors: ['Invalid token.'],
      },
      { status: 400, statusText: 'Bad Request' },
    );

    expect(component.error()).toBe('This reset link is no longer valid.');
    expect(component.errorDetails()).toEqual(['Invalid token.']);
    expect(component.done()).toBeNull();
  });
});
