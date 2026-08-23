import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../environments/environment';
import { ApiResponse } from '../_models/api-response.model';
import { TrainingStatus, TrainingType } from '../_models/enums';
import {
  EditTraining,
  MemberAttendanceSummary,
  ReportAttendance,
  SetPerformance,
  Training,
  TrainingAttendee,
  TrainingDetails,
} from '../_models/training.model';
import { unwrap } from './unwrap';

export interface TrainingFilter {
  title?: string | null;
  status?: TrainingStatus | null;
  type?: TrainingType | null;
}

/** Trainings, the calendar feed, invitations and attendance (SPEC 6.4, 6.5 and 6.6). */
@Injectable({ providedIn: 'root' })
export class TrainingService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/trainings`;

  /** The table view. Coach only. */
  getTrainings(filter: TrainingFilter = {}): Observable<Training[]> {
    let params = new HttpParams();
    if (filter.title) params = params.set('title', filter.title);
    // Both enums start at 0, so a plain truthiness test would silently drop the first value.
    if (filter.status !== null && filter.status !== undefined) {
      params = params.set('status', filter.status);
    }
    if (filter.type !== null && filter.type !== undefined) {
      params = params.set('type', filter.type);
    }

    return this.http.get<ApiResponse<Training[]>>(this.base, { params }).pipe(map(unwrap));
  }

  /** The calendar view of the same rows, for one month. Coach only. */
  getCalendar(year?: number | null, month?: number | null): Observable<Training[]> {
    let params = new HttpParams();
    if (year) params = params.set('year', year);
    if (month) params = params.set('month', month);

    return this.http
      .get<ApiResponse<Training[]>>(`${this.base}/calendar`, { params })
      .pipe(map(unwrap));
  }

  /** Open to an invited member as well as the coach; the server decides which. */
  getDetails(id: number): Observable<TrainingDetails> {
    return this.http.get<ApiResponse<TrainingDetails>>(`${this.base}/${id}`).pipe(map(unwrap));
  }

  create(training: EditTraining): Observable<TrainingDetails> {
    return this.http.post<ApiResponse<TrainingDetails>>(this.base, training).pipe(map(unwrap));
  }

  update(id: number, training: EditTraining): Observable<TrainingDetails> {
    return this.http
      .put<ApiResponse<TrainingDetails>>(`${this.base}/${id}`, training)
      .pipe(map(unwrap));
  }

  delete(id: number): Observable<void> {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/${id}`).pipe(map(() => undefined));
  }

  /**
   * Reports attendance. Omitting `memberId` reports for the caller, which is the only thing a
   * member is permitted to do — the coach may name anyone in the training.
   */
  reportAttendance(id: number, report: ReportAttendance): Observable<TrainingAttendee> {
    return this.http
      .post<ApiResponse<TrainingAttendee>>(`${this.base}/${id}/attendance`, report)
      .pipe(map(unwrap));
  }

  /** Coach only — a member may not score anyone, including themselves. */
  setPerformance(
    id: number,
    memberId: string,
    performance: SetPerformance,
  ): Observable<TrainingAttendee> {
    return this.http
      .put<ApiResponse<TrainingAttendee>>(
        `${this.base}/${id}/attendance/${memberId}/performance`,
        performance,
      )
      .pipe(map(unwrap));
  }

  /** Everything behind the three charts on a member's profile (SPEC 6.4). */
  getMemberAttendance(memberId: string, year?: number | null): Observable<MemberAttendanceSummary> {
    let params = new HttpParams();
    if (year) params = params.set('year', year);

    return this.http
      .get<ApiResponse<MemberAttendanceSummary>>(`${this.base}/member/${memberId}/attendance`, {
        params,
      })
      .pipe(map(unwrap));
  }
}
