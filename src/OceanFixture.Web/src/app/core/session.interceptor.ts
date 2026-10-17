import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { API_URL, SessionService } from './session.service';

export const sessionInterceptor: HttpInterceptorFn = (request, next) => {
  // Never send the session token to external APIs or map providers.
  if (!request.url.startsWith(`${API_URL}/`)) return next(request);
  const session = inject(SessionService);
  const router = inject(Router);
  const authenticated = session.token ? request.clone({ setHeaders: { Authorization: `Bearer ${session.token}` } }) : request;
  return next(authenticated).pipe(catchError(error => {
    if (error.status === 401 && !request.url.includes('/demo-session/login')) {
      session.clear(); void router.navigate(['/login']);
    }
    return throwError(() => error);
  }));
};
