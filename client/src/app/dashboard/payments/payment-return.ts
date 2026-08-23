import { DatePipe } from '@angular/common';
import { Component, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { PaymentService } from '../../_services/payment.service';
import { apiErrorMessage } from '../../_services/unwrap';
import { Payment } from '../../_models/payment.model';
import { BrandMark } from '../../_shared/components/brand-mark';

/**
 * Where Stripe sends the browser back to (SPEC section 3.2).
 *
 * Arriving here proves nothing. The session id in the URL is posted to the server, which
 * asks Stripe whether that session was actually paid before writing a `Payments` row — so a
 * member who types `/successful-payment?session_id=anything` gets an error, not a
 * membership. This screen only reports what the server decided.
 */
@Component({
  selector: 'app-payment-return',
  imports: [
    DatePipe,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    BrandMark,
  ],
  template: `
    <section class="return tcm-enter">
      <app-brand-mark [size]="36" />

      @if (outcome() === 'cancelled') {
        <span class="return-badge return-badge-quiet">
          <mat-icon aria-hidden="true">remove_shopping_cart</mat-icon>
        </span>
        <h1>Payment cancelled</h1>
        <p>Nothing was charged. You can pay your membership fee whenever you are ready.</p>
      } @else if (confirming()) {
        <div class="return-spinner" role="status" aria-live="polite">
          <mat-spinner diameter="40" />
          <p>Confirming your payment…</p>
        </div>
      } @else if (payment(); as record) {
        <span class="return-badge return-badge-positive">
          <mat-icon aria-hidden="true">check_circle</mat-icon>
        </span>
        <h1>Payment received</h1>
        <p>
          Thank you. Your membership is paid until
          <strong>{{ record.nextPaymentDate | date: 'd MMMM y' }}</strong
          >.
        </p>
      } @else {
        <span class="return-badge return-badge-critical">
          <mat-icon aria-hidden="true">error_outline</mat-icon>
        </span>
        <h1>We could not confirm that payment</h1>
        <p>{{ error() }}</p>
        <p class="return-note">
          If your card was charged, nothing is lost — send your coach the date and time and they can
          check it against the club's records.
        </p>
      }

      <a mat-flat-button routerLink="/dashboard">
        <mat-icon aria-hidden="true">arrow_back</mat-icon>
        <span>Back to the dashboard</span>
      </a>
    </section>
  `,
  styles: `
    .return {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: var(--tcm-space-3);
      min-block-size: 60dvh;
      padding: var(--tcm-space-6);
      text-align: center;
    }

    .return-badge {
      display: grid;
      place-items: center;
      inline-size: 3.5rem;
      block-size: 3.5rem;
      border-radius: 50%;

      mat-icon {
        inline-size: 1.75rem;
        block-size: 1.75rem;
        font-size: 1.75rem;
      }
    }

    .return-badge-positive {
      background: var(--tcm-positive-container);
      color: var(--tcm-on-positive-container);
    }

    .return-badge-critical {
      background: var(--tcm-critical-container);
      color: var(--tcm-on-critical-container);
    }

    .return-badge-quiet {
      background: var(--tcm-quiet-container);
      color: var(--tcm-on-quiet-container);
    }

    .return-spinner {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--tcm-space-3);
    }

    h1 {
      margin: 0;
      font: var(--mat-sys-headline-small);
    }

    p {
      margin: 0;
      max-inline-size: 46ch;
      color: var(--mat-sys-on-surface-variant);
    }

    .return-note {
      font: var(--mat-sys-body-small);
    }

    a {
      display: inline-flex;
      align-items: center;
      gap: var(--tcm-space-2);
      margin-block-start: var(--tcm-space-3);
    }
  `,
})
export class PaymentReturn {
  private readonly payments = inject(PaymentService);

  /** Set from route data, because the two landings share this component. */
  readonly outcome = input<'success' | 'cancelled'>('success');

  /**
   * Stripe appends `?session_id=…`. The alias is what makes an underscored query parameter
   * reach a normally-named input.
   */
  readonly sessionId = input<string | undefined>(undefined, { alias: 'session_id' });

  protected readonly confirming = signal(false);
  protected readonly payment = signal<Payment | null>(null);
  protected readonly error = signal(
    'No payment session was supplied, so there was nothing to confirm.',
  );

  constructor() {
    // Read once, in the constructor: confirming a session is not idempotent from the user's
    // point of view and must not re-run because something else on the page changed.
    queueMicrotask(() => this.confirm());
  }

  private confirm(): void {
    if (this.outcome() === 'cancelled') return;

    const sessionId = this.sessionId();
    if (!sessionId) return;

    this.confirming.set(true);

    this.payments.confirm(sessionId).subscribe({
      next: (payment) => {
        this.payment.set(payment);
        this.confirming.set(false);
      },
      error: (error: unknown) => {
        this.error.set(apiErrorMessage(error, 'That payment session could not be verified.'));
        this.confirming.set(false);
      },
    });
  }
}
