import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../environments/environment';
import {
  ClaimDetail,
  PaginatedClaimsResponse,
  ReportClaimSummary,
  SubmitClaimRequest,
  SubmitClaimResponse,
} from './models/claim.models';

@Injectable({ providedIn: 'root' })
export class ClaimService {
  private readonly http = inject(HttpClient);

  submit(
    reportId: string,
    request: SubmitClaimRequest,
    photo?: File,
  ): Observable<SubmitClaimResponse> {
    const formData = new FormData();
    formData.append('claim', JSON.stringify(request));

    if (photo) {
      formData.append('photo', photo, photo.name);
    }

    return this.http.post<SubmitClaimResponse>(
      `${environment.apiBaseUrl}/reports/${reportId}/claims`,
      formData,
    );
  }

  getMine(page = 1, pageSize = 20): Observable<PaginatedClaimsResponse> {
    const params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);

    return this.http.get<PaginatedClaimsResponse>(
      `${environment.apiBaseUrl}/claims/mine`,
      { params },
    );
  }

  getByReport(reportId: string): Observable<ReportClaimSummary[]> {
    return this.http.get<ReportClaimSummary[]>(
      `${environment.apiBaseUrl}/reports/${reportId}/claims`,
    );
  }

  getById(claimId: string): Observable<ClaimDetail> {
    return this.http.get<ClaimDetail>(
      `${environment.apiBaseUrl}/claims/${claimId}`,
    );
  }

  approve(claimId: string): Observable<void> {
    return this.http.post<void>(
      `${environment.apiBaseUrl}/claims/${claimId}/approve`,
      null,
    );
  }

  reject(claimId: string): Observable<void> {
    return this.http.post<void>(
      `${environment.apiBaseUrl}/claims/${claimId}/reject`,
      null,
    );
  }

  withdraw(claimId: string): Observable<void> {
    return this.http.post<void>(
      `${environment.apiBaseUrl}/claims/${claimId}/withdraw`,
      null,
    );
  }
}
