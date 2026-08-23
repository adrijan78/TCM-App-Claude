import { DatePipe } from '@angular/common';
import { Component, computed, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Note } from '../../_models/note.model';
import { NOTE_PRIORITY_PRESENTATION } from '../status-presentation';
import { StatusChip } from './status-chip';

/**
 * One note, as it appears on the club-wide page (SPEC 6.8), a member's profile (6.4) and a
 * training's notes panel (6.6). Three screens, one card — a note should not look like three
 * different things depending on where you found it.
 *
 * `canDelete` is UX only. The server allows a coach to delete any note in their club and a
 * member only their own, and re-checks on every request regardless of what this renders.
 */
@Component({
  selector: 'app-note-card',
  imports: [DatePipe, RouterLink, MatButtonModule, MatIconModule, MatTooltipModule, StatusChip],
  template: `
    <article class="note" [class.note-high]="isHigh()">
      <header class="note-head">
        <app-status-chip
          [label]="priority().label"
          [tone]="priority().tone"
          [icon]="priority().icon"
        />

        @if (canDelete()) {
          <button
            mat-icon-button
            class="note-delete"
            matTooltip="Delete note"
            aria-label="Delete note"
            (click)="remove.emit(note())"
          >
            <mat-icon>delete_outline</mat-icon>
          </button>
        }
      </header>

      <h3 class="note-title">{{ note().title }}</h3>
      <p class="note-content">{{ note().content }}</p>

      <footer class="note-foot">
        @if (showRecipient()) {
          <a class="note-person" [routerLink]="['/dashboard/members', note().toMemberId]">
            <mat-icon aria-hidden="true">person</mat-icon>
            <span>{{ note().toMemberFullName }}</span>
          </a>
        }

        <span class="note-meta">
          <mat-icon aria-hidden="true">edit_note</mat-icon>
          <span>{{ note().fromMemberFullName }}</span>
        </span>

        @if (note().trainingId; as trainingId) {
          <a class="note-person" [routerLink]="['/dashboard/trainings', trainingId]">
            <mat-icon aria-hidden="true">sports_martial_arts</mat-icon>
            <span>{{ note().trainingDescription }}</span>
          </a>
        }

        <time class="note-meta note-date" [attr.datetime]="note().createdAt">
          {{ note().createdAt | date: 'd MMM y' }}
        </time>
      </footer>
    </article>
  `,
  styles: `
    .note {
      display: flex;
      flex-direction: column;
      gap: var(--tcm-space-2);
      block-size: 100%;
      padding: var(--tcm-space-4);
      border: 1px solid var(--tcm-panel-border);
      border-radius: var(--tcm-radius-lg);
      background: var(--tcm-panel-bg);
      box-shadow: var(--tcm-shadow-1);
    }

    /* A second, non-colour cue that this one is urgent — the chip already says so, but a
       wall of cards needs to be scannable without reading every chip. */
    .note-high {
      border-inline-start: 3px solid var(--tcm-priority-high);
    }

    .note-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--tcm-space-2);
    }

    .note-delete {
      margin: calc(var(--tcm-space-2) * -1);
      color: var(--mat-sys-on-surface-variant);
    }

    .note-title {
      margin: 0;
      font: var(--mat-sys-title-medium);
      overflow-wrap: anywhere;
    }

    .note-content {
      margin: 0;
      flex: 1;
      color: var(--mat-sys-on-surface-variant);
      white-space: pre-wrap;
      overflow-wrap: anywhere;
    }

    .note-foot {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--tcm-space-1) var(--tcm-space-3);
      padding-block-start: var(--tcm-space-2);
      border-block-start: 1px solid var(--tcm-panel-border);
      font: var(--mat-sys-label-medium);
    }

    .note-person,
    .note-meta {
      display: inline-flex;
      align-items: center;
      gap: var(--tcm-space-1);
      min-inline-size: 0;
    }

    .note-meta {
      color: var(--mat-sys-on-surface-variant);
    }

    .note-date {
      margin-inline-start: auto;
    }

    mat-icon {
      inline-size: 1rem;
      block-size: 1rem;
      font-size: 1rem;
    }
  `,
})
export class NoteCard {
  readonly note = input.required<Note>();
  readonly canDelete = input(false);
  /** Off on a member's own profile, where every note is about them and saying so is noise. */
  readonly showRecipient = input(true);

  readonly remove = output<Note>();

  protected readonly priority = computed(() => NOTE_PRIORITY_PRESENTATION[this.note().priority]);
  protected readonly isHigh = computed(() => this.priority().tone === 'critical');
}
