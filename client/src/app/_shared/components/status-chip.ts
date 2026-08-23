import { Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/** The five signal ramps in `_tokens.scss`. Every state in the app maps onto one of them. */
export type Tone = 'positive' | 'caution' | 'critical' | 'info' | 'quiet';

/**
 * A state, shown as a chip.
 *
 * The icon is required, not decorative. Training status, attendance and note priority are
 * all encoded with colour, and colour alone fails for a colour-blind reader, a printed page
 * and a bad projector — so the glyph carries the same information independently.
 */
@Component({
  selector: 'app-status-chip',
  imports: [MatIconModule],
  template: `
    <span class="chip" [class]="'chip-' + tone()">
      <mat-icon class="chip-icon" aria-hidden="true">{{ icon() }}</mat-icon>
      <span class="chip-label">{{ label() }}</span>
    </span>
  `,
  styles: `
    .chip {
      display: inline-flex;
      align-items: center;
      gap: var(--tcm-space-1);
      padding: 0.1875rem var(--tcm-space-2);
      border-radius: var(--tcm-radius-pill);
      background: var(--chip-container);
      color: var(--chip-on-container);
      font: var(--mat-sys-label-medium);
      white-space: nowrap;
    }

    .chip-icon {
      inline-size: 1rem;
      block-size: 1rem;
      font-size: 1rem;
    }

    .chip-positive {
      --chip-container: var(--tcm-positive-container);
      --chip-on-container: var(--tcm-on-positive-container);
    }

    .chip-caution {
      --chip-container: var(--tcm-caution-container);
      --chip-on-container: var(--tcm-on-caution-container);
    }

    .chip-critical {
      --chip-container: var(--tcm-critical-container);
      --chip-on-container: var(--tcm-on-critical-container);
    }

    .chip-info {
      --chip-container: var(--tcm-info-container);
      --chip-on-container: var(--tcm-on-info-container);
    }

    .chip-quiet {
      --chip-container: var(--tcm-quiet-container);
      --chip-on-container: var(--tcm-on-quiet-container);
    }
  `,
})
export class StatusChip {
  readonly label = input.required<string>();
  readonly tone = input.required<Tone>();
  readonly icon = input.required<string>();
}

/** A coloured belt, shown as a ringed dot beside its name. */
@Component({
  selector: 'app-belt-swatch',
  template: `
    <span class="belt">
      <span class="belt-dot" [style.background]="swatch()" aria-hidden="true"></span>
      <span>{{ beltName() }}</span>
    </span>
  `,
  styles: `
    .belt {
      display: inline-flex;
      align-items: center;
      gap: var(--tcm-space-2);
      white-space: nowrap;
    }

    .belt-dot {
      inline-size: 0.75rem;
      block-size: 0.75rem;
      border-radius: 50%;
      /* The ring is what keeps White visible on a white panel and Black on a dark one. */
      box-shadow: inset 0 0 0 1px var(--tcm-belt-ring);
    }
  `,
})
export class BeltSwatch {
  readonly beltName = input.required<string>();

  /**
   * Belts are seeded rows, not an enum, so the colour is read out of the name. An unknown or
   * future belt falls back to the neutral swatch rather than disappearing.
   */
  protected readonly swatch = computed(() => {
    const name = this.beltName().toLowerCase();

    for (const colour of ['white', 'yellow', 'green', 'blue', 'red', 'black'] as const) {
      if (name.includes(colour)) {
        return `var(--tcm-belt-${colour})`;
      }
    }

    return 'var(--tcm-quiet)';
  });
}
