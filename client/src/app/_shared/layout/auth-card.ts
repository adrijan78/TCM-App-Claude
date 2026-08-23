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
          <app-brand-mark [size]="44" />
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

    .auth-brand {
      display: none;
      flex-direction: column;
      justify-content: center;
      gap: var(--tcm-space-5);
      padding: var(--tcm-space-8) var(--tcm-space-7);
      color: var(--mat-sys-on-primary-container);
      /* Two stops off the same hue: enough depth to read as a panel, not a poster. */
      background:
        radial-gradient(
          80% 60% at 15% 10%,
          color-mix(in srgb, var(--mat-sys-tertiary-container) 55%, transparent),
          transparent 70%
        ),
        var(--mat-sys-primary-container);

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
      font: var(--mat-sys-headline-medium);
      font-weight: 700;
      letter-spacing: 0.02em;
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
      opacity: 0.85;

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

    .auth-panel {
      display: grid;
      place-items: center;
      padding: var(--tcm-space-6) var(--tcm-space-4);
    }

    .auth-card {
      inline-size: min(26rem, 100%);
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

  protected readonly points = [
    { icon: 'event_available', label: 'Trainings, attendance and invitations' },
    { icon: 'military_tech', label: 'Belt progress and exam history' },
    { icon: 'receipt_long', label: 'Membership dues, paid online or in cash' },
  ];
}
