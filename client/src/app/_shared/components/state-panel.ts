import { Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Skeleton } from './skeleton';

/**
 * The loading / empty / error states every screen owes the user. Having one component means
 * a screen cannot quietly ship with only the happy path, and all three look the same
 * everywhere.
 *
 * Usage: render this when `loading`, `error` or "no rows" is true, and the real content
 * otherwise.
 *
 * **Say something specific.** The defaults exist so nothing is ever blank, not so they can
 * be shipped — "There is nothing here yet" is the same sentence for an empty member list and
 * a club with no payments this month, and it helps neither. Set `emptyTitle`, `emptyMessage`
 * and `emptyIcon` per screen, and offer the action that would fix it.
 */
@Component({
  selector: 'app-state-panel',
  imports: [MatProgressSpinnerModule, MatIconModule, MatButtonModule, Skeleton],
  template: `
    @if (loading()) {
      @if (skeleton()) {
        <div role="status" aria-live="polite">
          <span class="tcm-visually-hidden">{{ loadingMessage() }}</span>
          <app-skeleton [rowCount]="skeletonRows()" [variant]="skeletonVariant()" />
        </div>
      } @else {
        <div class="state-panel" role="status" aria-live="polite">
          <mat-spinner diameter="36" />
          <p class="state-message">{{ loadingMessage() }}</p>
        </div>
      }
    } @else if (error()) {
      <div class="state-panel" role="alert">
        <span class="state-badge state-badge-error">
          <mat-icon aria-hidden="true">error_outline</mat-icon>
        </span>
        <h2 class="state-title">{{ errorTitle() }}</h2>
        <p class="state-message">{{ error() }}</p>
        @if (canRetry()) {
          <button mat-flat-button (click)="retry.emit()">
            <mat-icon aria-hidden="true">refresh</mat-icon>
            <span>Try again</span>
          </button>
        }
      </div>
    } @else {
      <div class="state-panel">
        <span class="state-badge">
          <mat-icon aria-hidden="true">{{ emptyIcon() }}</mat-icon>
        </span>
        <h2 class="state-title">{{ emptyTitle() }}</h2>
        <p class="state-message">{{ emptyMessage() }}</p>
        @if (emptyActionLabel()) {
          <button mat-flat-button (click)="emptyAction.emit()">
            <mat-icon aria-hidden="true">{{ emptyActionIcon() }}</mat-icon>
            <span>{{ emptyActionLabel() }}</span>
          </button>
        }
      </div>
    }
  `,
  styles: `
    .state-panel {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--tcm-space-3);
      padding: var(--tcm-space-7) var(--tcm-space-4);
      text-align: center;
      animation: tcm-fade-in var(--tcm-duration-base) var(--tcm-ease-standard) both;
    }

    /* A tinted disc gives the icon somewhere to sit, so an empty screen looks composed
       rather than unfinished. */
    .state-badge {
      display: grid;
      place-items: center;
      inline-size: 3.5rem;
      block-size: 3.5rem;
      border-radius: 50%;
      background: var(--tcm-quiet-container);
      color: var(--tcm-on-quiet-container);

      mat-icon {
        inline-size: 1.75rem;
        block-size: 1.75rem;
        font-size: 1.75rem;
      }
    }

    .state-badge-error {
      background: var(--tcm-critical-container);
      color: var(--tcm-on-critical-container);
    }

    .state-title {
      margin: 0;
      font: var(--mat-sys-title-medium);
      color: var(--mat-sys-on-surface);
    }

    .state-message {
      margin: 0;
      max-inline-size: 42ch;
      color: var(--mat-sys-on-surface-variant);
    }

    button {
      display: inline-flex;
      align-items: center;
      gap: var(--tcm-space-2);
      margin-block-start: var(--tcm-space-1);
    }
  `,
})
export class StatePanel {
  readonly loading = input(false);
  readonly error = input<string | null>(null);
  readonly loadingMessage = input('Loading…');

  /** Prefer a skeleton wherever the shape of the result is predictable. */
  readonly skeleton = input(false);
  readonly skeletonRows = input(5);
  readonly skeletonVariant = input<'row' | 'card'>('row');

  readonly errorTitle = input('That did not work');
  readonly canRetry = input(true);

  readonly emptyTitle = input('Nothing here yet');
  readonly emptyMessage = input('There is nothing to show on this screen so far.');
  readonly emptyIcon = input('inbox');

  /** The thing that would fill this empty screen — "Register a member", "Add a training". */
  readonly emptyActionLabel = input('');
  readonly emptyActionIcon = input('add');

  readonly retry = output<void>();
  readonly emptyAction = output<void>();
}
