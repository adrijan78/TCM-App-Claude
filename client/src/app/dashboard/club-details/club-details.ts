import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, FormControl, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ChartConfiguration } from 'chart.js';
import { debounceTime, distinctUntilChanged, startWith, switchMap } from 'rxjs';
import { CommonService } from '../../_services/common.service';
import { TrainingService } from '../../_services/training.service';
import { MemberService } from '../../_services/member.service';
import { AuthService } from '../../_services/auth.service';
import { apiErrorMessage } from '../../_services/unwrap';
import { ClubNumbersInfo } from '../../_models/common.model';
import { Member } from '../../_models/member.model';
import { Training } from '../../_models/training.model';
import { TrainingStatus } from '../../_models/enums';
import { TRAINING_STATUS_PRESENTATION } from '../../_shared/status-presentation';
import { chartColour } from '../../_shared/chart-theme';
import { ChartComponent } from '../../_shared/components/chart';
import { MemberAvatar } from '../../_shared/components/member-avatar';
import { PageHeader } from '../../_shared/components/page-header';
import { StatePanel } from '../../_shared/components/state-panel';
import { StatCard } from '../../_shared/components/stat-card';
import { StatusChip } from '../../_shared/components/status-chip';

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
 * SPEC section 6.2 — the club dashboard.
 *
 * The **coach's** landing page. Since Phase 10, `/dashboard` matches `MemberHome` for a
 * member (see `coachHomeMatch`), so this screen is reached by a coach in normal use.
 *
 * The `isCoach()` skips below are kept all the same: three of its pieces — the calendar, the
 * countdown and the quick member search — sit behind coach-only endpoints, and a session
 * whose role cannot be read should show the club figures alone rather than fill with 403s.
 */
@Component({
  selector: 'app-club-details',
  imports: [
    DatePipe,
    RouterLink,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatButtonModule,
    MatIconModule,
    PageHeader,
    StatePanel,
    StatCard,
    StatusChip,
    MemberAvatar,
    ChartComponent,
  ],
  templateUrl: './club-details.html',
  styleUrl: './club-details.scss',
})
export class ClubDetails {
  private readonly common = inject(CommonService);
  private readonly trainings = inject(TrainingService);
  private readonly members = inject(MemberService);
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly isCoach = this.auth.isCoach;
  protected readonly firstName = computed(() => this.auth.currentUser()?.firstName ?? '');

  protected readonly months = MONTH_LABELS.map((label, index) => ({ value: index + 1, label }));
  protected readonly years = Array.from(
    { length: 6 },
    (_, index) => new Date().getFullYear() - index,
  );

  protected readonly filters = this.fb.nonNullable.group({
    year: this.fb.nonNullable.control<number | null>(null),
    month: this.fb.nonNullable.control<number | null>(null),
  });

  protected readonly numbers = signal<ClubNumbersInfo | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  // --- Calendar and countdown (coach only) ---------------------------------------------------
  protected readonly calendar = signal<Training[]>([]);
  protected readonly calendarLoading = signal(false);

  /**
   * The next session still to happen. Active only: a session already marked Finished or
   * Cancelled is not something to count down to, whatever its date says.
   */
  protected readonly nextTraining = computed(() => this.upcoming()[0] ?? null);

  /** Recomputed each minute so the countdown does not sit frozen on an open tab. */
  private readonly tick = signal(Date.now());

  protected readonly countdown = computed(() => {
    const next = this.nextTraining();
    if (!next) return null;

    const milliseconds = new Date(next.date).getTime() - this.tick();
    if (milliseconds <= 0) return null;

    const totalMinutes = Math.floor(milliseconds / 60000);
    const days = Math.floor(totalMinutes / 1440);
    const hours = Math.floor((totalMinutes % 1440) / 60);
    const minutes = totalMinutes % 60;

    return { days, hours, minutes };
  });

  protected readonly upcoming = computed(() => {
    // Reading `tick` keeps this honest on a tab left open past a session's start time.
    const now = this.tick();

    return this.calendar()
      .filter((t) => t.status === TrainingStatus.Active && new Date(t.date).getTime() > now)
      .sort((a, b) => new Date(a.date).getTime() - new Date(b.date).getTime())
      .slice(0, 5);
  });

