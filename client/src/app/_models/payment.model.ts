import { PaymentMethod } from './enums';

/** One row of a payment history (SPEC sections 6.4 and 6.7). */
export interface Payment {
  id: number;
  memberId: string;
  memberFullName: string;
  isPaidOnline: boolean;
  paymentDate: string;
  nextPaymentDate: string;
}

/** The banner at the top of the Membership tab. */
export interface MembershipStatus {
  nextPaymentDate: string | null;
  isOverdue: boolean;
  daysUntilDue: number | null;
}

export interface MemberPaymentHistory {
  memberId: string;
  memberFullName: string;
  membership: MembershipStatus;
  payments: Payment[];
}

/** Filters for the club-wide payments page (SPEC section 6.7). Coach only. */
export interface PaymentFilter {
  year?: number | null;
  month?: number | null;
  memberId?: string | null;
  method?: PaymentMethod | null;
}

/** Coach logs a cash payment. The member is never the caller. */
export interface CashPayment {
  memberId: string;
  paymentDate: string | null;
}

/**
 * Where to send the browser to pay. `isLiveStripe` is false while `Stripe:Enabled` is off
 * on the server, so the UI can say plainly that no money will move.
 */
export interface CheckoutSession {
  redirectUrl: string;
  isLiveStripe: boolean;
}

export interface ConfirmPayment {
  sessionId: string;
}
