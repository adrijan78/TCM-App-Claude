import { Component, input } from '@angular/core';

/**
 * The top of every dashboard screen: what this page is, one line of context, and the
 * screen's primary actions projected into the right-hand slot.
 *
 * It exists so the five feature modules of phase 9 cannot each invent their own heading
 * treatment — which is exactly what happens when the pattern is "an `<h1>` and some
 * flexbox, copied from whichever screen you looked at last".
 */
@Component({
  selector: 'app-page-header',
  template: `
    <header class="page-header">
      <div class="page-header-text">
        @if (eyebrow()) {
          <span class="tcm-eyebrow page-header-eyebrow">{{ eyebrow() }}</span>
        }
        <h1 class="page-header-title">{{ title() }}</h1>
        @if (subtitle()) {
          <p class="page-header-subtitle">{{ subtitle() }}</p>
        }
      </div>

      <div class="page-header-actions">
        <ng-content />
      </div>
    </header>
  `,
  styles: `
    .page-header {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-end;
      justify-content: space-between;
      gap: var(--tcm-space-4);
      margin-block-end: var(--tcm-space-5);
      padding-block-end: var(--tcm-space-4);
      /* A hairline under the header, so the page has a masthead rather than a floating title. */
      border-block-end: 1px solid var(--tcm-panel-border);
    }

    .page-header-text {
      min-inline-size: 0;
    }

    .page-header-eyebrow {
      margin-block-end: var(--tcm-space-1);
      /* Gold only on the marker, not the words: it ties the page to the chrome above it. */
      border-inline-start: 2px solid var(--tcm-gold);
      padding-inline-start: var(--tcm-space-2);
    }

    .page-header-title {
      margin: 0;
      font: var(--mat-sys-headline-medium);
      font-family: var(--tcm-font-display);
      font-weight: 600;
      letter-spacing: -0.01em;
      color: var(--mat-sys-on-surface);
    }

    .page-header-subtitle {
      margin: var(--tcm-space-1) 0 0;
      max-inline-size: 52ch;
      font: var(--mat-sys-body-medium);
      color: var(--mat-sys-on-surface-variant);
    }

    .page-header-actions {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--tcm-space-2);
    }

    /* An empty slot must not leave a gap the eye reads as a missing button. */
    .page-header-actions:empty {
      display: none;
    }
  `,
})
export class PageHeader {
  readonly title = input.required<string>();
  readonly subtitle = input('');

  /**
   * A short mono label above the title, saying where in the app this page sits — "Club ·
   * Dashboard", "Members · Profile". It is navigation, not decoration: the shell's rail
   * collapses to icons and disappears entirely on a phone, and this is what replaces it.
   */
  readonly eyebrow = input('');
}
