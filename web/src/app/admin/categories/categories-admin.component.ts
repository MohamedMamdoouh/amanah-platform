import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { ApiErrorService } from '../../i18n/api-error.service';
import { CatalogLabelService } from '../../i18n/catalog-label.service';
import { AlertComponent } from '../../shared/ui/alert/alert.component';
import { BadgeComponent } from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { CardComponent } from '../../shared/ui/card/card.component';
import { FormFieldComponent } from '../../shared/ui/form-field/form-field.component';
import { LoadingIndicatorComponent } from '../../shared/ui/loading-indicator/loading-indicator.component';
import { PageHeaderComponent } from '../../shared/ui/page-header/page-header.component';
import { CategoryFieldFormComponent, CategoryFieldFormGroup } from './category-field-form.component';
import {
  AdminCategoriesService,
  AdminCategory,
  AdminCategoryFieldDefinition,
  CreateCategoryFieldRequest,
  CreateCategoryRequest,
  UpdateCategoryFieldRequest,
  UpdateCategoryRequest,
} from '../admin-categories.service';

type FieldFormValue = {
  fieldKey: string;
  required: boolean;
  sortOrder: number;
  minLength: string;
  maxLength: string;
  textFormat: string;
};

type ParseOptionalIntResult =
  | { ok: true; value: number | null }
  | { ok: false };

@Component({
  selector: 'app-categories-admin',
  standalone: true,
  imports: [
    AlertComponent,
    BadgeComponent,
    ButtonComponent,
    CardComponent,
    CategoryFieldFormComponent,
    FormFieldComponent,
    LoadingIndicatorComponent,
    PageHeaderComponent,
    ReactiveFormsModule,
    TranslateModule,
  ],
  templateUrl: './categories-admin.component.html',
  styleUrl: './categories-admin.component.scss',
})
export class CategoriesAdminComponent implements OnInit {
  private readonly categoriesService = inject(AdminCategoriesService);
  private readonly catalogLabels = inject(CatalogLabelService);
  private readonly apiErrors = inject(ApiErrorService);
  private readonly translate = inject(TranslateService);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly categories = signal<AdminCategory[]>([]);
  readonly showAddCategory = signal(false);
  readonly editingCategoryId = signal<string | null>(null);
  readonly addingFieldCategoryId = signal<string | null>(null);
  readonly editingField = signal<{ categoryId: string; fieldId: string } | null>(null);
  readonly saving = signal(false);
  readonly successMessage = signal<string | null>(null);

  readonly addCategoryForm = this.fb.nonNullable.group({
    code: ['', [Validators.required, Validators.maxLength(40)]],
    sortOrder: [0, [Validators.required, Validators.min(0)]],
    photosPrivate: [false],
    isActive: [true],
  });

  readonly editCategoryForm = this.fb.nonNullable.group({
    code: ['', [Validators.required, Validators.maxLength(40)]],
    sortOrder: [0, [Validators.required, Validators.min(0)]],
    photosPrivate: [false],
    isActive: [true],
  });

  readonly addFieldForm = this.createFieldForm();
  readonly editFieldForm = this.createFieldForm();

  ngOnInit(): void {
    void this.loadCategories();
  }

  categoryLabel(code: string): string {
    return this.catalogLabels.category(code);
  }

  fieldLabel(categoryCode: string, fieldKey: string): string {
    return this.catalogLabels.field(categoryCode, fieldKey);
  }

  fieldTypeLabel(type: string): string {
    return this.translate.instant(`admin.categories.field_type.${type}`);
  }

  textFormatLabel(format: string | null | undefined): string {
    if (!format) {
      return this.translate.instant('admin.categories.text_format.none');
    }
    return this.translate.instant(`admin.categories.text_format.${format}`);
  }

  toggleAddCategory(): void {
    const willShow = !this.showAddCategory();
    this.showAddCategory.set(willShow);
    this.editingCategoryId.set(null);

    if (willShow) {
      this.addCategoryForm.reset({
        code: '',
        sortOrder: this.categories().length + 1,
        photosPrivate: false,
        isActive: true,
      });
    }
  }

  startEditCategory(category: AdminCategory): void {
    this.showAddCategory.set(false);
    this.editingCategoryId.set(category.id);
    this.editCategoryForm.reset({
      code: category.code,
      sortOrder: category.sortOrder,
      photosPrivate: category.photosPrivate,
      isActive: category.isActive,
    });
  }

  cancelEditCategory(): void {
    this.editingCategoryId.set(null);
  }

  startAddField(category: AdminCategory): void {
    this.editingField.set(null);
    this.addingFieldCategoryId.set(category.id);
    this.addFieldForm.reset(this.emptyFieldFormValue(this.nextFieldSortOrder(category)));
  }

  startEditField(category: AdminCategory, field: AdminCategoryFieldDefinition): void {
    this.addingFieldCategoryId.set(null);
    this.editingField.set({ categoryId: category.id, fieldId: field.id });
    this.editFieldForm.reset(this.toFieldFormValue(field));
  }

  cancelFieldForms(): void {
    this.addingFieldCategoryId.set(null);
    this.editingField.set(null);
  }

