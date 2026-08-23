import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../_services/auth.service';

/**
 * Keeps signed-out visitors out of the dashboard and remembers where they were headed.
 *
 * This is UX, not security. The server authorizes every request independently; a guard
 * that were bypassed would only reveal an empty screen full of 401s (SPEC section 7).
 */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isAuthenticated()) {
    return true;
  }

  // Drop a stale session rather than leaving the shell half-signed-in.
  auth.logout(null);

  return router.createUrlTree(['/login'], {
    queryParams: { returnUrl: state.url },
  });
};
