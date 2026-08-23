import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ChartConfiguration } from 'chart.js';
import { AuthService } from '../../_services/auth.service';
import { MemberService } from '../../_services/member.service';
import { NoteService } from '../../_services/note.service';
import { PaymentService } from '../../_services/payment.service';
import { TrainingService } from '../../_services/training.service';
import { apiErrorMessage } from '../../_services/unwrap';
import { Member } from '../../_models/member.model';
import { MemberPaymentHistory } from '../../_models/payment.model';
import { Note } from '../../_models/note.model';
import { MemberAttendanceSummary, MemberTraining } from '../../_models/training.model';
import { AttendanceStatus, TrainingStatus } from '../../_models/enums';
import {
  ATTENDANCE_STATUS_PRESENTATION,
  TRAINING_STATUS_PRESENTATION,
  TRAINING_TYPE_PRESENTATION,
} from '../../_shared/status-presentation';
import { beltColour } from '../../_shared/belt-colour';
import { doughnutDefaults, toneColour } from '../../_shared/chart-theme';
import { ChartComponent } from '../../_shared/components/chart';
import { MemberAvatar } from '../../_shared/components/member-avatar';
import { MembershipBanner } from '../../_shared/components/membership-banner';
import { NoteCard } from '../../_shared/components/note-card';
import { PageHeader } from '../../_shared/components/page-header';
import { StatCard } from '../../_shared/components/stat-card';
import { StatePanel } from '../../_shared/components/state-panel';
import { BeltSwatch, StatusChip } from '../../_shared/components/status-chip';

/** How many notes the preview panel shows before deferring to the profile. */
const NOTE_PREVIEW = 3;

/**
 * SPEC section 5 — "Home dashboard: member, own home page only".
 *
 * The coach's `ClubDetails` answers "how is the club doing"; this answers "what do I have to
 * do". Everything on it is the member's own: the sessions they were invited to, what they
 * still owe an answer on, their membership, their notes. Nothing here is club-wide, and
 * every endpoint it calls is one the server already scopes to the caller.
 *
 * The panels load independently. A member whose payment history fails should still see
 * tomorrow's training, so a failure is contained to the panel that raised it.
 */
