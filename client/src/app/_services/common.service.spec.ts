import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CommonService } from './common.service';
import { environment } from '../../environments/environment';

/**
 * The caching is the point of this service. Belts and roles change about once a decade, so they
 * are fetched once per session — but roles are coach-only, so the cache must not survive a
 * sign-out into the next account.
 */
describe('CommonService', () => {
  let service: CommonService;
  let controller: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(CommonService);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('unwraps the envelope so callers get plain data', () => {
    let belts: unknown;
    service.getBelts().subscribe((result) => (belts = result));

    controller.expectOne(`${environment.apiUrl}/common/belts`).flush({
      success: true,
      data: [{ id: 1, beltName: 'White', rank: 1 }],
      message: null,
      errors: [],
    });

    expect(belts).toEqual([{ id: 1, beltName: 'White', rank: 1 }]);
  });

  it('fetches the belt ladder once and replays it', () => {
    service.getBelts().subscribe();
    controller.expectOne(`${environment.apiUrl}/common/belts`).flush({
      success: true,
      data: [{ id: 1, beltName: 'White', rank: 1 }],
      message: null,
      errors: [],
    });

    let second: unknown;
    service.getBelts().subscribe((result) => (second = result));

    // No second request: expectNone would fail if one had gone out.
    controller.expectNone(`${environment.apiUrl}/common/belts`);
    expect(second).toEqual([{ id: 1, beltName: 'White', rank: 1 }]);
  });

  it('fetches roles once and replays them', () => {
    service.getRoles().subscribe();
    controller.expectOne(`${environment.apiUrl}/roles`).flush({
      success: true,
      data: [{ id: '1', name: 'Coach' }],
      message: null,
      errors: [],
    });

    service.getRoles().subscribe();
    controller.expectNone(`${environment.apiUrl}/roles`);
  });

  it('re-fetches after clearCache, so the next account does not inherit the previous one', () => {
    service.getRoles().subscribe();
    controller.expectOne(`${environment.apiUrl}/roles`).flush({
      success: true,
      data: [{ id: '1', name: 'Coach' }],
      message: null,
      errors: [],
    });

    service.clearCache();

    service.getRoles().subscribe();
    controller.expectOne(`${environment.apiUrl}/roles`).flush({
      success: true,
      data: [],
      message: null,
      errors: [],
    });
  });

  it('clears belts as well as roles', () => {
    service.getBelts().subscribe();
    controller.expectOne(`${environment.apiUrl}/common/belts`).flush({
      success: true,
      data: [],
      message: null,
      errors: [],
    });

    service.clearCache();

    service.getBelts().subscribe();
    controller.expectOne(`${environment.apiUrl}/common/belts`).flush({
      success: true,
      data: [],
      message: null,
      errors: [],
    });
  });

  it('sends year and month as query parameters when given', () => {
    service.getClubNumbers(2026, 3).subscribe();

    const request = controller.expectOne(
      (req) => req.url === `${environment.apiUrl}/common/club-numbers`,
    );

    expect(request.request.params.get('year')).toBe('2026');
    expect(request.request.params.get('month')).toBe('3');
    request.flush({ success: true, data: {}, message: null, errors: [] });
  });

  it('omits absent filters rather than sending empty ones', () => {
    service.getClubNumbers(null, null).subscribe();

    const request = controller.expectOne(
      (req) => req.url === `${environment.apiUrl}/common/club-numbers`,
    );

    expect(request.request.params.has('year')).toBe(false);
    expect(request.request.params.has('month')).toBe(false);
    request.flush({ success: true, data: {}, message: null, errors: [] });
  });

  it('is not cached, because the club figures change constantly', () => {
    service.getClubNumbers().subscribe();
    controller
      .expectOne((req) => req.url === `${environment.apiUrl}/common/club-numbers`)
      .flush({ success: true, data: {}, message: null, errors: [] });

    service.getClubNumbers().subscribe();
    controller
      .expectOne((req) => req.url === `${environment.apiUrl}/common/club-numbers`)
      .flush({ success: true, data: {}, message: null, errors: [] });
  });

  it('turns a success:false 200 into an error', () => {
    let failure: Error | undefined;
    service.getBelts().subscribe({ error: (error: Error) => (failure = error) });

    controller.expectOne(`${environment.apiUrl}/common/belts`).flush({
      success: false,
      data: null,
      message: 'Belts are unavailable.',
      errors: [],
    });

    expect(failure?.message).toBe('Belts are unavailable.');
  });
});
