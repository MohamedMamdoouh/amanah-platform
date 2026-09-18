import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../environments/environment';

export interface AccountDeletionStatus {
  canDelete: boolean;
  blockers: string[];
  deletionRequestedAt: string | null;
}

@Injectable({ providedIn: 'root' })
export class AccountService {
  private readonly http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.apiBaseUrl}/account`;
  }

  getDeletionStatus(): Observable<AccountDeletionStatus> {
    return this.http.get<AccountDeletionStatus>(`${this.baseUrl}/deletion-status`);
  }

  deleteAccount(): Observable<void> {
    return this.http.delete<void>(this.baseUrl);
  }
}