  // --- Quick member search (coach only) ------------------------------------------------------
  protected readonly memberSearch = new FormControl('', { nonNullable: true });
  protected readonly matches = signal<Member[]>([]);
  protected readonly searching = signal(false);

  // --- Chart ---------------------------------------------------------------------------------
  protected readonly trainingsChart = computed<ChartConfiguration['data']>(() => {
    const perMonth = this.numbers()?.trainingsPerMonth ?? [];

    return {
      labels: perMonth.map((m) => `${MONTH_LABELS[m.month - 1]} ${String(m.year).slice(2)}`),
      datasets: [
        {
          label: 'Trainings held',
          data: perMonth.map((m) => m.count),
          backgroundColor: chartColour(0),
          borderRadius: 6,
          maxBarThickness: 40,
        },
      ],
    };
  });

  protected readonly trainingsChartLabel = computed(() => {
    const perMonth = this.numbers()?.trainingsPerMonth ?? [];
    if (perMonth.length === 0) return 'Trainings per month: no sessions recorded yet.';

    const total = perMonth.reduce((sum, m) => sum + m.count, 0);
    return `Trainings per month, ${total} in total. ${perMonth
      .map((m) => `${MONTH_LABELS[m.month - 1]} ${m.year}: ${m.count}`)
      .join('. ')}.`;
  });

  protected readonly chartOptions: ChartConfiguration['options'] = {
    plugins: { legend: { display: false } },
    scales: { y: { beginAtZero: true, ticks: { precision: 0 } } },
  };

  constructor() {
    this.filters.valueChanges
      .pipe(
        startWith(this.filters.getRawValue()),
        debounceTime(150),
        switchMap(() => {
          this.loading.set(true);
          this.error.set(null);

          const { year, month } = this.filters.getRawValue();
          this.loadCalendar(year, month);

          return this.common.getClubNumbers(year, month);
        }),
        takeUntilDestroyed(),
      )
      .subscribe({
        next: (numbers) => {
          this.numbers.set(numbers);
          this.loading.set(false);
        },
        error: (error: unknown) => {
          this.loading.set(false);
          this.error.set(apiErrorMessage(error, "The club's figures could not be loaded."));
        },
      });

    this.memberSearch.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe((term) => this.runSearch(term));

    // One interval for the countdown, so an open tab does not sit showing "in 3 hours"
    // an hour later. A minute is fine: nothing here is measured in seconds.
    const timer = setInterval(() => this.tick.set(Date.now()), 60_000);
    this.destroyRef.onDestroy(() => clearInterval(timer));
  }

  protected statusOf(training: Training) {
    return TRAINING_STATUS_PRESENTATION[training.status];
  }

  protected reload(): void {
    this.filters.setValue(this.filters.getRawValue());
  }

  protected clearFilters(): void {
    this.filters.reset({ year: null, month: null });
  }

  protected clearSearch(): void {
    this.memberSearch.setValue('');
  }

  private loadCalendar(year: number | null, month: number | null): void {
    if (!this.isCoach()) return;

    this.calendarLoading.set(true);

    this.trainings.getCalendar(year, month).subscribe({
      next: (trainings) => {
        this.calendar.set(trainings);
        this.calendarLoading.set(false);
      },
      // The calendar is a supporting panel, not the page. A failure here should not replace
      // the club's figures with an error.
      error: () => {
        this.calendar.set([]);
        this.calendarLoading.set(false);
      },
    });
  }

  private runSearch(term: string): void {
    const query = term.trim();

    if (!this.isCoach() || query.length < 2) {
      this.matches.set([]);
      this.searching.set(false);
      return;
    }

    this.searching.set(true);

    this.members.getMembers({ search: query }).subscribe({
      next: (members) => {
        this.matches.set(members.slice(0, 6));
        this.searching.set(false);
      },
      error: () => {
        this.matches.set([]);
        this.searching.set(false);
      },
    });
  }
}
