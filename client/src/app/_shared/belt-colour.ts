/**
 * The one place a belt name becomes a colour.
 *
 * Belts are seeded rows, not an enum — a club can add "Blue Stripe" tomorrow — so the colour
 * is read out of the name rather than looked up by id. An unknown or future belt falls back
 * to the neutral swatch instead of disappearing.
 *
 * Shared because the belt is this design's signature: it rings an avatar, it is the spine on
 * a profile panel, and it is the dot beside a belt name. Three copies of this list would
 * eventually disagree about what "Yellow Stripe" looks like.
 */
const BELT_COLOURS = ['white', 'yellow', 'green', 'blue', 'red', 'black'] as const;

export function beltColour(beltName: string | null | undefined): string {
  const name = (beltName ?? '').toLowerCase();

  for (const colour of BELT_COLOURS) {
    if (name.includes(colour)) {
      return `var(--tcm-belt-${colour})`;
    }
  }

  return 'var(--tcm-quiet)';
}
