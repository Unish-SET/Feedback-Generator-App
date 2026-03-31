import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import {
  AdminSurveyListItem, AdminSurveyDetail, AdminStats,
  PaginatedResult, ApiResponse
} from '../../shared/models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AdminSurveyService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/admin/surveys`;

  // GET /api/admin/surveys → { success, data: PaginatedResult<AdminSurveyListDto> }
  getAll(params?: {
    search?: string;
    isDeleted?: boolean;
    fromDate?: string;
    toDate?: string;
    pageNumber?: number;
    pageSize?: number;
  }): Observable<PaginatedResult<AdminSurveyListItem>> {
    let httpParams = new HttpParams();
    if (params?.search)                       httpParams = httpParams.set('search', params.search);
    if (params?.isDeleted != null)            httpParams = httpParams.set('isDeleted', String(params.isDeleted));
    if (params?.fromDate)                     httpParams = httpParams.set('fromDate', params.fromDate);
    if (params?.toDate)                       httpParams = httpParams.set('toDate', params.toDate);
    if (params?.pageNumber != null)           httpParams = httpParams.set('pageNumber', params.pageNumber);
    if (params?.pageSize != null)             httpParams = httpParams.set('pageSize', params.pageSize);
    return this.http.get<ApiResponse<PaginatedResult<AdminSurveyListItem>>>(this.base, { params: httpParams })
      .pipe(map(r => r.data));
  }

  // GET /api/admin/surveys/stats → { success, data: AdminStatsDto }
  getStats(): Observable<AdminStats> {
    return this.http.get<ApiResponse<AdminStats>>(`${this.base}/stats`)
      .pipe(map(r => r.data));
  }

  // GET /api/admin/surveys/{id} → { success, data: AdminSurveyDetailDto }
  getDetail(id: number): Observable<AdminSurveyDetail> {
    return this.http.get<ApiResponse<AdminSurveyDetail>>(`${this.base}/${id}`)
      .pipe(map(r => r.data));
  }

  // PATCH /api/admin/surveys/{id}/state → { success, message }
  // ALIGN-02: renamed from /status (body: { status }) → /state (body: { state })
  setState(id: number, state: 'Inactive' | 'Active' | 'Closed'): Observable<void> {
    return this.http.patch<void>(`${this.base}/${id}/state`, { state });
  }

  // DELETE /api/admin/surveys/{id} → { success, message }
  softDelete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  // PATCH /api/admin/surveys/{id}/restore → { success, message }
  restore(id: number): Observable<void> {
    return this.http.patch<void>(`${this.base}/${id}/restore`, {});
  }
}
