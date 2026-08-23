import { Component, computed, effect, inject, signal } from '@angular/core';
import {
  ActivatedRouteSnapshot,
  NavigationEnd,
  Router,
  RouterLink,
  RouterLinkActive,
  RouterOutlet,
} from '@angular/router';
import { BreakpointObserver } from '@angular/cdk/layout';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { filter, map, startWith } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AuthService } from '../../_services/auth.service';
import { MemberService } from '../../_services/member.service';
import { ThemeService } from '../../_services/theme.service';
import { beltColour } from '../belt-colour';
import { BrandMark } from '../components/brand-mark';

interface NavItem {
  readonly label: string;
  readonly icon: string;
  readonly link: string;
  /** Coach-only entries are simply absent from a member's menu (SPEC section 6.2). */
  readonly coachOnly: boolean;
}

const NAV_ITEMS: readonly NavItem[] = [
  { label: 'Home', icon: 'dashboard', link: '/dashboard', coachOnly: false },
  { label: 'Members', icon: 'group', link: '/dashboard/members', coachOnly: true },
  {
    label: 'Trainings',
    icon: 'sports_martial_arts',
    link: '/dashboard/trainings',
    coachOnly: true,
  },
  { label: 'Payments', icon: 'payments', link: '/dashboard/payments', coachOnly: true },
  { label: 'Notes', icon: 'sticky_note_2', link: '/dashboard/notes', coachOnly: true },
  { label: 'My profile', icon: 'person', link: '/dashboard/profile', coachOnly: false },
];

const RAIL_KEY = 'tcm.rail-collapsed';
const HANDSET = '(max-width: 959.98px)';

@Component({
  selector: 'app-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatSidenavModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    MatMenuModule,
    MatTooltipModule,
    BrandMark,
  ],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  private readonly auth = inject(AuthService);
  private readonly members = inject(MemberService);
  private readonly theme = inject(ThemeService);
  private readonly router = inject(Router);
  private readonly breakpoints = inject(BreakpointObserver);

  protected readonly fullName = this.auth.fullName;
  protected readonly isCoach = this.auth.isCoach;
  protected readonly themeMode = this.theme.mode;
  protected readonly isDark = computed(() => this.theme.resolved() === 'dark');

  /**
   * Below the breakpoint the drawer floats over the content and starts closed; above it, it
   * is part of the layout and is always present — either full width or as an icon rail.
   */
  protected readonly isHandset = toSignal(
    this.breakpoints.observe(HANDSET).pipe(map((state) => state.matches)),
    { initialValue: false },
  );

  protected readonly drawerOpen = signal(false);
  protected readonly railCollapsed = signal(this.restoreRail());

  protected readonly drawerMode = computed(() => (this.isHandset() ? 'over' : 'side'));
  protected readonly drawerOpened = computed(() => (this.isHandset() ? this.drawerOpen() : true));

  /** Icons only, with tooltips. Never on handset — there the drawer is already an overlay. */
  protected readonly showLabels = computed(() => this.isHandset() || !this.railCollapsed());

  /**
   * The current route's own title, so a phone user — who cannot see which nav item is
   * highlighted — still knows where they are.
   *
   * Read off the router's snapshot rather than from `Title`. Angular's title strategy is
   * itself a NavigationEnd subscriber, so `Title.getTitle()` here returns the *previous*
   * page's title whenever this subscription happens to run first.
   */
  protected readonly pageTitle = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      startWith(null),
      map(() => deepestTitle(this.router.routerState.snapshot.root)),
    ),
    { initialValue: '' },
  );

  /** The two side-menu variants of SPEC section 6.2, from one list. */
  protected readonly navItems = computed(() =>
    NAV_ITEMS.filter((item) => !item.coachOnly || this.isCoach()),
  );

  protected readonly initials = computed(() => {
    const user = this.auth.currentUser();
    if (!user) return '';
    return `${user.firstName.charAt(0)}${user.lastName.charAt(0)}`.toUpperCase();
  });

  /**
   * The signed-in member's belt, ringing their initials in the toolbar.
   *
   * The token carries no belt — it is not an authorization fact — so this is one fetch per
   * session, and a failure simply leaves the ring off.
   */
  private readonly ownBelt = signal<string | null>(null);
  protected readonly beltColour = computed(() =>
    this.ownBelt() ? beltColour(this.ownBelt()) : 'transparent',
  );

  constructor() {
    const id = this.auth.currentUser()?.id;
    if (id) {
      this.members
        .getMember(id)
        .pipe(takeUntilDestroyed())
        .subscribe({
          next: (member) => this.ownBelt.set(member.currentBelt?.beltName ?? null),
          error: () => this.ownBelt.set(null),
        });
    }

    // Closing the overlay after a navigation: leaving it open would cover the page the user
    // just asked for.
    this.router.events.pipe(filter((event) => event instanceof NavigationEnd)).subscribe(() => {
      if (this.isHandset()) this.drawerOpen.set(false);
    });

    effect(() => {
      try {
        localStorage.setItem(RAIL_KEY, String(this.railCollapsed()));
      } catch {
        // Storage blocked: the rail still works, it just forgets between visits.
      }
    });
  }

  protected toggleDrawer(): void {
    if (this.isHandset()) {
      this.drawerOpen.update((open) => !open);
    } else {
      this.railCollapsed.update((collapsed) => !collapsed);
    }
  }

  protected toggleTheme(): void {
    this.theme.toggle();
  }

  protected useSystemTheme(): void {
    this.theme.set('system');
  }

  protected signOut(): void {
    this.auth.logout();
  }

  private restoreRail(): boolean {
    try {
      return localStorage.getItem(RAIL_KEY) === 'true';
    } catch {
      return false;
    }
  }
}

/** The title of the deepest activated route — the leaf is the one that names the page. */
function deepestTitle(route: ActivatedRouteSnapshot): string {
  let current = route;
  while (current.firstChild) {
    current = current.firstChild;
  }

  return current.title ?? '';
}
