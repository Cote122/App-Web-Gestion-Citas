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
      if (token && error.status === 401) {
        auth.logout();
        void router.navigate(['/login'], { queryParams: { session: 'changed' } });
      }
      return throwError(() => error);
    })
  );
};
