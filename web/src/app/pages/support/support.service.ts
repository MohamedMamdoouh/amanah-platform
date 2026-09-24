import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { SubmitSupportMessageRequest } from './support.models';

@Injectable({ providedIn: 'root' })
export class SupportService {
  private readonly http = inject(HttpClient);

  submitMessage(request: SubmitSupportMessageRequest): Observable<void> {
    return this.http.post<void>(
      `${environment.apiBaseUrl}/support/messages`,
      request,
    );
  }
}
