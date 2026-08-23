import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { debounceTime, startWith, switchMap } from 'rxjs';
import { PaymentService } from '../../_services/payment.service';
import { MemberService } from '../../_services/member.service';
import { apiErrorMessage } from '../../_services/unwrap';
import { Member } from '../../_models/member.model';
import { Payment } from '../../_models/payment.model';
import { PaymentMethod } from '../../_models/enums';
import { PAYMENT_METHOD_PRESENTATION } from '../../_shared/status-presentation';
import { ConfirmDialog, ConfirmDialogData } from '../../_shared/components/confirm-dialog';
import { PageHeader } from '../../_shared/components/page-header';
import { StatePanel } from '../../_shared/components/state-panel';
import { StatusChip } from '../../_shared/components/status-chip';

const MONTHS = [
  'January',
  'February',
  'March',
  'April',
  'May',
  'June',
  'July',
  'August',
  'September',
  'October',
  'November',
  'December',
];

/**
 * SPEC section 6.7 — every membership payment in the club, with the four filters the spec
 * names. Coach only.
 */
@Component({
  selector: 'app-payments',
  imports: [
    DatePipe,
    RouterLink,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatSelectModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    PageHeader,
    StatePanel,
    StatusChip,
  ],
  templateUrl: './payments.html',
  styleUrl: './payments.scss',
})
export class Payments {
  private readonly payments = inject(PaymentService);
  private readonly members = inject(MemberService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly fb = inject(FormBuilder);

  protected readonly columns = ['member', 'paidOn', 'nextDue', 'method', 'actions'];

  protected readonly months = MONTHS.map((label, index) => ({ value: index + 1, label }));

  /** Ten years back is more history than this club has; further back is noise in a dropdown. */
  protected readonly years = Array.from(
    { length: 10 },
    (_, index) => new Date().getFullYear() - index,
  );

  protected readonly filters = this.fb.nonNullable.group({
    year: this.fb.nonNullable.control<number | null>(null),
    month: this.fb.nonNullable.control<number | null>(null),
    memberId: this.fb.nonNullable.control<string | null>(null),
    method: this.fb.nonNullable.control<PaymentMethod | null>(null),
  });

  protected readonly rows = signal<Payment[]>([]);
  protected readonly memberList = signal<readonly Member[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly hasFilters = signal(false);

  protected readonly onlineCount = computed(
    () => this.rows().filter((row) => row.isPaidOnline).length,
  );

  constructor() {
    this.filters.valueChanges
      .pipe(
        startWith(this.filters.getRawValue()),
        // Selects fire one change each; a coach setting year then month should cost one request.
        debounceTime(150),
        switchMap(() => {
          this.loading.set(true);
          this.error.set(null);

          const value = this.filters.getRawValue();
          this.hasFilters.set(Object.values(value).some((v) => v !== null && v !== ''));

          return this.payments.getClubPayments(value);
        }),
        takeUntilDestroyed(),
      )
      .subscribe({
        next: (payments) => {
          this.rows.set(payments);
          this.loading.set(false);
        },
        error: (error: unknown) => {
          this.loading.set(false);
          this.error.set(apiErrorMessage(error, 'The payments could not be loaded.'));
        },
      });

    this.members.getMembers().subscribe({
      next: (members) => this.memberList.set(members),
      error: () => this.memberList.set([]),
    });
  }

  protected method(payment: Payment) {
    return PAYMENT_METHOD_PRESENTATION[
      payment.isPaidOnline ? PaymentMethod.Online : PaymentMethod.Cash
    ];
  }

  protected reload(): void {
    this.filters.setValue(this.filters.getRawValue());
  }

  protected clearFilters(): void {
    this.filters.reset({ year: null, month: null, memberId: null, method: null });
  }

  protected async logCash(): Promise<void> {
    const { CashPaymentDialog } = await import('./cash-payment-dialog');

    const created = await this.dialog
      .open(CashPaymentDialog, { data: { members: this.memberList() } })
      .afterClosed()
      .toPromise();

    if (created) {
      this.snackBar.open('Cash payment logged.', 'Dismiss', { duration: 4000 });
      this.reload();
    }
  }

  protected remove(payment: Payment): void {
    const data: ConfirmDialogData = {
      title: 'Delete this payment?',
      message:
        `The payment recorded for ${payment.memberFullName} will be removed, and their ` +
        'next due date will be recalculated from whatever remains. This cannot be undone.',
      confirmLabel: 'Delete',
      destructive: true,
    };

    this.dialog
      .open(ConfirmDialog, { data })
      .afterClosed()
      .subscribe((confirmed) => {
        if (confirmed !== true) return;

        this.payments.delete(payment.id).subscribe({
          next: () => {
            this.rows.update((rows) => rows.filter((row) => row.id !== payment.id));
            this.snackBar.open('Payment deleted.', 'Dismiss', { duration: 4000 });
          },
          error: (error: unknown) => {
            this.snackBar.open(
              apiErrorMessage(error, 'The payment could not be deleted.'),
              'Dismiss',
              { duration: 6000 },
            );
          },
        });
      });
  }
}
