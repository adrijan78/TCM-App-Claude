import { Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/**
 * The inline banner a form uses to report what the server said — the counterpart to
 * `StatePanel`, which speaks for a whole screen.
 *
 * Errors belong next to the form that produced them, not in a snackbar that has vanished by
 * the time the user has finished reading it, so the error interceptor stays quiet for the
 * 400 and 401 responses these screens raise.
 *
 * Colours come from the signal ramps in `_tokens.scss`, so a failure here is the same red as
 * a failure anywhere else in the app.
 */
@Component({
  selector: 'app-form-alert',
  imports: [MatIconModule],
  template: `
    <div
      class="form-alert"
      [class]="'form-alert-' + tone()"
      [attr.role]="tone() === 'success' ? 'status' : 'alert'"
    >
      <mat-icon class="form-alert-icon" aria-hidden="true">{{ icon() }}</mat-icon>

      <div class="form-alert-body">
        <p class="form-alert-message">{{ message() }}</p>

        @if (details().length) {
          <ul class="form-alert-details">
            @for (detail of details(); track detail) {
              <li>{{ detail }}</li>
            }
          </ul>
        }
      </div>
    </div>
  `,
  styles: `
    .form-alert {
      display: flex;
      gap: var(--tcm-space-3);
      padding: var(--tcm-space-3);
      border-radius: var(--tcm-radius-md);
      background: var(--alert-container);
      color: var(--alert-on-container);
      animation: tcm-fade-rise var(--tcm-duration-base) var(--tcm-ease-standard) both;
    }

    .form-alert-error {
      --alert-container: var(--tcm-critical-container);
      --alert-on-container: var(--tcm-on-critical-container);
    }

    .form-alert-success {
      --alert-container: var(--tcm-positive-container);
      --alert-on-container: var(--tcm-on-positive-container);
    }

    .form-alert-info {
      --alert-container: var(--tcm-info-container);
      --alert-on-container: var(--tcm-on-info-container);
    }

    .form-alert-icon {
      flex: none;
      inline-size: 1.25rem;
      block-size: 1.25rem;
      font-size: 1.25rem;
      margin-block-start: 0.125rem;
    }

    .form-alert-body {
      min-inline-size: 0;
    }

    .form-alert-message {
      margin: 0;
      font: var(--mat-sys-body-medium);
    }

    .form-alert-details {
      margin: var(--tcm-space-2) 0 0;
      padding-inline-start: var(--tcm-space-4);
      font: var(--mat-sys-body-small);
    }
  `,
})
export class FormAlert {
  readonly message = input.required<string>();
  readonly details = input<readonly string[]>([]);
  readonly tone = input<'error' | 'success' | 'info'>('error');

  protected readonly icon = computed(
    () => ({ error: 'error_outline', success: 'check_circle', info: 'info_outline' })[this.tone()],
  );
}
