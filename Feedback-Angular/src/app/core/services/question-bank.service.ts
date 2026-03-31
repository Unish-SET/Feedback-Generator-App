import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpEvent, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import {
  BankQuestion, CreateBankQuestionRequest,
  CloneFromBankRequest, CloneFromBankResult,
  QuestionImportResult, PaginatedResult, ApiResponse
} from '../../shared/models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class QuestionBankService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/question-bank`;

  // GET /api/question-bank → { success, data: PaginatedResult<BankQuestionDto> }
  getAll(params?: {
    search?: string;
    tag?: string;
    type?: string;
    pageNumber?: number;
    pageSize?: number;
  }): Observable<PaginatedResult<BankQuestion>> {
    let httpParams = new HttpParams();
    if (params?.search)           httpParams = httpParams.set('search', params.search);
    if (params?.tag)              httpParams = httpParams.set('tag', params.tag);
    if (params?.type)             httpParams = httpParams.set('type', params.type);
    if (params?.pageNumber != null) httpParams = httpParams.set('pageNumber', params.pageNumber);
    if (params?.pageSize != null)   httpParams = httpParams.set('pageSize', params.pageSize);
    return this.http.get<ApiResponse<PaginatedResult<BankQuestion>>>(this.base, { params: httpParams })
      .pipe(map(r => r.data));
  }

  // GET /api/question-bank/{id} → { success, data: BankQuestionDto }
  getById(id: number): Observable<BankQuestion> {
    return this.http.get<ApiResponse<BankQuestion>>(`${this.base}/${id}`)
      .pipe(map(r => r.data));
  }

  // POST /api/question-bank → { success, data: BankQuestionDto }
  create(req: CreateBankQuestionRequest): Observable<BankQuestion> {
    return this.http.post<ApiResponse<BankQuestion>>(this.base, req)
      .pipe(map(r => r.data));
  }

  // PUT /api/question-bank/{id} → { success, data: BankQuestionDto }
  update(id: number, req: CreateBankQuestionRequest): Observable<BankQuestion> {
    return this.http.put<ApiResponse<BankQuestion>>(`${this.base}/${id}`, req)
      .pipe(map(r => r.data));
  }

  // DELETE /api/question-bank/{id} → { success, message }
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  // POST /api/question-bank/clone-into-survey → { success, data: CloneFromBankResultDto }
  cloneIntoSurvey(req: CloneFromBankRequest): Observable<CloneFromBankResult> {
    return this.http.post<ApiResponse<CloneFromBankResult>>(`${this.base}/clone-into-survey`, req)
      .pipe(map(r => r.data));
  }

  // POST /api/questions/import-excel (multipart/form-data)
  importExcel(file: File, surveyId?: number, addToQuestionBank = false): Observable<QuestionImportResult> {
    const form = new FormData();
    form.append('file', file);
    let params = new HttpParams().set('addToQuestionBank', String(addToQuestionBank));
    if (surveyId != null) params = params.set('surveyId', surveyId);
    return this.http.post<ApiResponse<QuestionImportResult>>(
      `${environment.apiUrl}/questions/import-excel`, form, { params }
    ).pipe(map(r => r.data));
  }

  /**
   * Same as importExcel but supports upload progress (surveyId required for survey-targeted import).
   */
  importExcelWithProgress(
    file: File,
    options: { surveyId?: number; addToQuestionBank?: boolean }
  ): Observable<HttpEvent<ApiResponse<QuestionImportResult>>> {
    const form = new FormData();
    form.append('file', file);
    let params = new HttpParams().set('addToQuestionBank', String(options.addToQuestionBank ?? false));
    if (options.surveyId != null) params = params.set('surveyId', String(options.surveyId));
    return this.http.post<ApiResponse<QuestionImportResult>>(
      `${environment.apiUrl}/questions/import-excel`,
      form,
      { params, reportProgress: true, observe: 'events' }
    );
  }

  // GET /api/questions/import-template → Excel file download
  downloadTemplate(): Observable<Blob> {
    return this.http.get(`${environment.apiUrl}/questions/import-template`, { responseType: 'blob' });
  }
}
