import { ChartOptions } from 'chart.js';

/**
 * One Chart.js theme for the whole app, so the charts in SPEC 6.2 and 6.4 read as one
 * family rather than five separate experiments.
 *
 * Colours are read out of the CSS tokens at call time rather than hard-coded, which is what
 * makes the charts follow the light/dark toggle. `<app-chart>` rebuilds its chart when its
 * inputs change; `ThemeService` toggling `color-scheme` changes what these functions return.
 */

/**
 * Resolves a CSS custom property to a colour Chart.js can actually parse.
 *
 * Reading the property directly is not enough: `getComputedStyle().getPropertyValue()`
 * returns a custom property's *specified* value, so a `light-dark(#4c6fbf, #8fabe8)` token
 * comes back as that literal string — which Chart.js cannot understand, and silently draws
 * black instead. Assigning it to a real `color` property and reading *that* back makes the
 * browser resolve `light-dark()`, `color-mix()` and `var()` chains down to an `rgb()`.
 *
 * The probe element is created once and reused; it is inert, invisible, and never painted.
 */
let probe: HTMLElement | undefined;

function token(name: string, fallback = '#888888'): string {
  if (!probe) {
    probe = document.createElement('span');
    probe.style.display = 'none';
    document.body.appendChild(probe);
  }

  probe.style.color = '';
  probe.style.color = `var(${name})`;

  const resolved = getComputedStyle(probe).color;

  // An unknown token leaves `color` at its inherited value rather than erroring, so a
  // fully transparent result means nothing resolved.
  return !resolved || resolved === 'rgba(0, 0, 0, 0)' ? fallback : resolved;
}

/**
 * The categorical series colours, in fixed order. Fixed matters: "trainings held" should be
 * the same blue on the dashboard as on the member profile.
 */
export function chartPalette(): string[] {
  return [1, 2, 3, 4, 5, 6, 7, 8].map((index) => token(`--tcm-chart-${index}`));
}

export function chartColour(index: number): string {
  const palette = chartPalette();
  return palette[index % palette.length];
}

/** Semantic series colours, for charts whose meaning is fixed rather than categorical. */
export function toneColour(tone: 'positive' | 'caution' | 'critical' | 'info' | 'quiet'): string {
  return token(`--tcm-${tone}`);
}

/**
 * The options every chart starts from. Spread it and override what a specific chart needs:
 *
 * ```ts
 * options = { ...baseChartOptions(), scales: { y: { beginAtZero: true } } };
 * ```
 */
export function baseChartOptions(): ChartOptions {
  const ink = token('--mat-sys-on-surface', '#1c1b1f');
  const muted = token('--mat-sys-on-surface-variant', '#49454f');
  const grid = token('--tcm-chart-grid', 'rgba(0,0,0,0.08)');
  const surface = token('--mat-sys-surface-container-high', '#ffffff');

  return {
    responsive: true,
    maintainAspectRatio: false,
    // The canvas already carries an aria-label; the legend and tooltip are for sighted users.
    font: { family: 'Roboto, sans-serif' },
    layout: { padding: 4 },
    plugins: {
      legend: {
        position: 'bottom',
        labels: {
          color: muted,
          boxWidth: 12,
          boxHeight: 12,
          usePointStyle: true,
          pointStyle: 'circle',
          padding: 16,
        },
      },
      tooltip: {
        backgroundColor: surface,
        titleColor: ink,
        bodyColor: muted,
        borderColor: grid,
        borderWidth: 1,
        cornerRadius: 8,
        padding: 10,
        displayColors: true,
        usePointStyle: true,
      },
    },
    scales: {
      x: {
        border: { display: false },
        grid: { display: false },
        ticks: { color: muted },
      },
      y: {
        border: { display: false },
        // A horizontal rule per gridline is enough; the vertical ones are noise.
        grid: { color: grid },
        ticks: { color: muted, precision: 0 },
      },
    },
  };
}

/** Bar charts: rounded caps and a sensible max thickness so three bars are not three slabs. */
export function barDefaults() {
  return { borderRadius: 6, borderSkipped: false as const, maxBarThickness: 44 };
}

/** Line charts: a light tension reads as a trend without misrepresenting the points. */
export function lineDefaults() {
  return { tension: 0.35, borderWidth: 2, pointRadius: 3, pointHoverRadius: 5, fill: false };
}

/**
 * Doughnut rather than pie, and a gap between arcs, because adjacent slices of similar size
 * are hard to tell apart without one.
 */
export function doughnutDefaults() {
  return { borderWidth: 2, borderColor: token('--mat-sys-surface', '#ffffff'), cutout: '62%' };
}
