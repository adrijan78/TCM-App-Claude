import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../_services/auth.service';
import { BrandMark } from '../_shared/components/brand-mark';

/** SPEC section 3.3 — the 404 page. */
@Component({
  selector: 'app-not-found',
  imports: [RouterLink, MatButtonModule, MatIconModule, BrandMark],
  template: `
    <section class="not-found tcm-enter">
      <app-brand-mark [size]="40" />

      <p class="not-found-code">404</p>
      <span class="tcm-eyebrow">Page not found</span>
      <h1 class="not-found-title">We could not find that page</h1>
      <p class="not-found-body">
        The link may be out of date, or the page may have moved. Emailed links to a training or a
        member profile expire when the record does.
      </p>

      <a mat-flat-button [routerLink]="homeLink()">
        <mat-icon aria-hidden="true">arrow_back</mat-icon>
        <span>{{ isSignedIn() ? 'Back to the dashboard' : 'Back to sign in' }}</span>
      </a>
    </section>
  `,
  styles: `
    .not-found {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: var(--tcm-space-3);
      min-block-size: 100dvh;
      padding: var(--tcm-space-6);
      text-align: center;
      background: var(--tcm-page-bg);
    }

    .not-found-code {
      margin: var(--tcm-space-2) 0 0;
      font-family: var(--tcm-font-display);
      font-size: clamp(4rem, 18vw, 7rem);
      font-weight: 700;
      line-height: 0.9;
      letter-spacing: 0.02em;
      /* Big, but not shouting: it is a signpost, not the message. The gold is the only
         thing on this page that belongs to the club rather than to the error. */
      color: transparent;
      -webkit-text-stroke: 2px var(--tcm-gold-hairline);
    }

    .not-found-title {
      margin: 0;
      font: var(--mat-sys-headline-small);
      font-family: var(--tcm-font-display);
      font-weight: 600;
    }

    .not-found-body {
      margin: 0;
      max-inline-size: 44ch;
      color: var(--mat-sys-on-surface-variant);
    }

    a {
      display: inline-flex;
      align-items: center;
      gap: var(--tcm-space-2);
      margin-block-start: var(--tcm-space-2);
    }
  `,
})
export class NotFound {
  private readonly auth = inject(AuthService);

  protected isSignedIn(): boolean {
    return this.auth.isAuthenticated();
  }

  /** Sending a signed-out visitor to the dashboard would only bounce them to login. */
  protected homeLink(): string {
    return this.isSignedIn() ? '/dashboard' : '/login';
  }
}
