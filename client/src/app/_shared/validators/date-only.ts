/**
 * The wire format for a C# `DateOnly` — `yyyy-MM-dd`.
 *
 * Built from the local date parts on purpose. `toISOString()` converts to UTC first, which
 * moves a birthday west of Greenwich back a day for anyone who picks a date near midnight.
 */
export function toDateOnly(value: Date | null | undefined): string | null {
  if (!value || Number.isNaN(value.getTime())) {
    return null;
  }

  const year = value.getFullYear().toString().padStart(4, '0');
  const month = (value.getMonth() + 1).toString().padStart(2, '0');
  const day = value.getDate().toString().padStart(2, '0');

  return `${year}-${month}-${day}`;
}

/** The inverse, for binding an API `DateOnly` back into a datepicker. */
export function fromDateOnly(value: string | null | undefined): Date | null {
  if (!value) return null;

  const [year, month, day] = value.split('-').map(Number);
  if (!year || !month || !day) return null;

  return new Date(year, month - 1, day);
}
