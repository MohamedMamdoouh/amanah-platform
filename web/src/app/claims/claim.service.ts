import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { switchMap } from 'rxjs/operators';

import { environment } from '../../environments/environment';
import { PaginatedResponse } from '../shared/models/pagination.models';
import {
  ClaimDetail,
  MyClaimSummary,
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

  getMine(page = 1, pageSize = 20): Observable<PaginatedResponse<MyClaimSummary>> {
    const params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);

    return this.http.get<PaginatedResponse<MyClaimSummary>>(
      `${environment.apiBaseUrl}/claims/mine`,
      { params },
    );
  }

  findApprovedClaimForReport(
    reportId: string,
  ): Observable<MyClaimSummary | null> {
    return this.findMineForReport(reportId, 'approved');
  }

  findPendingClaimForReport(
    reportId: string,
  ): Observable<MyClaimSummary | null> {
    return this.findMineForReport(reportId, 'pending');
  }

  private findMineForReport(
    reportId: string,
    status: MyClaimSummary['status'],
  ): Observable<MyClaimSummary | null> {
    // Must stay within GET /claims/mine pageSize max (1–50) or the lookup 400s
    // and claimant confirm/cancel UI never appears on public report detail.
    const pageSize = 50;

    const scanPage = (page: number): Observable<MyClaimSummary | null> =>
      this.getMine(page, pageSize).pipe(
        switchMap((response) => {
          const found = response.items.find(
            (claim) => claim.reportId === reportId && claim.status === status,
          );
          if (found) {
            return of(found);
          }
          if (page >= response.totalPages) {
            return of(null);
          }
          return scanPage(page + 1);
        }),
      );

    return scanPage(1);
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

  confirmResolution(claimId: string): Observable<void> {
    return this.http.post<void>(
      `${environment.apiBaseUrl}/claims/${claimId}/confirm-resolution`,
      null,
    );
  }

  cancelClaim(claimId: string): Observable<void> {
    return this.http.post<void>(
      `${environment.apiBaseUrl}/claims/${claimId}/cancel`,
      null,
    );
  }
}
