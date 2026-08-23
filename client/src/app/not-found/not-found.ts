import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../_services/auth.service';

/** SPEC section 3.3 — the 404 page. */
@Component({
  selector: 'app-not-found',
  imports: [RouterLink, MatButtonModule, MatIconModule],
  template: `
    <section class="not-found">
      <mat-icon class="not-found-icon">explore_off</mat-icon>
      <h1>We could not find that page</h1>
      <p>The link may be out of date, or the page may have moved.</p>
      <a mat-flat-button [routerLink]="homeLink()">Back to safety</a>
    </section>
  `,
  styles: `
    .not-found {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 1rem;
      min-block-size: 100dvh;
      padding: 2rem;
      text-align: center;
    }

    .not-found-icon {
      inline-size: 4rem;
      block-size: 4rem;
      font-size: 4rem;
      color: var(--mat-sys-on-surface-variant);
    }

    h1 {
      margin: 0;
      font: var(--mat-sys-headline-small);
    }

    p {
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class NotFound {
  private readonly auth = inject(AuthService);

  /** Sending a signed-out visitor to the dashboard would only bounce them to login. */
  protected homeLink(): string {
    return this.auth.isAuthenticated() ? '/dashboard' : '/login';
  }
}
