import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, map, of, tap } from 'rxjs';

export const API_URL = 'http://localhost:5100/v1/api';
export interface SessionUser { id: string; name: string; role: 'Charterer' | 'Shipowner' | 'Admin'; companyId?: number; companyName: string; }
interface DemoSession { accessToken: string; user: SessionUser; expiresAt: string; }

@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly key = 'oceanfixture.localDemoToken';
  private accessToken = this.readToken();
  readonly user = signal<SessionUser | null>(null);
  get token(): string | null { return this.accessToken; }
  get canCreate(): boolean { return this.user()?.role === 'Charterer' || this.user()?.role === 'Admin'; }

  accounts(): Observable<SessionUser[]> { return this.http.get<SessionUser[]>(`${API_URL}/demo-session/accounts`); }
  signIn(accountId: string): Observable<SessionUser> {
    return this.http.post<DemoSession>(`${API_URL}/demo-session/login`, { accountId }).pipe(tap(session => {
      this.accessToken = session.accessToken;
      try { sessionStorage.setItem(this.key, session.accessToken); } catch { /* Memory-only if browser storage is unavailable. */ }
      this.user.set(session.user);
    }), map(session => session.user));
  }
  ensure(): Observable<SessionUser | null> {
    if (!this.token) { this.user.set(null); return of(null); }
    if (this.user()) return of(this.user());
    return this.http.get<SessionUser>(`${API_URL}/demo-session/me`).pipe(tap(user => this.user.set(user)),
      catchError(() => { this.clear(); return of(null); }));
  }
  signOut(): void {
    this.http.post(`${API_URL}/demo-session/logout`, {}).pipe(catchError(() => of(null)),
      finalize(() => { this.clear(); void this.router.navigate(['/login']); })).subscribe();
  }
  clear(): void {
    this.accessToken = null; this.user.set(null);
    try { sessionStorage.removeItem(this.key); } catch { /* No persisted token. */ }
  }
  private readToken(): string | null { try { return sessionStorage.getItem(this.key); } catch { return null; } }
}
