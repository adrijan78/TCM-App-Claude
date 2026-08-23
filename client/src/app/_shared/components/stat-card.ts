import { Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/**
 * One headline number: the club's stat cards on the dashboard (SPEC 6.2) and the member's on
 * their profile (SPEC 6.4).
 *
 * Shared because both screens had begun to grow their own near-identical version, and the
 * two were already a few pixels apart.
 */
@Component({
  selector: 'app-stat-card',
  imports: [MatIconModule],
  template: `
    <div class="stat">
      @if (icon()) {
        <span class="stat-icon">
          <mat-icon aria-hidden="true">{{ icon() }}</mat-icon>
        </span>
      }

      <p class="stat-label">{{ label() }}</p>
      <p class="stat-value">
        {{ shown() }}
        @if (suffix() && value() !== null) {
          <span class="stat-suffix">{{ suffix() }}</span>
        }
      </p>
    </div>
  `,
  styles: `
    .stat {
      padding: var(--tcm-space-4);
      border: 1px solid var(--tcm-panel-border);
      border-radius: var(--tcm-radius-lg);
      background: var(--tcm-panel-bg);
      box-shadow: var(--tcm-shadow-1);
      block-size: 100%;
    }

    .stat-icon {
      display: grid;
      place-items: center;
      inline-size: 2.25rem;
      block-size: 2.25rem;
      margin-block-end: var(--tcm-space-3);
      border-radius: var(--tcm-radius-md);
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);

      mat-icon {
        inline-size: 1.25rem;
        block-size: 1.25rem;
        font-size: 1.25rem;
      }
    }

    .stat-label {
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-label-medium);
      text-transform: uppercase;
      letter-spacing: 0.06em;
    }

    .stat-value {
      margin: var(--tcm-space-1) 0 0;
      font-family: 'Barlow Condensed', Roboto, sans-serif;
      font-size: 2.5rem;
      font-weight: 600;
      line-height: 1;
    }

    .stat-suffix {
      font-size: 1.25rem;
      font-family: Roboto, sans-serif;
      font-weight: 400;
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class StatCard {
  readonly label = input.required<string>();
  /** Null is a real answer — "no scores yet" is not zero — and renders as a dash. */
  readonly value = input.required<string | number | null>();
  readonly suffix = input('');
  readonly icon = input('');

  protected readonly shown = computed(() => {
    const value = this.value();
    return value === null || value === '' ? '—' : value;
  });
}
