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
    :host {
      display: inline-grid;
      place-items: center;
      inline-size: var(--avatar-size);
      block-size: var(--avatar-size);
      border-radius: 50%;
      overflow: hidden;
      flex: none;
      background: var(--mat-sys-primary-container);
      color: var(--mat-sys-on-primary-container);
    }

    .avatar-img {
      inline-size: 100%;
      block-size: 100%;
      object-fit: cover;
    }

    .avatar-fallback {
      font: var(--mat-sys-label-medium);
      font-weight: 600;
      /* Scale the initials with the avatar so one component covers 32px and 96px. */
      font-size: calc(var(--avatar-size) * 0.38);
      line-height: 1;
    }
  `,
  host: { '[style.--avatar-size.px]': 'size()' },
})
export class MemberAvatar {
  private readonly photos = inject(PhotoService);
  private readonly destroyRef = inject(DestroyRef);

  readonly firstName = input('');
  readonly lastName = input('');
  readonly photoPublicId = input<string | null>(null);
  readonly size = input(40);

  protected readonly objectUrl = signal<string | null>(null);

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
