import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { MemberService } from './member.service';
import { environment } from '../../environments/environment';
import { AgeGroup } from '../_models/enums';

describe('MemberService', () => {
  let service: MemberService;
  let controller: HttpTestingController;

  const base = `${environment.apiUrl}/members`;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(MemberService);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    controller.verify();
    localStorage.clear();
  });

  it('sends no query string when nothing is filtered', () => {
    service.getMembers().subscribe();

    const request = controller.expectOne(base);
    expect(request.request.params.keys()).toEqual([]);
    request.flush({ success: true, data: [], message: null, errors: [] });
  });

  it('sends the Kids age group even though its value is 0', () => {
    // AgeGroup.Kids === 0. A truthiness test would silently drop the filter and quietly
    // return the whole club instead of the children.
    service.getMembers({ ageGroup: AgeGroup.Kids }).subscribe();

    const request = controller.expectOne((r) => r.url === base);
    expect(request.request.params.get('ageGroup')).toBe('0');
    request.flush({ success: true, data: [], message: null, errors: [] });
  });

  it('passes search and belt through as query parameters', () => {
    service.getMembers({ search: 'marko', beltId: 3 }).subscribe();

    const request = controller.expectOne((r) => r.url === base);
    expect(request.request.params.get('search')).toBe('marko');
    expect(request.request.params.get('beltId')).toBe('3');
    request.flush({ success: true, data: [], message: null, errors: [] });
  });

  it('omits a null belt rather than sending the word null', () => {
    service.getMembers({ search: 'ana', beltId: null, ageGroup: null }).subscribe();

    const request = controller.expectOne((r) => r.url === base);
    expect(request.request.params.has('beltId')).toBe(false);
    expect(request.request.params.has('ageGroup')).toBe(false);
    request.flush({ success: true, data: [], message: null, errors: [] });
  });

  it('deactivates with PATCH, never DELETE — the row carries the member history', () => {
    service.deactivate('member-1').subscribe();

    const request = controller.expectOne(`${base}/member-1/deactivate`);
    expect(request.request.method).toBe('PATCH');
    request.flush({ success: true, data: {}, message: null, errors: [] });
  });

  it('surfaces a success:false envelope as an error', () => {
    let failure: unknown;

    service.getMember('nobody').subscribe({ error: (error: unknown) => (failure = error) });

    controller
      .expectOne(`${base}/nobody`)
      .flush({ success: false, data: null, message: 'Member not found.', errors: [] });

    expect((failure as Error).message).toBe('Member not found.');
  });

  it('posts a belt exam to the member it belongs to', () => {
    service
      .addBelt('member-1', {
        beltId: 2,
        dateReceived: '2026-05-04',
        description: null,
        isCurrentBelt: true,
      })
      .subscribe();

    const request = controller.expectOne(`${base}/member-1/belts`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body.dateReceived).toBe('2026-05-04');
    request.flush({ success: true, data: {}, message: null, errors: [] });
  });
});
