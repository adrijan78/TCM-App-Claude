import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../_services/auth.service';

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
          notify(snackBar, 'Cannot reach the server. Check your connection and try again.');
          break;

        case 401:
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

        case 404:
          // Left to the component: a missing record is usually part of a screen's own story,
          // not a global banner.
          break;

        default:
          notify(snackBar, messageFrom(error));
          break;
      }

      return throwError(() => error);
    }),
  );
};

function notify(snackBar: MatSnackBar, message: string): void {
  snackBar.open(message, 'Dismiss', { duration: 6000 });
}

/**
 * Prefers the server's own `ApiResponse` message. Never surfaces a raw exception string —
 * the API is built not to send one, and this is the second guard on that.
 */
function messageFrom(error: HttpErrorResponse): string {
  const body = error.error;

  if (body && typeof body === 'object' && typeof body.message === 'string') {
    const errors: string[] = Array.isArray(body.errors) ? body.errors : [];
    return errors.length ? `${body.message} ${errors.join(' ')}` : body.message;
  }

  return 'Something went wrong. Please try again.';
}
