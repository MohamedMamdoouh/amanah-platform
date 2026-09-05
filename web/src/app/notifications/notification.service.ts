import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { firstValueFrom, map, Observable } from 'rxjs';

import { AuthService } from '../auth/auth.service';
import { environment } from '../../environments/environment';

export interface NotificationPayload {
  type: string;
  createdAt: string;
  deepLink: string;
  reportId?: string | null;
  reasonCode?: string | null;
  note?: string | null;
}

export interface NotificationItem {
  id: string;
  payload: NotificationPayload;
  isRead: boolean;
}

export interface NotificationListResponse {
  items: NotificationItem[];
}

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  readonly unreadCount = signal(0);

  private get baseUrl(): string {
    return `${environment.apiBaseUrl}/notifications`;
  }

  getAll(): Observable<NotificationListResponse> {
    return this.http.get<NotificationListResponse>(this.baseUrl);
  }

  getUnreadCount(): Observable<number> {
    return this.http
      .get<{ count: number }>(`${this.baseUrl}/unread-count`)
      .pipe(map((response) => response.count));
  }

  markRead(id: string): Observable<void> {
    return this.http.patch<void>(`${this.baseUrl}/${id}/read`, null);
  }

  markAllRead(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/read-all`, null);
  }

  async refreshUnreadCount(): Promise<void> {
    if (!this.auth.isLoggedIn()) {
      this.unreadCount.set(0);
      return;
    }

    try {
      const count = await firstValueFrom(this.getUnreadCount());
      this.unreadCount.set(count);
    } catch {
      this.unreadCount.set(0);
    }
  }

  decrementUnreadCount(): void {
    this.unreadCount.update((count) => Math.max(0, count - 1));
  }

  clearUnreadCount(): void {
    this.unreadCount.set(0);
  }
}
