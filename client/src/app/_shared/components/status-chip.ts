import { Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { beltColour } from '../belt-colour';

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
    /*
      Squared off rather than a pill, and lettered in the data voice: a state is a reading
      off a record, the same register as a table header or an eyebrow. The leading edge is
      the rail again, at chip scale.
    */
    .chip {
      display: inline-flex;
      align-items: center;
      gap: var(--tcm-space-1);
      padding: 0.1875rem var(--tcm-space-2);
      border-radius: var(--tcm-radius-sm);
      background: var(--chip-container);
      color: var(--chip-on-container);
      box-shadow: inset 2px 0 0 0 var(--chip-accent);
      font-family: var(--tcm-font-mono);
      font-size: 0.6875rem;
      font-weight: 500;
      letter-spacing: 0.08em;
      text-transform: uppercase;
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
      --chip-accent: var(--tcm-positive);
    }

    .chip-caution {
      --chip-container: var(--tcm-caution-container);
      --chip-on-container: var(--tcm-on-caution-container);
      --chip-accent: var(--tcm-caution);
    }

    .chip-critical {
      --chip-container: var(--tcm-critical-container);
      --chip-on-container: var(--tcm-on-critical-container);
      --chip-accent: var(--tcm-critical);
    }

    .chip-info {
      --chip-container: var(--tcm-info-container);
      --chip-on-container: var(--tcm-on-info-container);
      --chip-accent: var(--tcm-info);
    }

    .chip-quiet {
      --chip-container: var(--tcm-quiet-container);
      --chip-on-container: var(--tcm-on-quiet-container);
      --chip-accent: var(--tcm-quiet);
    }
  `,
})
export class StatusChip {
  readonly label = input.required<string>();
  readonly tone = input.required<Tone>();
  readonly icon = input.required<string>();
}

/**
 * A rank, shown as the thing itself: a short length of belt beside its name.
 *
 * A dot would have done the same job, but a belt is a band — the one object everyone in a
 * dojang can read across the room — and this app is full of places where rank matters.
 */
@Component({
  selector: 'app-belt-swatch',
  template: `
    <span class="belt">
      <span class="belt-band" [style.background]="swatch()" aria-hidden="true"></span>
      <span class="belt-name">{{ beltName() }}</span>
    </span>
  `,
  styles: `
    .belt {
      display: inline-flex;
      align-items: center;
      gap: var(--tcm-space-2);
      white-space: nowrap;
    }

    .belt-band {
      inline-size: 1.375rem;
      block-size: 0.4375rem;
      border-radius: 1px;
      /* The ring is what keeps White visible on a white panel and Black on a dark one. */
      box-shadow: inset 0 0 0 1px var(--tcm-belt-ring);
    }

    .belt-name {
      font-weight: 500;
    }
  `,
})
export class BeltSwatch {
  readonly beltName = input.required<string>();

  protected readonly swatch = computed(() => beltColour(this.beltName()));
}
