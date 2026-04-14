import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import {
  SubmitResponseRequest, SurveyResponseRecord,
  PaginatedResult, ApiResponse
} from '../../shared/models';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class ResponseService {
  private readonly http        = inject(HttpClient);
  private readonly authService = inject(AuthService);

  // POST /api/surveys/{publicToken}/responses
  submit(publicToken: string, req: SubmitResponseRequest): Observable<SurveyResponseRecord> {
    // For anonymous users, attach a stable browser UUID so the backend can
    // detect duplicate submissions even without a user account.
    const headers = this.authService.isAuthenticated()
      ? new HttpHeaders()
      : new HttpHeaders({ 'X-Anon-Id': this.authService.getAnonId() });

    return this.http.post<ApiResponse<SurveyResponseRecord>>(
      `${environment.apiUrl}/surveys/${publicToken}/responses`, req, { headers }
    ).pipe(map(r => r.data));
  }

  // GET /api/surveys/{surveyId}/responses
  getAll(surveyId: number, params?: {
    pageNumber?: number;
    pageSize?: number;
    submittedFrom?: string;
    submittedTo?: string;
    fromDate?: string;
    toDate?: string;
    userId?: number;
  }): Observable<PaginatedResult<SurveyResponseRecord>> {
    let httpParams = new HttpParams()
      .set('pageNumber', params?.pageNumber ?? 1)
      .set('pageSize', params?.pageSize ?? 20);
    if (params?.submittedFrom) httpParams = httpParams.set('SubmittedFrom', params.submittedFrom);
    if (params?.submittedTo)   httpParams = httpParams.set('SubmittedTo', params.submittedTo);
    if (params?.fromDate)      httpParams = httpParams.set('FromDate', params.fromDate);
    if (params?.toDate)        httpParams = httpParams.set('ToDate', params.toDate);
    if (params?.userId != null) httpParams = httpParams.set('UserId', params.userId);
    return this.http.get<ApiResponse<PaginatedResult<SurveyResponseRecord>>>(
      `${environment.apiUrl}/surveys/${surveyId}/responses`, { params: httpParams }
    ).pipe(map(r => r.data));
  }
}
