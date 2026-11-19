import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const token = auth.token();
  const securedRequest = token ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request;

  return next(securedRequest).pipe(
    catchError((error: HttpErrorResponse) => {
      // HU-45: si el servidor rechaza el token (por ejemplo, se cerró sesión en otra
      // pestaña), se cierra la sesión aquí y en las demás pestañas.
      if (token && error.status === 401 && !request.url.endsWith('/api/auth/logout')) {
        auth.endSession();
        void router.navigate(['/login'], { queryParams: { session: 'changed' }, replaceUrl: true });
      }
      return throwError(() => error);
    })
  );
};
