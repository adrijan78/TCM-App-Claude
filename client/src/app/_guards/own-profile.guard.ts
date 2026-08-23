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

/**
 * `/dashboard/members/:id` is not coach-only — the note-notification email links a member
 * straight to their own profile — but a member has no business at anyone else's id.
 *
 * Without this the screen would render and then fill with 403s, which reads as a broken page
 * rather than as a boundary. Sending them to their own profile is the honest answer. As
 * always this is UX: `MemberService.ResolveAsync` refuses the underlying calls either way
 * (SPEC section 5).
 */
export const profileAccessGuard: CanActivateFn = (route) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isCoach()) {
    return true;
  }

  const myId = auth.currentUser()?.id;
  if (!myId) {
    return router.createUrlTree(['/login']);
  }

  return route.paramMap.get('id') === myId
    ? true
    : router.createUrlTree(['/dashboard/members', myId]);
};
