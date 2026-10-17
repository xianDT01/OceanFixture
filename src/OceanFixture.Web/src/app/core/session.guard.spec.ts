import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, provideRouter, RouterStateSnapshot, UrlTree } from '@angular/router';
import { firstValueFrom, Observable, of } from 'rxjs';
import { sessionGuard } from './session.guard';
import { SessionService } from './session.service';

describe('Session guard', () => {
  let ensure: jasmine.Spy;
  beforeEach(() => {
    ensure = jasmine.createSpy('ensure');
    TestBed.configureTestingModule({ providers: [provideRouter([]), { provide: SessionService, useValue: { ensure } }] });
  });
  async function check(roles?: string[]): Promise<boolean | UrlTree> {
    const result = TestBed.runInInjectionContext(() => sessionGuard({ data: { roles } } as unknown as ActivatedRouteSnapshot,
      {} as RouterStateSnapshot));
    return firstValueFrom(result as Observable<boolean | UrlTree>);
  }
  it('requires a session', async () => { ensure.and.returnValue(of(null)); expect((await check()).toString()).toBe('/login'); });
  it('blocks a shipowner from the new cargo route', async () => {
    ensure.and.returnValue(of({ role: 'Shipowner' }));
    expect((await check(['Charterer', 'Admin'])).toString()).toBe('/home');
  });
  it('allows the administrator to access permitted routes', async () => {
    ensure.and.returnValue(of({ role: 'Admin' })); expect(await check(['Charterer', 'Admin'])).toBeTrue();
  });
});
