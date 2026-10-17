import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { SessionService, API_URL } from './session.service';
import { sessionInterceptor } from './session.interceptor';

describe('Session interceptor', () => {
  let http: HttpTestingController;
  let client: HttpClient;
  let clear: jasmine.Spy;
  beforeEach(() => {
    clear = jasmine.createSpy('clear');
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(withInterceptors([sessionInterceptor])),
      provideHttpClientTesting(), { provide: SessionService, useValue: { token: 'synthetic-token', clear } }] });
    client = TestBed.inject(HttpClient); http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  it('adds the token only to the exact application API origin and path', () => {
    client.get(`${API_URL}/offers`).subscribe();
    const own = http.expectOne(`${API_URL}/offers`);
    expect(own.request.headers.get('Authorization')).toBe('Bearer synthetic-token'); own.flush([]);
    client.get('https://tile.openstreetmap.org/3/1/2.png').subscribe();
    const external = http.expectOne('https://tile.openstreetmap.org/3/1/2.png');
    expect(external.request.headers.has('Authorization')).toBeFalse(); external.flush({});
  });
  it('clears an expired session and returns to profile selection', () => {
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    client.get(`${API_URL}/offers`).subscribe({ error: () => undefined });
    http.expectOne(`${API_URL}/offers`).flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(clear).toHaveBeenCalledTimes(1);
    expect(navigate).toHaveBeenCalledWith(['/login']);
  });
});
