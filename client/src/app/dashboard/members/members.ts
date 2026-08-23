import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { debounceTime, startWith, switchMap } from 'rxjs';
import { MemberService } from '../../_services/member.service';
import { CommonService } from '../../_services/common.service';
import { apiErrorMessage } from '../../_services/unwrap';
import { Belt, Member } from '../../_models/member.model';
import { AGE_GROUP_LABELS, AgeGroup } from '../../_models/enums';
import { beltColour } from '../../_shared/belt-colour';
import { ConfirmDialog, ConfirmDialogData } from '../../_shared/components/confirm-dialog';
import { BeltSwatch, StatusChip } from '../../_shared/components/status-chip';
import { MemberAvatar } from '../../_shared/components/member-avatar';
import { PageHeader } from '../../_shared/components/page-header';
import { StatePanel } from '../../_shared/components/state-panel';

/**
 * SPEC section 6.3 — the coach's member list with its three filters.
 *
 * All three filter server-side. Filtering a fetched array would go wrong the moment the club
 * outgrows one page of members, and the API already does the work.
 */
@Component({
  selector: 'app-members',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    PageHeader,
    StatePanel,
    StatusChip,
    BeltSwatch,
    MemberAvatar,
  ],
  templateUrl: './members.html',
  styleUrl: './members.scss',
})
export class Members {
  private readonly members = inject(MemberService);
  private readonly common = inject(CommonService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly fb = inject(FormBuilder);

  protected readonly columns = ['member', 'belt', 'age', 'status', 'actions'];

  /** The rail down a member's row is the belt they currently hold. */
  protected beltRail(member: Member): string {
    return beltColour(member.currentBelt?.beltName);
  }

  protected readonly ageGroups = [
    AgeGroup.Kids,
    AgeGroup.Cadets,
    AgeGroup.Juniors,
    AgeGroup.Seniors,
  ].map((value) => ({ value, label: AGE_GROUP_LABELS[value] }));

  protected readonly filters = this.fb.nonNullable.group({
    search: this.fb.nonNullable.control<string>(''),
    beltId: this.fb.nonNullable.control<number | null>(null),
    ageGroup: this.fb.nonNullable.control<AgeGroup | null>(null),
  });

  protected readonly rows = signal<Member[]>([]);
  protected readonly belts = signal<Belt[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly hasFilters = signal(false);

  protected readonly activeCount = computed(() => this.rows().filter((row) => row.isActive).length);

  constructor() {
    this.filters.valueChanges
      .pipe(
        startWith(this.filters.getRawValue()),
        // Long enough that typing a name is one request, short enough to feel immediate.
        debounceTime(300),
        switchMap(() => {
          this.loading.set(true);
          this.error.set(null);

          const value = this.filters.getRawValue();
          this.hasFilters.set(!!value.search || value.beltId !== null || value.ageGroup !== null);

          return this.members.getMembers(value);
        }),
        takeUntilDestroyed(),
      )
      .subscribe({
        next: (members) => {
          this.rows.set(members);
          this.loading.set(false);
        },
        error: (error: unknown) => {
          this.loading.set(false);
          this.error.set(apiErrorMessage(error, 'The member list could not be loaded.'));
        },
      });

    this.common.getBelts().subscribe({
      next: (belts) => this.belts.set(belts),
      error: () => this.belts.set([]),
    });
  }

  protected reload(): void {
    this.filters.setValue(this.filters.getRawValue());
  }

  protected clearFilters(): void {
    this.filters.reset({ search: '', beltId: null, ageGroup: null });
  }

  /**
   * SPEC section 6.3 and the house rule in CLAUDE.md: members are deactivated, never deleted.
   * The confirmation says so, because "deactivate" reads like a soft word for delete and the
   * coach should know their history is safe.
   */
  protected deactivate(member: Member): void {
    const data: ConfirmDialogData = {
      title: `Deactivate ${member.firstName} ${member.lastName}?`,
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

        this.members.deactivate(member.id).subscribe({
          next: (updated) => {
            this.rows.update((rows) => rows.map((row) => (row.id === updated.id ? updated : row)));
            this.snackBar.open(`${updated.firstName} ${updated.lastName} deactivated.`, 'Dismiss', {
              duration: 4000,
            });
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
