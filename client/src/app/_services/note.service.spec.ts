import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { NoteService } from './note.service';
import { environment } from '../../environments/environment';
import { Note } from '../_models/note.model';
import { NotePriority } from '../_models/enums';

function note(id: number, priority: NotePriority, title: string): Note {
  return {
    id,
    title,
    content: 'body',
    createdAt: '2026-05-01T10:00:00Z',
    priority,
    fromMemberId: 'coach-1',
    fromMemberFullName: 'Head Coach',
    toMemberId: 'member-1',
    toMemberFullName: 'Marko Ilic',
    trainingId: null,
    trainingDescription: null,
  };
}

describe('NoteService', () => {
  let service: NoteService;
  let controller: HttpTestingController;

  const base = `${environment.apiUrl}/notes`;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(NoteService);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    controller.verify();
    localStorage.clear();
  });

  it('searches on the server rather than filtering what it already has', () => {
    service.getClubNotes('grading').subscribe();

    const request = controller.expectOne((r) => r.url === base);
    expect(request.request.params.get('search')).toBe('grading');
    request.flush({ success: true, data: [], message: null, errors: [] });
  });

  it('sends no search parameter for an empty term', () => {
    service.getClubNotes('').subscribe();

    const request = controller.expectOne((r) => r.url === base);
    expect(request.request.params.has('search')).toBe(false);
    request.flush({ success: true, data: [], message: null, errors: [] });
  });

  it('preserves the High-first order the server sends', () => {
    // SPEC 6.8 asks for High first. Re-sorting client-side by date would bury the urgent
    // ones, so the service must hand the array back untouched.
    const ordered = [
      note(1, NotePriority.High, 'Injury'),
      note(2, NotePriority.Medium, 'Grading'),
      note(3, NotePriority.Low, 'Kit'),
    ];

    let received: Note[] | undefined;
    service.getClubNotes().subscribe((notes) => (received = notes));

    controller
      .expectOne((r) => r.url === base)
      .flush({ success: true, data: ordered, message: null, errors: [] });

    expect(received?.map((n) => n.id)).toEqual([1, 2, 3]);
  });

  it('creates a note with no author field, so it cannot be attributed to someone else', () => {
    service
      .create({
        title: 'Good session',
        content: 'Sharp today.',
        priority: NotePriority.Low,
        toMemberId: 'member-1',
        trainingId: 4,
      })
      .subscribe();

    const request = controller.expectOne(base);
    expect(Object.keys(request.request.body).sort()).toEqual([
      'content',
      'priority',
      'title',
      'toMemberId',
      'trainingId',
    ]);
    request.flush({
      success: true,
      data: note(9, NotePriority.Low, 'Good session'),
      message: null,
      errors: [],
    });
  });

  it('reads the notes for one member at one training', () => {
    service.getForTraining(4, 'member-1').subscribe();

    controller
      .expectOne((r) => r.url === `${base}/training/4/member/member-1`)
      .flush({ success: true, data: [], message: null, errors: [] });
  });
});
