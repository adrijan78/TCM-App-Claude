import { TestBed } from '@angular/core/testing';
import { ThemeService } from './theme.service';

const STORAGE_KEY = 'tcm.theme';

describe('ThemeService', () => {
  function create(): ThemeService {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});
    const service = TestBed.inject(ThemeService);
    // The effect that writes `color-scheme` runs on the first change detection pass.
    TestBed.tick();
    return service;
  }

  beforeEach(() => {
    localStorage.clear();
    document.documentElement.style.colorScheme = '';
  });

  afterEach(() => {
    localStorage.clear();
    document.documentElement.style.colorScheme = '';
  });

  it('defaults to following the system', () => {
    const theme = create();

    expect(theme.mode()).toBe('system');
    // `light dark` is what makes every light-dark() token defer to the OS setting.
    expect(document.documentElement.style.colorScheme).toBe('light dark');
  });

  it('writes the chosen scheme onto the root element', () => {
    const theme = create();

    theme.set('dark');
    TestBed.tick();

    expect(document.documentElement.style.colorScheme).toBe('dark');
    expect(theme.resolved()).toBe('dark');
  });

  it('remembers the choice across a reload', () => {
    const theme = create();
    theme.set('light');
    TestBed.tick();

    expect(localStorage.getItem(STORAGE_KEY)).toBe('light');

    expect(create().mode()).toBe('light');
  });

  it('toggles against what is actually on screen, not against the stored mode', () => {
    // From `system`, the first toggle has to pick the opposite of whatever the system
    // resolved to — otherwise the button appears to do nothing on the first press.
    const theme = create();
    const wasDark = theme.resolved() === 'dark';

    theme.toggle();
    TestBed.tick();

    expect(theme.resolved()).toBe(wasDark ? 'light' : 'dark');
    expect(theme.mode()).not.toBe('system');
  });

  it('ignores a junk value in storage', () => {
    localStorage.setItem(STORAGE_KEY, 'chartreuse');

    expect(create().mode()).toBe('system');
  });
});
