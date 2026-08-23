import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { AuthService } from '../../_services/auth.service';

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
  { label: 'Trainings', icon: 'sports_martial_arts', link: '/dashboard/trainings', coachOnly: true },
  { label: 'Payments', icon: 'payments', link: '/dashboard/payments', coachOnly: true },
  { label: 'Notes', icon: 'sticky_note_2', link: '/dashboard/notes', coachOnly: true },
  { label: 'My profile', icon: 'person', link: '/dashboard/profile', coachOnly: false },
];

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
  ],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  private readonly auth = inject(AuthService);

  protected readonly fullName = this.auth.fullName;
  protected readonly isCoach = this.auth.isCoach;
  protected readonly sidenavOpen = signal(true);

  /** The two side-menu variants of SPEC section 6.2, from one list. */
  protected readonly navItems = computed(() =>
    NAV_ITEMS.filter((item) => !item.coachOnly || this.isCoach()),
  );

  protected readonly initials = computed(() => {
    const user = this.auth.currentUser();
    if (!user) return '';
    return `${user.firstName.charAt(0)}${user.lastName.charAt(0)}`.toUpperCase();
  });

  protected toggleSidenav(): void {
    this.sidenavOpen.update((open) => !open);
  }

  protected signOut(): void {
    this.auth.logout();
  }
}
