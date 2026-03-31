import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { ApiResponse, PaginatedResult, AuditLog, AuditMeta } from '../../shared/models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AuditService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/audit`;

  // GET /api/Audit?Search=&Action=&Entity=&UserId=&FromDate=&ToDate=&PageNumber=&PageSize=
  getLogs(params?: {
    page?: number;
    pageSize?: number;
    search?: string;
    action?: string;
    entity?: string;
    userId?: number;
    fromDate?: string;
    toDate?: string;
  }): Observable<PaginatedResult<AuditLog>> {
    let httpParams = new HttpParams()
      .set('pageNumber', params?.page ?? 1)
      .set('pageSize', params?.pageSize ?? 20);
    if (params?.search)          httpParams = httpParams.set('search', params.search);
    if (params?.action)          httpParams = httpParams.set('action', params.action);
    if (params?.entity)          httpParams = httpParams.set('entity', params.entity);
    if (params?.userId != null)  httpParams = httpParams.set('UserId', params.userId);
    if (params?.fromDate)        httpParams = httpParams.set('fromDate', params.fromDate);
    if (params?.toDate)          httpParams = httpParams.set('toDate', params.toDate);
    return this.http.get<ApiResponse<PaginatedResult<AuditLog>>>(this.base, { params: httpParams })
      .pipe(map(r => r.data));
  }

  getMeta(): Observable<AuditMeta> {
    return this.http.get<ApiResponse<AuditMeta>>(`${this.base}/meta`)
      .pipe(map(r => r.data));
  }
}