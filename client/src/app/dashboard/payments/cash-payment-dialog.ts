import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { PaymentService } from '../../_services/payment.service';
import { apiErrorParts } from '../../_services/unwrap';
import { Member } from '../../_models/member.model';
import { Payment } from '../../_models/payment.model';
import { FormAlert } from '../../_shared/components/form-alert';

export interface CashPaymentData {
  readonly members: readonly Member[];
}

/**
 * SPEC section 6.7 — the coach logging cash handed over in person.
 *
 * There is no amount field: the club has one membership fee, set server-side, and letting a
 * coach type a number here would put the price in the UI where it can disagree with Stripe.
 */
@Component({
  selector: 'app-cash-payment-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    FormAlert,
  ],
  template: `
    <h2 mat-dialog-title>Log a cash payment</h2>

    <mat-dialog-content>
      <form class="cash-form" [formGroup]="form" (ngSubmit)="save()" novalidate>
        @if (error(); as message) {
          <app-form-alert [message]="message" [details]="errorDetails()" />
        }

        <mat-form-field appearance="outline">
          <mat-label>Member</mat-label>
          <mat-select formControlName="memberId" required>
            @for (member of data.members; track member.id) {
              <mat-option [value]="member.id">
                {{ member.firstName }} {{ member.lastName }}
              </mat-option>
            }
          </mat-select>
          @if (form.controls.memberId.hasError('required')) {
            <mat-error>Choose who paid.</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Date paid</mat-label>
          <input matInput [matDatepicker]="picker" [max]="today" formControlName="paymentDate" />
          <mat-datepicker-toggle matIconSuffix [for]="picker" />
          <mat-datepicker #picker />
          <mat-hint>Leave blank for today.</mat-hint>
          @if (form.controls.paymentDate.hasError('matDatepickerMax')) {
            <mat-error>A payment cannot be dated in the future.</mat-error>
          }
        </mat-form-field>
      </form>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button type="button" (click)="dialogRef.close()">Cancel</button>
      <button mat-flat-button type="button" [disabled]="saving()" (click)="save()">
        @if (saving()) {
          <mat-spinner diameter="18" />
        }
        <span>Log payment</span>
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .cash-form {
      display: flex;
      flex-direction: column;
      gap: var(--tcm-space-2);
      min-inline-size: min(24rem, 70vw);
      padding-block-start: var(--tcm-space-2);
    }

    button[mat-flat-button] {
      display: inline-flex;
      align-items: center;
      gap: var(--tcm-space-2);
    }
  `,
})
export class CashPaymentDialog {
  protected readonly dialogRef = inject(MatDialogRef<CashPaymentDialog, Payment>);
  protected readonly data = inject<CashPaymentData>(MAT_DIALOG_DATA);

  private readonly payments = inject(PaymentService);
  private readonly fb = inject(FormBuilder);

  protected readonly today = new Date();

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly errorDetails = signal<readonly string[]>([]);

  protected readonly form = this.fb.nonNullable.group({
    memberId: ['', [Validators.required]],
    paymentDate: this.fb.nonNullable.control<Date | null>(null),
  });

  protected save(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.error.set(null);

    const { memberId, paymentDate } = this.form.getRawValue();

    this.payments
      .logCashPayment({
        memberId,
        // `Payments.PaymentDate` is stored UTC, and the API binds a full DateTime here — so
        // unlike the DateOnly fields elsewhere this one is sent as an ISO instant.
        paymentDate: paymentDate ? paymentDate.toISOString() : null,
      })
      .subscribe({
        next: (payment) => this.dialogRef.close(payment),
        error: (error: unknown) => {
          this.saving.set(false);

          const failure = apiErrorParts(error, 'The payment could not be logged.');
          this.error.set(failure.message);
          this.errorDetails.set(failure.details);
        },
      });
  }
}