@Component({
  selector: 'app-member-home',
  imports: [
    DatePipe,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
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
  templateUrl: './member-home.html',
  styleUrl: './member-home.scss',
})
export class MemberHome {
  private readonly members = inject(MemberService);
  private readonly trainings = inject(TrainingService);
  private readonly payments = inject(PaymentService);
  private readonly notes = inject(NoteService);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);

  /** The signed-in member. Every request below is scoped to this id and no other. */
  protected readonly myId = computed(() => this.auth.currentUser()?.id ?? '');
  protected readonly firstName = computed(() => this.auth.currentUser()?.firstName ?? '');

  // --- My details ----------------------------------------------------------------------------
  protected readonly member = signal<Member | null>(null);

  // --- Trainings, attendance and performance -------------------------------------------------
  protected readonly summary = signal<MemberAttendanceSummary | null>(null);
  protected readonly summaryLoading = signal(true);
  protected readonly summaryError = signal<string | null>(null);

  // --- Membership ----------------------------------------------------------------------------
  protected readonly history = signal<MemberPaymentHistory | null>(null);
  protected readonly historyLoading = signal(true);
  protected readonly historyError = signal<string | null>(null);

  // --- Notes ---------------------------------------------------------------------------------
  protected readonly memberNotes = signal<Note[]>([]);
  protected readonly notesLoading = signal(true);
  protected readonly notesError = signal<string | null>(null);

  /** Recomputed each minute so a tab left open does not sit showing a stale countdown. */
  private readonly tick = signal(Date.now());

  constructor() {
    this.reload();

    const timer = setInterval(() => this.tick.set(Date.now()), 60_000);
    this.destroyRef.onDestroy(() => clearInterval(timer));
  }

  // --- Derived ------------------------------------------------------------------------------

  /**
   * Everything still to come, soonest first. Cancelled sessions stay in the list — a member
   * needs to know a session is off far more than they need a tidy list.
   */
  protected readonly upcoming = computed(() => {
    const now = this.tick();

    return (this.summary()?.trainings ?? [])
      .filter((training) => new Date(training.date).getTime() > now)
      .sort((a, b) => new Date(a.date).getTime() - new Date(b.date).getTime())
      .slice(0, 6);
  });

  /** Sessions that have been and gone, newest first. */
  protected readonly recent = computed(() => {
    const now = this.tick();

    return (this.summary()?.trainings ?? [])
      .filter((training) => new Date(training.date).getTime() <= now)
      .sort((a, b) => new Date(b.date).getTime() - new Date(a.date).getTime())
      .slice(0, 5);
  });

  /** Invitations with no answer yet — the one thing this page is actually asking of them. */
  protected readonly awaiting = computed(() => this.upcoming().filter((t) => this.needsReply(t)));

  protected readonly nextSession = computed(
    () => this.upcoming().find((t) => t.trainingStatus === TrainingStatus.Active) ?? null,
  );

  protected readonly countdown = computed(() => {
    const next = this.nextSession();
    if (!next) return null;

    const milliseconds = new Date(next.date).getTime() - this.tick();
    if (milliseconds <= 0) return null;

    const totalMinutes = Math.floor(milliseconds / 60000);

    return {
      days: Math.floor(totalMinutes / 1440),
      hours: Math.floor((totalMinutes % 1440) / 60),
      minutes: totalMinutes % 60,
    };
  });

  /** Only scored sessions count: one the coach never scored is not a zero. */
  private readonly scored = computed(() =>
    (this.summary()?.trainings ?? []).filter((t) => t.performance !== null),
  );

  protected readonly averagePerformance = computed(() => {
    const scored = this.scored();
    if (scored.length === 0) return null;

    return scored.reduce((sum, t) => sum + (t.performance ?? 0), 0) / scored.length;
  });

  // The stat cards take a formatted value or null, and null renders as a dash — so "no
  // sessions yet" reads as no answer rather than as a score of zero.
  protected readonly attendancePercent = computed(() => {
    const summary = this.summary();
    return summary ? summary.attendancePercentage.toFixed(0) : null;
  });

  protected readonly presentCount = computed(() => this.summary()?.presentCount ?? null);

  protected readonly averageScore = computed(() => this.averagePerformance()?.toFixed(1) ?? null);

  protected readonly previewNotes = computed(() => this.memberNotes().slice(0, NOTE_PREVIEW));

  // --- Chart ---------------------------------------------------------------------------------

  protected readonly splitChart = computed<ChartConfiguration['data']>(() => {
    const summary = this.summary();
    const present = summary?.presentCount ?? 0;
    const absent = summary?.absentCount ?? 0;
    // Invited and never answered either way. Worth its own slice rather than folded into absent.
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
    if (!summary || summary.invitedCount === 0) {
      return 'Your attendance: no invitations recorded yet.';
    }

    return `Your attendance: ${summary.presentCount} present, ${summary.absentCount} absent, out of ${summary.invitedCount} invitations.`;
  });

  protected readonly doughnutOptions: ChartConfiguration['options'] = { scales: {} };

  // --- Presentation -------------------------------------------------------------------------

  protected readonly attendanceOf = (training: MemberTraining) =>
    ATTENDANCE_STATUS_PRESENTATION[training.attendanceStatus];

  protected readonly trainingStatusOf = (training: MemberTraining) =>
    TRAINING_STATUS_PRESENTATION[training.trainingStatus];

  protected readonly typeOf = (training: MemberTraining) =>
    TRAINING_TYPE_PRESENTATION[training.trainingType];

  /** An invitation to a session that is still going ahead, with no answer given. */
  protected needsReply(training: MemberTraining): boolean {
    return (
      training.trainingStatus === TrainingStatus.Active &&
      training.attendanceStatus === AttendanceStatus.Invited
    );
  }

  /**
   * A session that is cancelled or already closed. Those rows show the *training's* state
   * instead of the member's, because "cancelled" is the only thing worth reading there.
   */
  protected notGoingAhead(training: MemberTraining): boolean {
    return training.trainingStatus !== TrainingStatus.Active;
  }

  /**
   * The rail colour for an upcoming session: what the row is *asking of you* if anything,
   * otherwise the state it is already in. It is the same decision the row's chip makes, so
   * the spine and the chip can never disagree.
   */
  protected railFor(training: MemberTraining): string {
    if (this.notGoingAhead(training)) return `var(--tcm-${this.trainingStatusOf(training).tone})`;
    if (this.needsReply(training)) return 'var(--tcm-caution)';

    return `var(--tcm-${this.attendanceOf(training).tone})`;
  }

  /** The member's own belt, ringing the identity strip. */
  protected readonly beltRail = computed(() => beltColour(this.member()?.currentBelt?.beltName));

  // --- Loading ------------------------------------------------------------------------------

  protected reload(): void {
    this.loadMember();
    this.loadSummary();
    this.loadMembership();
    this.loadNotes();
  }

  private loadMember(): void {
    // Best-effort and deliberately quiet: it supplies the photo and belt beside the greeting,
    // and the page reads perfectly well without them.
    this.members.getMember(this.myId()).subscribe({
      next: (member) => this.member.set(member),
      error: () => this.member.set(null),
    });
  }

  protected loadSummary(): void {
    this.summaryLoading.set(true);
    this.summaryError.set(null);

    this.trainings.getMemberAttendance(this.myId()).subscribe({
      next: (summary) => {
        this.summary.set(summary);
        this.summaryLoading.set(false);
      },
      error: (error: unknown) => {
        this.summaryLoading.set(false);
        this.summaryError.set(apiErrorMessage(error, 'Your trainings could not be loaded.'));
      },
    });
  }

  protected loadMembership(): void {
    this.historyLoading.set(true);
    this.historyError.set(null);

    this.payments.getMemberHistory(this.myId()).subscribe({
      next: (history) => {
        this.history.set(history);
        this.historyLoading.set(false);
      },
      error: (error: unknown) => {
        this.historyLoading.set(false);
        this.historyError.set(apiErrorMessage(error, 'Your membership could not be loaded.'));
      },
    });
  }

  protected loadNotes(): void {
    this.notesLoading.set(true);
    this.notesError.set(null);

    this.notes.getForMember(this.myId()).subscribe({
      next: (notes) => {
        this.memberNotes.set(notes);
        this.notesLoading.set(false);
      },
      error: (error: unknown) => {
        this.notesLoading.set(false);
        this.notesError.set(apiErrorMessage(error, 'Your notes could not be loaded.'));
      },
    });
  }
}
