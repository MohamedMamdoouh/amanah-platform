import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../environments/environment';
import {
  CreateReportRequest,
  CreateReportResponse,
  ReportDetail,
  ReportListResponse,
  ReportStatus,
  WithdrawReportRequest,
} from './models/report.models';

@Injectable({ providedIn: 'root' })
export class ReportService {
  private readonly http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.apiBaseUrl}/reports`;
  }

  create(request: CreateReportRequest, photos: File[] = []): Observable<CreateReportResponse> {
    const formData = new FormData();
    formData.append('report', JSON.stringify(request));

    for (const photo of photos) {
      formData.append('photos', photo, photo.name);
    }

    return this.http.post<CreateReportResponse>(this.baseUrl, formData);
  }

  getMine(status?: ReportStatus): Observable<ReportListResponse> {
    if (status) {
      return this.http.get<ReportListResponse>(`${this.baseUrl}/mine`, {
        params: { status },
      });
    }

    return this.http.get<ReportListResponse>(`${this.baseUrl}/mine`);
  }

  getById(id: string): Observable<ReportDetail> {
    return this.http.get<ReportDetail>(`${this.baseUrl}/${id}`);
  }

  withdraw(id: string, request: WithdrawReportRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/withdraw`, request);
  }
}
