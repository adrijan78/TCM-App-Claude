import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, map, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { ApiResponse } from '../_models/api-response.model';
import {
  ForgotPasswordRequest,
  LoginRequest,
  MemberToken,
  MemberRegisterRequest,
  RegisteredMember,
  ResetPasswordRequest,
} from '../_models/auth.model';
import { unwrap } from './unwrap';

const STORAGE_KEY = 'tcm.session';

/**
 * The single owner of the session. Everything that needs to know who is signed in reads it
 * from here rather than decoding the token itself.
 *
 * The role held here drives what the UI offers. It is **not** a security boundary: the
 * server re-checks every request, and a member who edits their own storage gains nothing
 * but a menu they will be refused from (SPEC section 7).
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly session = signal<MemberToken | null>(this.restore());

  readonly currentUser = this.session.asReadonly();
  readonly isAuthenticated = computed(() => this.session() !== null && !this.isExpired());
  readonly isCoach = computed(() => this.session()?.roles.includes('Coach') ?? false);
  readonly fullName = computed(() => {
    const user = this.session();
    return user ? `${user.firstName} ${user.lastName}` : '';
  });

  get token(): string | null {
    return this.session()?.token ?? null;
  }

  login(request: LoginRequest): Observable<MemberToken> {
    return this.http
      .post<ApiResponse<MemberToken>>(`${environment.apiUrl}/account/login`, request)
      .pipe(
        map(unwrap),
        tap((user) => this.store(user)),
      );
  }

  /**
   * Coach-only (SPEC section 6.1). Returns the created member's details and no token —
   * registering someone does not sign you in as them, and must not disturb the coach's
   * own session.
   */
  register(request: MemberRegisterRequest): Observable<RegisteredMember> {
    return this.http
      .post<ApiResponse<RegisteredMember>>(`${environment.apiUrl}/account/register`, request)
      .pipe(map(unwrap));
  }

  forgotPassword(request: ForgotPasswordRequest): Observable<string> {
    return this.http
      .post<ApiResponse<unknown>>(`${environment.apiUrl}/account/forgot-password`, request)
      .pipe(map((response) => response.message ?? 'Check your inbox.'));
  }

  resetPassword(request: ResetPasswordRequest): Observable<string> {
    return this.http
      .post<ApiResponse<unknown>>(`${environment.apiUrl}/account/reset-password`, request)
      .pipe(map((response) => response.message ?? 'Your password has been changed.'));
  }

  logout(redirectTo: string | null = '/login'): void {
    this.session.set(null);
    localStorage.removeItem(STORAGE_KEY);

    if (redirectTo) {
      void this.router.navigateByUrl(redirectTo);
    }
  }

  /** True when there is no session, or the one we have has run out. */
  isExpired(): boolean {
    const user = this.session();
    if (!user) return true;

    const expiry = Date.parse(user.expiresAt);
    return Number.isNaN(expiry) ? false : expiry <= Date.now();
  }

  private store(user: MemberToken): void {
    this.session.set(user);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(user));
  }

  private restore(): MemberToken | null {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;

    try {
      const user = JSON.parse(raw) as MemberToken;
      // A stored session that has already expired is worse than none: it would let the shell
      // render a signed-in chrome that every request then bounces.
      if (user?.token && Date.parse(user.expiresAt) > Date.now()) {
        return user;
      }
    } catch {
      // Corrupt or hand-edited storage. Treat it as signed out.
    }

    localStorage.removeItem(STORAGE_KEY);
    return null;
  }
}
