import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../environments/environment';
import {
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
}
