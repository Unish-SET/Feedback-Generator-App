import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import {
  SubmitResponseRequest, SurveyResponseRecord,
  PaginatedResult, ApiResponse
} from '../../shared/models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ResponseService {
  private readonly http = inject(HttpClient);

  // POST /api/surveys/{publicToken}/responses
  submit(publicToken: string, req: SubmitResponseRequest): Observable<SurveyResponseRecord> {
    return this.http.post<ApiResponse<SurveyResponseRecord>>(
      `${environment.apiUrl}/surveys/${publicToken}/responses`, req
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
