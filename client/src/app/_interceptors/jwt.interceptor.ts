import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../_services/auth.service';
import { environment } from '../../environments/environment';

/**
 * Attaches the bearer token to requests going to our own API (SPEC section 7).
 *
 * The origin check matters: without it, any third-party URL the app ever fetches would
 * receive the token in an Authorization header.
 */
export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const token = auth.token;

  if (!token || !isOurApi(req.url)) {
    return next(req);
  }

  return next(
    req.clone({
      setHeaders: { Authorization: `Bearer ${token}` },
    }),
  );
};

function isOurApi(url: string): boolean {
  const apiUrl = environment.apiUrl;

  // A relative apiUrl ("/api") means same-origin, so a relative request URL is ours.
  if (apiUrl.startsWith('/')) {
    return url.startsWith(apiUrl) || !/^https?:\/\//i.test(url);
  }

  return url.startsWith(apiUrl);
}
