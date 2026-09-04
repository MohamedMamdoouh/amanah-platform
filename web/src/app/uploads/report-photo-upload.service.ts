import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../environments/environment';

export interface ReportPhotoPresignResponse {
  url: string;
}

@Injectable({ providedIn: 'root' })
export class ReportPhotoUploadService {
  private readonly http = inject(HttpClient);

  getPresignedUrl(photoId: string): Observable<ReportPhotoPresignResponse> {
    return this.http.get<ReportPhotoPresignResponse>(
      `${environment.apiBaseUrl}/uploads/report-photo/${photoId}/url`,
    );
  }
}
