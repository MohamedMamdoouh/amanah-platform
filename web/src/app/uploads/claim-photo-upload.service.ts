import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { ClaimPhotoPresignResponse } from '../claims/models/claim.models';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ClaimPhotoUploadService {
  private readonly http = inject(HttpClient);

  getPresignedUrl(claimId: string): Observable<ClaimPhotoPresignResponse> {
    return this.http.get<ClaimPhotoPresignResponse>(
      `${environment.apiBaseUrl}/uploads/claim-photo/${claimId}/url`,
    );
  }
}
