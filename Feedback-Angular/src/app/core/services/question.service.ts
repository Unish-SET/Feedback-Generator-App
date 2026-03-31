import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { Question, CreateQuestionRequest, UpdateQuestionRequest, ApiResponse } from '../../shared/models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class QuestionService {
  private readonly http = inject(HttpClient);

  private base(surveyId: number): string {
    return `${environment.apiUrl}/surveys/${surveyId}/questions`;
  }

  // GET /api/surveys/{surveyId}/questions → { success, data: QuestionResponseDto[] }
  getAll(surveyId: number, type?: string): Observable<Question[]> {
    let params = new HttpParams();
    if (type) params = params.set('type', type);
    return this.http.get<ApiResponse<Question[]>>(this.base(surveyId), { params })
      .pipe(map(r => r.data));
  }

  // POST /api/surveys/{surveyId}/questions → { success, data: QuestionResponseDto }
  create(surveyId: number, req: CreateQuestionRequest): Observable<Question> {
    return this.http.post<ApiResponse<Question>>(this.base(surveyId), req)
      .pipe(map(r => r.data));
  }

  // PUT /api/surveys/{surveyId}/questions/{questionId} → { success, data: QuestionResponseDto }
  update(surveyId: number, questionId: number, req: UpdateQuestionRequest): Observable<Question> {
    return this.http.put<ApiResponse<Question>>(`${this.base(surveyId)}/${questionId}`, req)
      .pipe(map(r => r.data));
  }

  // DELETE /api/surveys/{surveyId}/questions/{questionId} → { success, message }
  delete(surveyId: number, questionId: number): Observable<void> {
    return this.http.delete<void>(`${this.base(surveyId)}/${questionId}`);
  }
}
