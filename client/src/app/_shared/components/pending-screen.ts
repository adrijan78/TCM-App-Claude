import { Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';
import { PageHeader } from './page-header';
import { Skeleton } from './skeleton';

/**
 * Temporary target for the feature routes whose screens land in phase 9. It exists so the
 * routing shell, the guards and the two side menus can be wired and tested now, rather than
 * every route pointing at nothing.
 *
 * It shows the page furniture the real screen will have — header, then a skeleton where the
 * content goes — so navigating the app before phase 9 gives an honest sense of its shape
 * rather than a wall of identical "coming soon" cards.
 *
 * Each of these routes is replaced by its real component as its phase lands; nothing here
 * should survive phase 9.
 */
@Component({
  selector: 'app-pending-screen',
  imports: [MatIconModule, PageHeader, Skeleton],
  template: `
    <app-page-header [title]="title()" subtitle="This screen is built in phase 9 of the plan." />

    <div class="pending tcm-panel">
      <p class="pending-flag">
        <mat-icon aria-hidden="true">construction</mat-icon>
        <span>Not built yet</span>
      </p>

      <app-skeleton [rowCount]="4" />
    </div>
  `,
  styles: `
    .pending {
      display: flex;
      flex-direction: column;
      gap: var(--tcm-space-4);
    }

    .pending-flag {
      display: inline-flex;
      align-items: center;
      align-self: flex-start;
      gap: var(--tcm-space-2);
      margin: 0;
      padding: var(--tcm-space-1) var(--tcm-space-3);
      border-radius: var(--tcm-radius-pill);
      background: var(--tcm-caution-container);
      color: var(--tcm-on-caution-container);
      font: var(--mat-sys-label-medium);
    }

    .pending-flag mat-icon {
      inline-size: 1rem;
      block-size: 1rem;
      font-size: 1rem;
    }
  `,
})
export class PendingScreen {
  private readonly route = inject(ActivatedRoute);

  protected readonly title = toSignal(
    this.route.data.pipe(map((data) => (data['title'] as string) ?? 'Coming soon')),
    { initialValue: 'Coming soon' },
  );
}
