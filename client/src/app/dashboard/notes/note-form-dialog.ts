import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { NoteService } from '../../_services/note.service';
import { apiErrorParts } from '../../_services/unwrap';
import { Member } from '../../_models/member.model';
import { Note } from '../../_models/note.model';
import { NOTE_PRIORITY_LABELS, NotePriority } from '../../_models/enums';
import { FormAlert } from '../../_shared/components/form-alert';

export interface NoteFormData {
  /** Pre-selected recipient. When set, the member picker is replaced by a fixed label. */
  readonly toMember?: { readonly id: string; readonly fullName: string };
  /** Candidates for the picker. Ignored when `toMember` is set. */
  readonly members?: readonly Member[];
  /** Attaches the note to a training (SPEC 6.6). */
  readonly trainingId?: number | null;
}

/**
 * Writing a note (SPEC 6.4, 6.6 and 6.8).
 *
 * The author is never asked for — the server takes it from the token — and the dialog closes
 * with the created `Note` so the caller can drop it into its list without refetching.
 */
@Component({
  selector: 'app-note-form-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    FormAlert,
  ],
  template: `
    <h2 mat-dialog-title>New note</h2>

    <mat-dialog-content>
      <form class="note-form" [formGroup]="form" (ngSubmit)="save()" novalidate>
        @if (error(); as message) {
          <app-form-alert [message]="message" [details]="errorDetails()" />
        }

        @if (data.toMember) {
          <p class="note-about">
            About <strong>{{ data.toMember.fullName }}</strong>
          </p>
        } @else {
          <mat-form-field appearance="outline">
            <mat-label>About</mat-label>
            <mat-select formControlName="toMemberId" required>
              @for (member of data.members ?? []; track member.id) {
                <mat-option [value]="member.id">
                  {{ member.firstName }} {{ member.lastName }}
                </mat-option>
              }
            </mat-select>
            @if (form.controls.toMemberId.hasError('required')) {
              <mat-error>Choose who this note is about.</mat-error>
            }
          </mat-form-field>
        }

        <mat-form-field appearance="outline">
          <mat-label>Title</mat-label>
          <input matInput formControlName="title" maxlength="120" required />
          <mat-hint align="end">{{ form.controls.title.value.length }}/120</mat-hint>
          @if (form.controls.title.hasError('required')) {
            <mat-error>Give the note a title.</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Priority</mat-label>
          <mat-select formControlName="priority" required>
            @for (option of priorities; track option.value) {
              <mat-option [value]="option.value">{{ option.label }}</mat-option>
            }
          </mat-select>
          <mat-hint>High notes sort to the top of every list.</mat-hint>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Note</mat-label>
          <textarea matInput formControlName="content" rows="5" required></textarea>
          @if (form.controls.content.hasError('required')) {
            <mat-error>Write the note.</mat-error>
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
        <span>Save note</span>
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .note-form {
      display: flex;
      flex-direction: column;
      gap: var(--tcm-space-2);
      min-inline-size: min(28rem, 70vw);
      padding-block-start: var(--tcm-space-2);
    }

    .note-about {
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
export class NoteFormDialog {
  protected readonly dialogRef = inject(MatDialogRef<NoteFormDialog, Note>);
  protected readonly data = inject<NoteFormData>(MAT_DIALOG_DATA);

  private readonly notes = inject(NoteService);
  private readonly fb = inject(FormBuilder);

  protected readonly priorities = [NotePriority.High, NotePriority.Medium, NotePriority.Low].map(
    (value) => ({ value, label: NOTE_PRIORITY_LABELS[value] }),
  );

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly errorDetails = signal<readonly string[]>([]);

  protected readonly form = this.fb.nonNullable.group({
    toMemberId: [this.data.toMember?.id ?? '', [Validators.required]],
    title: ['', [Validators.required, Validators.maxLength(120)]],
    content: ['', [Validators.required]],
    priority: [NotePriority.Medium, [Validators.required]],
  });

  protected save(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.error.set(null);

    const value = this.form.getRawValue();

    this.notes
      .create({
        title: value.title.trim(),
        content: value.content.trim(),
        priority: value.priority,
        toMemberId: value.toMemberId,
        trainingId: this.data.trainingId ?? null,
      })
      .subscribe({
        next: (note) => this.dialogRef.close(note),
        error: (error: unknown) => {
          this.saving.set(false);

          const failure = apiErrorParts(error, 'The note could not be saved.');
          this.error.set(failure.message);
          this.errorDetails.set(failure.details);
        },
      });
  }
}
