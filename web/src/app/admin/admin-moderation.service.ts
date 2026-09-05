import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../environments/environment';
import { ReportDetail } from '../reports/models/report.models';

export interface ModerationQueueItem {
  id: string;
  type: string;
  title: string;
  categoryCode: string;
  status: string;
  createdAt: string;
}

export interface ModerationQueueResponse {
  items: ModerationQueueItem[];
  pendingCount: number;
}

export interface RejectReportRequest {
  reasonCode: string;
  note?: string | null;
}

@Injectable({ providedIn: 'root' })
export class AdminModerationService {
  private readonly http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.apiBaseUrl}/admin/moderation`;
  }

  getQueue(): Observable<ModerationQueueResponse> {
    return this.http.get<ModerationQueueResponse>(`${this.baseUrl}/queue`);
  }

  getReport(id: string): Observable<ReportDetail> {
    return this.http.get<ReportDetail>(`${this.baseUrl}/reports/${id}`);
  }

  approve(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/reports/${id}/approve`, null);
  }

  reject(id: string, request: RejectReportRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/reports/${id}/reject`, request);
  }
}
