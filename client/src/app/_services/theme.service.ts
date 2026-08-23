import { Injectable, computed, effect, signal } from '@angular/core';

export type ThemeMode = 'light' | 'dark' | 'system';

const STORAGE_KEY = 'tcm.theme';

/**
 * Owns the light/dark choice.
 *
 * Everything visual follows from one property: `color-scheme` on the root element. Angular
 * Material's system variables and every `light-dark()` token in `_tokens.scss` resolve
 * against it, so there is no second palette to keep in step and no class to remember to add.
 *
 * The default is `system`, which is the honest answer before someone has expressed a
 * preference — and it keeps following the OS if they change it later in the day.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly root = document.documentElement;
  private readonly query = window.matchMedia?.('(prefers-color-scheme: dark)');

  private readonly systemPrefersDark = signal(this.query?.matches ?? false);

  readonly mode = signal<ThemeMode>(this.restore());

  /** What is actually on screen right now, with `system` resolved. */
  readonly resolved = computed<'light' | 'dark'>(() => {
    const mode = this.mode();
    return mode === 'system' ? (this.systemPrefersDark() ? 'dark' : 'light') : mode;
  });

  constructor() {
    // Keep following the OS while the mode is `system` — someone whose machine switches at
    // sunset expects the app to switch with it, not at the next reload.
    this.query?.addEventListener('change', (event) => this.systemPrefersDark.set(event.matches));

    effect(() => {
      const mode = this.mode();
      this.root.style.colorScheme = mode === 'system' ? 'light dark' : mode;

      try {
        localStorage.setItem(STORAGE_KEY, mode);
      } catch {
        // A browser with storage blocked still gets the theme, just not the memory of it.
      }
    });
  }

  set(mode: ThemeMode): void {
    this.mode.set(mode);
  }

  /** What the toolbar button does: flip to the opposite of what is currently showing. */
  toggle(): void {
    this.mode.set(this.resolved() === 'dark' ? 'light' : 'dark');
  }

  private restore(): ThemeMode {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      if (stored === 'light' || stored === 'dark' || stored === 'system') {
        return stored;
      }
    } catch {
      // Fall through to the default.
    }

    return 'system';
  }
}
