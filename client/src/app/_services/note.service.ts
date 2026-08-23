import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../environments/environment';
import { ApiResponse } from '../_models/api-response.model';
import { CreateNote, Note } from '../_models/note.model';
import { unwrap } from './unwrap';

/**
 * Notes about members (SPEC 6.4, 6.6 and 6.8).
 *
 * The server returns them High priority first, then newest. **Do not re-sort in the
 * component** — SPEC 6.8 asks for that order specifically, and a client-side sort by date
 * would quietly bury the urgent ones.
 */
@Injectable({ providedIn: 'root' })
export class NoteService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/notes`;

  /** The club-wide notes page. Coach only. */
  getClubNotes(search?: string | null): Observable<Note[]> {
    return this.http
      .get<ApiResponse<Note[]>>(this.base, { params: searchParams(search) })
      .pipe(map(unwrap));
  }

  /** A coach sees anyone in their club here; a member sees only their own. */
  getForMember(memberId: string, search?: string | null): Observable<Note[]> {
    return this.http
      .get<ApiResponse<Note[]>>(`${this.base}/member/${memberId}`, { params: searchParams(search) })
      .pipe(map(unwrap));
  }

  getForTraining(trainingId: number, memberId: string, search?: string | null): Observable<Note[]> {
    return this.http
      .get<ApiResponse<Note[]>>(`${this.base}/training/${trainingId}/member/${memberId}`, {
        params: searchParams(search),
      })
      .pipe(map(unwrap));
  }

  /** The author comes from the token, which is why `CreateNote` has no field for it. */
  create(note: CreateNote): Observable<Note> {
    return this.http.post<ApiResponse<Note>>(this.base, note).pipe(map(unwrap));
  }

  /** A coach may delete any note in their club; a member only ones they wrote. */
  delete(id: number): Observable<void> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/${id}`).pipe(map(() => undefined));
  }
}

function searchParams(search?: string | null): HttpParams {
  return search ? new HttpParams().set('search', search) : new HttpParams();
}
