import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MemberService } from '../../_services/member.service';
import { apiErrorParts } from '../../_services/unwrap';
import { Belt, MemberBelt } from '../../_models/member.model';
import { FormAlert } from '../../_shared/components/form-alert';
import { toDateOnly } from '../../_shared/validators/date-only';

export interface AddBeltData {
  readonly memberId: string;
  readonly memberName: string;
  readonly belts: readonly Belt[];
}

/** SPEC section 6.4 — recording a belt exam. Coach only. */
@Component({
  selector: 'app-add-belt-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatCheckboxModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    FormAlert,
  ],
  template: `
    <h2 mat-dialog-title>Record a belt exam</h2>

    <mat-dialog-content>
      <form class="belt-form" [formGroup]="form" (ngSubmit)="save()" novalidate>
        @if (error(); as message) {
          <app-form-alert [message]="message" [details]="errorDetails()" />
        }

        <p class="belt-about">
          For <strong>{{ data.memberName }}</strong>
        </p>

        <mat-form-field appearance="outline">
          <mat-label>Belt</mat-label>
          <mat-select formControlName="beltId" required>
            @for (belt of data.belts; track belt.id) {
              <mat-option [value]="belt.id">{{ belt.beltName }}</mat-option>
            }
          </mat-select>
          @if (form.controls.beltId.hasError('required')) {
            <mat-error>Choose the belt awarded.</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Date received</mat-label>
          <input
            matInput
            [matDatepicker]="picker"
            [max]="today"
            formControlName="dateReceived"
            required
          />
          <mat-datepicker-toggle matIconSuffix [for]="picker" />
          <mat-datepicker #picker />
          @if (form.controls.dateReceived.hasError('required')) {
            <mat-error>Pick the exam date.</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Notes on the exam</mat-label>
          <textarea matInput formControlName="description" rows="3"></textarea>
          <mat-hint>Optional — what they did well, what to work on.</mat-hint>
        </mat-form-field>

        <mat-checkbox formControlName="isCurrentBelt">
          This is now their current belt
        </mat-checkbox>
      </form>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button type="button" (click)="dialogRef.close()">Cancel</button>
      <button mat-flat-button type="button" [disabled]="saving()" (click)="save()">
        @if (saving()) {
          <mat-spinner diameter="18" />
        }
        <span>Record exam</span>
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .belt-form {
      display: flex;
      flex-direction: column;
      gap: var(--tcm-space-2);
      min-inline-size: min(26rem, 72vw);
      padding-block-start: var(--tcm-space-2);
    }

    .belt-about {
      margin: 0 0 var(--tcm-space-2);
      color: var(--mat-sys-on-surface-variant);
    }

    button[mat-flat-button] {
      display: inline-flex;
      align-items: center;
      gap: var(--tcm-space-2);
    }
  `,
})
export class AddBeltDialog {
  protected readonly dialogRef = inject(MatDialogRef<AddBeltDialog, MemberBelt>);
  protected readonly data = inject<AddBeltData>(MAT_DIALOG_DATA);

  private readonly members = inject(MemberService);
  private readonly fb = inject(FormBuilder);

  protected readonly today = new Date();

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly errorDetails = signal<readonly string[]>([]);

  protected readonly form = this.fb.nonNullable.group({
    beltId: this.fb.nonNullable.control<number | null>(null, [Validators.required]),
    dateReceived: this.fb.nonNullable.control<Date | null>(new Date(), [Validators.required]),
    description: this.fb.nonNullable.control<string>(''),
    // Almost always true: a coach records an exam because the member passed it.
    isCurrentBelt: this.fb.nonNullable.control<boolean>(true),
  });

  protected save(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const dateReceived = toDateOnly(value.dateReceived);
    if (!dateReceived) {
      this.form.controls.dateReceived.setErrors({ required: true });
      return;
    }

    this.saving.set(true);
    this.error.set(null);

    this.members
      .addBelt(this.data.memberId, {
        beltId: value.beltId!,
        dateReceived,
        description: value.description.trim() || null,
        isCurrentBelt: value.isCurrentBelt,
      })
      .subscribe({
        next: (belt) => this.dialogRef.close(belt),
        error: (error: unknown) => {
          this.saving.set(false);

          const failure = apiErrorParts(error, 'The belt exam could not be recorded.');
          this.error.set(failure.message);
          this.errorDetails.set(failure.details);
        },
      });
  }
}
