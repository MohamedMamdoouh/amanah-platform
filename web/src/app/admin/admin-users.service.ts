import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../environments/environment';

export type AdminUserSearchBy = 'name' | 'phone';

export interface AdminUserSummary {
  id: string;
  displayName: string;
}

export interface AdminUserListResponse {
  items: AdminUserSummary[];
}

export interface AdminUserDetail {
  id: string;
  displayName: string;
  normalizedPhone: string;
  role: string;
  isBanned: boolean;
  banReason: string | null;
  bannedAt: string | null;
  reportsCount: number;
  createdAt: string;
}

export interface BanUserRequest {
  reason: string;
}

export interface BanUserResponse {
  userId: string;
  banReason: string;
  bannedAt: string;
}

export interface UnbanUserResponse {
  userId: string;
}

@Injectable({ providedIn: 'root' })
export class AdminUsersService {
  private readonly http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.apiBaseUrl}/admin/users`;
  }

  searchUsers(
    searchBy: AdminUserSearchBy,
    query: string,
  ): Observable<AdminUserListResponse> {
    const params = new HttpParams()
      .set('searchBy', searchBy)
      .set('query', query);

    return this.http.get<AdminUserListResponse>(this.baseUrl, { params });
  }

  getUser(userId: string): Observable<AdminUserDetail> {
    return this.http.get<AdminUserDetail>(`${this.baseUrl}/${userId}`);
  }

  ban(userId: string, reason: string): Observable<BanUserResponse> {
    return this.http.post<BanUserResponse>(`${this.baseUrl}/${userId}/ban`, {
      reason,
    });
  }

  unban(userId: string): Observable<UnbanUserResponse> {
    return this.http.post<UnbanUserResponse>(
      `${this.baseUrl}/${userId}/unban`,
      null,
    );
  }
}
