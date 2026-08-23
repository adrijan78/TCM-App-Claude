import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../_services/auth.service';
import { apiErrorMessage } from '../_services/unwrap';
import { AuthCard } from '../_shared/layout/auth-card';
import { FormAlert } from '../_shared/components/form-alert';
import { Trim } from '../_shared/directives/trim.directive';

/**
 * SPEC section 6.1 — "Forgot Password".
 *
 * The server answers identically whether or not the address is registered, so this screen
 * must not hint either way: on success it shows the server's own wording and nothing more.
 * A "we could not find that account" message here would undo the enumeration defence in
 * `AccountService`.
 */
@Component({
  selector: 'app-forgot-password',
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
    Trim,
  ],
  templateUrl: './forgot-password.html',
  styleUrl: './forgot-password.scss',
})
export class ForgotPassword {
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);

  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
  });

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly sent = signal<string | null>(null);

  protected submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set(null);

    this.auth.forgotPassword(this.form.getRawValue()).subscribe({
      next: (message) => {
        this.submitting.set(false);
        this.sent.set(message);
      },
      error: (error: unknown) => {
        this.submitting.set(false);
        this.error.set(apiErrorMessage(error));
      },
    });
  }
}
