import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { Trim } from './trim.directive';

@Component({
  imports: [ReactiveFormsModule, Trim],
  template: `<input appTrim type="email" [formControl]="email" />`,
})
class Host {
  readonly email = new FormControl('', [Validators.required, Validators.email]);
}

describe('Trim', () => {
  function setup() {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();

    return {
      fixture,
      control: fixture.componentInstance.email,
      input: fixture.nativeElement.querySelector('input') as HTMLInputElement,
    };
  }

  it('strips the spaces a pasted email brings with it', () => {
    const { control, input, fixture } = setup();

    control.setValue('  ana@example.test  ');
    // Untrimmed, Angular's email validator calls the address malformed.
    expect(control.hasError('email')).toBe(true);

    input.dispatchEvent(new Event('blur'));
    fixture.detectChanges();

    expect(control.value).toBe('ana@example.test');
    expect(control.valid).toBe(true);
  });

  it('leaves an already-clean value untouched', () => {
    const { control, input } = setup();

    control.setValue('ana@example.test');
    control.markAsPristine();
    input.dispatchEvent(new Event('blur'));

    expect(control.value).toBe('ana@example.test');
    // No write happened, so the control was not dirtied by the directive.
    expect(control.pristine).toBe(true);
  });

  it('collapses a value that is nothing but spaces, so required fires', () => {
    const { control, input, fixture } = setup();

    control.setValue('   ');
    input.dispatchEvent(new Event('blur'));
    fixture.detectChanges();

    expect(control.value).toBe('');
    expect(control.hasError('required')).toBe(true);
  });
});
