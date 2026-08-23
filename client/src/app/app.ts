import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

/**
 * Root component. It holds nothing but the outlet — the signed-in chrome lives in
 * `Shell`, so the login and reset screens render full-page without it.
 */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  template: '<router-outlet />',
})
export class App {}
