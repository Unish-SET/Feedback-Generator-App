import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import {
  Survey, SurveyListItem, CreateSurveyRequest, UpdateSurveyRequest,
  SurveyState, PaginatedResult, PublicSurvey, ApiResponse
} from '../../shared/models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class SurveyService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/survey`;

  // GET /api/Survey?Status=&Search=&CreatedBy=&StartDateFrom=&StartDateTo=&SortBy=&SortDir=&PageNumber=&PageSize=
  getAll(params?: {
    pageNumber?: number;
    pageSize?: number;
    status?: string;
    search?: string;
    createdBy?: number;
    startDateFrom?: string;
    startDateTo?: string;
    sortBy?: string;
    sortDir?: string;
  }): Observable<PaginatedResult<SurveyListItem>> {
    let httpParams = new HttpParams()
      .set('pageNumber', params?.pageNumber ?? 1)
      .set('pageSize', params?.pageSize ?? 10);
    if (params?.status)        httpParams = httpParams.set('Status', params.status);
    if (params?.search)        httpParams = httpParams.set('Search', params.search);
    if (params?.createdBy != null) httpParams = httpParams.set('CreatedBy', params.createdBy);
    if (params?.startDateFrom) httpParams = httpParams.set('StartDateFrom', params.startDateFrom);
    if (params?.startDateTo)   httpParams = httpParams.set('StartDateTo', params.startDateTo);
    if (params?.sortBy)        httpParams = httpParams.set('SortBy', params.sortBy);
    if (params?.sortDir)       httpParams = httpParams.set('SortDir', params.sortDir);
    return this.http.get<ApiResponse<PaginatedResult<SurveyListItem>>>(this.base, { params: httpParams })
      .pipe(map(r => r.data));
  }

  // GET /api/survey/{id}
  getById(id: number): Observable<Survey> {
    return this.http.get<ApiResponse<Survey>>(`${this.base}/${id}`)
      .pipe(map(r => r.data));
  }

  // POST /api/survey
  create(req: CreateSurveyRequest): Observable<Survey> {
    return this.http.post<ApiResponse<Survey>>(this.base, req)
      .pipe(map(r => r.data));
  }

  // PUT /api/survey/{id}
  update(id: number, req: UpdateSurveyRequest): Observable<Survey> {
    return this.http.put<ApiResponse<Survey>>(`${this.base}/${id}`, req)
      .pipe(map(r => r.data));
  }

  // DELETE /api/survey/{id}
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  // PATCH /api/survey/{id}/state — single endpoint replacing /status + /availability
  setState(id: number, state: SurveyState): Observable<Survey> {
    return this.http.patch<ApiResponse<Survey>>(`${this.base}/${id}/state`, { state })
      .pipe(map(r => r.data));
  }

  // PATCH /api/survey/{id}/schedule — update start/end dates on any state
  updateSchedule(id: number, startDate?: string, endDate?: string): Observable<Survey> {
    return this.http.patch<ApiResponse<Survey>>(`${this.base}/${id}/schedule`, { startDate, endDate })
      .pipe(map(r => r.data));
  }

  // POST /api/survey/{sourceId}/clone-questions/{targetId}
  cloneQuestions(sourceId: number, targetId: number): Observable<void> {
    return this.http.post<void>(`${this.base}/${sourceId}/clone-questions/${targetId}`, {});
  }

  // GET /survey/{publicToken} — NOTE: PublicSurveyController has NO /api prefix
  getPublic(publicToken: string): Observable<PublicSurvey> {
    const baseUrl = environment.apiUrl.replace('/api', '');
    return this.http.get<ApiResponse<PublicSurvey>>(`${baseUrl}/survey/${publicToken}`)
      .pipe(map(r => r.data));
  }
}
