import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * Mirrors the Identity password options in
 * server/TCM.Infrastructure/DependencyInjection.cs: ten characters, at least one digit,
 * one lower-case and one upper-case letter; symbols allowed but not required.
 *
 * The server remains the authority — this only saves a round trip and tells the user which
 * rule they have not met yet, which Identity's error list does not do until submit.
 */
export const PASSWORD_MIN_LENGTH = 10;

export interface PasswordRule {
  readonly key: string;
  readonly label: string;
  readonly test: (value: string) => boolean;
}

export const PASSWORD_RULES: readonly PasswordRule[] = [
  {
    key: 'length',
    label: `At least ${PASSWORD_MIN_LENGTH} characters`,
    test: (value) => value.length >= PASSWORD_MIN_LENGTH,
  },
  { key: 'uppercase', label: 'An upper-case letter', test: (value) => /[A-Z]/.test(value) },
  { key: 'lowercase', label: 'A lower-case letter', test: (value) => /[a-z]/.test(value) },
  { key: 'digit', label: 'A number', test: (value) => /[0-9]/.test(value) },
];

export function passwordPolicy(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = control.value;

    // An empty box is `required`'s business, not ours — reporting both at once is noise.
    if (typeof value !== 'string' || value.length === 0) {
      return null;
    }

    const unmet = PASSWORD_RULES.filter((rule) => !rule.test(value)).map((rule) => rule.key);

    return unmet.length ? { passwordPolicy: unmet } : null;
  };
}

/**
 * Cross-field check for the two "confirm password" forms. The error is set on the group so
 * it survives the confirm box being edited, and mirrored onto the confirm control so the
 * message can sit under the field the user is looking at.
 */
export function matchFields(sourceName: string, confirmName: string): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const source = group.get(sourceName);
    const confirm = group.get(confirmName);

    if (!source || !confirm || !confirm.value) {
      return null;
    }

    if (source.value === confirm.value) {
      // Clear only our own error; whatever else is on the control is not ours to remove.
      if (confirm.hasError('fieldsMismatch')) {
        const { fieldsMismatch, ...rest } = confirm.errors ?? {};
        confirm.setErrors(Object.keys(rest).length ? rest : null);
      }
      return null;
    }

    confirm.setErrors({ ...(confirm.errors ?? {}), fieldsMismatch: true });
    return { fieldsMismatch: true };
  };
}
