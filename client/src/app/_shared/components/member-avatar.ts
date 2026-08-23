import {
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { PhotoService } from '../../_services/photo.service';
import { beltColour } from '../belt-colour';

/**
 * A member's photo, falling back to their initials.
 *
 * This exists because member photos are behind an authenticated endpoint: `<img src>` cannot
 * send a bearer token, so the bytes have to be fetched through `HttpClient` and bound as an
 * object URL. An object URL that is never revoked is a leak that holds the whole image in
 * memory for the life of the tab — a member list of forty photos is forty leaks — so the
 * revoking lives here, once, instead of in every screen that shows a face.
 */
@Component({
  selector: 'app-member-avatar',
  template: `
    @if (objectUrl(); as url) {
      <img class="avatar-img" [src]="url" [alt]="name()" [width]="size()" [height]="size()" />
    } @else {
      <span class="avatar-fallback" aria-hidden="true">{{ initials() }}</span>
      <span class="tcm-visually-hidden">{{ name() }}</span>
    }
  `,
  styles: `
    /*
      The ring is the member's belt. It is the design's signature at its smallest size, and
      it is the reason a coach can scan a list of forty faces and see the grading spread
      without reading a word. No belt on record leaves the ring off entirely.
    */
    :host {
      display: inline-grid;
      place-items: center;
      inline-size: var(--avatar-size);
      block-size: var(--avatar-size);
      border-radius: 50%;
      flex: none;
      background: var(--mat-sys-primary-container);
      color: var(--mat-sys-on-primary-container);
      box-shadow:
        0 0 0 var(--avatar-ring-width) var(--tcm-panel-bg),
        0 0 0 calc(var(--avatar-ring-width) + var(--avatar-belt-width)) var(--avatar-belt);
    }

    .avatar-img,
    .avatar-fallback {
      /* The clip lives on the children: a ring drawn on :host would be cut off by overflow. */
      border-radius: 50%;
      overflow: hidden;
    }

    .avatar-img {
      inline-size: 100%;
      block-size: 100%;
      object-fit: cover;
    }

    .avatar-fallback {
      display: grid;
      place-items: center;
      inline-size: 100%;
      block-size: 100%;
      font-family: var(--tcm-font-mono);
      font-weight: 600;
      /* Scale the initials with the avatar so one component covers 32px and 96px. */
      font-size: calc(var(--avatar-size) * 0.34);
      line-height: 1;
    }
  `,
  host: {
    '[style.--avatar-size.px]': 'size()',
    '[style.--avatar-belt]': 'belt()',
    '[style.--avatar-belt-width.px]': 'beltWidth()',
    '[style.--avatar-ring-width.px]': 'ringWidth()',
  },
})
export class MemberAvatar {
  private readonly photos = inject(PhotoService);
  private readonly destroyRef = inject(DestroyRef);

  readonly firstName = input('');
  readonly lastName = input('');
  readonly photoPublicId = input<string | null>(null);
  readonly size = input(40);

  /** The member's current belt. Left unset, the avatar simply has no ring. */
  readonly beltName = input<string | null>(null);

  protected readonly objectUrl = signal<string | null>(null);

  protected readonly belt = computed(() =>
    this.beltName() ? beltColour(this.beltName()) : 'transparent',
  );

  /** The ring scales with the avatar, or a 96px profile photo would wear a hairline. */
  protected readonly beltWidth = computed(() => (this.beltName() ? Math.max(2, Math.round(this.size() * 0.055)) : 0));

  /** A gap in the panel's own colour, so the belt reads as a separate band. */
  protected readonly ringWidth = computed(() => (this.beltName() ? 2 : 0));

  protected readonly name = computed(() => `${this.firstName()} ${this.lastName()}`.trim());

  protected readonly initials = computed(() =>
    `${this.firstName().charAt(0)}${this.lastName().charAt(0)}`.toUpperCase(),
  );

  constructor() {
    effect(() => {
      // The photo id is the *only* thing this effect reacts to. Everything else runs
      // untracked: `release()` reads `objectUrl`, and the fetch below writes it, so without
      // this the effect would depend on its own output and refetch the photo forever.
      const id = this.photoPublicId();

      untracked(() => {
        // Whatever we were showing is about to be replaced or dropped either way.
        this.release();

        if (!id) return;

        this.photos.getObjectUrl(id).subscribe({
          next: (url) => this.objectUrl.set(url),
          // A missing or forbidden photo is not worth a message; initials are a fine answer.
          error: () => this.objectUrl.set(null),
        });
      });
    });

    this.destroyRef.onDestroy(() => this.release());
  }

  private release(): void {
    const current = this.objectUrl();
    if (current) {
      URL.revokeObjectURL(current);
      this.objectUrl.set(null);
    }
  }
}
