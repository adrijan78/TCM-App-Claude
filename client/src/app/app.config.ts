import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import {
  provideRouter,
  withComponentInputBinding,
  withInMemoryScrolling,
  withViewTransitions,
} from '@angular/router';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideNativeDateAdapter } from '@angular/material/core';
import { routes } from './app.routes';
import { jwtInterceptor } from './_interceptors/jwt.interceptor';
import { errorInterceptor } from './_interceptors/error.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),

    provideRouter(
      routes,
      // Route params arrive as component inputs, so screens do not each re-read ActivatedRoute.
      withComponentInputBinding(),
      withInMemoryScrolling({ scrollPositionRestoration: 'enabled', anchorScrolling: 'enabled' }),
      // Route changes cross-fade through the browser's View Transitions API. This is how the
      // app gets route motion without @angular/animations, which Angular Material 22 dropped.
      // Browsers without the API simply navigate; the timing is capped and disabled under
      // prefers-reduced-motion in styles/_motion.scss.
      withViewTransitions({ skipInitialTransition: true }),
    ),

    // Order matters: jwtInterceptor attaches the token on the way out, errorInterceptor
    // handles what comes back — including the 401 that clears the session.
    provideHttpClient(withFetch(), withInterceptors([jwtInterceptor, errorInterceptor])),

    // MatDatepicker needs a date adapter, and the native one is enough: every date this app
    // sends is a `DateOnly` string built from local parts by `toDateOnly`, so no second date
    // library is earning its place here.
    provideNativeDateAdapter(),

    // No provideAnimationsAsync: Angular Material 22 dropped @angular/animations from its
    // peer dependencies and animates with CSS, so requesting it only fails the build on a
    // package that is no longer installed.
  ],
};
