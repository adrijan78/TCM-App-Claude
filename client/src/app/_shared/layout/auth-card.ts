import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { BrandMark } from '../components/brand-mark';

/**
 * The full-page frame the three signed-out screens share (SPEC section 6.1). They render
 * outside `Shell` — there is no navigation to offer someone who is not signed in — so the
 * club's identity has to come from somewhere, and this is it.
 *
 * The left panel is decorative and disappears below 992px, where the form is the only thing
 * worth the space. Content is projected, so each screen owns nothing but its own form.
 */
@Component({
  selector: 'app-auth-card',
  imports: [MatIconModule, BrandMark],
  template: `
    <div class="auth-page">
      <section class="auth-brand" aria-hidden="true">
        <div class="auth-brand-head">
          <app-brand-mark [size]="44" tone="chrome" />
          <p class="auth-brand-name">Taekwondo Club</p>
        </div>

        <p class="auth-brand-tagline">Members, trainings, belts and dues — in one place.</p>

        <ul class="auth-brand-points">
          @for (point of points; track point.label) {
            <li>
              <mat-icon>{{ point.icon }}</mat-icon>
              <span>{{ point.label }}</span>
            </li>
          }
        </ul>

        <!--
          The grading ladder, white through black. It is the club's whole progression in one
          line, and it is the only ornament on the page.
        -->
        <div class="auth-belts">
          @for (belt of belts; track belt) {
            <span class="auth-belt" [style.background]="belt"></span>
          }
        </div>
      </section>

      <main class="auth-panel">
        <div class="auth-card tcm-enter">
          <!-- The mark again, for the narrow layout where the left panel is gone. -->
          <div class="auth-card-mark">
            <app-brand-mark [size]="36" label="Taekwondo Club" />
          </div>

          <header class="auth-header">
            <h1 class="auth-title">{{ title() }}</h1>
            @if (subtitle()) {
              <p class="auth-subtitle">{{ subtitle() }}</p>
            }
          </header>

          <ng-content />
        </div>
      </main>
    </div>
  `,
  styles: `
    .auth-page {
      display: grid;
      grid-template-columns: 1fr;
      min-block-size: 100dvh;
      background: var(--tcm-page-bg);

      @media (min-width: 992px) {
        grid-template-columns: 5fr 7fr;
      }
    }

    /*
      The same ink as the app's chrome, so signing in and being signed in are recognisably
      the same place. The gold hairline down the inside edge is the one the toolbar carries.
    */
    .auth-brand {
      display: none;
      flex-direction: column;
      justify-content: center;
      gap: var(--tcm-space-5);
      padding: var(--tcm-space-8) var(--tcm-space-7);
      color: var(--tcm-on-ink);
      background: var(--tcm-ink);
      /* The same gold seam the toolbar carries, on the one page that has no toolbar. */
      border-inline-end: 2px solid var(--tcm-gold-hairline);

      @media (min-width: 992px) {
        display: flex;
      }
    }

    .auth-brand-head {
      display: flex;
      align-items: center;
      gap: var(--tcm-space-3);
    }

    .auth-brand-name {
      margin: 0;
      font-family: var(--tcm-font-display);
      font-size: 1.75rem;
      font-weight: 700;
      letter-spacing: 0.14em;
      text-transform: uppercase;
    }

    .auth-brand-tagline {
      margin: 0;
      max-inline-size: 26ch;
      font: var(--mat-sys-title-medium);
      opacity: 0.9;
    }

    .auth-brand-points {
      margin: 0;
      padding: 0;
      list-style: none;
      display: grid;
      gap: var(--tcm-space-3);
      font: var(--mat-sys-body-medium);
      color: var(--tcm-on-ink-muted);

      li {
        display: flex;
        align-items: center;
        gap: var(--tcm-space-3);
      }

      mat-icon {
        inline-size: 1.25rem;
        block-size: 1.25rem;
        font-size: 1.25rem;
      }
    }

    .auth-belts {
      display: flex;
      gap: 3px;
      margin-block-start: var(--tcm-space-4);
    }

    .auth-belt {
      block-size: 0.5rem;
      inline-size: 2.25rem;
      border-radius: 1px;
      /* This panel is ink in both themes, so the ring is a fixed light one — the themed
         token would hide the black belt against the wall. */
      box-shadow: inset 0 0 0 1px rgb(255 255 255 / 35%);
    }

    .auth-panel {
      display: grid;
      place-items: center;
      padding: var(--tcm-space-6) var(--tcm-space-4);
    }

    .auth-card {
      inline-size: min(26rem, 100%);
      padding: var(--tcm-space-6);
      border: 1px solid var(--tcm-panel-border);
      border-radius: var(--tcm-radius-lg);
      background: var(--tcm-panel-bg);
      box-shadow: var(--tcm-shadow-2);
    }

    .auth-card-mark {
      display: flex;
      justify-content: center;
      margin-block-end: var(--tcm-space-5);

      @media (min-width: 992px) {
        display: none;
      }
    }

    .auth-header {
      margin-block-end: var(--tcm-space-5);
    }

    .auth-title {
      margin: 0 0 var(--tcm-space-1);
      font: var(--mat-sys-headline-medium);
      font-family: var(--tcm-font-display);
      font-weight: 600;
      color: var(--mat-sys-on-surface);
    }

    .auth-subtitle {
      margin: 0;
      font: var(--mat-sys-body-medium);
      color: var(--mat-sys-on-surface-variant);
      overflow-wrap: anywhere;
    }
  `,
})
export class AuthCard {
  readonly title = input.required<string>();
  readonly subtitle = input('');

  /** White through black: the grading ladder, drawn from the same tokens as every swatch. */
  protected readonly belts = [
    'var(--tcm-belt-white)',
    'var(--tcm-belt-yellow)',
    'var(--tcm-belt-green)',
    'var(--tcm-belt-blue)',
    'var(--tcm-belt-red)',
    'var(--tcm-belt-black)',
  ];

  protected readonly points = [
    { icon: 'event_available', label: 'Trainings, attendance and invitations' },
    { icon: 'military_tech', label: 'Belt progress and exam history' },
    { icon: 'receipt_long', label: 'Membership dues, paid online or in cash' },
  ];
}
