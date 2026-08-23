import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MemberService } from '../../_services/member.service';
import { apiErrorParts } from '../../_services/unwrap';
import { Member } from '../../_models/member.model';
import { FormAlert } from '../../_shared/components/form-alert';
import { Trim } from '../../_shared/directives/trim.directive';
import { fromDateOnly, toDateOnly } from '../../_shared/validators/date-only';

/**
 * SPEC section 6.4 — "Edit Data".
 *
 * The fields here are exactly `EditMemberDto` and no more. There is no role, status or club
 * field, deliberately: a member editing their own profile must not be able to promote
 * themselves or reopen a closed account, and the surest guarantee is that the shape they
 * post into has nowhere to put those values.
 */
@Component({
  selector: 'app-edit-member-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatDatepickerModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    FormAlert,
    Trim,
  ],
  template: `
    <h2 mat-dialog-title>Edit details</h2>

    <mat-dialog-content>
      <form class="edit-form" [formGroup]="form" (ngSubmit)="save()" novalidate>
        @if (error(); as message) {
          <app-form-alert [message]="message" [details]="errorDetails()" />
        }

        <div class="edit-row">
          <mat-form-field appearance="outline">
            <mat-label>First name</mat-label>
            <input matInput formControlName="firstName" required />
            @if (form.controls.firstName.hasError('required')) {
              <mat-error>Enter a first name.</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Last name</mat-label>
            <input matInput formControlName="lastName" required />
            @if (form.controls.lastName.hasError('required')) {
              <mat-error>Enter a last name.</mat-error>
            }
          </mat-form-field>
        </div>

        <mat-form-field appearance="outline">
          <mat-label>Email</mat-label>
          <input matInput appTrim type="email" formControlName="email" required />
          <mat-hint>Changing this changes the address they sign in with.</mat-hint>
          @if (form.controls.email.hasError('required')) {
            <mat-error>Enter an email address.</mat-error>
          } @else if (form.controls.email.hasError('email')) {
            <mat-error>That does not look like an email address.</mat-error>
          }
        </mat-form-field>

        <div class="edit-row">
          <mat-form-field appearance="outline">
            <mat-label>Phone</mat-label>
            <input matInput appTrim type="tel" formControlName="phoneNumber" />
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Date of birth</mat-label>
            <input
              matInput
              [matDatepicker]="dob"
              [max]="today"
              formControlName="dateOfBirth"
              required
            />
            <mat-datepicker-toggle matIconSuffix [for]="dob" />
            <mat-datepicker #dob startView="multi-year" />
            @if (form.controls.dateOfBirth.hasError('required')) {
              <mat-error>Pick a date of birth.</mat-error>
            }
          </mat-form-field>
        </div>

        <div class="edit-row">
          <mat-form-field appearance="outline">
            <mat-label>Height</mat-label>
            <input matInput type="number" formControlName="height" min="50" max="250" />
            <span matTextSuffix>cm</span>
            @if (form.controls.height.invalid) {
              <mat-error>Between 50 and 250 cm.</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Weight</mat-label>
            <input matInput type="number" formControlName="weight" min="10" max="300" />
            <span matTextSuffix>kg</span>
            @if (form.controls.weight.invalid) {
              <mat-error>Between 10 and 300 kg.</mat-error>
            }
          </mat-form-field>
        </div>
      </form>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button type="button" (click)="dialogRef.close()">Cancel</button>
      <button mat-flat-button type="button" [disabled]="saving()" (click)="save()">
        @if (saving()) {
          <mat-spinner diameter="18" />
        }
        <span>Save changes</span>
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .edit-form {
      display: flex;
      flex-direction: column;
      gap: var(--tcm-space-2);
      min-inline-size: min(32rem, 76vw);
      padding-block-start: var(--tcm-space-2);
    }

    .edit-row {
      display: flex;
      flex-wrap: wrap;
      gap: var(--tcm-space-3);
    }

    .edit-row mat-form-field {
      flex: 1 1 12rem;
    }

    button[mat-flat-button] {
      display: inline-flex;
      align-items: center;
      gap: var(--tcm-space-2);
    }
  `,
})
export class EditMemberDialog {
  protected readonly dialogRef = inject(MatDialogRef<EditMemberDialog, Member>);
  protected readonly member = inject<Member>(MAT_DIALOG_DATA);

  private readonly members = inject(MemberService);
  private readonly fb = inject(FormBuilder);

  protected readonly today = new Date();

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly errorDetails = signal<readonly string[]>([]);

  protected readonly form = this.fb.nonNullable.group({
    firstName: [this.member.firstName, [Validators.required, Validators.maxLength(100)]],
    lastName: [this.member.lastName, [Validators.required, Validators.maxLength(100)]],
    email: [this.member.email, [Validators.required, Validators.email]],
    phoneNumber: this.fb.nonNullable.control<string>(this.member.phoneNumber ?? ''),
    dateOfBirth: this.fb.nonNullable.control<Date | null>(fromDateOnly(this.member.dateOfBirth), [
      Validators.required,
    ]),
    height: this.fb.nonNullable.control<number | null>(this.member.height, [
      Validators.min(50),
      Validators.max(250),
    ]),
    weight: this.fb.nonNullable.control<number | null>(this.member.weight, [
      Validators.min(10),
      Validators.max(300),
    ]),
  });

  protected save(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const dateOfBirth = toDateOnly(value.dateOfBirth);
    if (!dateOfBirth) {
      this.form.controls.dateOfBirth.setErrors({ required: true });
      return;
    }

    this.saving.set(true);
    this.error.set(null);

    this.members
      .updateMember(this.member.id, {
        firstName: value.firstName.trim(),
        lastName: value.lastName.trim(),
        email: value.email.trim(),
        phoneNumber: value.phoneNumber.trim() || null,
        dateOfBirth,
        height: value.height,
        weight: value.weight,
      })
      .subscribe({
        next: (updated) => this.dialogRef.close(updated),
        error: (error: unknown) => {
          this.saving.set(false);

          const failure = apiErrorParts(error, 'The changes could not be saved.');
          this.error.set(failure.message);
          this.errorDetails.set(failure.details);
        },
      });
  }
}
