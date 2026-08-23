import { Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

/**
 * The loading / empty / error states every screen owes the user. Having one component means
 * a screen cannot quietly ship with only the happy path, and all three look the same
 * everywhere.
 *
 * Usage: render this when `loading`, `error` or "no rows" is true, and the real content
 * otherwise.
 */
@Component({
  selector: 'app-state-panel',
  imports: [MatProgressSpinnerModule, MatIconModule, MatButtonModule],
  template: `
    @if (loading()) {
      <div class="state-panel" role="status" aria-live="polite">
        <mat-spinner diameter="40" />
        <p class="state-message">{{ loadingMessage() }}</p>
      </div>
    } @else if (error()) {
      <div class="state-panel" role="alert">
        <mat-icon class="state-icon state-icon-error">error_outline</mat-icon>
        <p class="state-message">{{ error() }}</p>
        @if (canRetry()) {
          <button mat-stroked-button (click)="retry.emit()">Try again</button>
        }
      </div>
    } @else {
      <div class="state-panel">
        <mat-icon class="state-icon">{{ emptyIcon() }}</mat-icon>
        <p class="state-message">{{ emptyMessage() }}</p>
      </div>
    }
  `,
  styles: `
    .state-panel {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.75rem;
      padding: 2.5rem 1rem;
      text-align: center;
    }

    .state-icon {
      inline-size: 2.5rem;
      block-size: 2.5rem;
      font-size: 2.5rem;
      color: var(--mat-sys-on-surface-variant);
    }

    .state-icon-error {
      color: var(--mat-sys-error);
    }

    .state-message {
      margin: 0;
      max-inline-size: 32rem;
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class StatePanel {
  readonly loading = input(false);
  readonly error = input<string | null>(null);
  readonly loadingMessage = input('Loading…');
  readonly emptyMessage = input('There is nothing here yet.');
  readonly emptyIcon = input('inbox');
  readonly canRetry = input(true);

  readonly retry = output<void>();
}
