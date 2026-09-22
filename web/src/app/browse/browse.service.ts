import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../environments/environment';
import {
  BrowseReportsQuery,
  PaginatedResponse,
  PublicReportDetail,
  PublicReportSummary,
} from './models/browse.models';

export type BrowseErrorRoute = 'not-found' | 'unavailable';

@Injectable({ providedIn: 'root' })
export class BrowseService {
  private readonly http = inject(HttpClient);

  getReports(
    query: BrowseReportsQuery = {},
  ): Observable<PaginatedResponse<PublicReportSummary>> {
    let params = new HttpParams();

    if (query.q) {
      params = params.set('q', query.q);
    }

    if (query.category) {
      params = params.set('category', query.category);
    }

    if (query.governorate) {
      params = params.set('governorate', query.governorate);
    }

    if (query.type) {
      params = params.set('type', query.type);
    }

    if (query.dateFrom) {
      params = params.set('dateFrom', query.dateFrom);
    }

    if (query.dateTo) {
      params = params.set('dateTo', query.dateTo);
    }

    if (query.page) {
      params = params.set('page', query.page.toString());
    }

    if (query.pageSize) {
      params = params.set('pageSize', query.pageSize.toString());
    }

    return this.http.get<PaginatedResponse<PublicReportSummary>>(
      `${environment.apiBaseUrl}/reports`,
      { params },
    );
  }

  getLostDetail(id: string): Observable<PublicReportDetail> {
    return this.http.get<PublicReportDetail>(
      `${environment.apiBaseUrl}/lost/${id}`,
    );
  }

  getFoundDetail(id: string): Observable<PublicReportDetail> {
    return this.http.get<PublicReportDetail>(
      `${environment.apiBaseUrl}/found/${id}`,
    );
  }
}

export function mapBrowseError(error: unknown): BrowseErrorRoute | null {
  if (!(error instanceof HttpErrorResponse)) {
    return null;
  }

  if (error.status === 404) {
    return 'not-found';
  }

  if (error.status === 410) {
    return 'unavailable';
  }

  return null;
}
