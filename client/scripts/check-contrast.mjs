/**
 * Checks the design system's colour pairs against WCAG AA, reading the *compiled* stylesheet
 * rather than the Sass source — so what is verified is what actually ships.
 *
 * Text needs 4.5:1. Icons, borders and chart marks are non-text UI, which AA puts at 3:1.
 * Both themes are checked: `light-dark()` declarations carry the two values side by side.
 *
 *   npm run build && npm run check:contrast
 *
 * A failure here is not a style nit. The published Okabe-Ito chart palette sits at about
 * 2.2:1 on white for orange and sky blue, which is why the values in `_tokens.scss` are
 * darkened versions rather than the originals — this script is what caught that.
 */
import { readFileSync } from 'node:fs';
import { globSync } from 'node:fs';

const pattern = process.argv[2] ?? 'dist/client/browser/styles-*.css';
const files = globSync(pattern);

if (files.length === 0) {
  console.error(`No stylesheet matched ${pattern}. Run "npm run build" first.`);
  process.exit(2);
}

const css = readFileSync(files[0], 'utf8');

const DECL =
  /--([a-z0-9-]+):\s*(?:light-dark\(\s*(#[0-9a-fA-F]{3,8})\s*,\s*(#[0-9a-fA-F]{3,8})\s*\)|(#[0-9a-fA-F]{3,8}))\s*;/g;

const light = new Map();
const dark = new Map();

for (const [, name, l, d, flat] of css.matchAll(DECL)) {
  if (light.has(name)) continue;
  light.set(name, flat ?? l);
  dark.set(name, flat ?? d);
}

const channel = (v) => {
  const c = v / 255;
  return c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
};

function luminance(hex) {
  let h = hex.replace('#', '');
  if (h.length === 3) h = [...h].map((c) => c + c).join('');
  const [r, g, b] = [0, 2, 4].map((i) => parseInt(h.slice(i, i + 2), 16));
  return 0.2126 * channel(r) + 0.7152 * channel(g) + 0.0722 * channel(b);
}

function ratio(a, b) {
  const [x, y] = [luminance(a), luminance(b)].sort((p, q) => q - p);
  return (x + 0.05) / (y + 0.05);
}

const RAMPS = ['positive', 'caution', 'critical', 'info', 'quiet'];

const TEXT = [
  ...RAMPS.map((r) => [`tcm-on-${r}-container`, `tcm-${r}-container`]),
  ['mat-sys-on-surface', 'mat-sys-surface'],
  ['mat-sys-on-surface-variant', 'mat-sys-surface'],
  ['mat-sys-on-primary-container', 'mat-sys-primary-container'],
  ['mat-sys-primary', 'mat-sys-surface'],
];

const NON_TEXT = [
  ...RAMPS.map((r) => [`tcm-${r}`, 'mat-sys-surface']),
  ...Array.from({ length: 8 }, (_, i) => [`tcm-chart-${i + 1}`, 'mat-sys-surface']),
];

let failures = 0;

for (const [heading, pairs, floor] of [
  ['TEXT — AA 4.5:1', TEXT, 4.5],
  ['UI AND CHART MARKS — AA 3:1', NON_TEXT, 3],
]) {
  console.log(`\n=== ${heading} ===`);

  for (const [fg, bg] of pairs) {
    if (!light.has(fg) || !light.has(bg)) {
      console.log(`  ??   ${fg} on ${bg}: not declared`);
      failures++;
      continue;
    }

    const l = ratio(light.get(fg), light.get(bg));
    const d = ratio(dark.get(fg), dark.get(bg));
    const ok = l >= floor && d >= floor;
    if (!ok) failures++;

    console.log(
      `  ${ok ? 'OK  ' : 'FAIL'} ${fg.padEnd(32)} on ${bg.padEnd(30)} light ${l.toFixed(2).padStart(5)}  dark ${d.toFixed(2).padStart(5)}`,
    );
  }
}

console.log(failures ? `\n${failures} failing pair(s)` : '\nAll pairs pass AA in both themes.');
process.exit(failures ? 1 : 0);
