import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { API_URL, SessionService, SessionUser } from './session.service';

describe('SessionService', () => {
  let session: SessionService;
  let http: HttpTestingController;
  const user: SessionUser = { id: 'shipowner', name: 'Demo owner', role: 'Shipowner', companyId: 119, companyName: 'Owner' };
  beforeEach(() => {
    sessionStorage.removeItem('oceanfixture.localDemoToken');
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    session = TestBed.inject(SessionService); http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => { http.verify(); session.clear(); });

  it('uses the role returned by the server and preserves only the opaque token', () => {
    session.signIn('shipowner').subscribe();
    const request = http.expectOne(`${API_URL}/demo-session/login`);
    expect(request.request.body).toEqual({ accountId: 'shipowner' });
    request.flush({ accessToken: 'synthetic-test-token', user, expiresAt: '' });
    expect(session.user()).toEqual(user);
    expect(session.canCreate).toBeFalse();
    expect(sessionStorage.getItem('oceanfixture.localDemoToken')).toBe('synthetic-test-token');
  });

  it('does not invent a session when no token is present', () => {
    session.ensure().subscribe(value => expect(value).toBeNull());
    expect(session.user()).toBeNull();
    http.expectNone(`${API_URL}/demo-session/me`);
  });

  it('verifies a restored token with the API before restoring the role', () => {
    sessionStorage.setItem('oceanfixture.localDemoToken', 'restored-synthetic-token');
    const restored = TestBed.runInInjectionContext(() => new SessionService());
    expect(restored.user()).toBeNull();
    restored.ensure().subscribe();
    http.expectOne(`${API_URL}/demo-session/me`).flush(user);
    expect(restored.user()).toEqual(user);
  });

  it('clears both the token and user', () => {
    session.signIn('shipowner').subscribe();
    http.expectOne(`${API_URL}/demo-session/login`).flush({ accessToken: 'synthetic-test-token', user });
    session.clear();
    expect(session.token).toBeNull();
    expect(session.user()).toBeNull();
    expect(sessionStorage.getItem('oceanfixture.localDemoToken')).toBeNull();
  });
});
