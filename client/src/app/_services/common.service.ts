import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map, shareReplay } from 'rxjs';
import { environment } from '../../environments/environment';
import { ApiResponse } from '../_models/api-response.model';
import { ClubNumbersInfo } from '../_models/common.model';
import { Belt } from '../_models/member.model';
import { Role } from '../_models/auth.model';
import { unwrap } from './unwrap';

/**
 * The reference data every screen borrows: the belt ladder, the role list, and the club
 * figures behind SPEC section 6.2's stat cards.
 *
 * Belts and roles are cached for the life of the session — they change about once a decade,
 * and without this every form that opens a belt dropdown would re-fetch the same nine rows.
 */
@Injectable({ providedIn: 'root' })
export class CommonService {
  private readonly http = inject(HttpClient);

  private belts$?: Observable<Belt[]>;
  private roles$?: Observable<Role[]>;

  /** Ordered by rank on the server, lowest first. */
  getBelts(): Observable<Belt[]> {
    this.belts$ ??= this.http
      .get<ApiResponse<Belt[]>>(`${environment.apiUrl}/common/belts`)
      .pipe(map(unwrap), shareReplay({ bufferSize: 1, refCount: false }));

    return this.belts$;
  }

  /** Coach-only on the server — it populates the registration form's Role dropdown. */
  getRoles(): Observable<Role[]> {
    this.roles$ ??= this.http
      .get<ApiResponse<Role[]>>(`${environment.apiUrl}/roles`)
      .pipe(map(unwrap), shareReplay({ bufferSize: 1, refCount: false }));

    return this.roles$;
  }

  getClubNumbers(year?: number | null, month?: number | null): Observable<ClubNumbersInfo> {
    let params = new HttpParams();
    if (year) params = params.set('year', year);
    if (month) params = params.set('month', month);

    return this.http
      .get<ApiResponse<ClubNumbersInfo>>(`${environment.apiUrl}/common/club-numbers`, { params })
      .pipe(map(unwrap));
  }

  /**
   * Drops the cached lookups. Called on sign-out so the next account does not inherit the
   * previous one's data — roles in particular are coach-only.
   */
  clearCache(): void {
    this.belts$ = undefined;
    this.roles$ = undefined;
  }
}
