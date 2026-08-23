import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { FormsModule } from '@angular/forms';
import { TrainingService } from '../../_services/training.service';
import { MemberService } from '../../_services/member.service';
import { AuthService } from '../../_services/auth.service';
import { apiErrorMessage } from '../../_services/unwrap';
import { TrainingAttendee, TrainingDetails } from '../../_models/training.model';
import { AttendanceStatus } from '../../_models/enums';
import {
  ATTENDANCE_STATUS_PRESENTATION,
  TRAINING_STATUS_PRESENTATION,
  TRAINING_TYPE_PRESENTATION,
} from '../../_shared/status-presentation';
import { ConfirmDialog, ConfirmDialogData } from '../../_shared/components/confirm-dialog';
import { MemberAvatar } from '../../_shared/components/member-avatar';
import { PageHeader } from '../../_shared/components/page-header';
import { StatePanel } from '../../_shared/components/state-panel';
import { StatusChip } from '../../_shared/components/status-chip';

/**
 * SPEC section 6.6 — one training, its invitees, and what each of them reported.
 *
 * Both roles reach this screen: a coach for any session in their club, an invited member for
 * the one they were invited to — that is what the invitation email links to. What each can
 * do differs, and the difference is enforced on the server:
 *
 * - reporting attendance for **yourself** is open to everyone;
 * - reporting it for **someone else**, and scoring anyone at all, is coach-only.
 *
 * The controls below follow that split, but hiding a button is not the protection. The API
 * refuses the call either way, and Phase 10 verifies that it does.
 */
