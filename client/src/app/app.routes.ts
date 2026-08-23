import { Routes } from '@angular/router';
import { authGuard } from './_guards/auth.guard';
import { coachGuard } from './_guards/coach.guard';

/**
 * SPEC section 3.3. Everything under /dashboard is lazy-loaded and sits behind `authGuard`;
 * the coach-only areas of SPEC section 5 add `coachGuard`.
 *
 * The email deep links the API sends must keep working, so these two paths are fixed:
 *   /dashboard/trainings/:id   — training invitation (TrainingService.BuildTrainingLink)
 *   /dashboard/members/:id     — note notification   (NoteService.BuildProfileLink)
 *
 * Routes marked "phase 8" / "phase 9" point at PendingScreen until their screens are built.
 */
export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'dashboard',
  },

  // ---- Authentication (SPEC section 6.1) — screens land in phase 8 --------------------------
  {
    path: 'login',
    title: 'Sign in',
    loadComponent: () =>
      import('./_shared/components/pending-screen').then((m) => m.PendingScreen),
    data: { title: 'Sign in' },
  },
  {
    path: 'forgot-password',
    title: 'Forgot password',
    loadComponent: () =>
      import('./_shared/components/pending-screen').then((m) => m.PendingScreen),
    data: { title: 'Forgot password' },
  },
  {
    path: 'reset-password',
    title: 'Choose a new password',
    loadComponent: () =>
      import('./_shared/components/pending-screen').then((m) => m.PendingScreen),
    data: { title: 'Choose a new password' },
  },

  // ---- The application shell ----------------------------------------------------------------
  {
    path: 'dashboard',
    canActivate: [authGuard],
    loadComponent: () => import('./_shared/layout/shell').then((m) => m.Shell),
    children: [
      {
        path: '',
        pathMatch: 'full',
        title: 'Home',
        loadComponent: () =>
          import('./_shared/components/pending-screen').then((m) => m.PendingScreen),
        data: { title: 'Home' },
      },
      {
        path: 'profile',
        title: 'My profile',
        loadComponent: () =>
          import('./_shared/components/pending-screen').then((m) => m.PendingScreen),
        data: { title: 'My profile' },
      },

      // Coach-only areas (SPEC section 5).
      {
        path: 'members',
        canActivate: [coachGuard],
        title: 'Members',
        loadComponent: () =>
          import('./_shared/components/pending-screen').then((m) => m.PendingScreen),
        data: { title: 'Members' },
      },
      {
        // Reached from the note-notification email. A member may open their own profile here,
        // so this one is not coach-gated — the API decides whose record they may read.
        path: 'members/:id',
        title: 'Member profile',
        loadComponent: () =>
          import('./_shared/components/pending-screen').then((m) => m.PendingScreen),
        data: { title: 'Member profile' },
      },
      {
        path: 'trainings',
        canActivate: [coachGuard],
        title: 'Trainings',
        loadComponent: () =>
          import('./_shared/components/pending-screen').then((m) => m.PendingScreen),
        data: { title: 'Trainings' },
      },
      {
        // Reached from the training-invitation email, by an invited member who is not a coach.
        path: 'trainings/:id',
        title: 'Training details',
        loadComponent: () =>
          import('./_shared/components/pending-screen').then((m) => m.PendingScreen),
        data: { title: 'Training details' },
      },
      {
        path: 'payments',
        canActivate: [coachGuard],
        title: 'Payments',
        loadComponent: () =>
          import('./_shared/components/pending-screen').then((m) => m.PendingScreen),
        data: { title: 'Payments' },
      },
      {
        path: 'notes',
        canActivate: [coachGuard],
        title: 'Notes',
        loadComponent: () =>
          import('./_shared/components/pending-screen').then((m) => m.PendingScreen),
        data: { title: 'Notes' },
      },
      {
        path: 'register-member',
        canActivate: [coachGuard],
        title: 'Register a member',
        loadComponent: () =>
          import('./_shared/components/pending-screen').then((m) => m.PendingScreen),
        data: { title: 'Register a member' },
      },
    ],
  },

  // ---- Stripe return landings (SPEC section 3.2) --------------------------------------------
  {
    path: 'successful-payment',
    title: 'Payment complete',
    loadComponent: () =>
      import('./_shared/components/pending-screen').then((m) => m.PendingScreen),
    data: { title: 'Payment complete' },
  },
  {
    path: 'failed-payment',
    title: 'Payment cancelled',
    loadComponent: () =>
      import('./_shared/components/pending-screen').then((m) => m.PendingScreen),
    data: { title: 'Payment cancelled' },
  },

  {
    path: '**',
    title: 'Page not found',
    loadComponent: () => import('./not-found/not-found').then((m) => m.NotFound),
  },
];
