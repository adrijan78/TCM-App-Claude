import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../_services/auth.service';

/**
 * The mirror of `authGuard`: keeps someone who is already signed in off the login and
 * password-reset screens, which would otherwise offer to authenticate them a second time
 * and quietly replace their session.
 *
 * Honours `returnUrl` so a session that outlived a bookmarked login link still lands where
 * the link was pointing.
 */
export const guestGuard: CanActivateFn = (route) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!auth.isAuthenticated()) {
    return true;
  }

  const returnUrl = route.queryParamMap.get('returnUrl');
  return router.parseUrl(returnUrl && returnUrl.startsWith('/') ? returnUrl : '/dashboard');
};
