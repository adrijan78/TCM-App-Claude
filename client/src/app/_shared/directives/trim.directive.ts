import { Directive, inject } from '@angular/core';
import { NgControl } from '@angular/forms';

/**
 * Strips leading and trailing whitespace from a text control when it loses focus.
 *
 * Email addresses are pasted far more often than they are typed, and a pasted address
 * usually brings a space with it. Without this, `Validators.email` rejects
 * `" ana@example.test "` and the user is told their own address is malformed — with no
 * visible difference between the value they see and the one that failed.
 *
 * Trimming on blur rather than on every keystroke leaves the caret where the user put it.
 */
@Directive({
  selector: 'input[appTrim]',
  host: { '(blur)': 'trim()' },
})
export class Trim {
  private readonly control = inject(NgControl, { optional: true, self: true });

  protected trim(): void {
    const control = this.control?.control;
    const value = control?.value;

    if (typeof value !== 'string') return;

    const trimmed = value.trim();
    if (trimmed !== value) {
      control!.setValue(trimmed);
    }
  }
}
