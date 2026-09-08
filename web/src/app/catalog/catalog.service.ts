import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { forkJoin, Observable } from 'rxjs';
import { map } from 'rxjs/operators';

import { environment } from '../../environments/environment';
import {
  Category,
  CategoryListResponse,
  Governorate,
  GovernorateListResponse,
} from './models/catalog.models';

export interface BrowseCatalogOptions {
  categories: Category[];
  governorates: Governorate[];
}

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

  getBrowseOptions(): Observable<BrowseCatalogOptions> {
    return forkJoin({
      categories: this.getCategories(),
      governorates: this.getGovernorates(),
    }).pipe(
      map(({ categories, governorates }) => ({
        categories: categories.items,
        governorates: governorates.items,
      })),
    );
  }
}
