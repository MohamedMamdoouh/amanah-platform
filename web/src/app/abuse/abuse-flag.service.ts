import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../environments/environment';

export const ABUSE_FLAG_REASONS = [
  'abuse.scam_fraud',
  'abuse.spam',
  'abuse.illegal_prohibited',
  'abuse.harassment_threat',
  'abuse.other',
] as const;

export type AbuseFlagStatus = 'open' | 'resolved';

export interface FlagListingRequest {
  reason: (typeof ABUSE_FLAG_REASONS)[number];
  note?: string | null;
}

export interface FlagListingResponse {
  id: string;
  reportId: string;
  reason: string;
  note?: string | null;
  status: AbuseFlagStatus;
  createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class AbuseFlagService {
  private readonly http = inject(HttpClient);

  private flagUrl(reportId: string): string {
    return `${environment.apiBaseUrl}/reports/${reportId}/flag`;
  }

  getOpenFlag(reportId: string): Observable<FlagListingResponse> {
    return this.http.get<FlagListingResponse>(this.flagUrl(reportId));
  }

  createFlag(
    reportId: string,
    request: FlagListingRequest,
  ): Observable<FlagListingResponse> {
    return this.http.post<FlagListingResponse>(this.flagUrl(reportId), {
      reason: request.reason,
      note: request.note ?? null,
    });
  }
}
