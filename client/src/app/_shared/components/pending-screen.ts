import { Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';

/**
 * Temporary target for feature routes whose screens land in phases 8 and 9. It exists so the
 * routing shell, the guards and the two side menus can be wired and tested now, rather than
 * every route pointing at nothing.
 *
 * Each of these routes is replaced by its real component as its phase lands; nothing here
 * should survive Phase 9.
 */
@Component({
  selector: 'app-pending-screen',
  imports: [MatIconModule],
  template: `
    <section class="pending">
      <mat-icon class="pending-icon">construction</mat-icon>
      <h1>{{ title() }}</h1>
      <p>This screen is built in a later phase of the plan.</p>
    </section>
  `,
  styles: `
    .pending {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.75rem;
      padding: 3rem 1rem;
      text-align: center;
      color: var(--mat-sys-on-surface-variant);
    }

    .pending-icon {
      inline-size: 3rem;
      block-size: 3rem;
      font-size: 3rem;
    }

    h1 {
      margin: 0;
      font: var(--mat-sys-headline-small);
      color: var(--mat-sys-on-surface);
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
