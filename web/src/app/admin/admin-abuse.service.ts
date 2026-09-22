import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../environments/environment';
import { ChatThreadDetail } from '../chats/models/chat.models';

export type AbuseResolutionOutcome = 'no_action' | 'takedown' | 'ban';

export interface AbuseQueueItem {
  id: string;
  reportId: string;
  reportType: string;
  reportTitle: string;
  reportStatus: string;
  reason: string;
  abuseReporterUserId: string;
  abuseReporterDisplayName: string;
  status: string;
  createdAt: string;
}

export interface AbuseQueueResponse {
  items: AbuseQueueItem[];
  openCount: number;
}

export interface FlaggedListingSummary {
  id: string;
  type: string;
  status: string;
  title: string;
  categoryCode: string;
  governorateCode: string;
  listingOwnerUserId: string;
  listingOwnerDisplayName: string;
  createdAt: string;
  publishedAt?: string | null;
}

export interface AbuseReportDetail {
  id: string;
  reason: string;
  note?: string | null;
  status: string;
  resolutionOutcome?: string | null;
  createdAt: string;
  resolvedAt?: string | null;
  abuseReporterUserId: string;
  abuseReporterDisplayName: string;
  listing: FlaggedListingSummary;
}

export interface ResolveAbuseReportRequest {
  outcome: AbuseResolutionOutcome;
  adminNote?: string | null;
  banTargetUserId?: string | null;
}

export interface ResolveAbuseReportResponse {
  id: string;
  status: string;
  resolutionOutcome: string;
  resolvedAt: string;
}

export interface InvestigationChatResponse {
  threads: ChatThreadDetail[];
}

export interface InvestigationClaim {
  id: string;
  status: string;
  submittedAnswer: string;
  hasPhoto: boolean;
  photoUrl?: string | null;
  submittedAt: string;
  reviewedAt?: string | null;
  decisionReason?: string | null;
  attemptNumber: number;
  claimantDisplayName: string;
  chatThreadId?: string | null;
}

export interface InvestigationClaimsResponse {
  items: InvestigationClaim[];
}

export interface InvestigationPhoto {
  id: string;
  url: string;
  sortOrder: number;
}

export interface InvestigationPhotosResponse {
  photos: InvestigationPhoto[];
}

@Injectable({ providedIn: 'root' })
export class AdminAbuseService {
  private readonly http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.apiBaseUrl}/admin/abuse`;
  }

  private get investigationsUrl(): string {
    return `${environment.apiBaseUrl}/admin/investigations`;
  }

  getQueue(): Observable<AbuseQueueResponse> {
    return this.http.get<AbuseQueueResponse>(this.baseUrl);
  }

  getReport(id: string): Observable<AbuseReportDetail> {
    return this.http.get<AbuseReportDetail>(`${this.baseUrl}/${id}`);
  }

  resolve(
    id: string,
    request: ResolveAbuseReportRequest,
  ): Observable<ResolveAbuseReportResponse> {
    return this.http.post<ResolveAbuseReportResponse>(
      `${this.baseUrl}/${id}/resolve`,
      request,
    );
  }

  getInvestigationChat(reportId: string): Observable<InvestigationChatResponse> {
    return this.http.get<InvestigationChatResponse>(
      `${this.investigationsUrl}/${reportId}/chat`,
    );
  }

  getInvestigationClaims(
    reportId: string,
  ): Observable<InvestigationClaimsResponse> {
    return this.http.get<InvestigationClaimsResponse>(
      `${this.investigationsUrl}/${reportId}/claims`,
    );
  }

  getInvestigationPhotos(
    reportId: string,
  ): Observable<InvestigationPhotosResponse> {
    return this.http.get<InvestigationPhotosResponse>(
      `${this.investigationsUrl}/${reportId}/photos`,
    );
  }
}
