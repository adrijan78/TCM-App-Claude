import {
  ApplicationConfig,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter, withComponentInputBinding, withInMemoryScrolling } from '@angular/router';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
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
    ),

    // Order matters: jwtInterceptor attaches the token on the way out, errorInterceptor
    // handles what comes back — including the 401 that clears the session.
    provideHttpClient(withFetch(), withInterceptors([jwtInterceptor, errorInterceptor])),

    // No provideAnimationsAsync: Angular Material 22 dropped @angular/animations from its
    // peer dependencies and animates with CSS, so requesting it only fails the build on a
    // package that is no longer installed.
  ],
};
