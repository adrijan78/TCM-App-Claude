import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { TrainingService } from './training.service';
import { environment } from '../../environments/environment';
import { AttendanceStatus, TrainingStatus, TrainingType } from '../_models/enums';

describe('TrainingService', () => {
  let service: TrainingService;
  let controller: HttpTestingController;

  const base = `${environment.apiUrl}/trainings`;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(TrainingService);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    controller.verify();
    localStorage.clear();
  });

  it('sends the Active status and Regular type even though both are 0', () => {
    // TrainingStatus.Active === 0 and TrainingType.Regular === 0. Filtering on either with a
    // truthiness test would return everything instead.
    service.getTrainings({ status: TrainingStatus.Active, type: TrainingType.Regular }).subscribe();

    const request = controller.expectOne((r) => r.url === base);
    expect(request.request.params.get('status')).toBe('0');
    expect(request.request.params.get('type')).toBe('0');
    request.flush({ success: true, data: [], message: null, errors: [] });
  });

  it('asks the calendar endpoint for one month', () => {
    service.getCalendar(2026, 5).subscribe();

    const request = controller.expectOne((r) => r.url === `${base}/calendar`);
    expect(request.request.params.get('year')).toBe('2026');
    expect(request.request.params.get('month')).toBe('5');
    request.flush({ success: true, data: [], message: null, errors: [] });
  });

  it('reports own attendance with a null memberId', () => {
    // Omitting the id is the only shape a member may send: naming yourself explicitly needs
    // coach rights on the server.
    service
      .reportAttendance(7, {
        memberId: null,
        status: AttendanceStatus.Present,
        absenceReason: null,
      })
      .subscribe();

    const request = controller.expectOne(`${base}/7/attendance`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      memberId: null,
      status: AttendanceStatus.Present,
      absenceReason: null,
    });
    request.flush({ success: true, data: {}, message: null, errors: [] });
  });

  it('scores a member on the coach-only performance route', () => {
    service.setPerformance(7, 'member-2', { performance: 8 }).subscribe();

    const request = controller.expectOne(`${base}/7/attendance/member-2/performance`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ performance: 8 });
    request.flush({ success: true, data: {}, message: null, errors: [] });
  });

  it('sends the whole invitee list on update, because it replaces rather than merges', () => {
    service
      .update(7, {
        description: 'Sparring',
        date: '2026-05-04T17:00:00.000Z',
        trainingType: TrainingType.Sparring,
        status: TrainingStatus.Active,
        memberIds: ['a', 'b'],
      })
      .subscribe();

    const request = controller.expectOne(`${base}/7`);
    expect(request.request.body.memberIds).toEqual(['a', 'b']);
    request.flush({ success: true, data: {}, message: null, errors: [] });
  });

  it('omits the year when asking for a whole history', () => {
    service.getMemberAttendance('member-1').subscribe();

    const request = controller.expectOne((r) => r.url === `${base}/member/member-1/attendance`);
    expect(request.request.params.has('year')).toBe(false);
    request.flush({ success: true, data: {}, message: null, errors: [] });
  });
});
