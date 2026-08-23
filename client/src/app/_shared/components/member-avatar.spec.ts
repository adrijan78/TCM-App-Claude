import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { MemberAvatar } from './member-avatar';
import { environment } from '../../../environments/environment';

describe('MemberAvatar', () => {
  let fixture: ComponentFixture<MemberAvatar>;
  let controller: HttpTestingController;
  let created: string[];
  let revoked: string[];

  beforeEach(() => {
    localStorage.clear();
    created = [];
    revoked = [];

    // jsdom has no object-URL implementation, and the point of this component is that it
    // pairs every create with a revoke — so both are recorded rather than stubbed away.
    let counter = 0;
    vi.spyOn(URL, 'createObjectURL').mockImplementation(() => {
      const url = `blob:test/${++counter}`;
      created.push(url);
      return url;
    });
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation((url: string) => {
      revoked.push(url);
    });

    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });

    fixture = TestBed.createComponent(MemberAvatar);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    controller.verify();
    vi.restoreAllMocks();
    localStorage.clear();
  });

  function setUp(photoPublicId: string | null): void {
    fixture.componentRef.setInput('firstName', 'Ana');
    fixture.componentRef.setInput('lastName', 'Petrova');
    fixture.componentRef.setInput('photoPublicId', photoPublicId);
    fixture.detectChanges();
  }

  function photoUrl(id: string): string {
    return `${environment.apiUrl}/photos/${id}`;
  }

  it('shows initials and fetches nothing when there is no photo', () => {
    setUp(null);

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('AP');
    controller.expectNone(() => true);
  });

  it('fetches the bytes through HttpClient, because an img src cannot carry the token', () => {
    setUp('photo-1');

    const request = controller.expectOne(photoUrl('photo-1'));
    expect(request.request.responseType).toBe('blob');
    request.flush(new Blob(['x']));
    fixture.detectChanges();

    const img = (fixture.nativeElement as HTMLElement).querySelector('img');
    expect(img?.getAttribute('src')).toBe(created[0]);
    expect(img?.getAttribute('alt')).toBe('Ana Petrova');
  });

  it('revokes the object URL on destroy', () => {
    setUp('photo-1');
    controller.expectOne(photoUrl('photo-1')).flush(new Blob(['x']));
    fixture.detectChanges();

    fixture.destroy();

    // A list of forty photos would otherwise be forty images pinned in memory for the life
    // of the tab.
    expect(revoked).toEqual(created);
  });

  it('revokes the old URL when the photo changes', () => {
    setUp('photo-1');
    controller.expectOne(photoUrl('photo-1')).flush(new Blob(['x']));
    fixture.detectChanges();

    fixture.componentRef.setInput('photoPublicId', 'photo-2');
    fixture.detectChanges();
    controller.expectOne(photoUrl('photo-2')).flush(new Blob(['y']));
    fixture.detectChanges();

    expect(revoked).toContain(created[0]);
    expect(created).toHaveLength(2);
  });

  it('falls back to initials when the photo is missing or forbidden', () => {
    setUp('photo-gone');

    // `error`, not `flush`: the request asked for a blob, and the testing backend refuses to
    // convert a plain body into one.
    controller
      .expectOne(photoUrl('photo-gone'))
      .error(new ProgressEvent('error'), { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('img')).toBeNull();
    expect(host.textContent).toContain('AP');
  });
});
