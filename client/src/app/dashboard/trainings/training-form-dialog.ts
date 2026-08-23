import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { TrainingService } from '../../_services/training.service';
import { apiErrorParts } from '../../_services/unwrap';
import { Member } from '../../_models/member.model';
import { TrainingDetails } from '../../_models/training.model';
import {
  TRAINING_STATUS_LABELS,
  TRAINING_TYPE_LABELS,
  TrainingStatus,
  TrainingType,
} from '../../_models/enums';
import { FormAlert } from '../../_shared/components/form-alert';

export interface TrainingFormData {
  readonly members: readonly Member[];
  /** Absent for a new training; present when editing an existing one. */
  readonly training?: TrainingDetails | null;
  /** Pre-fills the date when the coach clicked a day on the calendar. */
  readonly date?: Date | null;
}

/**
 * SPEC section 6.5 — the add/edit training form, including the invitee list.
 *
 * `memberIds` is the complete invitee list on both create and update, not a delta: the
 * server replaces what it has. Unchecking someone is therefore how you uninvite them.
 */
@Component({
  selector: 'app-training-form-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatCheckboxModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    FormAlert,
  ],
  templateUrl: './training-form-dialog.html',
  styleUrl: './training-form-dialog.scss',
})
export class TrainingFormDialog {
  protected readonly dialogRef = inject(MatDialogRef<TrainingFormDialog, TrainingDetails>);
  protected readonly data = inject<TrainingFormData>(MAT_DIALOG_DATA);

  private readonly trainings = inject(TrainingService);
  private readonly fb = inject(FormBuilder);

  protected readonly isEdit = !!this.data.training;

  protected readonly types = [TrainingType.Regular, TrainingType.Sparring].map((value) => ({
    value,
    label: TRAINING_TYPE_LABELS[value],
  }));

  protected readonly statuses = [
    TrainingStatus.Active,
    TrainingStatus.Finished,
    TrainingStatus.Cancelled,
  ].map((value) => ({ value, label: TRAINING_STATUS_LABELS[value] }));

  /** Only active members can be invited — a deactivated one cannot sign in to report. */
  protected readonly invitable = computed(() =>
    this.data.members.filter((member) => member.isActive),
  );

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly errorDetails = signal<readonly string[]>([]);

  protected readonly form = this.fb.nonNullable.group({
    description: [
      this.data.training?.description ?? '',
      [Validators.required, Validators.maxLength(200)],
    ],
    date: this.fb.nonNullable.control<Date | null>(this.initialDate(), [Validators.required]),
    time: [this.initialTime(), [Validators.required]],
    trainingType: [this.data.training?.trainingType ?? TrainingType.Regular, [Validators.required]],
    status: [this.data.training?.status ?? TrainingStatus.Active, [Validators.required]],
    memberIds: this.fb.nonNullable.control<string[]>(
      this.data.training?.attendees.map((a) => a.memberId) ?? [],
    ),
  });

  protected readonly selectedCount = computed(() => this.form.controls.memberIds.value.length);

  protected inviteEveryone(): void {
    this.form.controls.memberIds.setValue(this.invitable().map((member) => member.id));
  }

  protected inviteNobody(): void {
    this.form.controls.memberIds.setValue([]);
  }

  protected save(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const when = combine(value.date!, value.time);

    this.saving.set(true);
    this.error.set(null);

    const payload = {
      description: value.description.trim(),
      // `Training.Date` is a UTC DateTime on the server (EF Core 10 cannot translate
      // DateTimeOffset.Month in a GroupBy — see CLAUDE.md), so send an ISO instant.
      date: when.toISOString(),
      trainingType: value.trainingType,
      status: value.status,
      memberIds: value.memberIds,
    };

    const request = this.data.training
      ? this.trainings.update(this.data.training.id, payload)
      : this.trainings.create(payload);

    request.subscribe({
      next: (training) => this.dialogRef.close(training),
      error: (error: unknown) => {
        this.saving.set(false);

        const failure = apiErrorParts(error, 'The training could not be saved.');
        this.error.set(failure.message);
        this.errorDetails.set(failure.details);
      },
    });
  }

  private initialDate(): Date | null {
    if (this.data.training) return new Date(this.data.training.date);
    return this.data.date ?? new Date();
  }

  private initialTime(): string {
    const source = this.data.training ? new Date(this.data.training.date) : null;
    if (!source) return '18:00';

    return `${pad(source.getHours())}:${pad(source.getMinutes())}`;
  }
}

function pad(value: number): string {
  return value.toString().padStart(2, '0');
}

/** Merges the date picker's day with the time input's clock reading, in local time. */
function combine(date: Date, time: string): Date {
  const [hours, minutes] = time.split(':').map(Number);
  const merged = new Date(date);
  merged.setHours(hours || 0, minutes || 0, 0, 0);
  return merged;
}
