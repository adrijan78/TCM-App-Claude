import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { jwtInterceptor } from './jwt.interceptor';
import { AuthService } from '../_services/auth.service';
import { environment } from '../../environments/environment';

describe('jwtInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;
  let auth: AuthService;

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([jwtInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
  });

  afterEach(() => {
    controller.verify();
    localStorage.clear();
  });

  function signIn(token = 'a.test.token'): void {
    // Reaching through the service's own storage keeps the test to one public seam.
    localStorage.setItem(
      'tcm.session',
      JSON.stringify({
        id: 'member-1',
        firstName: 'Ana',
        lastName: 'Petrova',
        email: 'ana@example.test',
        isCoach: false,
        roles: ['Member'],
        token,
        expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
        photoUrl: null,
      }),
    );

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([jwtInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
  }

  it('attaches the bearer token to API requests', () => {
    signIn('signed.in.token');

    http.get(`${environment.apiUrl}/members`).subscribe();

    const request = controller.expectOne(`${environment.apiUrl}/members`);
    expect(request.request.headers.get('Authorization')).toBe('Bearer signed.in.token');
  });

  it('sends no Authorization header when signed out', () => {
    expect(auth.token).toBeNull();

    http.get(`${environment.apiUrl}/common/belts`).subscribe();

    const request = controller.expectOne(`${environment.apiUrl}/common/belts`);
    expect(request.request.headers.has('Authorization')).toBe(false);
  });

  it('never leaks the token to a third-party origin', () => {
    // Without the origin check, any outbound URL would receive the credential.
    signIn('secret.token.value');

    http.get('https://someone-elses-api.example.com/collect').subscribe();

    const request = controller.expectOne('https://someone-elses-api.example.com/collect');
    expect(request.request.headers.has('Authorization')).toBe(false);
  });
});
