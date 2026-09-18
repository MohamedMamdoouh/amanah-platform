import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../environments/environment';

export interface AccountDeactivationStatus {
  canDeactivate: boolean;
  blockers: string[];
  deactivatedAt: string | null;
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
}
