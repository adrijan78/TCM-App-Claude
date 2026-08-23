import { Component, input } from '@angular/core';

/**
 * The club mark: a taegeuk, drawn from the theme's own primary and tertiary colours rather
 * than the traditional red and blue, so the logo belongs to the palette instead of fighting
 * it. Inline SVG, because it has to recolour with the theme and survive a dark background.
 *
 * Decorative wherever a text label sits beside it — which is everywhere it is used — so it
 * is hidden from assistive technology by default.
 */
@Component({
  selector: 'app-brand-mark',
  template: `
    <svg
      class="mark"
      [class.mark-chrome]="tone() === 'chrome'"
      viewBox="0 0 64 64"
      [attr.width]="size()"
      [attr.height]="size()"
      [attr.role]="label() ? 'img' : 'presentation'"
      [attr.aria-label]="label() || null"
      [attr.aria-hidden]="label() ? null : 'true'"
      focusable="false"
    >
      <circle cx="32" cy="32" r="30" class="mark-yin" />
      <path d="M32 2 A30 30 0 0 1 32 62 A15 15 0 0 1 32 32 A15 15 0 0 0 32 2 Z" class="mark-yang" />
      <circle cx="32" cy="17" r="4.5" class="mark-yang" />
      <circle cx="32" cy="47" r="4.5" class="mark-yin" />
    </svg>
  `,
  styles: `
    .mark {
      display: block;
      flex: none;
    }

    .mark-yin {
      fill: var(--mark-yin, var(--mat-sys-primary));
    }

    .mark-yang {
      fill: var(--mark-yang, var(--mat-sys-tertiary));
    }

    /* On the ink chrome, where the navy half would disappear: paper against gold. */
    .mark-chrome {
      --mark-yin: var(--tcm-on-ink);
      --mark-yang: var(--tcm-gold);
    }
  `,
})
export class BrandMark {
  readonly size = input(28);
  /** Set only where the mark stands alone; otherwise the adjacent text is the label. */
  readonly label = input('');
  /** `chrome` recolours the mark for the ink toolbar and nav rail. */
  readonly tone = input<'surface' | 'chrome'>('surface');
}
