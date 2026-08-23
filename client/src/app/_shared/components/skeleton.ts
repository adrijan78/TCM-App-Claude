import { Component, computed, input } from '@angular/core';

/**
 * A placeholder in the shape of the thing that is loading.
 *
 * A spinner tells the user to wait; a skeleton tells them what they are waiting for, and
 * the page does not jump when the data lands. Use it wherever the layout is predictable —
 * a table with a known column count, a row of stat cards, a member list. Where it is not
 * (a chart whose height depends on the data), `StatePanel`'s spinner is still the right
 * answer.
 *
 * `aria-hidden` throughout: the live region announcing "loading" belongs to the screen,
 * not to twelve individual grey rectangles.
 */
@Component({
  selector: 'app-skeleton',
  template: `
    <div class="skeleton-set" aria-hidden="true">
      @for (row of rows(); track row) {
        <div class="skeleton-row">
          @for (cell of cells(); track cell) {
            <span class="skeleton-cell" [style.flex]="cell"></span>
          }
        </div>
      }
    </div>
  `,
  styles: `
    .skeleton-set {
      display: flex;
      flex-direction: column;
      gap: var(--tcm-space-3);
    }

    .skeleton-row {
      display: flex;
      gap: var(--tcm-space-3);
    }

    .skeleton-cell {
      block-size: 1.25rem;
      border-radius: var(--tcm-radius-sm);
      background: linear-gradient(
          90deg,
          var(--tcm-skeleton-base) 25%,
          var(--tcm-skeleton-sheen) 37%,
          var(--tcm-skeleton-base) 63%
        )
        0 0 / 400% 100%;
      animation: tcm-shimmer 1.4s ease-in-out infinite;
    }

    :host([variant='card']) .skeleton-cell {
      block-size: 5.5rem;
      border-radius: var(--tcm-radius-lg);
    }
  `,
  host: { '[attr.variant]': 'variant()' },
})
export class Skeleton {
  readonly rowCount = input(5);
  /** Relative column widths. Uneven values read as real content; equal ones read as a grid. */
  readonly columns = input<readonly number[]>([3, 2, 2, 1]);
  readonly variant = input<'row' | 'card'>('row');

  protected readonly rows = computed(() =>
    Array.from({ length: Math.max(1, this.rowCount()) }, (_, index) => index),
  );

  protected readonly cells = computed(() => this.columns());
}
