import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';

import { AuthService } from '../auth/auth.service';
import { environment } from '../../environments/environment';

function isAuthEndpoint(url: string): boolean {
  return url.includes('/auth/otp/') || url.includes('/auth/refresh');
}

function isApiRequest(url: string): boolean {
  return url.startsWith(environment.apiBaseUrl);
}

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!isApiRequest(req.url)) {
    return next(req);
  }

  const token = auth.getAccessToken();
  const authReq = req.clone({
    withCredentials: true,
    setHeaders: token ? { Authorization: `Bearer ${token}` } : {},
  });

  return next(authReq).pipe(
    catchError((error: HttpErrorResponse) => {
      const apiCode =
        error.error && typeof error.error === 'object'
          ? (error.error as { code?: string }).code
          : undefined;

      const shouldRefresh =
        error.status === 401 &&
        !isAuthEndpoint(req.url) &&
        (apiCode === 'auth.token_expired' || apiCode === undefined);

      if (!shouldRefresh) {
        return throwError(() => error);
      }

      return auth.refreshSession().pipe(
        switchMap((session) =>
          next(
            req.clone({
              withCredentials: true,
              setHeaders: { Authorization: `Bearer ${session.accessToken}` },
            }),
          ),
        ),
        catchError((refreshError) => {
          auth.clearSession();
          void router.navigate(['/login']);
          return throwError(() => refreshError);
        }),
      );
    }),
  );
};
