import { Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { Tone } from './status-chip';

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
    <div class="stat" [style.--tcm-rail]="rail()">
      <div class="stat-head">
        <span class="tcm-eyebrow stat-label">{{ label() }}</span>
        @if (icon()) {
          <mat-icon class="stat-icon" aria-hidden="true">{{ icon() }}</mat-icon>
        }
      </div>

      <p class="stat-value tcm-figure">
        {{ shown() }}
        @if (suffix() && value() !== null) {
          <span class="stat-suffix">{{ suffix() }}</span>
        }
      </p>
    </div>
  `,
  styles: `
    /*
      The figure leads and the label sits above it in the data voice — a scoreboard reading,
      not a marketing tile. The rail down the leading edge carries the card's meaning.
    */
    .stat {
      padding: var(--tcm-space-4);
      border: 1px solid var(--tcm-panel-border);
      border-radius: var(--tcm-radius-lg);
      background: var(--tcm-panel-bg);
      box-shadow:
        inset var(--tcm-rail-width) 0 0 0 var(--tcm-rail),
        var(--tcm-shadow-1);
      block-size: 100%;
    }

    .stat-head {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: var(--tcm-space-2);
      /* Two lines' worth, so a long label and a short one leave their figures on the same
         baseline when the cards sit side by side on a phone. */
      min-block-size: 2rem;
    }

    /* Wrapping beats truncating: "Awaiting your reply" clipped to "Awaiting you…" tells the
       reader less than the second line costs. */
    .stat-label {
      min-inline-size: 0;
    }

    .stat-icon {
      flex: none;
      inline-size: 1.125rem;
      block-size: 1.125rem;
      font-size: 1.125rem;
      color: var(--tcm-rail);
      opacity: 0.9;
    }

    .stat-value {
      margin: var(--tcm-space-3) 0 0;
      font-size: 2.75rem;
    }

    .stat-suffix {
      margin-inline-start: 0.125rem;
      font-family: var(--tcm-font-body);
      font-size: 1.125rem;
      font-weight: 500;
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

  /**
   * Which signal ramp this figure belongs to. It colours the rail and the icon, so a row of
   * cards reads as a row of *different* measurements rather than four identical boxes.
   */
  readonly tone = input<Tone>('info');

  protected readonly rail = computed(() => `var(--tcm-${this.tone()})`);

  protected readonly shown = computed(() => {
    const value = this.value();
    return value === null || value === '' ? '—' : value;
  });
}
