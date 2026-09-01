import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, catchError, firstValueFrom, from, of, tap } from 'rxjs';

import { environment } from '../../environments/environment';
import {
  AuthSession,
  LoginRequest,
  RegisterRequest,
  ResetPasswordRequest,
  SendOtpRequest,
  UserProfile,
  VerifyOtpRequest,
  VerifyOtpResult,
} from './models/auth.models';

const AUTH_HTTP_OPTIONS = { withCredentials: true } as const;

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  private accessToken: string | null = null;
  private refreshPromise: Promise<AuthSession> | null = null;

  private readonly userSignal = signal<UserProfile | null>(null);
  readonly currentUser = this.userSignal.asReadonly();

  private get authBaseUrl(): string {
    return `${environment.apiBaseUrl}/auth`;
  }

  async initialize(): Promise<void> {
    try {
      await firstValueFrom(this.refreshSession());
    } catch {
      this.clearSession();
    }
  }

  getAccessToken(): string | null {
    return this.accessToken;
  }

  isLoggedIn(): boolean {
    return this.accessToken !== null;
  }

  isAdmin(): boolean {
    return this.currentUser()?.role === 'Admin';
  }

  sendOtp(request: SendOtpRequest): Observable<void> {
    return this.http.post<void>(
      `${this.authBaseUrl}/otp/send`,
      request,
      AUTH_HTTP_OPTIONS,
    );
  }

  verifyOtp(request: VerifyOtpRequest): Observable<VerifyOtpResult> {
    return this.http.post<VerifyOtpResult>(
      `${this.authBaseUrl}/otp/verify`,
      request,
      AUTH_HTTP_OPTIONS,
    );
  }

  register(request: RegisterRequest): Observable<AuthSession> {
    return this.http
      .post<AuthSession>(
        `${this.authBaseUrl}/register`,
        request,
        AUTH_HTTP_OPTIONS,
      )
      .pipe(tap((session) => this.applySession(session)));
  }

  login(request: LoginRequest): Observable<AuthSession> {
    return this.http
      .post<AuthSession>(
        `${this.authBaseUrl}/login`,
        request,
        AUTH_HTTP_OPTIONS,
      )
      .pipe(tap((session) => this.applySession(session)));
  }

  resetPassword(request: ResetPasswordRequest): Observable<AuthSession> {
    return this.http
      .post<AuthSession>(
        `${this.authBaseUrl}/password/reset`,
        request,
        AUTH_HTTP_OPTIONS,
      )
      .pipe(tap((session) => this.applySession(session)));
  }

  refreshSession(): Observable<AuthSession> {
    this.refreshPromise ??= this.createRefreshRequest();
    return from(this.refreshPromise);
  }

  private async createRefreshRequest(): Promise<AuthSession> {
    return await firstValueFrom(
      this.http
        .post<AuthSession>(`${this.authBaseUrl}/refresh`, {}, AUTH_HTTP_OPTIONS)
        .pipe(tap((session) => this.applySession(session))),
    ).finally(() => {
      this.refreshPromise = null;
    });
  }

  fetchCurrentUser(): Observable<UserProfile> {
    return this.http
      .get<UserProfile>(`${this.authBaseUrl}/me`, AUTH_HTTP_OPTIONS)
      .pipe(tap((user) => this.userSignal.set(user)));
  }

  logout(): Observable<void> {
    if (!this.accessToken) {
      this.clearSession();
      return of(undefined);
    }

    return this.http
      .post<void>(`${this.authBaseUrl}/logout`, {}, AUTH_HTTP_OPTIONS)
      .pipe(
        tap(() => this.clearSession()),
        catchError((error: HttpErrorResponse) => {
          this.clearSession();
          throw error;
        }),
      );
  }

  clearSession(): void {
    this.accessToken = null;
    this.userSignal.set(null);
  }

  private applySession(session: AuthSession): void {
    this.accessToken = session.accessToken;
    this.userSignal.set(session.user);
  }
}
