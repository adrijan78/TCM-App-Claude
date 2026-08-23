import { Component, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { debounceTime, distinctUntilChanged, startWith, switchMap } from 'rxjs';
import { NoteService } from '../../_services/note.service';
import { MemberService } from '../../_services/member.service';
import { apiErrorMessage } from '../../_services/unwrap';
import { Member } from '../../_models/member.model';
import { Note } from '../../_models/note.model';
import { ConfirmDialog, ConfirmDialogData } from '../../_shared/components/confirm-dialog';
import { NoteCard } from '../../_shared/components/note-card';
import { PageHeader } from '../../_shared/components/page-header';
import { StatePanel } from '../../_shared/components/state-panel';

/**
 * SPEC section 6.8 — the club-wide notes page. Coach only.
 *
 * Search runs on the server, debounced, so the list stays correct for a club with hundreds
 * of notes rather than filtering whatever happened to be fetched first.
 */
@Component({
  selector: 'app-notes',
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    PageHeader,
    StatePanel,
    NoteCard,
  ],
  templateUrl: './notes.html',
  styleUrl: './notes.scss',
})
export class Notes {
  private readonly notes = inject(NoteService);
  private readonly members = inject(MemberService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly search = new FormControl('', { nonNullable: true });

  protected readonly rows = signal<Note[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  /** Only needed when the dialog opens, but fetched once up front so it opens instantly. */
  private readonly memberList = signal<readonly Member[]>([]);

  protected readonly searchTerm = toSignal(this.search.valueChanges, { initialValue: '' });

  constructor() {
    this.search.valueChanges
      .pipe(
        startWith(this.search.value),
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((term) => {
          this.loading.set(true);
          this.error.set(null);
          return this.notes.getClubNotes(term);
        }),
        takeUntilDestroyed(),
      )
      .subscribe({
        next: (notes) => {
          // Server order is High first, then newest (SPEC 6.8). Never re-sort here.
          this.rows.set(notes);
          this.loading.set(false);
        },
        error: (error: unknown) => {
          this.loading.set(false);
          this.error.set(apiErrorMessage(error, 'The notes could not be loaded.'));
        },
      });

    this.members.getMembers().subscribe({
      next: (members) => this.memberList.set(members),
      error: () => this.memberList.set([]),
    });
  }

  protected reload(): void {
    // Re-emitting the current term re-runs the switchMap above.
    this.search.setValue(this.search.value, { emitEvent: true });
  }

  protected clearSearch(): void {
    this.search.setValue('');
  }

  protected async addNote(): Promise<void> {
    const { NoteFormDialog } = await import('./note-form-dialog');

    const created = await this.dialog
      .open(NoteFormDialog, { data: { members: this.memberList() } })
      .afterClosed()
      .toPromise();

    if (created) {
      this.snackBar.open('Note added.', 'Dismiss', { duration: 4000 });
      this.reload();
    }
  }

  protected remove(note: Note): void {
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
        // Dismissing by backdrop or Escape resolves undefined, which must count as "no".
        if (confirmed !== true) return;

        this.notes.delete(note.id).subscribe({
          next: () => {
            this.rows.update((rows) => rows.filter((row) => row.id !== note.id));
            this.snackBar.open('Note deleted.', 'Dismiss', { duration: 4000 });
          },
          error: (error: unknown) => {
            this.snackBar.open(
              apiErrorMessage(error, 'The note could not be deleted.'),
              'Dismiss',
              { duration: 6000 },
            );
          },
        });
      });
  }
}
