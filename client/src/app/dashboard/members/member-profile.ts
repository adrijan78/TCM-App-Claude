import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ChartConfiguration } from 'chart.js';
import { MemberService } from '../../_services/member.service';
import { TrainingService } from '../../_services/training.service';
import { PaymentService } from '../../_services/payment.service';
import { NoteService } from '../../_services/note.service';
import { PhotoService } from '../../_services/photo.service';
import { CommonService } from '../../_services/common.service';
import { AuthService } from '../../_services/auth.service';
import { apiErrorMessage } from '../../_services/unwrap';
import { Belt, Member, MemberBelt } from '../../_models/member.model';
import { MemberAttendanceSummary } from '../../_models/training.model';
import { MemberPaymentHistory, Payment } from '../../_models/payment.model';
import { Note } from '../../_models/note.model';
import { AttendanceStatus, PaymentMethod } from '../../_models/enums';
import {
  ATTENDANCE_STATUS_PRESENTATION,
  PAYMENT_METHOD_PRESENTATION,
  TRAINING_STATUS_PRESENTATION,
} from '../../_shared/status-presentation';
import { chartColour, doughnutDefaults, lineDefaults, toneColour } from '../../_shared/chart-theme';
import { ChartComponent } from '../../_shared/components/chart';
import { ConfirmDialog, ConfirmDialogData } from '../../_shared/components/confirm-dialog';
import { MemberAvatar } from '../../_shared/components/member-avatar';
import { MembershipBanner } from '../../_shared/components/membership-banner';
import { NoteCard } from '../../_shared/components/note-card';
import { PageHeader } from '../../_shared/components/page-header';
import { StatePanel } from '../../_shared/components/state-panel';
import { StatCard } from '../../_shared/components/stat-card';
import { BeltSwatch, StatusChip } from '../../_shared/components/status-chip';

const MONTH_LABELS = [
  'Jan',
  'Feb',
  'Mar',
  'Apr',
  'May',
  'Jun',
  'Jul',
  'Aug',
  'Sep',
  'Oct',
  'Nov',
  'Dec',
];

/**
 * SPEC section 6.4 — the member profile, in the three tabs the spec names: attendance and
 * performance, membership, then belt exams and notes.
 *
 * Reached by a coach for anyone in their club, and by a member for themselves — the note
 * notification email links straight here. Each tab fetches only when it is first opened: a
 * coach checking someone's belt history should not pay for three charts and a payment
 * history they never look at.
 *
 * Coach-only controls are hidden for a member, but that is UX. The server refuses the
 * underlying calls regardless, and Phase 10 verifies that it does.
 */
