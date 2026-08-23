import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../_services/auth.service';

/**
 * Coach-only routes (SPEC section 5): the member list, trainings, the club-wide payments
 * and notes pages.
 *
 * Like `authGuard`, this only decides what to render. A member who forced their way past
 * it would reach a screen whose every request the API refuses.
 */
export const coachGuard: CanActivateFn = (route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!auth.isAuthenticated()) {
    auth.logout(null);
    return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
  }

  if (auth.isCoach()) {
    return true;
  }

  // Send them somewhere they belong rather than to a dead end.
  return router.createUrlTree(['/dashboard']);
};
