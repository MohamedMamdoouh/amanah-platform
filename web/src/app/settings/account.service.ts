import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../environments/environment';

export interface AccountDeactivationStatus {
  canDeactivate: boolean;
  blockers: string[];
  deactivatedAt: string | null;
}

export interface AccountIdentifiers {
  phone: string | null;
  email: string | null;
}

@Injectable({ providedIn: 'root' })
export class AccountService {
  private readonly http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.apiBaseUrl}/account`;
  }

  getDeactivationStatus(): Observable<AccountDeactivationStatus> {
    return this.http.get<AccountDeactivationStatus>(
      `${this.baseUrl}/deactivation-status`,
    );
  }

  deactivateAccount(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/deactivate`, null);
  }

  reactivateAccount(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/reactivate`, null);
  }

  getIdentifiers(): Observable<AccountIdentifiers> {
    return this.http.get<AccountIdentifiers>(`${this.baseUrl}/identifiers`);
  }

  sendLinkIdentifierOtp(
    channel: 'phone' | 'email',
    identifier: string,
    captchaToken: string,
  ): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/identifiers/otp/send`, {
      channel,
      identifier,
      captchaToken,
    });
  }

  verifyLinkIdentifierOtp(
    channel: 'phone' | 'email',
    identifier: string,
    code: string,
  ): Observable<AccountIdentifiers> {
    return this.http.post<AccountIdentifiers>(
      `${this.baseUrl}/identifiers/otp/verify`,
      { channel, identifier, code },
    );
  }
}