@Component({
  selector: 'app-member-profile',
  imports: [
    DatePipe,
    DecimalPipe,
    RouterLink,
    ReactiveFormsModule,
    MatTabsModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatFormFieldModule,
    MatSelectModule,
    PageHeader,
    StatePanel,
    StatCard,
    StatusChip,
    BeltSwatch,
    MemberAvatar,
    MembershipBanner,
    NoteCard,
    ChartComponent,
  ],
  templateUrl: './member-profile.html',
  styleUrl: './member-profile.scss',
})
export class MemberProfile {
  private readonly members = inject(MemberService);
  private readonly trainings = inject(TrainingService);
  private readonly payments = inject(PaymentService);
  private readonly notes = inject(NoteService);
  private readonly photos = inject(PhotoService);
  private readonly common = inject(CommonService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  /** Route parameter, bound by `withComponentInputBinding()`. */
  readonly id = input.required<string>();

  protected readonly isCoach = this.auth.isCoach;
  protected readonly isSelf = computed(() => this.auth.currentUser()?.id === this.id());

  // --- The member themselves -----------------------------------------------------------------
  protected readonly member = signal<Member | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly uploading = signal(false);

  // --- Tab 1: attendance and performance -----------------------------------------------------
  protected readonly yearFilter = new FormControl<number | null>(null);
  protected readonly summary = signal<MemberAttendanceSummary | null>(null);
  protected readonly summaryLoading = signal(true);
  protected readonly summaryError = signal<string | null>(null);

  protected readonly years = Array.from(
    { length: 6 },
    (_, index) => new Date().getFullYear() - index,
  );

  private readonly selectedYear = toSignal(this.yearFilter.valueChanges, { initialValue: null });

  // --- Tab 2: membership ---------------------------------------------------------------------
  protected readonly history = signal<MemberPaymentHistory | null>(null);
  protected readonly historyLoading = signal(false);
  protected readonly historyError = signal<string | null>(null);
  private historyRequested = false;

  // --- Tab 3: belts and notes ----------------------------------------------------------------
  protected readonly belts = signal<MemberBelt[]>([]);
  protected readonly memberNotes = signal<Note[]>([]);
  protected readonly beltsLoading = signal(false);
  protected readonly beltsError = signal<string | null>(null);
  private beltsRequested = false;

  protected readonly beltCatalogue = signal<Belt[]>([]);

  protected readonly paymentColumns = ['paidOn', 'nextDue', 'method'];
  protected readonly trainingColumns = [
    'date',
    'description',
    'status',
    'attendance',
    'performance',
  ];

  constructor() {
    // An effect, not the constructor: `id` is a signal input, and inputs are not set until
    // after construction, so reading a required one there throws.
    //
    // It also handles the route parameter changing without the component being recreated —
    // the note notification email links from one member's profile straight to another's, and
    // Angular reuses the component when only the parameter differs.
    effect(() => {
      const id = this.id();

      untracked(() => {
        // Everything below belongs to the member we are leaving.
        this.historyRequested = false;
        this.beltsRequested = false;
        this.history.set(null);
        this.belts.set([]);
        this.memberNotes.set([]);

        this.loadMember();
        this.loadSummary();
      });

      // Referenced so the linter can see the id is the dependency, not the loads.
      void id;
    });
  }

  // --- Loading ------------------------------------------------------------------------------

  protected loadMember(): void {
    this.loading.set(true);
    this.error.set(null);

    this.members.getMember(this.id()).subscribe({
      next: (member) => {
        this.member.set(member);
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.loading.set(false);
        this.error.set(apiErrorMessage(error, 'This member could not be loaded.'));
      },
    });
  }

  protected loadSummary(): void {
    this.summaryLoading.set(true);
    this.summaryError.set(null);

    this.trainings.getMemberAttendance(this.id(), this.selectedYear()).subscribe({
      next: (summary) => {
        this.summary.set(summary);
        this.summaryLoading.set(false);
      },
      error: (error: unknown) => {
        this.summaryLoading.set(false);
        this.summaryError.set(
          apiErrorMessage(error, 'The attendance figures could not be loaded.'),
        );
      },
    });
  }

  protected loadHistory(): void {
    this.historyRequested = true;
    this.historyLoading.set(true);
    this.historyError.set(null);

    this.payments.getMemberHistory(this.id()).subscribe({
      next: (history) => {
        this.history.set(history);
        this.historyLoading.set(false);
      },
      error: (error: unknown) => {
        this.historyLoading.set(false);
        this.historyError.set(apiErrorMessage(error, 'The payment history could not be loaded.'));
      },
    });
  }

  protected loadBeltsAndNotes(): void {
    this.beltsRequested = true;
    this.beltsLoading.set(true);
    this.beltsError.set(null);

    this.members.getBelts(this.id()).subscribe({
      next: (belts) => {
        this.belts.set(belts);
        this.beltsLoading.set(false);
      },
      error: (error: unknown) => {
        this.beltsLoading.set(false);
        this.beltsError.set(apiErrorMessage(error, 'The belt history could not be loaded.'));
      },
    });

    this.notes.getForMember(this.id()).subscribe({
      next: (notes) => this.memberNotes.set(notes),
      error: () => this.memberNotes.set([]),
    });

    if (this.isCoach() && this.beltCatalogue().length === 0) {
      this.common.getBelts().subscribe({ next: (belts) => this.beltCatalogue.set(belts) });
    }
  }

  /** Each tab pays for its own data, and only once. */
  protected onTabChange(index: number): void {
    if (index === 1 && !this.historyRequested) this.loadHistory();
    if (index === 2 && !this.beltsRequested) this.loadBeltsAndNotes();
  }

  protected onYearChange(): void {
    this.loadSummary();
  }

  // --- Presentation -------------------------------------------------------------------------

  protected readonly attendanceOf = (status: AttendanceStatus) =>
    ATTENDANCE_STATUS_PRESENTATION[status];

  protected readonly trainingStatusOf = (status: number) =>
    TRAINING_STATUS_PRESENTATION[status as keyof typeof TRAINING_STATUS_PRESENTATION];

  protected methodOf(payment: Payment) {
    return PAYMENT_METHOD_PRESENTATION[
      payment.isPaidOnline ? PaymentMethod.Online : PaymentMethod.Cash
    ];
  }

  /** Grouped bars: how many sessions they were at, and how many they missed, each month. */
  protected readonly attendanceChart = computed<ChartConfiguration['data']>(() => {
    const months = this.summary()?.perMonth ?? [];

    return {
      labels: months.map((m) => `${MONTH_LABELS[m.month - 1]} ${String(m.year).slice(2)}`),
      datasets: [
        {
          label: 'Present',
          data: months.map((m) => m.present),
          backgroundColor: toneColour('positive'),
          borderRadius: 6,
          maxBarThickness: 28,
        },
        {
          label: 'Absent',
          data: months.map((m) => m.absent),
          backgroundColor: toneColour('critical'),
          borderRadius: 6,
          maxBarThickness: 28,
        },
      ],
    };
  });

  protected readonly attendanceChartLabel = computed(() => {
    const months = this.summary()?.perMonth ?? [];
    if (months.length === 0) return 'Attendance per month: no sessions recorded.';

    return `Attendance per month. ${months
      .map(
        (m) => `${MONTH_LABELS[m.month - 1]} ${m.year}: ${m.present} present, ${m.absent} absent`,
      )
      .join('. ')}.`;
  });

  protected readonly splitChart = computed<ChartConfiguration['data']>(() => {
    const summary = this.summary();
    const present = summary?.presentCount ?? 0;
    const absent = summary?.absentCount ?? 0;
    // Invited but never reported either way — worth showing rather than folding into absent.
    const unreported = Math.max(0, (summary?.invitedCount ?? 0) - present - absent);

    return {
      labels: ['Present', 'Absent', 'Not reported'],
      datasets: [
        {
          data: [present, absent, unreported],
          backgroundColor: [toneColour('positive'), toneColour('critical'), toneColour('quiet')],
          ...doughnutDefaults(),
        },
      ],
    };
  });

  protected readonly splitChartLabel = computed(() => {
    const summary = this.summary();
    if (!summary) return 'Attendance split.';

    return `Attendance split: ${summary.presentCount} present, ${summary.absentCount} absent, out of ${summary.invitedCount} invitations.`;
  });

  /** Only sessions the coach actually scored — an unscored training is not a zero. */
  private readonly scored = computed(() =>
    (this.summary()?.trainings ?? []).filter((t) => t.performance !== null).reverse(),
  );

  protected readonly performanceChart = computed<ChartConfiguration['data']>(() => {
    const scored = this.scored();

    return {
      labels: scored.map((t) => new DatePipe('en-GB').transform(t.date, 'd MMM') ?? ''),
      datasets: [
        {
          label: 'Performance',
          data: scored.map((t) => t.performance as number),
          borderColor: chartColour(0),
          backgroundColor: chartColour(0),
          ...lineDefaults(),
        },
      ],
    };
  });

  protected readonly performanceChartLabel = computed(() => {
    const scored = this.scored();
    if (scored.length === 0) return 'Performance over time: no sessions have been scored yet.';

    const average = scored.reduce((sum, t) => sum + (t.performance ?? 0), 0) / scored.length;
    return `Performance over ${scored.length} scored sessions, averaging ${average.toFixed(1)} out of 10.`;
  });

  protected readonly averagePerformance = computed(() => {
    const scored = this.scored();
    if (scored.length === 0) return null;
    return scored.reduce((sum, t) => sum + (t.performance ?? 0), 0) / scored.length;
  });

  protected readonly barOptions: ChartConfiguration['options'] = {
    scales: { x: { stacked: false }, y: { beginAtZero: true, ticks: { precision: 0 } } },
  };

  protected readonly doughnutOptions: ChartConfiguration['options'] = { scales: {} };

  protected readonly lineOptions: ChartConfiguration['options'] = {
    scales: { y: { min: 0, max: 10, ticks: { stepSize: 2 } } },
  };

  // --- Actions ------------------------------------------------------------------------------

  protected async edit(): Promise<void> {
    const current = this.member();
    if (!current) return;

    const { EditMemberDialog } = await import('./edit-member-dialog');

    const updated = await this.dialog
      .open(EditMemberDialog, { data: current })
      .afterClosed()
      .toPromise();

    if (updated) {
      this.member.set(updated);
      this.snackBar.open('Details saved.', 'Dismiss', { duration: 4000 });
    }
  }

  protected async addBelt(): Promise<void> {
    const current = this.member();
    if (!current) return;

    const { AddBeltDialog } = await import('./add-belt-dialog');

    const added = await this.dialog
      .open(AddBeltDialog, {
        data: {
          memberId: current.id,
          memberName: `${current.firstName} ${current.lastName}`,
          belts: this.beltCatalogue(),
        },
      })
      .afterClosed()
      .toPromise();

    if (added) {
      this.snackBar.open('Belt exam recorded.', 'Dismiss', { duration: 4000 });
      // The current belt on the header may have moved, so refetch both.
      this.loadBeltsAndNotes();
      this.loadMember();
    }
  }

  protected removeBelt(belt: MemberBelt): void {
    const data: ConfirmDialogData = {
      title: 'Remove this belt exam?',
      message: `The ${belt.belt.beltName} exam recorded for ${new DatePipe('en-GB').transform(
        belt.dateReceived,
        'd MMMM y',
      )} will be deleted. This cannot be undone.`,
      confirmLabel: 'Remove',
      destructive: true,
    };

    this.dialog
      .open(ConfirmDialog, { data })
      .afterClosed()
      .subscribe((confirmed) => {
        if (confirmed !== true) return;

        this.members.deleteBelt(this.id(), belt.id).subscribe({
          next: () => {
            this.belts.update((rows) => rows.filter((row) => row.id !== belt.id));
            this.loadMember();
            this.snackBar.open('Belt exam removed.', 'Dismiss', { duration: 4000 });
          },
          error: (error: unknown) => {
            this.snackBar.open(
              apiErrorMessage(error, 'The belt exam could not be removed.'),
              'Dismiss',
              { duration: 6000 },
            );
          },
        });
      });
  }

  protected async addNote(): Promise<void> {
    const current = this.member();
    if (!current) return;

    const { NoteFormDialog } = await import('../notes/note-form-dialog');

    const created = await this.dialog
      .open(NoteFormDialog, {
        data: {
          toMember: { id: current.id, fullName: `${current.firstName} ${current.lastName}` },
        },
      })
      .afterClosed()
      .toPromise();

    if (created) {
      this.snackBar.open('Note added.', 'Dismiss', { duration: 4000 });
      this.notes.getForMember(this.id()).subscribe({ next: (n) => this.memberNotes.set(n) });
    }
  }

  protected removeNote(note: Note): void {
    const data: ConfirmDialogData = {
      title: 'Delete this note?',
      message: `"${note.title}" will be removed permanently. This cannot be undone.`,
      confirmLabel: 'Delete',
      destructive: true,
    };

    this.dialog
      .open(ConfirmDialog, { data })
      .afterClosed()
      .subscribe((confirmed) => {
        if (confirmed !== true) return;

        this.notes.delete(note.id).subscribe({
          next: () => {
            this.memberNotes.update((rows) => rows.filter((row) => row.id !== note.id));
            this.snackBar.open('Note deleted.', 'Dismiss', { duration: 4000 });
          },
          error: (error: unknown) => {
            this.snackBar.open(
              apiErrorMessage(error, 'The note could not be deleted.'),
              'Dismiss',
              {
                duration: 6000,
              },
            );
          },
        });
      });
  }

  /** A member may delete only their own notes; a coach may delete any in their club. */
  protected canDeleteNote(note: Note): boolean {
    return this.isCoach() || note.fromMemberId === this.auth.currentUser()?.id;
  }

  protected uploadPhoto(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    this.uploading.set(true);

    this.photos.upload(this.id(), file).subscribe({
      next: () => {
        this.uploading.set(false);
        // Refetch rather than patching: the server owns the new publicId.
        this.loadMember();
        this.snackBar.open('Photo updated.', 'Dismiss', { duration: 4000 });
      },
      error: (error: unknown) => {
        this.uploading.set(false);
        this.snackBar.open(apiErrorMessage(error, 'The photo could not be uploaded.'), 'Dismiss', {
          duration: 6000,
        });
      },
      complete: () => (input.value = ''),
    });
  }

  protected deactivate(): void {
    const current = this.member();
    if (!current) return;

    const data: ConfirmDialogData = {
      title: `Deactivate ${current.firstName} ${current.lastName}?`,
      message:
        'They will not be able to sign in, and they stop appearing as someone you can invite ' +
        'to a training. Their attendance, payments, belts and notes are all kept.',
      confirmLabel: 'Deactivate',
      destructive: true,
    };

    this.dialog
      .open(ConfirmDialog, { data })
      .afterClosed()
      .subscribe((confirmed) => {
        if (confirmed !== true) return;

        this.members.deactivate(current.id).subscribe({
          next: (updated) => {
            this.member.set(updated);
            this.snackBar.open('Member deactivated.', 'Dismiss', { duration: 4000 });
          },
          error: (error: unknown) => {
            this.snackBar.open(
              apiErrorMessage(error, 'The member could not be deactivated.'),
              'Dismiss',
              { duration: 6000 },
            );
          },
        });
      });
  }
}
