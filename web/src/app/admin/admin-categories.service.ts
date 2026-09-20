import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../environments/environment';

export interface AdminCategoryFieldDefinition {
  id: string;
  fieldKey: string;
  type: string;
  required: boolean;
  sortOrder: number;
  minLength?: number | null;
  maxLength?: number | null;
  textFormat?: string | null;
}

export interface AdminCategory {
  id: string;
  code: string;
  sortOrder: number;
  photosPrivate: boolean;
  isActive: boolean;
  fieldDefinitions: AdminCategoryFieldDefinition[];
}

export interface AdminCategoryListResponse {
  items: AdminCategory[];
}

export interface CreateCategoryRequest {
  code: string;
  sortOrder: number;
  photosPrivate: boolean;
  isActive: boolean;
}

export interface UpdateCategoryRequest {
  code: string;
  sortOrder: number;
  photosPrivate: boolean;
  isActive: boolean;
}

export interface CreateCategoryFieldRequest {
  fieldKey: string;
  type: string;
  required: boolean;
  sortOrder: number;
  minLength?: number | null;
  maxLength?: number | null;
  textFormat?: string | null;
}

export type UpdateCategoryFieldRequest = CreateCategoryFieldRequest;

@Injectable({ providedIn: 'root' })
export class AdminCategoriesService {
  private readonly http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.apiBaseUrl}/admin/categories`;
  }

  list(): Observable<AdminCategoryListResponse> {
    return this.http.get<AdminCategoryListResponse>(this.baseUrl);
  }

  createCategory(request: CreateCategoryRequest): Observable<AdminCategory> {
    return this.http.post<AdminCategory>(this.baseUrl, request);
  }

  updateCategory(id: string, request: UpdateCategoryRequest): Observable<AdminCategory> {
    return this.http.put<AdminCategory>(`${this.baseUrl}/${id}`, request);
  }

  createField(
    categoryId: string,
    request: CreateCategoryFieldRequest,
  ): Observable<AdminCategoryFieldDefinition> {
    return this.http.post<AdminCategoryFieldDefinition>(
      `${this.baseUrl}/${categoryId}/fields`,
      request,
    );
  }

  updateField(
    categoryId: string,
    fieldId: string,
    request: UpdateCategoryFieldRequest,
  ): Observable<AdminCategoryFieldDefinition> {
    return this.http.put<AdminCategoryFieldDefinition>(
      `${this.baseUrl}/${categoryId}/fields/${fieldId}`,
      request,
    );
  }
}
