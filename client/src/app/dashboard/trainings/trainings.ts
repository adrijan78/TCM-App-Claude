import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { debounceTime, startWith, switchMap } from 'rxjs';
import { TrainingService } from '../../_services/training.service';
import { MemberService } from '../../_services/member.service';
import { apiErrorMessage } from '../../_services/unwrap';
import { Member } from '../../_models/member.model';
import { Training } from '../../_models/training.model';
import {
  TRAINING_STATUS_LABELS,
  TRAINING_TYPE_LABELS,
  TrainingStatus,
  TrainingType,
} from '../../_models/enums';
import {
  TRAINING_STATUS_PRESENTATION,
  TRAINING_TYPE_PRESENTATION,
} from '../../_shared/status-presentation';
import { ConfirmDialog, ConfirmDialogData } from '../../_shared/components/confirm-dialog';
import { PageHeader } from '../../_shared/components/page-header';
import { StatePanel } from '../../_shared/components/state-panel';
import { StatusChip } from '../../_shared/components/status-chip';

/**
 * SPEC section 6.5 — the same trainings as a table and as a calendar.
 *
 * Two views of one list, not two screens: the toggle swaps the presentation and the filters
 * keep applying. The calendar colour-codes each day by the status of the session on it —
 * with a marker as well as a colour, because colour alone fails a colour-blind reader.
 */
@Component({
  selector: 'app-trainings',
  imports: [
    DatePipe,
    RouterLink,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonToggleModule,
    MatDatepickerModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    PageHeader,
    StatePanel,
    StatusChip,
  ],
  templateUrl: './trainings.html',
  styleUrl: './trainings.scss',
})
export class Trainings {
  private readonly trainings = inject(TrainingService);
  private readonly members = inject(MemberService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly fb = inject(FormBuilder);

  protected readonly columns = ['date', 'description', 'type', 'status', 'turnout', 'actions'];

  protected readonly types = [TrainingType.Regular, TrainingType.Sparring].map((value) => ({
    value,
    label: TRAINING_TYPE_LABELS[value],
  }));

  protected readonly statuses = [
    TrainingStatus.Active,
    TrainingStatus.Finished,
    TrainingStatus.Cancelled,
  ].map((value) => ({ value, label: TRAINING_STATUS_LABELS[value] }));

  protected readonly view = signal<'table' | 'calendar'>('table');

  protected readonly filters = this.fb.nonNullable.group({
    title: this.fb.nonNullable.control<string>(''),
    status: this.fb.nonNullable.control<TrainingStatus | null>(null),
    type: this.fb.nonNullable.control<TrainingType | null>(null),
  });

  protected readonly rows = signal<Training[]>([]);
  protected readonly memberList = signal<readonly Member[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly hasFilters = signal(false);

  /** The month the calendar is showing, and the day the coach last clicked. */
  protected readonly selectedDate = signal<Date | null>(null);

  /** Indexed by `yyyy-mm-dd` so `dateClass` can colour a day without scanning the list. */
  private readonly byDay = computed(() => {
    const map = new Map<string, Training[]>();

    for (const training of this.rows()) {
      const key = dayKey(new Date(training.date));
      const existing = map.get(key);
      if (existing) existing.push(training);
      else map.set(key, [training]);
    }

    return map;
  });

  protected readonly selectedDayTrainings = computed(() => {
    const date = this.selectedDate();
    return date ? (this.byDay().get(dayKey(date)) ?? []) : [];
  });

  /**
   * Bound to `MatCalendar`'s `dateClass`. Arrow-bound because Material calls it detached
   * from the component instance.
   */
  protected readonly dateClass = (date: Date): string => {
    const sessions = this.byDay().get(dayKey(date));
    if (!sessions?.length) return '';

    // A day with several sessions takes the most notable one: active outranks finished.
    if (sessions.some((s) => s.status === TrainingStatus.Active)) return 'cal-active';
    if (sessions.some((s) => s.status === TrainingStatus.Finished)) return 'cal-finished';
    return 'cal-cancelled';
  };

  constructor() {
    this.filters.valueChanges
      .pipe(
        startWith(this.filters.getRawValue()),
        debounceTime(300),
        switchMap(() => {
          this.loading.set(true);
          this.error.set(null);

          const value = this.filters.getRawValue();
          this.hasFilters.set(!!value.title || value.status !== null || value.type !== null);

          return this.trainings.getTrainings(value);
        }),
        takeUntilDestroyed(),
      )
      .subscribe({
        next: (trainings) => {
          this.rows.set(trainings);
          this.loading.set(false);
        },
        error: (error: unknown) => {
          this.loading.set(false);
          this.error.set(apiErrorMessage(error, 'The trainings could not be loaded.'));
        },
      });

    this.members.getMembers().subscribe({
      next: (members) => this.memberList.set(members),
      error: () => this.memberList.set([]),
    });
  }

  protected statusOf(training: Training) {
    return TRAINING_STATUS_PRESENTATION[training.status];
  }

  protected typeOf(training: Training) {
    return TRAINING_TYPE_PRESENTATION[training.trainingType];
  }

  protected turnout(training: Training): number {
    return training.invitedCount === 0
      ? 0
      : Math.round((training.presentCount / training.invitedCount) * 100);
  }

  protected reload(): void {
    this.filters.setValue(this.filters.getRawValue());
  }

  protected clearFilters(): void {
    this.filters.reset({ title: '', status: null, type: null });
  }

  protected async openForm(training?: Training, date?: Date | null): Promise<void> {
    const { TrainingFormDialog } = await import('./training-form-dialog');

    // The table row carries counts, not the invitee list, so editing needs the full record.
    const details = training ? await this.trainings.getDetails(training.id).toPromise() : null;

    const saved = await this.dialog
      .open(TrainingFormDialog, {
        data: { members: this.memberList(), training: details, date },
      })
      .afterClosed()
      .toPromise();

    if (saved) {
      this.snackBar.open(training ? 'Training updated.' : 'Training created.', 'Dismiss', {
        duration: 4000,
      });
      this.reload();
    }
  }

  protected remove(training: Training): void {
    const data: ConfirmDialogData = {
      title: 'Delete this training?',
      message:
        `"${training.description}" and the attendance recorded against it will be removed. ` +
        'If the session simply did not happen, setting its status to Cancelled keeps the record.',
      confirmLabel: 'Delete',
      destructive: true,
    };

    this.dialog
      .open(ConfirmDialog, { data })
      .afterClosed()
      .subscribe((confirmed) => {
        if (confirmed !== true) return;

        this.trainings.delete(training.id).subscribe({
          next: () => {
            this.rows.update((rows) => rows.filter((row) => row.id !== training.id));
            this.snackBar.open('Training deleted.', 'Dismiss', { duration: 4000 });
          },
          error: (error: unknown) => {
            this.snackBar.open(
              apiErrorMessage(error, 'The training could not be deleted.'),
              'Dismiss',
              { duration: 6000 },
            );
          },
        });
      });
  }
}

/** Local-date key. Built from the parts, not toISOString, so it does not shift time zone. */
function dayKey(date: Date): string {
  return `${date.getFullYear()}-${date.getMonth() + 1}-${date.getDate()}`;
}
