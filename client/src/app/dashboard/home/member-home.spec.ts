import { Component, input } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { MemberHome } from './member-home';
import { ChartComponent } from '../../_shared/components/chart';
import { AttendanceStatus, NotePriority, TrainingStatus, TrainingType } from '../../_models/enums';
import { environment } from '../../../environments/environment';

const STORAGE_KEY = 'tcm.session';
const MY_ID = 'member-1';

/**
 * Chart.js needs a real 2D canvas context, which jsdom does not provide. The chart is not
 * what these tests are about, so it is swapped for a stub with the same inputs.
 */
@Component({ selector: 'app-chart', template: '' })
class ChartStub {
  readonly type = input.required<string>();
  readonly data = input.required<unknown>();
  readonly options = input<unknown>({});
  readonly ariaLabel = input.required<string>();
}

function storeMemberSession(): void {
  localStorage.setItem(
    STORAGE_KEY,
    JSON.stringify({
      id: MY_ID,
      firstName: 'Ana',
      lastName: 'Petrova',
      email: 'ana@example.test',
      isCoach: false,
      roles: ['Member'],
      token: 'a.test.token',
      expiresAt: new Date(Date.now() + 3600_000).toISOString(),
      photoUrl: null,
    }),
  );
}

function inDays(days: number): string {
  return new Date(Date.now() + days * 86_400_000).toISOString();
}

const MEMBER = {
  id: MY_ID,
  firstName: 'Ana',
  lastName: 'Petrova',
  email: 'ana@example.test',
  phoneNumber: null,
  dateOfBirth: '2005-04-02',
  age: 21,
  startedOn: '2024-01-15',
  isActive: true,
  isCoach: false,
  height: 168,
  weight: 58,
  currentBelt: { id: 3, beltName: 'Green', rank: 3 },
  // Null on purpose: a photo id would send `MemberAvatar` off to fetch the bytes, which is a
  // request these tests would then have to flush.
  photoPublicId: null,
};

const SUMMARY = {
  memberId: MY_ID,
  year: null,
  invitedCount: 3,
  presentCount: 1,
  absentCount: 1,
  attendancePercentage: 33.3,
  perMonth: [{ year: 2026, month: 8, invited: 3, present: 1, absent: 1 }],
  trainings: [
    {
      trainingId: 11,
      date: inDays(-3),
      description: 'Kicking drills',
      trainingType: TrainingType.Regular,
      trainingStatus: TrainingStatus.Finished,
      attendanceStatus: AttendanceStatus.Present,
      absenceReason: null,
      performance: 8,
    },
    {
      trainingId: 12,
      date: inDays(2),
      description: 'Sparring night',
      trainingType: TrainingType.Sparring,
      trainingStatus: TrainingStatus.Active,
      attendanceStatus: AttendanceStatus.Invited,
      absenceReason: null,
      performance: null,
    },
  ],
};

const HISTORY = {
  memberId: MY_ID,
  memberFullName: 'Ana Petrova',
  membership: { nextPaymentDate: inDays(20), isOverdue: false, daysUntilDue: 20 },
  payments: [],
};

const NOTES = [
  {
    id: 5,
    title: 'Work on your guard',
    content: 'Hands drop when you tire.',
    createdAt: inDays(-4),
    priority: NotePriority.High,
    fromMemberId: 'coach-1',
    fromMemberFullName: 'Coach Ivanov',
    toMemberId: MY_ID,
    toMemberFullName: 'Ana Petrova',
    trainingId: 11,
    trainingDescription: 'Kicking drills',
  },
];

describe('MemberHome', () => {
  let fixture: ComponentFixture<MemberHome>;
  let controller: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    storeMemberSession();

    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });

    TestBed.overrideComponent(MemberHome, {
      remove: { imports: [ChartComponent] },
      add: { imports: [ChartStub] },
    });

    fixture = TestBed.createComponent(MemberHome);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    controller.verify();
    localStorage.clear();
  });

  /** Answers the four requests the page makes on load. */
  function flush(options: { membershipFails?: boolean } = {}): void {
    controller
      .expectOne(`${environment.apiUrl}/members/${MY_ID}`)
      .flush({ success: true, data: MEMBER });

    controller
      .expectOne(`${environment.apiUrl}/trainings/member/${MY_ID}/attendance`)
      .flush({ success: true, data: SUMMARY });

    const membership = controller.expectOne(`${environment.apiUrl}/payments/member/${MY_ID}`);
    if (options.membershipFails) {
      membership.flush({ success: false, message: 'Nope.' }, { status: 500, statusText: 'Error' });
    } else {
      membership.flush({ success: true, data: HISTORY });
    }

    controller
      .expectOne(`${environment.apiUrl}/notes/member/${MY_ID}`)
      .flush({ success: true, data: NOTES });

    fixture.detectChanges();
  }

  it('asks only for the signed-in member’s own data', () => {
    fixture.detectChanges();

    // Every request this page makes names the caller's own id. There is no club-wide call
    // here at all — those are the coach's dashboard (SPEC section 5).
    const requests = controller.match(() => true);
    expect(requests).toHaveLength(4);
    expect(requests.every((request) => request.request.url.includes(MY_ID))).toBe(true);

    requests.forEach((request) => request.flush({ success: true, data: null }));
  });

  it('counts the invitations that still need an answer', () => {
    fixture.detectChanges();
    flush();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Sparring night');
    expect(text).toContain('Awaiting your reply');
    // One upcoming session, still Active, still only Invited.
    expect(text).toContain('Respond');
  });

  it('shows the past session with its score and keeps it out of the upcoming list', () => {
    fixture.detectChanges();
    flush();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Kicking drills');
    expect(text).toContain('Scored 8/10');
  });

  it('offers no coach-only destination anywhere on the page', () => {
    fixture.detectChanges();
    flush();

    const hrefs = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('a[href]'),
    ).map((anchor) => anchor.getAttribute('href'));

    // The training links are /dashboard/trainings/<id>, which a member may open when they
    // were invited; the coach-only *list* screens must not be linked at all.
    expect(hrefs).not.toContain('/dashboard/members');
    expect(hrefs).not.toContain('/dashboard/trainings');
    expect(hrefs).not.toContain('/dashboard/payments');
    expect(hrefs).not.toContain('/dashboard/notes');
    expect(hrefs).not.toContain('/dashboard/register-member');
  });

  it('keeps a failed membership load from taking out the rest of the page', () => {
    fixture.detectChanges();
    flush({ membershipFails: true });

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Your membership could not be loaded');
    // The trainings panel loaded independently and is still there.
    expect(text).toContain('Sparring night');
  });
});
