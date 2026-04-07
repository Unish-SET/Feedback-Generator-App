import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { ApiResponse, SurveyInviteItem } from '../../shared/models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class InviteService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/survey`;

  sendInvites(surveyId: number, emails: string[]): Observable<void> {
    return this.http.post<void>(`${this.base}/${surveyId}/invites`, { emails });
  }

  getInvites(surveyId: number): Observable<SurveyInviteItem[]> {
    return this.http.get<ApiResponse<SurveyInviteItem[]>>(
      `${this.base}/${surveyId}/invites`
    ).pipe(map(r => r.data));
  }

  setInviteOnly(surveyId: number, isInviteOnly: boolean): Observable<void> {
    return this.http.patch<void>(`${this.base}/${surveyId}/invite-only`, { isInviteOnly });
  }}
