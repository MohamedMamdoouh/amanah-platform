import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../environments/environment';
import {
  CategoryListResponse,
  GovernorateListResponse,
} from './models/catalog.models';

@Injectable({ providedIn: 'root' })
export class CatalogService {
  private readonly http = inject(HttpClient);

  getCategories(): Observable<CategoryListResponse> {
    return this.http.get<CategoryListResponse>(
      `${environment.apiBaseUrl}/categories`,
    );
  }

  getGovernorates(): Observable<GovernorateListResponse> {
    return this.http.get<GovernorateListResponse>(
      `${environment.apiBaseUrl}/governorates`,
    );
  }
}
