import { FormControl, FormGroup, Validators } from '@angular/forms';
import { fromDateOnly, toDateOnly } from './date-only';
import { PASSWORD_RULES, matchFields, passwordPolicy } from './password.validator';

describe('passwordPolicy', () => {
  const validate = (value: string) => passwordPolicy()(new FormControl(value));

  it('ignores an empty value and leaves it to required', () => {
    expect(validate('')).toBeNull();
  });

  it('accepts a password that meets every Identity rule', () => {
    expect(validate('Sup3rSecret')).toBeNull();
  });

  it('names the rules that are not met', () => {
    // Nine characters, no digit, no upper case.
    expect(validate('shortpass')).toEqual({ passwordPolicy: ['length', 'uppercase', 'digit'] });
  });

  it('does not require a symbol, matching RequireNonAlphanumeric = false', () => {
    expect(validate('Passw0rdWord')).toBeNull();
  });

  it('exposes one rule per Identity option, for the checklist', () => {
    expect(PASSWORD_RULES.map((rule) => rule.key)).toEqual([
      'length',
      'uppercase',
      'lowercase',
      'digit',
    ]);
  });
});

describe('matchFields', () => {
  function group(a: string, b: string): FormGroup {
    const form = new FormGroup(
      {
        newPassword: new FormControl(a, Validators.required),
        confirmPassword: new FormControl(b, Validators.required),
      },
      { validators: matchFields('newPassword', 'confirmPassword') },
    );

    form.updateValueAndValidity();
    return form;
  }

  it('flags a mismatch on the group and on the confirm control', () => {
    const form = group('Sup3rSecret', 'Sup3rSecrat');

    expect(form.hasError('fieldsMismatch')).toBe(true);
    expect(form.controls['confirmPassword'].hasError('fieldsMismatch')).toBe(true);
    expect(form.invalid).toBe(true);
  });

  it('clears its own error once the two agree', () => {
    const form = group('Sup3rSecret', 'Sup3rSecrat');
    form.controls['confirmPassword'].setValue('Sup3rSecret');

    expect(form.hasError('fieldsMismatch')).toBe(false);
    expect(form.controls['confirmPassword'].errors).toBeNull();
  });

  it('leaves required alone when it clears its own error', () => {
    // Emptying the confirm box must surface `required`, not swallow it.
    const form = group('Sup3rSecret', 'Sup3rSecrat');
    form.controls['confirmPassword'].setValue('');

    expect(form.controls['confirmPassword'].hasError('required')).toBe(true);
  });

  it('stays quiet while the confirm box is still empty', () => {
    expect(group('Sup3rSecret', '').hasError('fieldsMismatch')).toBe(false);
  });
});

describe('toDateOnly', () => {
  it('formats from local parts, not UTC', () => {
    // 1 January at ten past midnight local time is still 1 January. toISOString() would
    // move it to 31 December for anyone west of Greenwich.
    expect(toDateOnly(new Date(2010, 0, 1, 0, 10))).toBe('2010-01-01');
  });

  it('pads month and day', () => {
    expect(toDateOnly(new Date(2005, 3, 7))).toBe('2005-04-07');
  });

  it('returns null for nothing and for an invalid date', () => {
    expect(toDateOnly(null)).toBeNull();
    expect(toDateOnly(new Date('not a date'))).toBeNull();
  });

  it('round-trips through fromDateOnly', () => {
    const parsed = fromDateOnly('1998-11-30')!;

    expect(parsed.getFullYear()).toBe(1998);
    expect(parsed.getMonth()).toBe(10);
    expect(parsed.getDate()).toBe(30);
    expect(toDateOnly(parsed)).toBe('1998-11-30');
  });

  it('returns null for a malformed date string', () => {
    expect(fromDateOnly('')).toBeNull();
    expect(fromDateOnly('never')).toBeNull();
  });
});