  async submitAddCategory(): Promise<void> {
    if (this.addCategoryForm.invalid) {
      this.addCategoryForm.markAllAsTouched();
      this.error.set(this.translate.instant('common.form.validation_summary'));
      return;
    }

    await this.runSave(async () => {
      const value = this.addCategoryForm.getRawValue();
      const request: CreateCategoryRequest = {
        code: value.code.trim(),
        sortOrder: value.sortOrder,
        photosPrivate: value.photosPrivate,
        isActive: value.isActive,
      };
      await firstValueFrom(this.categoriesService.createCategory(request));
      this.showAddCategory.set(false);
      this.successMessage.set(this.translate.instant('admin.categories.created'));
    });
  }

  async submitEditCategory(categoryId: string): Promise<void> {
    if (this.editCategoryForm.invalid) {
      this.editCategoryForm.markAllAsTouched();
      this.error.set(this.translate.instant('common.form.validation_summary'));
      return;
    }

    await this.runSave(async () => {
      const value = this.editCategoryForm.getRawValue();
      const request: UpdateCategoryRequest = {
        code: value.code.trim(),
        sortOrder: value.sortOrder,
        photosPrivate: value.photosPrivate,
        isActive: value.isActive,
      };
      await firstValueFrom(this.categoriesService.updateCategory(categoryId, request));
      this.editingCategoryId.set(null);
      this.successMessage.set(this.translate.instant('admin.categories.updated'));
    });
  }

  async submitAddField(categoryId: string): Promise<void> {
    const request = this.buildFieldRequest(this.addFieldForm.getRawValue());
    if (!request) {
      this.addFieldForm.markAllAsTouched();
      this.error.set(this.translate.instant('common.form.validation_summary'));
      return;
    }

    await this.runSave(async () => {
      await firstValueFrom(this.categoriesService.createField(categoryId, request));
      this.addingFieldCategoryId.set(null);
      this.successMessage.set(this.translate.instant('admin.categories.field_created'));
    });
  }

  async submitEditField(categoryId: string, fieldId: string): Promise<void> {
    const request = this.buildFieldRequest(this.editFieldForm.getRawValue());
    if (!request) {
      this.editFieldForm.markAllAsTouched();
      this.error.set(this.translate.instant('common.form.validation_summary'));
      return;
    }

    await this.runSave(async () => {
      await firstValueFrom(
        this.categoriesService.updateField(categoryId, fieldId, request),
      );
      this.editingField.set(null);
      this.successMessage.set(this.translate.instant('admin.categories.field_updated'));
    });
  }

  private async loadCategories(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const response = await firstValueFrom(this.categoriesService.list());
      this.categories.set(response.items);
    } catch (err) {
      this.error.set(this.apiErrors.messageFromHttpError(err));
    } finally {
      this.loading.set(false);
    }
  }

  private async runSave(action: () => Promise<void>): Promise<void> {
    this.saving.set(true);
    this.error.set(null);
    this.successMessage.set(null);

    try {
      await action();
      await this.loadCategories();
    } catch (err) {
      this.error.set(this.apiErrors.messageFromHttpError(err));
    } finally {
      this.saving.set(false);
    }
  }

  private createFieldForm(): CategoryFieldFormGroup {
    return this.fb.nonNullable.group({
      fieldKey: ['', [Validators.required, Validators.maxLength(40)]],
      required: [true],
      sortOrder: [1, [Validators.required, Validators.min(0)]],
      minLength: [''],
      maxLength: [''],
      textFormat: [''],
    });
  }

  private nextFieldSortOrder(category: AdminCategory): number {
    if (category.fieldDefinitions.length === 0) {
      return 1;
    }

    const maxSort = category.fieldDefinitions.reduce(
      (max, field) => Math.max(max, field.sortOrder),
      0,
    );
    return maxSort + 1;
  }

  private emptyFieldFormValue(sortOrder: number): FieldFormValue {
    return {
      fieldKey: '',
      required: true,
      sortOrder,
      minLength: '',
      maxLength: '',
      textFormat: '',
    };
  }

  private toFieldFormValue(field: AdminCategoryFieldDefinition): FieldFormValue {
    return {
      fieldKey: field.fieldKey,
      required: field.required,
      sortOrder: field.sortOrder,
      minLength: field.minLength?.toString() ?? '',
      maxLength: field.maxLength?.toString() ?? '',
      textFormat: field.textFormat ?? '',
    };
  }

  private buildFieldRequest(
    value: FieldFormValue,
  ): CreateCategoryFieldRequest | UpdateCategoryFieldRequest | null {
    if (!value.fieldKey.trim()) {
      return null;
    }

    const minLength = this.parseOptionalInt(value.minLength);
    if (!minLength.ok) {
      return null;
    }

    const maxLength = this.parseOptionalInt(value.maxLength);
    if (!maxLength.ok) {
      return null;
    }

    return {
      fieldKey: value.fieldKey.trim(),
      type: 'text',
      required: value.required,
      sortOrder: value.sortOrder,
      minLength: minLength.value,
      maxLength: maxLength.value,
      textFormat: value.textFormat || null,
    };
  }

  private parseOptionalInt(
    value: string | number | null | undefined,
  ): ParseOptionalIntResult {
    if (value === null || value === undefined || value === '') {
      return { ok: true, value: null };
    }

    if (typeof value === 'number') {
      return Number.isFinite(value)
        ? { ok: true, value: Math.trunc(value) }
        : { ok: false };
    }

    const trimmed = value.trim();
    if (!trimmed) {
      return { ok: true, value: null };
    }

    const parsed = Number.parseInt(trimmed, 10);
    return Number.isNaN(parsed) ? { ok: false } : { ok: true, value: parsed };
  }
}
