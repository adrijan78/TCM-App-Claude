import { DatePipe } from '@angular/common';
import { Component, computed, inject, input, output, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { PaymentService } from '../../_services/payment.service';
import { apiErrorMessage } from '../../_services/unwrap';
import { MembershipStatus } from '../../_models/payment.model';
import { MEMBERSHIP_PRESENTATION } from '../status-presentation';

/**
 * The next-due banner at the top of the Membership tab (SPEC 6.4), and the "Pay Membership
 * Fee" hand-off of SPEC 3.2.
 *
 * The payment flow is deliberately thin: ask the server for a URL, send the browser there.
 * Card details never touch this application, and nothing here can mark a membership paid —
 * only the server can, after verifying the session itself.
 */
@Component({
  selector: 'app-membership-banner',
  imports: [MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  template: `
    <div class="banner" [class]="'banner-' + state().tone">
      <span class="banner-badge">
        <mat-icon aria-hidden="true">{{ state().icon }}</mat-icon>
      </span>

      <div class="banner-text">
        <p class="banner-title">{{ headline() }}</p>
        <p class="banner-detail">{{ detail() }}</p>
      </div>

      @if (canPay()) {
        <button mat-flat-button [disabled]="starting()" (click)="pay()">
          @if (starting()) {
            <mat-spinner diameter="18" />
          } @else {
            <mat-icon aria-hidden="true">credit_card</mat-icon>
          }
          <span>Pay membership fee</span>
        </button>
      }
    </div>
  `,
  styles: `
    .banner {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--tcm-space-4);
      padding: var(--tcm-space-4);
      border-radius: var(--tcm-radius-lg);
      background: var(--banner-container);
      color: var(--banner-on-container);
    }

    .banner-positive {
      --banner-container: var(--tcm-positive-container);
      --banner-on-container: var(--tcm-on-positive-container);
    }

    .banner-caution {
      --banner-container: var(--tcm-caution-container);
      --banner-on-container: var(--tcm-on-caution-container);
    }

    .banner-critical {
      --banner-container: var(--tcm-critical-container);
      --banner-on-container: var(--tcm-on-critical-container);
    }

    .banner-badge {
      display: grid;
      place-items: center;
      inline-size: 2.5rem;
      block-size: 2.5rem;
      border-radius: 50%;
      background: color-mix(in srgb, currentColor 12%, transparent);
    }

    .banner-text {
      flex: 1 1 14rem;
      min-inline-size: 0;
    }

    .banner-title {
      margin: 0;
      font: var(--mat-sys-title-medium);
    }

    .banner-detail {
      margin: 0;
      font: var(--mat-sys-body-medium);
      opacity: 0.85;
    }

    button {
      display: inline-flex;
      align-items: center;
      gap: var(--tcm-space-2);
    }
  `,
})
export class MembershipBanner {
  private readonly payments = inject(PaymentService);
  private readonly snackBar = inject(MatSnackBar);

  readonly membership = input.required<MembershipStatus>();
  /** Shown only to the member themselves — a coach cannot pay on someone's behalf online. */
  readonly canPay = input(false);

  readonly paid = output<void>();

  protected readonly starting = signal(false);

  protected readonly state = computed(() => {
    const status = this.membership();
    if (status.isOverdue) return MEMBERSHIP_PRESENTATION.overdue;
    // Inside a week is close enough to warn about.
    if ((status.daysUntilDue ?? 0) <= 7) return MEMBERSHIP_PRESENTATION.due;
    return MEMBERSHIP_PRESENTATION.paid;
  });

  protected readonly headline = computed(() => {
    const status = this.membership();

    if (!status.nextPaymentDate) return 'No membership payment on record';
    if (status.isOverdue) return 'Membership overdue';

    const days = status.daysUntilDue ?? 0;
    if (days === 0) return 'Membership due today';
    return days <= 7
      ? `Membership due in ${days} ${days === 1 ? 'day' : 'days'}`
      : 'Membership up to date';
  });

  protected readonly detail = computed(() => {
    const status = this.membership();

    if (!status.nextPaymentDate) {
      return 'Once a payment is recorded, the next due date appears here.';
    }

    const due = new DatePipe('en-GB').transform(status.nextPaymentDate, 'd MMMM y');
    const days = status.daysUntilDue ?? 0;

    return status.isOverdue
      ? `Due ${due} — ${Math.abs(days)} ${Math.abs(days) === 1 ? 'day' : 'days'} ago.`
      : `Next payment due ${due}.`;
  });

  protected pay(): void {
    if (this.starting()) return;

    this.starting.set(true);

    this.payments.startCheckout().subscribe({
      next: (session) => {
        if (!session.isLiveStripe) {
          // Stripe:Enabled is off and a local fake is standing in. Saying so is the honest
          // thing: nobody should believe money moved when it did not.
          this.snackBar.open(
            'Stripe is not configured, so this is a simulated payment — no money will move.',
            'Dismiss',
            { duration: 8000 },
          );
        }

        // Leaving the app entirely, so no need to unset `starting`.
        window.location.assign(session.redirectUrl);
      },
      error: (error: unknown) => {
        this.starting.set(false);
        this.snackBar.open(apiErrorMessage(error, 'The payment could not be started.'), 'Dismiss', {
          duration: 6000,
        });
      },
    });
  }
}
