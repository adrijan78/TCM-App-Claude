import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../_services/auth.service';
import { apiErrorMessage } from '../_services/unwrap';

/**
 * The endpoints a signed-out visitor is meant to call. A 401 from one of these is a rejected
 * credential or a spent reset token — not an expired session — so it must not clear storage
 * or bounce the user to a login page they are already standing on. The screen shows it
 * inline instead.
 *
 * `/account/register` is absent deliberately: it is coach-authenticated, so a 401 there
 * really does mean the coach's session has gone.
 */
const ANONYMOUS_ENDPOINTS = [
  '/account/login',
  '/account/forgot-password',
  '/account/reset-password',
];

/**
 * One place that turns an HTTP failure into something a person can read, so no component
 * has to. The error is still rethrown, because a component may need to know its request
 * failed even after the message has been shown.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const auth = inject(AuthService);
  const snackBar = inject(MatSnackBar);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      switch (error.status) {
        case 0:
          notify(snackBar, apiErrorMessage(error));
          break;

        case 401:
          if (ANONYMOUS_ENDPOINTS.some((path) => req.url.includes(path))) {
            break;
          }

          // The token is gone, expired, or was never valid. Clear it and send them to sign in
          // again, remembering where they were so they land back there.
          auth.logout(null);
          void router.navigate(['/login'], {
            queryParams: { returnUrl: router.url },
          });
          notify(snackBar, 'Your session has ended. Please sign in again.');
          break;

        case 403:
          notify(snackBar, 'You are not permitted to do that.');
          break;

        case 400:
        case 404:
          // Left to the component: a rejected form or a missing record is part of a screen's
          // own story, not a global banner.
          break;

        default:
          notify(snackBar, apiErrorMessage(error));
          break;
      }

      return throwError(() => error);
    }),
  );
};

function notify(snackBar: MatSnackBar, message: string): void {
  snackBar.open(message, 'Dismiss', { duration: 6000 });
}
