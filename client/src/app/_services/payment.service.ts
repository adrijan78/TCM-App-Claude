import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../environments/environment';
import { ApiResponse } from '../_models/api-response.model';
import {
  CashPayment,
  CheckoutSession,
  MemberPaymentHistory,
  Payment,
  PaymentFilter,
} from '../_models/payment.model';
import { unwrap } from './unwrap';

/**
 * Membership payments (SPEC 6.4 and 6.7) and the Stripe hand-off (SPEC 3.2).
 *
 * Note what is missing: there is no way from here to record an online payment. Only
 * `confirm()` can, and only by handing the server a session id it then verifies with Stripe
 * itself. Card details never reach this application.
 */
@Injectable({ providedIn: 'root' })
export class PaymentService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/payments`;
  private readonly stripeBase = `${environment.apiUrl}/stripe`;

  /** The club-wide table with the four filters of SPEC 6.7. Coach only. */
  getClubPayments(filter: PaymentFilter = {}): Observable<Payment[]> {
    let params = new HttpParams();
    if (filter.year) params = params.set('year', filter.year);
    if (filter.month) params = params.set('month', filter.month);
    if (filter.memberId) params = params.set('memberId', filter.memberId);
    // Cash is 0, so this needs an explicit null check rather than a truthiness test.
    if (filter.method !== null && filter.method !== undefined) {
      params = params.set('method', filter.method);
    }

    return this.http.get<ApiResponse<Payment[]>>(this.base, { params }).pipe(map(unwrap));
  }

  /** One member's history plus the next-due banner above it. */
  getMemberHistory(memberId: string): Observable<MemberPaymentHistory> {
    return this.http
      .get<ApiResponse<MemberPaymentHistory>>(`${this.base}/member/${memberId}`)
      .pipe(map(unwrap));
  }

  /** Coach only: logs cash handed over in person. */
  logCashPayment(payment: CashPayment): Observable<Payment> {
    return this.http.post<ApiResponse<Payment>>(`${this.base}/cash`, payment).pipe(map(unwrap));
  }

  delete(id: number): Observable<void> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/${id}`).pipe(map(() => undefined));
  }

  /**
   * Starts a payment for the signed-in member. The response's `isLiveStripe` is false while
   * `Stripe:Enabled` is off on the server, and the UI has to say so plainly rather than let
   * someone believe they have paid.
   */
  startCheckout(): Observable<CheckoutSession> {
    return this.http
      .post<ApiResponse<CheckoutSession>>(`${this.stripeBase}/checkout-session`, {})
      .pipe(map(unwrap));
  }

  /** Posted after returning from the payment page. The server verifies before recording. */
  confirm(sessionId: string): Observable<Payment> {
    return this.http
      .post<ApiResponse<Payment>>(`${this.stripeBase}/confirm`, { sessionId })
      .pipe(map(unwrap));
  }
}
