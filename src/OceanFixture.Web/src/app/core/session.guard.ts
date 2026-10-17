import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { SessionService } from './session.service';

export const sessionGuard: CanActivateFn = route => {
  const session = inject(SessionService);
  const router = inject(Router);
  return session.ensure().pipe(map(user => {
    if (!user) return router.createUrlTree(['/login']);
    const roles = route.data['roles'] as string[] | undefined;
    return !roles || roles.includes(user.role) ? true : router.createUrlTree(['/home']);
  }));
};