@Component({
  selector: 'app-training-details',
  imports: [
    DatePipe,
    RouterLink,
    FormsModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    PageHeader,
    StatePanel,
    StatusChip,
    MemberAvatar,
  ],
  templateUrl: './training-details.html',
  styleUrl: './training-details.scss',
})
export class TrainingDetailsScreen {
  private readonly trainings = inject(TrainingService);
  private readonly members = inject(MemberService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly id = input.required<number>();

  protected readonly isCoach = this.auth.isCoach;
  protected readonly myId = computed(() => this.auth.currentUser()?.id ?? '');

  protected readonly training = signal<TrainingDetails | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  /** Rows currently mid-save, so a row can show progress without freezing the whole table. */
  protected readonly saving = signal<ReadonlySet<string>>(new Set());

  protected readonly columns = computed(() =>
    this.isCoach() ? ['member', 'status', 'reason', 'performance'] : ['member', 'status', 'reason'],
  );

  protected readonly scores = Array.from({ length: 10 }, (_, index) => index + 1);

  protected readonly attendees = computed(() => this.training()?.attendees ?? []);

  protected readonly presentCount = computed(
    () => this.attendees().filter((a) => a.status === AttendanceStatus.Present).length,
  );

  protected readonly absentCount = computed(
    () => this.attendees().filter((a) => a.status === AttendanceStatus.Absent).length,
  );

  protected readonly turnout = computed(() => {
    const total = this.attendees().length;
    return total === 0 ? 0 : Math.round((this.presentCount() / total) * 100);
  });

  protected readonly averageScore = computed(() => {
    const scored = this.attendees().filter((a) => a.performance !== null);
    if (scored.length === 0) return null;
    return scored.reduce((sum, a) => sum + (a.performance ?? 0), 0) / scored.length;
  });

  constructor() {
    // `id` is a signal input, so it is not readable until after construction.
    effect(() => {
      const id = this.id();
      untracked(() => this.load());
      void id;
    });
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.trainings.getDetails(this.id()).subscribe({
      next: (training) => {
        this.training.set(training);
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.loading.set(false);
        this.error.set(apiErrorMessage(error, 'This training could not be opened.'));
      },
    });
  }

  // --- Presentation -------------------------------------------------------------------------

  protected readonly statusOf = (attendee: TrainingAttendee) =>
    ATTENDANCE_STATUS_PRESENTATION[attendee.status];

  protected trainingStatus() {
    const training = this.training();
    return training ? TRAINING_STATUS_PRESENTATION[training.status] : null;
  }

  protected trainingType() {
    const training = this.training();
    return training ? TRAINING_TYPE_PRESENTATION[training.trainingType] : null;
  }

  /** A member may report only their own attendance; a coach may report anyone's. */
  protected canReport(attendee: TrainingAttendee): boolean {
    return this.isCoach() || attendee.memberId === this.myId();
  }

  protected isSaving(attendee: TrainingAttendee): boolean {
    return this.saving().has(attendee.memberId);
  }

  // --- Actions ------------------------------------------------------------------------------

  protected report(attendee: TrainingAttendee, status: AttendanceStatus): void {
    if (!this.canReport(attendee) || this.isSaving(attendee)) return;

    this.markSaving(attendee.memberId, true);

    this.trainings
      .reportAttendance(this.id(), {
        // Omitted for yourself: that is the only shape a member is allowed to send, and
        // sending your own id explicitly would need coach rights on the server.
        memberId: attendee.memberId === this.myId() ? null : attendee.memberId,
        status,
        absenceReason: status === AttendanceStatus.Absent ? attendee.absenceReason : null,
      })
      .subscribe({
        next: (updated) => this.applyRow(updated),
        error: (error: unknown) => this.reportFailure(error, 'The attendance could not be saved.'),
        complete: () => this.markSaving(attendee.memberId, false),
      });
  }

  protected saveReason(attendee: TrainingAttendee, reason: string): void {
    const trimmed = reason.trim();
    if (!this.canReport(attendee) || trimmed === (attendee.absenceReason ?? '')) return;

    this.markSaving(attendee.memberId, true);

    this.trainings
      .reportAttendance(this.id(), {
        memberId: attendee.memberId === this.myId() ? null : attendee.memberId,
        status: attendee.status,
        absenceReason: trimmed || null,
      })
      .subscribe({
        next: (updated) => this.applyRow(updated),
        error: (error: unknown) => this.reportFailure(error, 'The reason could not be saved.'),
        complete: () => this.markSaving(attendee.memberId, false),
      });
  }

  protected setPerformance(attendee: TrainingAttendee, performance: number): void {
    if (!this.isCoach()) return;

    this.markSaving(attendee.memberId, true);

    this.trainings.setPerformance(this.id(), attendee.memberId, { performance }).subscribe({
      next: (updated) => this.applyRow(updated),
      error: (error: unknown) => this.reportFailure(error, 'The score could not be saved.'),
      complete: () => this.markSaving(attendee.memberId, false),
    });
  }

  protected async edit(): Promise<void> {
    const current = this.training();
    if (!current) return;

    const { TrainingFormDialog } = await import('./training-form-dialog');
    const members = await this.members.getMembers().toPromise();

    const saved = await this.dialog
      .open(TrainingFormDialog, { data: { members: members ?? [], training: current } })
      .afterClosed()
      .toPromise();

    if (saved) {
      this.training.set(saved);
      this.snackBar.open('Training updated.', 'Dismiss', { duration: 4000 });
    }
  }

  protected async addNote(attendee: TrainingAttendee): Promise<void> {
    const { NoteFormDialog } = await import('../notes/note-form-dialog');

    const created = await this.dialog
      .open(NoteFormDialog, {
        data: {
          toMember: {
            id: attendee.memberId,
            fullName: `${attendee.firstName} ${attendee.lastName}`,
          },
          // Attaching it to this session is what SPEC 6.6's notes panel is for.
          trainingId: this.id(),
        },
      })
      .afterClosed()
      .toPromise();

    if (created) {
      this.snackBar.open('Note added.', 'Dismiss', { duration: 4000 });
    }
  }

  protected remove(): void {
    const current = this.training();
    if (!current) return;

    const data: ConfirmDialogData = {
      title: 'Delete this training?',
      message:
        `"${current.description}" and the attendance recorded against it will be removed. ` +
        'If the session simply did not happen, setting its status to Cancelled keeps the record.',
      confirmLabel: 'Delete',
      destructive: true,
    };

    this.dialog
      .open(ConfirmDialog, { data })
      .afterClosed()
      .subscribe((confirmed) => {
        if (confirmed !== true) return;

        this.trainings.delete(current.id).subscribe({
          next: () => {
            this.snackBar.open('Training deleted.', 'Dismiss', { duration: 4000 });
            void window.history.back();
          },
          error: (error: unknown) =>
            this.reportFailure(error, 'The training could not be deleted.'),
        });
      });
  }

  // --- Plumbing -----------------------------------------------------------------------------

  private applyRow(updated: TrainingAttendee): void {
    this.training.update((training) =>
      training
        ? {
            ...training,
            attendees: training.attendees.map((a) =>
              a.memberId === updated.memberId ? updated : a,
            ),
          }
        : training,
    );
  }

  private markSaving(memberId: string, busy: boolean): void {
    this.saving.update((current) => {
      const next = new Set(current);
      if (busy) next.add(memberId);
      else next.delete(memberId);
      return next;
    });
  }

  private reportFailure(error: unknown, fallback: string): void {
    this.snackBar.open(apiErrorMessage(error, fallback), 'Dismiss', { duration: 6000 });
  }

  protected readonly AttendanceStatus = AttendanceStatus;
}
