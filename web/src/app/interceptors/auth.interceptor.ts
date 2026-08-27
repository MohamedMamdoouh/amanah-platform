import { HttpInterceptorFn } from '@angular/common/http';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  // TODO sub-phase 11: attach Authorization header from AuthService
  return next(req);
};
