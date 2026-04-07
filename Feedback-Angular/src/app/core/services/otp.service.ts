import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { ApiResponse, OtpVerifiedResponse } from '../../shared/models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class OtpService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/otp`;

  sendOtp(email: string, surveyPublicToken: string): Observable<void> {
    return this.http.post<void>(`${this.base}/send`, { email, surveyPublicToken });
  }

  verifyOtp(email: string, surveyPublicToken: string, code: string): Observable<OtpVerifiedResponse> {
    return this.http.post<ApiResponse<OtpVerifiedResponse>>(
      `${this.base}/verify`, { email, surveyPublicToken, code }
    ).pipe(map(r => r.data));
  }
}
