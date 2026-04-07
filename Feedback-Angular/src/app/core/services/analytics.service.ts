import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { SurveyAnalytics, ApiResponse } from '../../shared/models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AnalyticsService {
  private readonly http = inject(HttpClient);

  get(surveyId: number, params?: {
    fromDate?: string;
    toDate?: string;
  }): Observable<SurveyAnalytics> {
    let httpParams = new HttpParams();
    if (params?.fromDate) httpParams = httpParams.set('FromDate', params.fromDate);
    if (params?.toDate)   httpParams = httpParams.set('ToDate', params.toDate);
    return this.http.get<ApiResponse<SurveyAnalytics>>(
      `${environment.apiUrl}/surveys/${surveyId}/analytics`, { params: httpParams }
    ).pipe(map(r => r.data));
  }

  // GET /api/surveys/{surveyId}/export/excel
  exportExcel(surveyId: number): Observable<Blob> {
    return this.http.get(
      `${environment.apiUrl}/surveys/${surveyId}/export/excel`,
      { responseType: 'blob' }
    );
  }

  sendReport(surveyId: number, recipientEmail: string): Observable<void> {
    return this.http.post<void>(
      `${environment.apiUrl}/surveys/${surveyId}/analytics/send-report`,
      { recipientEmail }
    );
  }
}
