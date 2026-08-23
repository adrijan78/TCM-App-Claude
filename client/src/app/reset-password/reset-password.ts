import { Component, computed, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../_services/auth.service';
import { apiErrorParts } from '../_services/unwrap';
import { AuthCard } from '../_shared/layout/auth-card';
import { FormAlert } from '../_shared/components/form-alert';
import {
  PASSWORD_RULES,
  matchFields,
  passwordPolicy,
} from '../_shared/validators/password.validator';

/**
 * SPEC section 6.1 — the screen the emailed reset link lands on.
 *
 * `email` and `token` arrive as query parameters, bound by `withComponentInputBinding()`.
 * The link `AccountService.BuildResetLink` builds is
 * `{client}/reset-password?email=...&token=...`, and both halves are required: without them
 * there is nothing to submit, so the screen says so rather than showing a form that cannot
 * work.
 */
@Component({
  selector: 'app-reset-password',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    AuthCard,
    FormAlert,
  ],
  templateUrl: './reset-password.html',
  styleUrl: './reset-password.scss',
})
export class ResetPassword {
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);

  readonly email = input<string>();
  readonly token = input<string>();

  protected readonly linkIsUsable = computed(() => !!this.email() && !!this.token());

  protected readonly form = this.fb.nonNullable.group(
    {
      newPassword: ['', [Validators.required, passwordPolicy()]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: matchFields('newPassword', 'confirmPassword') },
  );

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly errorDetails = signal<readonly string[]>([]);
  protected readonly done = signal<string | null>(null);
  protected readonly showPassword = signal(false);

  /** Live checklist, so the policy is visible while typing rather than only after submit. */
  private readonly typed = toSignal(this.form.controls.newPassword.valueChanges, {
    initialValue: '',
  });

  protected readonly rules = computed(() => {
    const value = this.typed();
    return PASSWORD_RULES.map((rule) => ({ label: rule.label, met: rule.test(value) }));
  });

  protected submit(): void {
    const email = this.email();
    const token = this.token();

    if (!email || !token || this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    this.errorDetails.set([]);

    const { newPassword, confirmPassword } = this.form.getRawValue();

    this.auth.resetPassword({ email, token, newPassword, confirmPassword }).subscribe({
      next: (message) => {
        this.submitting.set(false);
        this.done.set(message);
      },
      error: (error: unknown) => {
        this.submitting.set(false);

        const failure = apiErrorParts(
          error,
          'That reset link is no longer valid. Request a new one.',
        );
        this.error.set(failure.message);
        this.errorDetails.set(failure.details);
      },
    });
  }

  protected togglePassword(): void {
    this.showPassword.update((shown) => !shown);
  }
}
