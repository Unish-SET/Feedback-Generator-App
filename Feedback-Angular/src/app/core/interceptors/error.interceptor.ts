import {
  HttpInterceptorFn, HttpRequest, HttpHandlerFn,
  HttpEvent, HttpErrorResponse
} from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ToastService } from '../services/toast.service';
import { AuthService } from '../services/auth.service';

export const errorInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn
): Observable<HttpEvent<unknown>> => {
  const toast = inject(ToastService);
  const auth = inject(AuthService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      // Backend GlobalExceptionMiddleware returns:
      // { success: false, statusCode, message, traceId }
      const backendMessage = error.error?.message ?? '';

      let userMessage = 'Something went wrong. Please try again.';

      if (error.status === 0) {
        userMessage = 'Unable to connect to server. Check your connection.';
      } else if (error.status === 400) {
        // These are semantic survey state codes handled by the public-survey component itself.
        // Skip the global toast so the component can show its own friendly message.
        const surveyStateCodes = ['SURVEY_PAUSED','SURVEY_CLOSED','SURVEY_NOT_STARTED','SURVEY_EXPIRED','SURVEY_NO_QUESTIONS'];
        if (surveyStateCodes.includes(backendMessage)) {
          return throwError(() => error);
        }
        userMessage = backendMessage || 'Invalid input. Please check your data.';
      } else if (error.status === 401) {
        if (auth.isAuthenticated()) {
          userMessage = 'Session expired. Please log in again.';
          auth.logout();
        } else {
          userMessage = 'Authentication required.';
        }
      } else if (error.status === 403) {
        userMessage = backendMessage || 'Access denied. You do not have permission.';
      } else if (error.status === 404) {
        userMessage = backendMessage || 'Resource not found.';
      } else if (error.status === 409) {
        userMessage = backendMessage || 'Conflict: this resource already exists.';
      } else if (error.status === 429) {
        userMessage = backendMessage || 'Too many requests. Please slow down and try again.';
      } else if (error.status >= 500) {
        userMessage = 'Server error. Please try again later.';
      }

      toast.error(userMessage);
      return throwError(() => error);
    })
  );
};
