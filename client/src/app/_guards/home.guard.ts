import { inject } from '@angular/core';
import { CanMatchFn } from '@angular/router';
import { AuthService } from '../_services/auth.service';

/**
 * `/dashboard` is the landing page for both roles, but they are not the same page: a coach
 * gets the club (SPEC 6.2), a member gets their own home (SPEC section 5, "own home page
 * only"). This decides which of the two route definitions matches.
 *
 * `canMatch` rather than a redirect or a switching component: when it returns false the
 * router simply tries the next route, so only the component that is actually shown is ever
 * downloaded — the member never pays for the club dashboard's chunk.
 */
export const coachHomeMatch: CanMatchFn = () => inject(AuthService).isCoach();
