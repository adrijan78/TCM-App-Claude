import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { provideNativeDateAdapter } from '@angular/material/core';
import { FormGroup } from '@angular/forms';
import { RegisterMember } from './register-member';
import { environment } from '../../../environments/environment';
import { PASSWORD_RULES } from '../../_shared/validators/password.validator';

interface RegisterInternals {
  form: FormGroup;
  submit(): void;
  generatePassword(): void;
  loadLookups(): void;
  lookupsLoading(): boolean;
  lookupsError(): string | null;
  error(): string | null;
  errorDetails(): readonly string[];
  registered(): string | null;
}

const BELTS = [
  { id: 1, beltName: 'White', rank: 1 },
  { id: 2, beltName: 'Yellow', rank: 2 },
];

const ROLES = [
  { id: 'r1', name: 'Coach' },
  { id: 'r2', name: 'Member' },
];

describe('RegisterMember', () => {
  let fixture: ComponentFixture<RegisterMember>;
  let component: RegisterInternals;
  let controller: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNativeDateAdapter(),
      ],
    });

    fixture = TestBed.createComponent(RegisterMember);
    component = fixture.componentInstance as unknown as RegisterInternals;
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    controller.verify();
    localStorage.clear();
  });

  function flushLookups(): void {
    controller
      .expectOne(`${environment.apiUrl}/common/belts`)
      .flush({ success: true, data: BELTS, message: null, errors: [] });
    controller
      .expectOne(`${environment.apiUrl}/roles`)
      .flush({ success: true, data: ROLES, message: null, errors: [] });
    fixture.detectChanges();
  }

  function fill(): void {
    component.form.patchValue({
      firstName: '  Marko ',
      lastName: ' Ilic ',
      // Spaces around an email would fail Validators.email; the `appTrim` directive removes
      // them on blur, before validation ever sees them.
      email: 'marko@example.test',
      password: 'Sup3rSecret',
      height: 180,
      weight: 75,
      dateOfBirth: new Date(2005, 3, 11),
    });
  }

  it('loads belts and roles, and defaults to the lowest belt and the Member role', () => {
    fixture.detectChanges();
    expect(component.lookupsLoading()).toBe(true);

    flushLookups();

    expect(component.lookupsLoading()).toBe(false);
    expect(component.form.controls['beltId'].value).toBe(1);
    expect(component.form.controls['role'].value).toBe('Member');
  });

  it('offers a retry when the lookups fail', () => {
    fixture.detectChanges();

    controller
      .expectOne(`${environment.apiUrl}/common/belts`)
      .flush(
        { success: false, data: null, message: 'Belts are unavailable.', errors: [] },
        { status: 500, statusText: 'Server Error' },
      );
    controller
      .expectOne(`${environment.apiUrl}/roles`)
      .flush({ success: true, data: ROLES, message: null, errors: [] });
    fixture.detectChanges();

    expect(component.lookupsError()).toBe('Belts are unavailable.');

    component.loadLookups();
    // Roles were cached by the first, successful call, so only belts is asked for again.
    controller
      .expectOne(`${environment.apiUrl}/common/belts`)
      .flush({ success: true, data: BELTS, message: null, errors: [] });

    expect(component.lookupsError()).toBeNull();
  });

  it('sends nothing while the form is incomplete', () => {
    fixture.detectChanges();
    flushLookups();

    component.submit();

    controller.expectNone(`${environment.apiUrl}/account/register`);
    expect(component.form.controls['firstName'].touched).toBe(true);
  });

  it('posts a trimmed payload with the date of birth as a DateOnly string', () => {
    fixture.detectChanges();
    flushLookups();
    fill();

    component.submit();

    const request = controller.expectOne(`${environment.apiUrl}/account/register`);
    expect(request.request.body).toEqual({
      firstName: 'Marko',
      lastName: 'Ilic',
      email: 'marko@example.test',
      password: 'Sup3rSecret',
      height: 180,
      weight: 75,
      // Local parts, not UTC — see toDateOnly.
      dateOfBirth: '2005-04-11',
      beltId: 1,
      role: 'Member',
    });

    request.flush({
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

    expect(component.registered()).toContain('marko@example.test');
  });

  it('keeps the belt and role but clears the person, ready for the next one', () => {
    fixture.detectChanges();
    flushLookups();
    component.form.patchValue({ beltId: 2, role: 'Coach' });
    fill();

    component.submit();
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

    expect(component.form.controls['firstName'].value).toBe('');
    expect(component.form.controls['password'].value).toBe('');
    expect(component.form.controls['beltId'].value).toBe(2);
    expect(component.form.controls['role'].value).toBe('Coach');
  });

  it('shows the duplicate-email conflict with any detail the server added', () => {
    fixture.detectChanges();
    flushLookups();
    fill();

    component.submit();

    controller.expectOne(`${environment.apiUrl}/account/register`).flush(
      {
        success: false,
        data: null,
        message: 'A member with that email already exists.',
        errors: [],
      },
      { status: 409, statusText: 'Conflict' },
    );

    expect(component.error()).toBe('A member with that email already exists.');
    expect(component.registered()).toBeNull();
    // The form is left as it was so the coach can correct the address, not retype everything.
    expect(component.form.controls['firstName'].value).toBe('  Marko ');
    expect(component.form.controls['password'].value).toBe('Sup3rSecret');
  });

  it('generates a password that satisfies every policy rule', () => {
    fixture.detectChanges();
    flushLookups();

    for (let attempt = 0; attempt < 20; attempt++) {
      component.generatePassword();
      const generated = component.form.controls['password'].value as string;

      expect(PASSWORD_RULES.every((rule) => rule.test(generated))).toBe(true);
      expect(component.form.controls['password'].valid).toBe(true);
    }
  });
});
