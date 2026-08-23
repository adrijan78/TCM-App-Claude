import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { forkJoin } from 'rxjs';
import { AuthService } from '../../_services/auth.service';
import { CommonService } from '../../_services/common.service';
import { apiErrorMessage, apiErrorParts } from '../../_services/unwrap';
import { Role } from '../../_models/auth.model';
import { Belt } from '../../_models/member.model';
import { FormAlert } from '../../_shared/components/form-alert';
import { PageHeader } from '../../_shared/components/page-header';
import { StatePanel } from '../../_shared/components/state-panel';
import { Trim } from '../../_shared/directives/trim.directive';
import { toDateOnly } from '../../_shared/validators/date-only';
import { PASSWORD_RULES, passwordPolicy } from '../../_shared/validators/password.validator';

/**
 * SPEC section 6.1 — the coach-only registration form. This is the only way anyone enters
 * the system, so every field the spec lists is here: first name, last name, email, password,
 * height, weight, date of birth, belt and role.
 *
 * The coach sets the member's first password and passes it on; the member changes it through
 * "Forgot password". That is why the generator exists — a coach registering a dozen members
 * should not be inventing a dozen ten-character passwords by hand.
 */
@Component({
  selector: 'app-register-member',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    FormAlert,
    PageHeader,
    StatePanel,
    Trim,
  ],
  templateUrl: './register-member.html',
  styleUrl: './register-member.scss',
})
export class RegisterMember {
  private readonly auth = inject(AuthService);
  private readonly common = inject(CommonService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly fb = inject(FormBuilder);

  /** A member has to have been born; there is no upper bound worth enforcing beyond that. */
  protected readonly maxDateOfBirth = new Date();

  protected readonly belts = signal<Belt[]>([]);
  protected readonly roles = signal<Role[]>([]);
  protected readonly lookupsLoading = signal(true);
  protected readonly lookupsError = signal<string | null>(null);

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly errorDetails = signal<readonly string[]>([]);
  protected readonly registered = signal<string | null>(null);
  protected readonly showPassword = signal(false);

  protected readonly passwordRules = PASSWORD_RULES;

  protected readonly form = this.fb.nonNullable.group({
    firstName: ['', [Validators.required, Validators.maxLength(100)]],
    lastName: ['', [Validators.required, Validators.maxLength(100)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    password: ['', [Validators.required, passwordPolicy()]],
    // Bounds mirror MemberRegisterDtoValidator so the server does not have to say no twice.
    height: this.fb.nonNullable.control<number | null>(null, [
      Validators.min(50),
      Validators.max(250),
    ]),
    weight: this.fb.nonNullable.control<number | null>(null, [
      Validators.min(10),
      Validators.max(300),
    ]),
    dateOfBirth: this.fb.nonNullable.control<Date | null>(null, [Validators.required]),
    beltId: this.fb.nonNullable.control<number | null>(null, [Validators.required]),
    role: ['', [Validators.required]],
  });

  constructor() {
    this.loadLookups();
  }

  protected loadLookups(): void {
    this.lookupsLoading.set(true);
    this.lookupsError.set(null);

    forkJoin({ belts: this.common.getBelts(), roles: this.common.getRoles() }).subscribe({
      next: ({ belts, roles }) => {
        this.belts.set(belts);
        this.roles.set(roles);

        // Sensible defaults: the lowest belt, and the role a coach picks nearly every time.
        this.form.patchValue({
          beltId: belts[0]?.id ?? null,
          role: roles.find((role) => role.name === 'Member')?.name ?? roles[0]?.name ?? '',
        });

        this.lookupsLoading.set(false);
      },
      error: (error: unknown) => {
        this.lookupsLoading.set(false);
        this.lookupsError.set(apiErrorMessage(error, 'Could not load belts and roles.'));
      },
    });
  }

  protected submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const dateOfBirth = toDateOnly(value.dateOfBirth);
    if (!dateOfBirth) {
      this.form.controls.dateOfBirth.setErrors({ required: true });
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    this.errorDetails.set([]);
    this.registered.set(null);

    this.auth
      .register({
        firstName: value.firstName.trim(),
        lastName: value.lastName.trim(),
        email: value.email.trim(),
        password: value.password,
        height: value.height,
        weight: value.weight,
        dateOfBirth,
        beltId: value.beltId!,
        role: value.role,
      })
      .subscribe({
        next: (member) => {
          this.submitting.set(false);
          this.registered.set(
            `${member.firstName} ${member.lastName} can now sign in with ${member.email}. Pass on the password you set — they can change it from "Forgot password".`,
          );
          this.snackBar.open(`${member.firstName} ${member.lastName} registered.`, 'Dismiss', {
            duration: 5000,
          });
          this.resetForNext();
        },
        error: (error: unknown) => {
          this.submitting.set(false);

          const failure = apiErrorParts(error, 'Could not register the member.');
          this.error.set(failure.message);
          this.errorDetails.set(failure.details);
        },
      });
  }

  /**
   * Fourteen characters drawn from the three classes Identity requires, one of each seeded
   * first so a generated password can never fail the policy by chance.
   */
  protected generatePassword(): void {
    const upper = 'ABCDEFGHJKLMNPQRSTUVWXYZ';
    const lower = 'abcdefghijkmnopqrstuvwxyz';
    const digits = '23456789';
    const all = upper + lower + digits;

    const bytes = crypto.getRandomValues(new Uint32Array(14));
    const pick = (alphabet: string, index: number) => alphabet[bytes[index] % alphabet.length];

    const characters = [pick(upper, 0), pick(lower, 1), pick(digits, 2)];
    for (let i = 3; i < bytes.length; i++) {
      characters.push(pick(all, i));
    }

    // Shuffle so the first three positions are not always upper/lower/digit.
    for (let i = characters.length - 1; i > 0; i--) {
      const j = bytes[i] % (i + 1);
      [characters[i], characters[j]] = [characters[j], characters[i]];
    }

    this.form.controls.password.setValue(characters.join(''));
    this.form.controls.password.markAsTouched();
    this.showPassword.set(true);
  }

  protected togglePassword(): void {
    this.showPassword.update((shown) => !shown);
  }

  /** Clears the person, keeps the belt and role — a coach usually adds several in a row. */
  private resetForNext(): void {
    const { beltId, role } = this.form.getRawValue();

    this.form.reset({ beltId, role, height: null, weight: null, dateOfBirth: null });
    this.showPassword.set(false);
  }
}
