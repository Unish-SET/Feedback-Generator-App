import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { AppUser, UpdateRoleRequest, ApiResponse, PaginatedResult, SurveyListItem } from '../../shared/models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class UserService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/user`;

  // GET /api/User?Role=&Search=&FromDate=&ToDate=&PageNumber=&PageSize=
  getAll(params?: {
    pageNumber?: number;
    pageSize?: number;
    role?: string;
    search?: string;
    fromDate?: string;
    toDate?: string;
  }): Observable<PaginatedResult<AppUser>> {
    let httpParams = new HttpParams()
      .set('pageNumber', params?.pageNumber ?? 1)
      .set('pageSize', params?.pageSize ?? 50);
    if (params?.role)     httpParams = httpParams.set('Role', params.role);
    if (params?.search)   httpParams = httpParams.set('Search', params.search);
    if (params?.fromDate) httpParams = httpParams.set('FromDate', params.fromDate);
    if (params?.toDate)   httpParams = httpParams.set('ToDate', params.toDate);
    return this.http.get<ApiResponse<PaginatedResult<AppUser>>>(this.base, { params: httpParams })
      .pipe(map(r => r.data));
  }

  // GET /api/user/{id}
  getById(id: number): Observable<AppUser> {
    return this.http.get<ApiResponse<AppUser>>(`${this.base}/${id}`)
      .pipe(map(r => r.data));
  }

  // GET /api/user/{id}/surveys
  getSurveysByUser(id: number, pageNumber = 1, pageSize = 20): Observable<PaginatedResult<SurveyListItem>> {
    return this.http.get<ApiResponse<PaginatedResult<SurveyListItem>>>(`${this.base}/${id}/surveys`, {
      params: { pageNumber, pageSize }
    }).pipe(map(r => r.data));
  }

  // PATCH /api/user/{id}/role
  updateRole(id: number, req: UpdateRoleRequest): Observable<AppUser> {
    return this.http.patch<ApiResponse<AppUser>>(`${this.base}/${id}/role`, req)
      .pipe(map(r => r.data));
  }

  // PATCH /api/user/{id}/status
  setStatus(id: number, isActive: boolean): Observable<AppUser> {
    return this.http.patch<ApiResponse<AppUser>>(`${this.base}/${id}/status`, { isActive })
      .pipe(map(r => r.data));
  }

  // DELETE /api/user/{id}
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
