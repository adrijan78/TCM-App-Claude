import { Component, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
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
 * SPEC section 6.1 — sign in.
 *
 * `returnUrl` is bound from the query string by `withComponentInputBinding()`; both
 * `authGuard` and the 401 branch of the error interceptor set it. It is only ever followed
 * when it is a path on this origin — an absolute URL there would turn the login page into
 * an open redirect.
 */
@Component({
  selector: 'app-login',
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
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  /** Bound from `?returnUrl=` by the router's component input binding. */
  readonly returnUrl = input<string>();

  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly showPassword = signal(false);

  protected submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set(null);

    this.auth.login(this.form.getRawValue()).subscribe({
      next: () => {
        this.submitting.set(false);
        void this.router.navigateByUrl(this.safeReturnUrl());
      },
      error: (error: unknown) => {
        this.submitting.set(false);
        this.error.set(apiErrorMessage(error, 'Invalid email or password.'));
        this.form.controls.password.reset();
      },
    });
  }

  protected togglePassword(): void {
    this.showPassword.update((shown) => !shown);
  }

  /**
   * Only a same-origin path is followed. Anything else — an absolute URL, a protocol-relative
   * `//evil.example` — is discarded in favour of the dashboard.
   */
  private safeReturnUrl(): string {
    const target = this.returnUrl();

    return target && target.startsWith('/') && !target.startsWith('//') ? target : '/dashboard';
  }
}
