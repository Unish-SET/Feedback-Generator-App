import { Injectable, signal, computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap, map } from 'rxjs/operators';
import { Observable } from 'rxjs';
import { AuthUser, LoginRequest, RegisterRequest, AuthResponse, ApiResponse } from '../../shared/models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly TOKEN_KEY = 'fsm_token';

  // ─── Signals ──────────────────────────────────────────
  private readonly _user = signal<AuthUser | null>(null);

  readonly user = this._user.asReadonly();
  readonly isAuthenticated = computed(() => !!this._user());
  readonly isAdmin = computed(() => this._user()?.role === 'Admin');
  readonly isCreator = computed(() => this._user()?.role === 'Creator');

  constructor() {
    this.initFromStorage();
  }

  // ─── Init from localStorage on page reload ─────────────
  // ALIGN-03: on a fresh page load the token is in localStorage but the response body
  // is gone, so we still need to JWT-decode to restore the user identity.
  // On login/register we now read from the response body directly (no decode needed there).
  private initFromStorage(): void {
    const token = localStorage.getItem(this.TOKEN_KEY);
    if (token) {
      const exp = this.getTokenExp(token);
      if (exp && !this.isTokenExpired(exp)) {
        const user = this.decodeTokenForRestore(token);
        if (user) this._user.set(user);
      } else {
        this.clearStorage();
      }
    }
  }

  // ─── JWT decode used ONLY for page-reload restoration ──
  // Kept private and narrow-purpose: never call this after a fresh login/register.
  private decodeTokenForRestore(token: string): AuthUser | null {
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      const userId = parseInt(
        payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'] ||
        payload['nameid'] || payload['sub'] || '0'
      );
      const username =
        payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'] ||
        payload['unique_name'] || payload['name'] || '';
      const email =
        payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] ||
        payload['email'] || '';
      const role =
        payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ||
        payload['role'] || 'Creator';
      return { userId, username, email, role, exp: payload['exp'] || 0 };
    } catch {
      return null;
    }
  }

  private getTokenExp(token: string): number | null {
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return payload['exp'] ?? null;
    } catch {
      return null;
    }
  }

  private isTokenExpired(exp: number): boolean {
    return Date.now() / 1000 > exp;
  }

  private clearStorage(): void {
    localStorage.removeItem(this.TOKEN_KEY);
  }

  // ─── API Methods ───────────────────────────────────────
  // ALIGN-03: backend now returns userId/username/email/role alongside the token,
  // so we build AuthUser directly from the response — no JWT decoding required.
  login(req: LoginRequest): Observable<AuthResponse> {
    return this.http.post<ApiResponse<AuthResponse>>(
      `${environment.apiUrl}/auth/login`, req
    ).pipe(
      map(res => res.data),
      tap(data => {
        localStorage.setItem(this.TOKEN_KEY, data.token);
        const exp = this.getTokenExp(data.token);
        this._user.set({
          userId:   data.userId,
          username: data.username,
          email:    data.email,
          role:     data.role,
          exp:      exp ?? 0
        });
      })
    );
  }

  register(req: RegisterRequest): Observable<AuthResponse> {
    return this.http.post<ApiResponse<AuthResponse>>(
      `${environment.apiUrl}/auth/register`, req
    ).pipe(
      map(res => res.data),
      tap(data => {
        localStorage.setItem(this.TOKEN_KEY, data.token);
        const exp = this.getTokenExp(data.token);
        this._user.set({
          userId:   data.userId,
          username: data.username,
          email:    data.email,
          role:     data.role,
          exp:      exp ?? 0
        });
      })
    );
  }

  logout(): void {
    this.clearStorage();
    this._user.set(null);
    this.router.navigate(['/auth/login']);
  }

  getToken(): string | null {
    return localStorage.getItem(this.TOKEN_KEY);
  }

  checkTokenExpiry(): void {
    const user = this._user();
    if (user && this.isTokenExpired(user.exp)) {
      this.logout();
    }
  }
}
