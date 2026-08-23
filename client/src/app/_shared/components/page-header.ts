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
      align-items: flex-start;
      justify-content: space-between;
      gap: var(--tcm-space-4);
      margin-block-end: var(--tcm-space-5);
    }

    .page-header-text {
      min-inline-size: 0;
    }

    .page-header-title {
      margin: 0;
      font: var(--mat-sys-headline-medium);
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
}
