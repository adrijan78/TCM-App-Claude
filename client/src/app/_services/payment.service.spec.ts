import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { PaymentService } from './payment.service';
import { environment } from '../../environments/environment';
import { PaymentMethod } from '../_models/enums';

describe('PaymentService', () => {
  let service: PaymentService;
  let controller: HttpTestingController;

  const base = `${environment.apiUrl}/payments`;
  const stripe = `${environment.apiUrl}/stripe`;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(PaymentService);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    controller.verify();
    localStorage.clear();
  });

  it('sends the Cash filter even though its value is 0', () => {
    service.getClubPayments({ method: PaymentMethod.Cash }).subscribe();

    const request = controller.expectOne((r) => r.url === base);
    expect(request.request.params.get('method')).toBe('0');
    request.flush({ success: true, data: [], message: null, errors: [] });
  });

  it('combines the year, month and member filters', () => {
    service.getClubPayments({ year: 2026, month: 3, memberId: 'member-1' }).subscribe();

    const request = controller.expectOne((r) => r.url === base);
    expect(request.request.params.get('year')).toBe('2026');
    expect(request.request.params.get('month')).toBe('3');
    expect(request.request.params.get('memberId')).toBe('member-1');
    request.flush({ success: true, data: [], message: null, errors: [] });
  });

  it('logs a cash payment without an amount, because the fee is set server-side', () => {
    service.logCashPayment({ memberId: 'member-1', paymentDate: null }).subscribe();

    const request = controller.expectOne(`${base}/cash`);
    expect(request.request.method).toBe('POST');
    expect(Object.keys(request.request.body).sort()).toEqual(['memberId', 'paymentDate']);
    request.flush({ success: true, data: {}, message: null, errors: [] });
  });

  it('confirms a checkout by posting only the session id', () => {
    // The client cannot record a payment; it can only hand over an id the server verifies
    // with Stripe before writing anything.
    service.confirm('cs_test_123').subscribe();

    const request = controller.expectOne(`${stripe}/confirm`);
    expect(request.request.body).toEqual({ sessionId: 'cs_test_123' });
    request.flush({ success: true, data: {}, message: null, errors: [] });
  });

  it('reports a rejected session as an error rather than a payment', () => {
    let failure: unknown;

    service.confirm('cs_forged').subscribe({ error: (error: unknown) => (failure = error) });

    controller
      .expectOne(`${stripe}/confirm`)
      .flush(
        { success: false, data: null, message: 'That session was not paid.', errors: [] },
        { status: 400, statusText: 'Bad Request' },
      );

    expect(failure).toBeTruthy();
  });
});
