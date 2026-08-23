import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../environments/environment';
import { ApiResponse } from '../_models/api-response.model';
import {
  AddMemberBelt,
  EditMember,
  Member,
  MemberBelt,
  MemberFilter,
} from '../_models/member.model';
import { unwrap } from './unwrap';

/**
 * The member list (SPEC 6.3) and the member profile with its belt history (SPEC 6.4).
 *
 * Every method here is also authorized on the server: the list and deactivate are coach-only,
 * and reading or editing one member is checked against who the caller is. Nothing on this
 * class is a security boundary — it is the typed shape of the endpoints.
 */
@Injectable({ providedIn: 'root' })
export class MemberService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/members`;

  /** Coach only. All three filters are optional and narrow the query server-side. */
  getMembers(filter: MemberFilter = {}): Observable<Member[]> {
    let params = new HttpParams();
    if (filter.search) params = params.set('search', filter.search);
    if (filter.beltId) params = params.set('beltId', filter.beltId);
    // Age group 0 (Kids) is falsy, so this one has to test for null explicitly.
    if (filter.ageGroup !== null && filter.ageGroup !== undefined) {
      params = params.set('ageGroup', filter.ageGroup);
    }

    return this.http.get<ApiResponse<Member[]>>(this.base, { params }).pipe(map(unwrap));
  }

  getMember(id: string): Observable<Member> {
    return this.http.get<ApiResponse<Member>>(`${this.base}/${id}`).pipe(map(unwrap));
  }

  updateMember(id: string, member: EditMember): Observable<Member> {
    return this.http.put<ApiResponse<Member>>(`${this.base}/${id}`, member).pipe(map(unwrap));
  }

  /**
   * PATCH, not DELETE, and named for what it does: members are deactivated, never removed,
   * because their attendance, payment and note history references the row.
   */
  deactivate(id: string): Observable<Member> {
    return this.http
      .patch<ApiResponse<Member>>(`${this.base}/${id}/deactivate`, {})
      .pipe(map(unwrap));
  }

  getBelts(memberId: string): Observable<MemberBelt[]> {
    return this.http
      .get<ApiResponse<MemberBelt[]>>(`${this.base}/${memberId}/belts`)
      .pipe(map(unwrap));
  }

  addBelt(memberId: string, belt: AddMemberBelt): Observable<MemberBelt> {
    return this.http
      .post<ApiResponse<MemberBelt>>(`${this.base}/${memberId}/belts`, belt)
      .pipe(map(unwrap));
  }

  deleteBelt(memberId: string, beltRecordId: number): Observable<void> {
    return this.http
      .delete<ApiResponse<unknown>>(`${this.base}/${memberId}/belts/${beltRecordId}`)
      .pipe(map(() => undefined));
  }
}
