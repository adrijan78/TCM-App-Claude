import { Routes } from '@angular/router';
import { authGuard } from './_guards/auth.guard';
import { coachGuard } from './_guards/coach.guard';
import { guestGuard } from './_guards/guest.guard';
import { coachHomeMatch } from './_guards/home.guard';
import { ownProfileRedirect, profileAccessGuard } from './_guards/own-profile.guard';

/**
 * SPEC section 3.3. Everything under /dashboard is lazy-loaded and sits behind `authGuard`;
 * the coach-only areas of SPEC section 5 add `coachGuard`.
 *
 * The email deep links the API sends must keep working, so these two paths are fixed:
 *   /dashboard/trainings/:id   — training invitation (TrainingService.BuildTrainingLink)
 *   /dashboard/members/:id     — note notification   (NoteService.BuildProfileLink)
 *
 */
export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'dashboard',
  },

  // ---- Authentication (SPEC section 6.1) ----------------------------------------------------
  // These render outside `Shell`: there is no navigation to offer someone who is not signed
  // in. `guestGuard` keeps an existing session from being replaced by a second sign-in.
  {
    path: 'login',
    title: 'Sign in',
    canActivate: [guestGuard],
    loadComponent: () => import('./login/login').then((m) => m.Login),
  },
  {
    path: 'forgot-password',
    title: 'Forgot password',
    canActivate: [guestGuard],
    loadComponent: () => import('./forgot-password/forgot-password').then((m) => m.ForgotPassword),
  },
  {
    // `email` and `token` come in as query parameters and reach the component as inputs.
    path: 'reset-password',
    title: 'Choose a new password',
    canActivate: [guestGuard],
    loadComponent: () => import('./reset-password/reset-password').then((m) => m.ResetPassword),
  },

  // ---- The application shell ----------------------------------------------------------------
  {
    path: 'dashboard',
    canActivate: [authGuard],
    loadComponent: () => import('./_shared/layout/shell').then((m) => m.Shell),
    children: [
      // Two home pages behind one path (SPEC section 5): the club for a coach, their own
      // for a member. `coachHomeMatch` picks; the member's is the fallback, so a session
      // whose role cannot be read still lands somewhere that shows only their own data.
      {
        path: '',
        pathMatch: 'full',
        title: 'Home',
        canMatch: [coachHomeMatch],
        loadComponent: () =>
          import('./dashboard/club-details/club-details').then((m) => m.ClubDetails),
      },
      {
        path: '',
        pathMatch: 'full',
        title: 'Home',
        loadComponent: () => import('./dashboard/home/member-home').then((m) => m.MemberHome),
      },
      {
        // "My profile" is the same screen as a member profile, resolved to the signed-in
        // user. `ownProfileRedirect` swaps in their own id so there is one component, not two.
        path: 'profile',
        title: 'My profile',
        canActivate: [ownProfileRedirect],
        loadComponent: () =>
          import('./dashboard/members/member-profile').then((m) => m.MemberProfile),
      },

      // Coach-only areas (SPEC section 5).
      {
        path: 'members',
        canActivate: [coachGuard],
        title: 'Members',
        loadComponent: () => import('./dashboard/members/members').then((m) => m.Members),
      },
      {
        // Reached from the note-notification email. A member may open their own profile here,
        // so this one is not coach-gated — `profileAccessGuard` lets a coach through to
        // anyone and a member only to themselves, and the API decides regardless.
        path: 'members/:id',
        title: 'Member profile',
        canActivate: [profileAccessGuard],
        loadComponent: () =>
          import('./dashboard/members/member-profile').then((m) => m.MemberProfile),
      },
      {
        path: 'trainings',
        canActivate: [coachGuard],
        title: 'Trainings',
        loadComponent: () => import('./dashboard/trainings/trainings').then((m) => m.Trainings),
      },
      {
        // Reached from the training-invitation email, by an invited member who is not a coach.
        path: 'trainings/:id',
        title: 'Training details',
        loadComponent: () =>
          import('./dashboard/trainings/training-details').then((m) => m.TrainingDetailsScreen),
      },
      {
        path: 'payments',
        canActivate: [coachGuard],
        title: 'Payments',
        loadComponent: () => import('./dashboard/payments/payments').then((m) => m.Payments),
      },
      {
        path: 'notes',
        canActivate: [coachGuard],
        title: 'Notes',
        loadComponent: () => import('./dashboard/notes/notes').then((m) => m.Notes),
      },
      {
        path: 'register-member',
        canActivate: [coachGuard],
        title: 'Register a member',
        loadComponent: () =>
          import('./dashboard/register-member/register-member').then((m) => m.RegisterMember),
      },
    ],
  },

  // ---- Stripe return landings (SPEC section 3.2) --------------------------------------------
  // Behind `authGuard`: confirming a session needs the member's token. These two paths are
  // fixed — they are configured server-side as `Stripe:SuccessUrl` / `Stripe:CancelUrl`.
  // `outcome` reaches the component as an input via `withComponentInputBinding`.
  {
    path: 'successful-payment',
    canActivate: [authGuard],
    title: 'Payment complete',
    loadComponent: () => import('./dashboard/payments/payment-return').then((m) => m.PaymentReturn),
    data: { outcome: 'success' },
  },
  {
    path: 'failed-payment',
    canActivate: [authGuard],
    title: 'Payment cancelled',
    loadComponent: () => import('./dashboard/payments/payment-return').then((m) => m.PaymentReturn),
    data: { outcome: 'cancelled' },
  },

  {
    path: '**',
    title: 'Page not found',
    loadComponent: () => import('./not-found/not-found').then((m) => m.NotFound),
  },
];
