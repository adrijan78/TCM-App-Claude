import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../_services/auth.service';

/**
 * Turns `/dashboard/profile` into `/dashboard/members/<my id>`.
 *
 * "My profile" and "a member's profile" are the same screen (SPEC 6.4) looked at from two
 * directions, so redirecting is better than a second component that drifts out of step with
 * the first. The member profile then works for both without knowing which route it came
 * from — the id is just an input.
 */
export const ownProfileRedirect: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const id = auth.currentUser()?.id;

  // No session should be impossible here — authGuard runs on the parent — but a redirect
  // to login beats rendering a profile screen with an undefined id.
  return id ? router.createUrlTree(['/dashboard/members', id]) : router.createUrlTree(['/login']);
};
