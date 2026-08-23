import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../environments/environment';
import { ApiResponse } from '../_models/api-response.model';
import { Photo } from '../_models/member.model';
import { unwrap } from './unwrap';

/**
 * Member photos, stored as bytes in SQL Server (decided 2026-08-22, superseding SPEC section
 * 2's Firebase Storage choice).
 *
 * The read endpoint is authenticated on purpose, and an `<img src>` cannot carry a bearer
 * token — so the bytes are fetched through `HttpClient` and bound as an object URL.
 * **Whoever creates an object URL owns revoking it**; use `<app-member-avatar>` rather than
 * calling `getContent` directly, because it does that for you on destroy.
 */
@Injectable({ providedIn: 'root' })
export class PhotoService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/photos`;

  /** The raw bytes. The caller must `URL.revokeObjectURL` whatever it makes of them. */
  getContent(publicId: string): Observable<Blob> {
    return this.http.get(`${this.base}/${publicId}`, { responseType: 'blob' });
  }

  /** Fetches and wraps in an object URL, ready to bind to an `img`. Caller revokes. */
  getObjectUrl(publicId: string): Observable<string> {
    return this.getContent(publicId).pipe(map((blob) => URL.createObjectURL(blob)));
  }

  upload(memberId: string, file: File): Observable<Photo> {
    const body = new FormData();
    body.append('file', file, file.name);

    return this.http
      .post<ApiResponse<Photo>>(`${this.base}/member/${memberId}`, body)
      .pipe(map(unwrap));
  }

  delete(publicId: string): Observable<void> {
    return this.http
      .delete<ApiResponse<unknown>>(`${this.base}/${publicId}`)
      .pipe(map(() => undefined));
  }
}
